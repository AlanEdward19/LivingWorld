using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T5 (VTT-03): <see cref="InteriorProjector"/> — identidade do prédio é real
/// (cidade/tipo), mas ocupação por interior não existe no domínio (<c>Building</c> não tem
/// <c>CellCoord</c> própria, nenhum <c>Npc</c> referencia "dentro de qual prédio"), então
/// <c>OccupancyModeled</c> fica sempre falso em vez de inventar quem está dentro.</summary>
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
    public void Build_returns_the_buildings_identity_with_occupancy_unmodeled()
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
        Assert.False(result.Value!.OccupancyModeled);
    }
}
