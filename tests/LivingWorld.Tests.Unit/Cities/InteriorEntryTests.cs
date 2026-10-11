using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>godot-api-readiness C5: entrada no prédio valida andar e célula contra a planta derivada.</summary>
public class InteriorEntryTests
{
    private static (WorldState World, Building Building, Npc Npc) Setup()
    {
        var world = ScenarioRunner.Create(seed: 21, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: -1, completedAtTick: 0);
        world.AddBuilding(building);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "bia", Sex.Female, WorldDate.Epoch(new WorldCalendar(24, 30, 12)), new CultureId(1),
            new CellCoord(0, 0), motherId: null, fatherId: null, household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0), city: city.Id);
        world.AddNpc(npc);
        return (world, building, npc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Floor_out_of_range_fails_and_hash_unchanged(int floor)
    {
        var (world, building, npc) = Setup();
        var stair = BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells.Single(c => c.Material == BuildingMaterial.Stair).Cell;
        var before = WorldSnapshot.CanonicalHash(world);

        var result = InteriorEntry.TryEnter(world, npc, building.Id, new FloorLevel(floor), stair);

        Assert.False(result.IsSuccess);
        Assert.Null(npc.Interior);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Wall_cell_fails_and_valid_entry_succeeds()
    {
        var (world, building, npc) = Setup();
        var cells = BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells;
        var wall = cells.First(c => c.Material is BuildingMaterial.StoneWall or BuildingMaterial.WoodWall).Cell;
        var door = cells.Single(c => c.Material == BuildingMaterial.Door).Cell;

        Assert.False(InteriorEntry.TryEnter(world, npc, building.Id, FloorLevel.Ground, wall).IsSuccess);
        Assert.True(InteriorEntry.TryEnter(world, npc, building.Id, FloorLevel.Ground, door).IsSuccess);
        Assert.Equal(building.Id, npc.Interior!.Building);
    }
}
