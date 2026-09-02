using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Fase 28, T2 (LOD-01, LOD-04): <see cref="ObservationRegistry"/> — união de escopos
/// multi-fonte e não-participação no hash canônico.</summary>
public class ObservationRegistryTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, City CityA, City CityB, Building BuildingA, Npc InCityA,
        Npc InBuildingA, Npc InCityB) BuildFixture()
    {
        var world = ScenarioRunner.Create(seed: 28, initialPopulation: 0).World;
        var cityA = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        var cityB = new City(world.NextCityId(), new CellCoord(20, 20), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityA);
        world.AddCity(cityB);

        var buildingA = new Building(new BuildingId(1), cityA.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(buildingA);

        var inCityA = AddNpc(world, new CellCoord(5, 5), cityA.Id);
        var inBuildingA = AddNpc(world, new CellCoord(5, 5), cityA.Id);
        inBuildingA.EnterBuilding(buildingA.Id, FloorLevel.Ground, new CellCoord(1, 1));

        var inCityB = AddNpc(world, new CellCoord(20, 20), cityB.Id);

        return (world, cityA, cityB, buildingA, inCityA, inBuildingA, inCityB);
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

    [Fact]
    public void No_active_sources_means_no_npc_is_observed()
    {
        var (world, _, _, _, inCityA, inBuildingA, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();

        Assert.False(registry.IsObserved(inCityA, world));
        Assert.False(registry.IsObserved(inBuildingA, world));
        Assert.False(registry.IsObserved(inCityB, world));
    }

    [Fact]
    public void World_scope_observes_every_npc()
    {
        var (world, _, _, _, inCityA, inBuildingA, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();
        registry.SetScope("client", SpaceScope.World());

        Assert.True(registry.IsObserved(inCityA, world));
        Assert.True(registry.IsObserved(inBuildingA, world));
        Assert.True(registry.IsObserved(inCityB, world));
    }

    [Fact]
    public void City_scope_observes_only_npcs_in_that_city_including_inside_buildings()
    {
        var (world, cityA, cityB, _, inCityA, inBuildingA, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();
        registry.SetScope("client", SpaceScope.City(cityA.Id));

        Assert.True(registry.IsObserved(inCityA, world));
        Assert.True(registry.IsObserved(inBuildingA, world));
        Assert.False(registry.IsObserved(inCityB, world));
    }

    [Fact]
    public void Building_scope_observes_only_npcs_inside_that_building()
    {
        var (world, cityA, _, buildingA, inCityA, inBuildingA, _) = BuildFixture();
        var registry = new ObservationRegistry();
        registry.SetScope("client", SpaceScope.Building(cityA.Id, buildingA.Id));

        Assert.False(registry.IsObserved(inCityA, world));
        Assert.True(registry.IsObserved(inBuildingA, world));
    }

    [Fact]
    public void Union_of_multiple_sources_observes_if_any_source_covers_the_npc()
    {
        var (world, cityA, cityB, _, inCityA, _, inCityB) = BuildFixture();
        var buildingB = new Building(new BuildingId(2), cityB.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(buildingB);
        var inBuildingB = AddNpc(world, new CellCoord(20, 20), cityB.Id);
        inBuildingB.EnterBuilding(buildingB.Id, FloorLevel.Ground, new CellCoord(2, 2));

        var registry = new ObservationRegistry();
        registry.SetScope("spectator-a", SpaceScope.City(cityA.Id));
        registry.SetScope("spectator-b", SpaceScope.Building(cityB.Id, buildingB.Id));

        Assert.True(registry.IsObserved(inCityA, world));
        Assert.False(registry.IsObserved(inCityB, world));
        Assert.True(registry.IsObserved(inBuildingB, world));
    }

    [Fact]
    public void ClearScope_removes_a_source_from_the_union()
    {
        var (world, cityA, _, _, inCityA, _, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();
        registry.SetScope("client", SpaceScope.World());

        Assert.True(registry.IsObserved(inCityB, world));

        registry.ClearScope("client");
        registry.SetScope("client", SpaceScope.City(cityA.Id));

        Assert.True(registry.IsObserved(inCityA, world));
        Assert.False(registry.IsObserved(inCityB, world));
    }

    [Fact]
    public void SetScope_overwrites_the_previous_scope_for_the_same_source()
    {
        var (world, cityA, cityB, _, inCityA, _, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();
        registry.SetScope("client", SpaceScope.City(cityA.Id));
        registry.SetScope("client", SpaceScope.City(cityB.Id));

        Assert.False(registry.IsObserved(inCityA, world));
        Assert.True(registry.IsObserved(inCityB, world));
    }

    [Fact]
    public void Changing_observation_scopes_does_not_change_canonical_hash()
    {
        var (world, cityA, cityB, buildingA, inCityA, inBuildingA, inCityB) = BuildFixture();
        var registry = new ObservationRegistry();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        registry.SetScope("a", SpaceScope.World());
        registry.SetScope("b", SpaceScope.City(cityA.Id));
        registry.SetScope("c", SpaceScope.Building(cityB.Id, buildingA.Id));
        _ = registry.IsObserved(inCityA, world);
        _ = registry.IsObserved(inBuildingA, world);
        _ = registry.IsObserved(inCityB, world);
        registry.ClearScope("a");
        registry.ClearScope("b");
        registry.ClearScope("c");

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }
}
