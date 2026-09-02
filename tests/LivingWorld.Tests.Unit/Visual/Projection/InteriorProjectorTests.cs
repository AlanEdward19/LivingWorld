using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
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
}
