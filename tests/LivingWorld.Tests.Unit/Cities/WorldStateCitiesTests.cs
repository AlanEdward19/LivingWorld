using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T5 (CITY-01/CITY-03/CITY-04): <see cref="City"/>/<see cref="Building"/> em
/// <see cref="WorldState"/> — canônico, snapshot round-trip, e CityId/LocationId derivados só do
/// stream de RNG dedicado (nunca Guid.NewGuid()).</summary>
public class WorldStateCitiesTests
{
    private static WorldState MakeWorld() => ScenarioRunner.Create(seed: 1, initialPopulation: 1).World;

    [Fact]
    public void AddCity_makes_it_findable_and_listed()
    {
        var world = MakeWorld();
        var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);

        world.AddCity(city);

        Assert.Same(city, world.FindCity(city.Id));
        Assert.Contains(city, world.Cities);
    }

    [Fact]
    public void AddBuilding_makes_it_findable_and_listed()
    {
        var world = MakeWorld();
        var building = new Building(world.NextBuildingIdAndAdvance(), new CityId(Guid.NewGuid()), buildingTypeId: 1, completedAtTick: 5);

        world.AddBuilding(building);

        Assert.Same(building, world.FindBuilding(building.Id));
        Assert.Contains(building, world.Buildings);
    }

    [Fact]
    public void NextBuildingIdAndAdvance_never_repeats_a_value()
    {
        var world = MakeWorld();

        var first = world.NextBuildingIdAndAdvance();
        var second = world.NextBuildingIdAndAdvance();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NextCityId_derives_from_the_dedicated_rng_stream_deterministically()
    {
        var worldA = ScenarioRunner.Create(seed: 99, initialPopulation: 1).World;
        var worldB = ScenarioRunner.Create(seed: 99, initialPopulation: 1).World;

        Assert.Equal(worldA.NextCityId(), worldB.NextCityId());
    }

    [Fact]
    public void NextCityId_differs_across_successive_calls_on_the_same_world()
    {
        var world = MakeWorld();

        var first = world.NextCityId();
        var second = world.NextCityId();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Snapshot_round_trip_preserves_cities_and_buildings()
    {
        var world = MakeWorld();
        var city = new City(world.NextCityId(), new CellCoord(2, 3), foundedAtTick: 7, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(10, 100, 90));
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 3, completedAtTick: 12);
        world.AddBuilding(building);

        var before = WorldSnapshot.CanonicalHash(world);
        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var after = WorldSnapshot.CanonicalHash(rehydrated);

        Assert.Equal(before, after);
        var rehydratedCity = Assert.Single(rehydrated.Cities, candidate => candidate.Id == city.Id);
        Assert.Equal(city.Id, rehydratedCity.Id);
        Assert.Equal(new AggregatePopulationPool(10, 100, 90), rehydratedCity.AggregatePool);
        var rehydratedBuilding = Assert.Single(rehydrated.Buildings, candidate => candidate.Id == building.Id);
        Assert.Equal(building.Id, rehydratedBuilding.Id);
        Assert.Equal(city.Id, rehydratedBuilding.City);
    }
}
