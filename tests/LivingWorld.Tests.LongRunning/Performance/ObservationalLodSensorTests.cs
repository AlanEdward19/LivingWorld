using System.Diagnostics;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>Fase 28, T14 (LOD-20..22): sensor de custo da camada cosmética observacional —
/// µs/NPC-tick separado para observado vs. aproximado; fração declarada; rastro zero fora de
/// escopo.</summary>
[Collection(ScalePerformanceCollection.Name)]
public class ObservationalLodSensorTests
{
    private const long OneMonthTicks = 30 * 24;
    private const int Seed = 42;

    /// <summary>LOD-21: custo aproximado ≤ esta fração do observado (task 4 / spec P2 sensor).</summary>
    private const double MaxApproximateToObservedMicrosRatio = 0.65;

    private const long OneDayTicks = 24;

    public sealed record ObservationalLodSensorSample(
        double MicrosPerNpcTickObservedCosmetic,
        double MicrosPerNpcTickApproximateCosmetic);

    [Theory]
    [Trait("Category", "Scenario")]
    [InlineData(ScaleScenarioFixture.PopulationSmall)]
    public void Observational_lod_sensor_reports_observed_and_approximate_micros_separately(int population)
    {
        var observedArm = MeasureCosmeticArm(population, ConfigureAllObserved, observedArm: true);
        var approximateArm = MeasureCosmeticArm(population, static _ => { }, observedArm: false);

        Assert.True(
            observedArm.MicrosPerNpcTickObservedCosmetic > 0,
            $"MicrosPerNpcTickObservedCosmetic={observedArm.MicrosPerNpcTickObservedCosmetic}");
        Assert.True(
            approximateArm.MicrosPerNpcTickApproximateCosmetic > 0,
            $"MicrosPerNpcTickApproximateCosmetic={approximateArm.MicrosPerNpcTickApproximateCosmetic}");

        double blendedAverage = (observedArm.MicrosPerNpcTickObservedCosmetic
            + approximateArm.MicrosPerNpcTickApproximateCosmetic) / 2.0;

        Assert.NotEqual(
            blendedAverage,
            observedArm.MicrosPerNpcTickObservedCosmetic);
        Assert.NotEqual(
            blendedAverage,
            approximateArm.MicrosPerNpcTickApproximateCosmetic);
    }

    [Theory]
    [Trait("Category", "Scenario")]
    [InlineData(ScaleScenarioFixture.PopulationSmall)]
    public void Approximate_cosmetic_cost_stays_within_declared_fraction_of_observed(int population)
    {
        var observedArm = MeasureCosmeticArm(population, ConfigureAllObserved, observedArm: true);
        var approximateArm = MeasureCosmeticArm(population, static _ => { }, observedArm: false);

        double ceiling = MaxApproximateToObservedMicrosRatio
            * observedArm.MicrosPerNpcTickObservedCosmetic;

        Assert.True(
            approximateArm.MicrosPerNpcTickApproximateCosmetic <= ceiling,
            $"approx={approximateArm.MicrosPerNpcTickApproximateCosmetic:F4} "
            + $"observed={observedArm.MicrosPerNpcTickObservedCosmetic:F4} "
            + $"ceiling={ceiling:F4} (ratio={MaxApproximateToObservedMicrosRatio})");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Mixed_city_scope_run_buckets_observed_and_approximate_in_one_pass()
    {
        const int population = ScaleScenarioFixture.PopulationSmall;
        var sample = MeasureCosmeticMixedScope(population);

        Assert.True(sample.MicrosPerNpcTickObservedCosmetic > 0);
        Assert.True(sample.MicrosPerNpcTickApproximateCosmetic > 0);
        Assert.NotEqual(
            sample.MicrosPerNpcTickObservedCosmetic,
            sample.MicrosPerNpcTickApproximateCosmetic);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Cognition_log_has_zero_entries_outside_observation_scope()
    {
        var (world, ctx, city, materialized, pooledIds) = BuildCityWithPoolAndMaterializedNpc(seed: 314);

        world.ObservationRegistry.SetScope("sensor", SpaceScope.City(city.Id));

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.NotEmpty(world.CognitionLog.RecentEntries(materialized.Id, 10));

        var aggregatedOnlyIds = pooledIds.Where(id => world.FindNpc(id) is null).ToList();
        Assert.NotEmpty(aggregatedOnlyIds);
        foreach (var pooledId in aggregatedOnlyIds)
            Assert.Empty(world.CognitionLog.RecentEntries(pooledId, 10));
    }

    private static void ConfigureAllObserved(WorldState world) =>
        world.ObservationRegistry.SetScope("sensor", SpaceScope.World());

    private static void ConfigureCityScope(WorldState world)
    {
        var city = world.Cities.First();
        world.ObservationRegistry.SetScope("sensor", SpaceScope.City(city.Id));
    }

    private static ObservationalLodSensorSample MeasureCosmeticArm(
        int population,
        Action<WorldState> configureObservation,
        bool observedArm)
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld((ulong)Seed, population);
        configureObservation(world);
        PrimeCosmetic(world);
        return RunCosmeticMeasurement(world, clock, mixedScope: false, observedArm: observedArm, tickCount: OneMonthTicks);
    }

    private static ObservationalLodSensorSample MeasureCosmeticMixedScope(int population)
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld((ulong)Seed, population);
        EnsureInteriorNpcForMixedScope(world);
        ConfigureCityScope(world);
        Assert.True(CityPopulationQuery.CosmeticallyApproximatePopulation(world, world.Cities.First().Id) > 0);
        PrimeCosmetic(world);
        return RunCosmeticMeasurement(world, clock, mixedScope: true, observedArm: true, tickCount: OneDayTicks);
    }

    private static ObservationalLodSensorSample RunCosmeticMeasurement(
        WorldState world,
        WorldClock clock,
        bool mixedScope,
        bool observedArm,
        long tickCount)
    {
        long observedNpcTicks = 0;
        long approximateNpcTicks = 0;
        long observedStopwatchTicks = 0;
        long approximateStopwatchTicks = 0;

        for (long t = 0; t < tickCount; t++)
        {
            foreach (var npc in world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value))
            {
                bool observedBucket = mixedScope
                    ? MaterializationSystem.HasFullCosmeticDetail(world, npc)
                    : observedArm;

                var sw = Stopwatch.StartNew();
                world.CosmeticDetail.SyncObservation(npc, world, t);
                _ = world.CosmeticDetail.ResolvePosition(npc, world, t);
                sw.Stop();

                if (observedBucket)
                {
                    observedStopwatchTicks += sw.ElapsedTicks;
                    observedNpcTicks++;
                }
                else
                {
                    approximateStopwatchTicks += sw.ElapsedTicks;
                    approximateNpcTicks++;
                }
            }

            clock.Tick(world);
        }

        return new ObservationalLodSensorSample(
            MicrosPerNpcTick(observedStopwatchTicks, observedNpcTicks),
            MicrosPerNpcTick(approximateStopwatchTicks, approximateNpcTicks));
    }

    private static void PrimeCosmetic(WorldState world)
    {
        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            world.CosmeticDetail.EnsureNpc(npc, world, tick: 0);
    }

    private static void EnsureInteriorNpcForMixedScope(WorldState world)
    {
        var city = world.Cities.First();
        var building = CreateDefaultBuilding(world, city);

        int targetApproximate = Math.Max(1, world.Npcs.Count(n => n.IsAlive && n.City == city.Id) / 20);
        int placed = 0;

        foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.City == city.Id).OrderBy(n => n.Id.Value))
        {
            npc.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(1, 1));
            placed++;
            if (placed >= targetApproximate)
                break;
        }

        Assert.True(placed > 0);
    }

    private static Building CreateDefaultBuilding(WorldState world, City city)
    {
        var building = new Building(new BuildingId(9_001), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);
        return building;
    }

    private static double MicrosPerNpcTick(long stopwatchTicks, long npcTicks) =>
        stopwatchTicks * 1_000_000.0 / Stopwatch.Frequency / Math.Max(1, npcTicks);

    private static (WorldState World, TickContext Ctx, City City, Npc Materialized, IReadOnlyList<NpcId> PooledIds)
        BuildCityWithPoolAndMaterializedNpc(ulong seed, int poolCount = 3)
    {
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            maxDurationHours: new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2,
                [ActionType.Sleep] = 8,
                [ActionType.Work] = 8,
                [ActionType.Socialize] = 3,
                [ActionType.Travel] = 4,
                [ActionType.Idle] = 2,
                [ActionType.Buy] = 2,
                [ActionType.UsePower] = 1,
            },
            routineSlots: [new RoutineSlot(null, LifeStage.Adult, 0, 23, ActionType.Work)],
            defaultAction: ActionType.Idle).Value!;
        var stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

        var cityRules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
            foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 50)
            .Value!;

        var world = new WorldState(
            new WorldCalendar(24, 30, 12), seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, stages, cityRules: cityRules);

        PopulationSeeder.SeedInitial(world, count: 1, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var pool = new AggregatePopulationPool(poolCount, poolCount * 100, poolCount * 80);
        var poolNpcIds = world.ReserveNpcIdBlock(pool.Count);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool, poolNpcIds: poolNpcIds);
        world.AddCity(city);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var materialized = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        materialized.SetHunger(15, tick: 0);
        SimulationWakeTestHelper.Wake(world, materialized);

        return (world, ctx, city, materialized, poolNpcIds);
    }
}
