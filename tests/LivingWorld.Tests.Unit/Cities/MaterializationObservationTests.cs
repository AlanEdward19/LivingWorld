using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 28, T4 (LOD-01..03): <see cref="MaterializationSystem"/> e
/// <see cref="CityPopulationQuery"/> ligados a <see cref="ObservationRegistry"/> /
/// <see cref="CosmeticDetailSystem"/> — camada cosmética aproximada vs. plena sem tocar eventos
/// de vida.</summary>
public class MaterializationObservationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, City City, Building Building, Npc StreetNpc, Npc BuildingNpc) BuildFixture()
    {
        var world = ScenarioRunner.Create(seed: 28, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var building = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);

        var streetNpc = AddNpc(world, new CellCoord(5, 5), city.Id);
        var buildingNpc = AddNpc(world, new CellCoord(5, 5), city.Id);
        buildingNpc.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(1, 1));

        return (world, city, building, streetNpc, buildingNpc);
    }

    private static Npc AddNpc(WorldState world, CellCoord location, CityId city)
    {
        var npcId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: location, city: city);
        world.AddNpc(npc);
        return npc;
    }

    private static void SyncCosmetic(WorldState world)
    {
        new MaterializationSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static CosmeticDetailLayer LayerOf(WorldState world, NpcId id) =>
        world.CosmeticDetail.TryGetState(id, out var state) ? state.Layer : default;

    [Fact]
    public void No_scope_keeps_npc_without_full_cosmetic_detail()
    {
        var (world, _, _, streetNpc, buildingNpc) = BuildFixture();
        SyncCosmetic(world);

        Assert.False(MaterializationSystem.HasFullCosmeticDetail(world, streetNpc));
        Assert.False(MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(CosmeticDetailLayer.Approximate, LayerOf(world, streetNpc.Id));
    }

    [Fact]
    public void World_scope_grants_full_cosmetic_detail_to_street_and_building_npcs()
    {
        var (world, _, _, streetNpc, buildingNpc) = BuildFixture();
        world.ObservationRegistry.SetScope("client", SpaceScope.World());
        SyncCosmetic(world);

        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, streetNpc));
        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, streetNpc.Id));
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, buildingNpc.Id));
    }

    [Fact]
    public void City_scope_grants_full_detail_to_street_npc_only()
    {
        var (world, city, _, streetNpc, buildingNpc) = BuildFixture();
        world.ObservationRegistry.SetScope("client", SpaceScope.City(city.Id));
        SyncCosmetic(world);

        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, streetNpc));
        Assert.False(MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, streetNpc.Id));
        Assert.Equal(CosmeticDetailLayer.Approximate, LayerOf(world, buildingNpc.Id));
    }

    [Fact]
    public void Building_scope_grants_full_detail_only_inside_framed_building()
    {
        var (world, city, building, streetNpc, buildingNpc) = BuildFixture();
        world.ObservationRegistry.SetScope("client", SpaceScope.Building(city.Id, building.Id));
        SyncCosmetic(world);

        Assert.False(MaterializationSystem.HasFullCosmeticDetail(world, streetNpc));
        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(CosmeticDetailLayer.Approximate, LayerOf(world, streetNpc.Id));
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, buildingNpc.Id));
    }

    [Fact]
    public void City_and_building_union_keeps_framed_interior_at_full_detail()
    {
        var (world, city, building, streetNpc, buildingNpc) = BuildFixture();
        world.ObservationRegistry.SetScope("city-view", SpaceScope.City(city.Id));
        world.ObservationRegistry.SetScope("interior", SpaceScope.Building(city.Id, building.Id));
        SyncCosmetic(world);

        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, streetNpc));
        Assert.True(MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(2, CityPopulationQuery.CosmeticallyDetailedPopulation(world, city.Id));
        Assert.Equal(0, CityPopulationQuery.CosmeticallyApproximatePopulation(world, city.Id));
    }

    [Fact]
    public void City_scope_leaves_unframed_building_in_approximate_cosmetic_layer()
    {
        var (world, city, _, _, buildingNpc) = BuildFixture();
        world.ObservationRegistry.SetScope("client", SpaceScope.City(city.Id));
        SyncCosmetic(world);

        Assert.True(CityPopulationQuery.IsCosmeticallyObserved(world, buildingNpc));
        Assert.False(CityPopulationQuery.HasFullCosmeticDetail(world, buildingNpc));
        Assert.Equal(1, CityPopulationQuery.CosmeticallyApproximatePopulation(world, city.Id));
    }

    [Fact]
    public void Clearing_scope_demotes_cosmetic_layer_on_next_sync()
    {
        var (world, _, _, streetNpc, _) = BuildFixture();
        world.ObservationRegistry.SetScope("client", SpaceScope.World());
        SyncCosmetic(world);
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, streetNpc.Id));

        world.ObservationRegistry.ClearScope("client");
        SyncCosmetic(world);
        Assert.Equal(CosmeticDetailLayer.Approximate, LayerOf(world, streetNpc.Id));
    }

    [Fact]
    public void MaterializeOne_applies_cosmetic_layer_matching_active_scope()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 7, ScenarioRunner.DefaultMap(7),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: CityRules.Create(
                enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
                emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
                migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
                foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
                foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!);

        PopulationSeeder.SeedInitial(world, count: 1, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        var poolNpcIds = world.ReserveNpcIdBlock(3);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(3, 300, 240), poolNpcIds: poolNpcIds);
        world.AddCity(city);
        world.ObservationRegistry.SetScope("client", SpaceScope.City(city.Id));

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;

        Assert.True(CityPopulationQuery.HasFullCosmeticDetail(world, npc));
        Assert.Equal(CosmeticDetailLayer.FullDetail, LayerOf(world, npc.Id));
    }

    [Fact]
    public void Changing_observation_scopes_does_not_change_canonical_hash()
    {
        var (world, city, building, streetNpc, buildingNpc) = BuildFixture();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        world.ObservationRegistry.SetScope("a", SpaceScope.World());
        world.ObservationRegistry.SetScope("b", SpaceScope.City(city.Id));
        world.ObservationRegistry.SetScope("c", SpaceScope.Building(city.Id, building.Id));
        SyncCosmetic(world);
        _ = MaterializationSystem.HasFullCosmeticDetail(world, streetNpc);
        _ = MaterializationSystem.HasFullCosmeticDetail(world, buildingNpc);
        world.ObservationRegistry.ClearScope("a");
        world.ObservationRegistry.ClearScope("b");
        world.ObservationRegistry.ClearScope("c");

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Life_events_match_with_and_without_observation_for_seed(int seed)
    {
        const long horizonHours = 30 * 24;

        var (baseline, baselineClock) = ScenarioRunner.Create(seed: (ulong)seed);
        var (observed, observedClock) = ScenarioRunner.Create(seed: (ulong)seed);
        observed.ObservationRegistry.SetScope("client", SpaceScope.World());

        for (long tick = 0; tick < horizonHours; tick++)
        {
            baselineClock.Tick(baseline);
            observedClock.Tick(observed);
        }

        var baselineMetrics = CaptureLifeMetrics(baseline);
        var observedMetrics = CaptureLifeMetrics(observed);

        Assert.Equal(baselineMetrics.Alive, observedMetrics.Alive);
        Assert.Equal(baselineMetrics.Dead, observedMetrics.Dead);
        Assert.Equal(baselineMetrics.Married, observedMetrics.Married);
    }

    private sealed record LifeMetrics(int Alive, int Dead, int Married);

    private static LifeMetrics CaptureLifeMetrics(WorldState world) => new(
        Alive: world.Npcs.Count(n => n.IsAlive),
        Dead: world.Npcs.Count(n => !n.IsAlive),
        Married: world.Npcs.Count(n => n.IsAlive && n.Spouse is not null));
}
