using LivingWorld.Api.Visual;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T5 (VTT-03, VTT-05, VTT-11): <see cref="CityProjector"/> — moradores
/// materializados com posição/atividade, prédios da cidade, e camadas locais (sempre não
/// modeladas ainda, mesmo padrão de T4) incluindo o overlay de clima reusado do escopo global.</summary>
public class CityProjectorTests
{
    private static (WorldState World, City City, Npc Resident) MakeWorldWithCity()
    {
        var world = ScenarioRunner.Create(seed: 11, initialPopulation: 2).World;
        var resident = world.Npcs.First();

        var city = new City(
            world.NextCityId(), resident.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(4, 400, 300));
        world.AddCity(city);
        resident.JoinCity(city.Id);

        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);

        return (world, city, resident);
    }

    [Fact]
    public void Build_returns_failure_for_a_city_id_that_does_not_exist()
    {
        var world = ScenarioRunner.Create(seed: 11, initialPopulation: 0).World;

        var result = CityProjector.Build(world, new CityId(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Build_lists_materialized_residents_with_location_and_current_action()
    {
        var (world, city, resident) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.True(result.IsSuccess);
        var marker = Assert.Single(result.Value!.Residents);
        Assert.Equal(resident.Id, marker.Id);
        Assert.Equal(resident.CurrentLocation, marker.Location);
        Assert.Equal(resident.CurrentAction, marker.CurrentAction);
    }

    [Fact]
    public void Build_lists_the_citys_buildings()
    {
        var (world, city, _) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.Single(result.Value!.Buildings);
    }

    [Fact]
    public void Build_includes_the_aggregate_pool_as_is()
    {
        var (world, city, _) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.Equal(city.AggregatePool, result.Value!.AggregatePool);
    }

    [Theory]
    [InlineData(VisualLayerId.Cities)]
    [InlineData(VisualLayerId.Villages)]
    [InlineData(VisualLayerId.Routes)]
    [InlineData(VisualLayerId.Migrations)]
    [InlineData(VisualLayerId.Conflicts)]
    [InlineData(VisualLayerId.Climate)]
    public void Build_returns_not_yet_modeled_for_every_local_layer(VisualLayerId layerId)
    {
        var (world, city, _) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.False(result.Value!.Layers[layerId].IsModeled);
    }

    // --- Fase 15.1, T21: campo Portals (SpatialPortal como conceito canônico) ---

    [Fact]
    public void Build_includes_a_portal_that_references_this_city_by_RefId()
    {
        var (world, city, _) = MakeWorldWithCity();
        var portal = new SpatialPortal(
            "portal-south", "Portão Sul",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(1, 1)));
        world.AddPortal(portal);

        var result = CityProjector.Build(world, city.Id);

        Assert.Equal(portal, Assert.Single(result.Value!.Portals));
    }

    [Fact]
    public void Two_portals_to_the_same_city_are_both_listed_distinguishable_only_by_label()
    {
        var (world, city, _) = MakeWorldWithCity();
        var north = new SpatialPortal(
            "portal-north", "Portão Norte",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(1, 1)));
        var south = new SpatialPortal(
            "portal-south", "Portão Sul",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(2, 2)),
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(3, 3)));
        world.AddPortal(north);
        world.AddPortal(south);

        var result = CityProjector.Build(world, city.Id);

        Assert.Equal(2, result.Value!.Portals.Count);
        Assert.Contains(north, result.Value!.Portals);
        Assert.Contains(south, result.Value!.Portals);
    }

    [Fact]
    public void Build_excludes_a_portal_that_references_a_different_city()
    {
        var (world, city, _) = MakeWorldWithCity();
        var otherCityId = world.NextCityId();
        world.AddPortal(new SpatialPortal(
            "portal-elsewhere", "Portão de Outra Cidade",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.City, otherCityId.ToString(), new CellCoord(1, 1))));

        var result = CityProjector.Build(world, city.Id);

        Assert.Empty(result.Value!.Portals);
    }
}
