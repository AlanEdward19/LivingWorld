using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Visual.Projection;

/// <summary>Fase 15, T5 (VTT-03); ocupação real desde Fase 15.1, T47 (G7): identidade do prédio
/// é real (cidade/tipo) e <c>Occupants</c> lista todo NPC vivo cujo <c>Npc.Interior</c> aponta
/// para este prédio.</summary>
public class InteriorProjectorTests
{
    [Fact]
    public void Build_returns_failure_for_a_building_id_that_does_not_exist()
    {
        var world = ScenarioRunner.Create(seed: 13, initialPopulation: 0).World;

        var result = InteriorProjector.Build(world, new BuildingId(999));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Build_returns_the_buildings_identity_with_occupancy_modeled_and_no_occupants()
    {
        var world = ScenarioRunner.Create(seed: 13, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 7, completedAtTick: 0);
        world.AddBuilding(building);

        var result = InteriorProjector.Build(world, building.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(building.Id, result.Value!.Id);
        Assert.Equal(city.Id, result.Value!.City);
        Assert.Equal(7, result.Value!.BuildingTypeId);
        Assert.True(result.Value!.OccupancyModeled);
        Assert.Empty(result.Value!.Occupants);
    }

    private static (WorldState World, Building Building) WorldWithBuilding(int typeId)
    {
        var world = ScenarioRunner.Create(seed: 13, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, typeId, completedAtTick: 0);
        world.AddBuilding(building);
        return (world, building);
    }

    [Fact]
    public void Layout_lists_cells_with_material_per_floor()
    {
        var (world, building) = WorldWithBuilding(typeId: -1);

        var layout = InteriorProjector.Build(world, building.Id).Value!.Layout;

        Assert.Equal(2, layout.Floors);
        Assert.Equal(new[] { 0, 1 }, layout.FloorLayouts.Select(f => f.Level));
        Assert.All(layout.FloorLayouts, f => Assert.NotEmpty(f.Cells));
        Assert.Single(layout.FloorLayouts[0].Cells, c => c.Material == BuildingMaterial.Door);
        Assert.Single(layout.FloorLayouts[1].Cells, c => c.Material == BuildingMaterial.Stair);
    }

    [Fact]
    public void Occupant_floor_within_building_floors()
    {
        var (world, building) = WorldWithBuilding(typeId: -1);
        var npc = world.NextNpcIdAndAdvance();
        var walker = new LivingWorld.Domain.Population.Npc(
            npc, "ana", Sex.Female, WorldDate.Epoch(new WorldCalendar(24, 30, 12)), new CultureId(1), new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0), city: building.City);
        world.AddNpc(walker);
        var stair = BuildingLayoutGenerator.Generate(building).FloorLayouts[1].Cells.Single(c => c.Material == BuildingMaterial.Stair).Cell;
        Assert.True(LivingWorld.Simulation.Cities.InteriorEntry.TryEnter(world, walker, building.Id, new FloorLevel(1), stair).IsSuccess);

        var snapshot = InteriorProjector.Build(world, building.Id).Value!;

        var occupant = Assert.Single(snapshot.Occupants);
        Assert.InRange(occupant.Floor.Value, 0, snapshot.Layout.Floors - 1);
    }
}
