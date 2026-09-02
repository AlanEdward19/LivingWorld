using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 28 T6 (COG-01..03): <see cref="BehaviorDecisionSystem"/> grava
/// <see cref="DecisionTrace"/> em <see cref="WorldState.CognitionLog"/> só para NPCs
/// materializados dentro do escopo observacional com detalhe cosmético pleno (COG-02).</summary>
public class BehaviorDecisionSystemCognitionTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules UrgentRules(int threshold = 70) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        threshold, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static ActionCatalog RoutineCatalog() => ActionCatalog.Create(
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
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildDetailedNpcWorld(
        ulong seed, int hunger = 15, int thirst = 90, int sleep = 80, int social = 80)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            UrgentRules(), RoutineCatalog(), Stages);
        var location = new CellCoord(1, 1);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: thirst, sleep: sleep, social: social);

        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        world.ObservationRegistry.SetScope("test", SpaceScope.World());
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, npc);
        return (world, ctx, npc);
    }

    private static (WorldState World, TickContext Ctx, City City, Npc Materialized, IReadOnlyList<NpcId> PooledIds)
        BuildCityWithPoolAndMaterializedNpc(ulong seed, int poolCount = 3)
    {
        var cityRules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
            foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 50)
            .Value!;

        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, UrgentRules(), RoutineCatalog(), Stages, cityRules: cityRules);

        PopulationSeeder.SeedInitial(world, count: 1, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var pool = new AggregatePopulationPool(poolCount, poolCount * 100, poolCount * 80);
        var poolNpcIds = world.ReserveNpcIdBlock(pool.Count);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool, poolNpcIds: poolNpcIds);
        world.AddCity(city);
        world.ObservationRegistry.SetScope("test", SpaceScope.City(city.Id));

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var materialized = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        materialized.SetHunger(15, tick: 0);
        SimulationWakeTestHelper.Wake(world, materialized);

        return (world, ctx, city, materialized, poolNpcIds);
    }

    private static void TickDecision(WorldState world, TickContext ctx) =>
        new BehaviorDecisionSystem().Tick(world, ctx);

    private static IReadOnlyList<TraceEntry> Entries(WorldState world, NpcId id, int count = 10) =>
        world.CognitionLog.RecentEntries(id, count);

    [Fact]
    public void Urgent_need_on_materialized_npc_records_trace()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 11);

        TickDecision(world, ctx);

        var entries = Entries(world, npc.Id);
        Assert.NotEmpty(entries);
        Assert.Equal(ActionType.Eat, entries[^1].Trace.Winner);
    }

    [Fact]
    public void Aggregated_pool_ids_receive_no_cognition_entries()
    {
        var (world, ctx, city, materialized, pooledIds) = BuildCityWithPoolAndMaterializedNpc(seed: 22);

        TickDecision(world, ctx);

        Assert.NotEmpty(Entries(world, materialized.Id));
        var aggregatedOnlyIds = pooledIds.Where(id => world.FindNpc(id) is null).ToList();
        Assert.NotEmpty(aggregatedOnlyIds);
        foreach (var pooledId in aggregatedOnlyIds)
            Assert.Empty(Entries(world, pooledId));

        Assert.Equal(city.AggregatePool.Count, pooledIds.Count - 1);
    }

    [Fact]
    public void Same_seed_produces_identical_cognition_log()
    {
        static IReadOnlyList<TraceEntry> RunOnce()
        {
            var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 33);
            TickDecision(world, ctx);
            return Entries(world, npc.Id, 5);
        }

        var a = RunOnce();
        var b = RunOnce();

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Tick, b[i].Tick);
            Assert.Equal(a[i].Trace.Winner, b[i].Trace.Winner);
            Assert.Equal(a[i].Trace.WinningUtility, b[i].Trace.WinningUtility);
            Assert.Equal(a[i].Trace.WakeReason, b[i].Trace.WakeReason);
            Assert.Equal(a[i].Trace.TopPressures.Select(p => p.Kind), b[i].Trace.TopPressures.Select(p => p.Kind));
            Assert.Equal(a[i].Trace.KnownAlternatives, b[i].Trace.KnownAlternatives);
        }
    }

    [Fact]
    public void Recorded_trace_contains_top_pressures()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 44);

        TickDecision(world, ctx);

        var trace = Entries(world, npc.Id)[^1].Trace;
        Assert.NotEmpty(trace.TopPressures);
        Assert.Contains(trace.TopPressures, p => p.Kind == PressureModel.AcquireFood);
    }

    [Fact]
    public void Recorded_trace_contains_known_alternatives()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 55);

        TickDecision(world, ctx);

        var trace = Entries(world, npc.Id)[^1].Trace;
        Assert.NotEmpty(trace.KnownAlternatives);
        Assert.DoesNotContain(trace.Winner, trace.KnownAlternatives);
    }

    [Fact]
    public void Recorded_trace_winner_matches_chosen_action()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 66);

        TickDecision(world, ctx);

        var entry = Entries(world, npc.Id)[^1];
        Assert.Equal(npc.CurrentAction, entry.Trace.Winner);
    }

    [Fact]
    public void Routine_without_urgent_need_does_not_record()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 77, hunger: 100, thirst: 100, sleep: 100, social: 100);

        TickDecision(world, ctx);

        Assert.Empty(Entries(world, npc.Id));
        Assert.Equal(ActionType.Work, npc.CurrentAction);
    }

    [Fact]
    public void Multiple_urgent_ticks_accumulate_entries()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 88);

        TickDecision(world, ctx);
        Assert.Single(Entries(world, npc.Id));

        npc.SetHunger(15, world.CurrentDate.TotalHours);
        world.CurrentDate = world.CurrentDate.AddHours(1);
        ctx = new TickContext(world, world.Rng, world.Scheduler);
        TickDecision(world, ctx);

        Assert.Equal(2, Entries(world, npc.Id, 10).Count);
    }

    [Fact]
    public void Recording_does_not_change_canonical_hash()
    {
        var (world, _, npc) = BuildDetailedNpcWorld(seed: 99);
        var decision = BehaviorDecisionSystem.SelectByUtility(
            DecisionContextBuilder.Build(world, npc, tick: 0),
            world.NeedsRules,
            world.EconomyRules,
            continuityAction: null,
            wakeReason: WakeReason.UrgentNeed,
            previousIntent: npc.CurrentIntent);

        var before = WorldSnapshot.CanonicalHash(world);
        world.CognitionLog.Record(npc.Id, 0, decision.Trace);
        var after = WorldSnapshot.CanonicalHash(world);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Different_npcs_have_isolated_cognition_logs()
    {
        var map = ScenarioRunner.DefaultMap(101);
        var world = new WorldState(
            Calendar, seed: 101, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            UrgentRules(), RoutineCatalog(), Stages);
        var location = new CellCoord(1, 1);

        var first = new Npc(
            new NpcId(1), "first", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            null, null, null, 100, Neutral, ProfessionType.None, location, hunger: 15);
        var second = new Npc(
            new NpcId(2), "second", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-28), new CultureId(1), location,
            null, null, null, 100, Neutral, ProfessionType.None, location, hunger: 100, thirst: 15);

        world.AddNpc(first);
        world.AddNpc(second);
        world.AdvanceNpcIdTo(3);
        world.ObservationRegistry.SetScope("test", SpaceScope.World());
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, first);
        SimulationWakeTestHelper.Wake(world, second);

        TickDecision(world, ctx);

        Assert.NotEmpty(Entries(world, first.Id));
        Assert.NotEmpty(Entries(world, second.Id));
        Assert.Equal(ActionType.Eat, Entries(world, first.Id)[^1].Trace.Winner);
        Assert.Equal(ActionType.Eat, Entries(world, second.Id)[^1].Trace.Winner);
    }

    [Fact]
    public void Recorded_trace_includes_wake_reason_for_urgent_need()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 112);

        TickDecision(world, ctx);

        Assert.Equal(WakeReason.UrgentNeed, Entries(world, npc.Id)[^1].Trace.WakeReason);
    }

    [Fact]
    public void Seed_npc_without_materialized_at_tick_still_records_when_detailed()
    {
        var (world, ctx, npc) = BuildDetailedNpcWorld(seed: 123);

        Assert.Null(npc.MaterializedAtTick);

        TickDecision(world, ctx);

        Assert.NotEmpty(Entries(world, npc.Id));
    }

    private static (WorldState World, TickContext Ctx, City City, Building Building, Npc Npc)
        BuildMaterializedNpcInsideBuilding(ulong seed, bool cityScope, bool buildingScope)
    {
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var building = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);

        var location = new CellCoord(5, 5);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "interior-npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30),
            new CultureId(1), location, null, null, null, 100, Neutral, ProfessionType.None, location,
            city: city.Id, hunger: 15, thirst: 90, sleep: 80, social: 80);
        npc.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(1, 1));
        world.AddNpc(npc);

        if (cityScope)
            world.ObservationRegistry.SetScope("city", SpaceScope.City(city.Id));
        if (buildingScope)
            world.ObservationRegistry.SetScope("building", SpaceScope.Building(city.Id, building.Id));

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, npc);
        return (world, ctx, city, building, npc);
    }

    [Fact]
    public void Interior_npc_with_city_scope_only_does_not_record_cognition_trace()
    {
        var (world, ctx, _, _, npc) = BuildMaterializedNpcInsideBuilding(seed: 201, cityScope: true, buildingScope: false);

        TickDecision(world, ctx);

        Assert.Empty(Entries(world, npc.Id));
    }

    [Fact]
    public void Interior_npc_records_trace_when_building_scope_added()
    {
        var (world, ctx, city, building, npc) =
            BuildMaterializedNpcInsideBuilding(seed: 202, cityScope: true, buildingScope: false);

        TickDecision(world, ctx);
        Assert.Empty(Entries(world, npc.Id));

        world.ObservationRegistry.SetScope("building", SpaceScope.Building(city.Id, building.Id));
        world.CurrentDate = world.CurrentDate.AddHours(1);
        ctx = new TickContext(world, world.Rng, world.Scheduler);
        npc.SetHunger(15, world.CurrentDate.TotalHours);
        TickDecision(world, ctx);

        Assert.NotEmpty(Entries(world, npc.Id));
        Assert.Equal(ActionType.Eat, Entries(world, npc.Id)[^1].Trace.Winner);
    }
}
