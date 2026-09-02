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

        var city = world.ActiveCities().Single();

        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: -1, completedAtTick: 0);
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
        var marker = Assert.Single(result.Value!.Residents, item => item.Id == resident.Id);
        Assert.Equal(resident.Id, marker.Id);
        Assert.Equal(resident.CurrentLocation, marker.Location);
        Assert.Equal(resident.CurrentAction, marker.CurrentAction);
    }

    [Fact]
    public void Build_lists_the_citys_buildings()
    {
        var (world, city, _) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.NotEmpty(result.Value!.Buildings);
    }

    // --- Fase 15.1, T20: campos de posição de prédio (Location/LocationIsDerived) ---

    [Fact]
    public void Build_resolves_an_unauthored_buildings_location_as_derived_and_stable()
    {
        var (world, city, _) = MakeWorldWithCity();
        var engineBuilding = world.Buildings.Single(building => building.City == city.Id && building.Position is null);

        var marker = CityProjector.Build(world, city.Id).Value!.Buildings.Single(building => building.Id == engineBuilding.Id);

        Assert.True(marker.LocationIsDerived);
        var repeated = CityProjector.Build(world, city.Id).Value!.Buildings.Single(building => building.Id == engineBuilding.Id);
        Assert.Equal(marker.Location, repeated.Location);
    }

    [Fact]
    public void Build_does_not_render_an_authored_overflow_building_outside_the_city_grid()
    {
        var (world, city, _) = MakeWorldWithCity();
        var authored = new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 2, completedAtTick: 0,
            position: new CellCoord(city.Location.X + 9, city.Location.Y + 9), orientation: 90);
        world.AddBuilding(authored);

        var markers = CityProjector.Build(world, city.Id).Value!.Buildings;

        Assert.DoesNotContain(markers, marker => marker.Id == authored.Id);
    }

    [Fact]
    public void Build_does_not_change_the_canonical_hash_by_projecting_building_locations()
    {
        var (world, city, _) = MakeWorldWithCity();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        CityProjector.Build(world, city.Id);

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Build_includes_the_aggregate_pool_as_is()
    {
        var (world, city, _) = MakeWorldWithCity();

        var result = CityProjector.Build(world, city.Id);

        Assert.Equal(city.AggregatePool, result.Value!.AggregatePool);
    }

    // --- Fase 15.1, T30: campo Indicators (os 6 indicadores de CityPopulationQuery) ---

    [Fact]
    public void Build_includes_the_six_indicators_matching_CityPopulationQuery()
    {
        var (world, city, _) = MakeWorldWithCity();

        var indicators = CityProjector.Build(world, city.Id).Value!.Indicators;

        Assert.Equal(CityPopulationQuery.Population(world, city.Id), indicators.Population);
        Assert.Equal(CityPopulationQuery.Wealth(world, city.Id), indicators.Wealth);
        Assert.Equal(CityPopulationQuery.Health(world, city.Id), indicators.Health);
        Assert.Equal(CityPopulationQuery.Inequality(world, city.Id), indicators.Inequality);
        Assert.Equal(CityPopulationQuery.Economy(world, city.Id), indicators.Economy);
        Assert.Equal(CityPopulationQuery.Housing(world, city.Id), indicators.Housing);
    }

    [Fact]
    public void Build_does_not_change_the_canonical_hash_by_projecting_city_indicators()
    {
        var (world, city, _) = MakeWorldWithCity();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        CityProjector.Build(world, city.Id);

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Build_reports_zero_indicators_for_a_city_with_no_residents_or_buildings()
    {
        var world = ScenarioRunner.Create(seed: 11, initialPopulation: 0).World;
        var emptyCity = new City(
            world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(0, 0, 0));
        world.AddCity(emptyCity);

        var indicators = CityProjector.Build(world, emptyCity.Id).Value!.Indicators;

        Assert.Equal(0, indicators.Population);
        Assert.Equal(0, indicators.Wealth);
        Assert.Equal(0, indicators.Health);
        Assert.Equal(0.0, indicators.Inequality);
        Assert.Equal(0, indicators.Economy);
        Assert.Equal(0, indicators.Housing);
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
