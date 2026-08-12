using LivingWorld.Api.Visual;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T4 (VTT-01, VTT-04, VTT-06): <see cref="GlobalProjector"/> — cidades com
/// população agregada, NPCs materializados fora da própria cidade marcados como "externos", e
/// camadas globais com dado real (Terrain/Biome/Resources/Rivers) vs ainda-não-modeladas
/// (Mountains/Roads/Borders/Kingdoms/Climate).</summary>
public class GlobalProjectorTests
{
    private static (WorldState World, City City, Npc Resident, Npc Traveler) MakeWorldWithCity()
    {
        var world = ScenarioRunner.Create(seed: 5, initialPopulation: 2).World;
        var npcs = world.Npcs.ToList();
        var resident = npcs[0];
        var traveler = npcs[1];

        var city = new City(
            world.NextCityId(), resident.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(3, 300, 200));
        world.AddCity(city);

        resident.JoinCity(city.Id);
        traveler.JoinCity(city.Id);
        traveler.MoveTo(new CellCoord(city.Location.X + 1, city.Location.Y + 1), tick: 0);

        return (world, city, resident, traveler);
    }

    [Fact]
    public void Build_lists_every_city_with_its_aggregate_population()
    {
        var (world, city, _, _) = MakeWorldWithCity();

        var snapshot = GlobalProjector.Build(world);

        var marker = Assert.Single(snapshot.Cities);
        Assert.Equal(city.Id, marker.Id);
        Assert.Equal(city.Location, marker.Location);
        Assert.Equal(CityPopulationQuery(world, city.Id), marker.Population);
    }

    [Fact]
    public void Build_marks_a_materialized_npc_away_from_its_city_location_as_external()
    {
        var (world, city, resident, traveler) = MakeWorldWithCity();

        var snapshot = GlobalProjector.Build(world);

        var marker = Assert.Single(snapshot.ExternalNpcs);
        Assert.Equal(traveler.Id, marker.Id);
        Assert.Equal(traveler.CurrentLocation, marker.Location);
        Assert.DoesNotContain(snapshot.ExternalNpcs, m => m.Id == resident.Id);
    }

    [Fact]
    public void Build_returns_real_data_for_terrain_biome_resources_and_rivers()
    {
        var (world, _, _, _) = MakeWorldWithCity();

        var snapshot = GlobalProjector.Build(world);

        Assert.True(snapshot.Layers[VisualLayerId.Terrain].IsModeled);
        Assert.True(snapshot.Layers[VisualLayerId.Biome].IsModeled);
        Assert.True(snapshot.Layers[VisualLayerId.Resources].IsModeled);
        Assert.True(snapshot.Layers[VisualLayerId.Rivers].IsModeled);
    }

    [Theory]
    [InlineData(VisualLayerId.Mountains)]
    [InlineData(VisualLayerId.Roads)]
    [InlineData(VisualLayerId.Borders)]
    [InlineData(VisualLayerId.Kingdoms)]
    [InlineData(VisualLayerId.Climate)]
    public void Build_returns_not_yet_modeled_for_layers_without_domain_data(VisualLayerId layerId)
    {
        var (world, _, _, _) = MakeWorldWithCity();

        var snapshot = GlobalProjector.Build(world);

        Assert.False(snapshot.Layers[layerId].IsModeled);
        Assert.Null(snapshot.Layers[layerId].Payload);
    }

    [Fact]
    public void Build_never_returns_active_events_yet()
    {
        var (world, _, _, _) = MakeWorldWithCity();

        var snapshot = GlobalProjector.Build(world);

        Assert.Empty(snapshot.ActiveEvents);
    }

    // --- Fase 15.1, T21: campo Portals (SpatialPortal como conceito canônico) ---

    [Fact]
    public void Build_includes_a_portal_whose_origin_is_the_World_scope()
    {
        var (world, city, _, _) = MakeWorldWithCity();
        var portal = new SpatialPortal(
            "portal-north", "Portão Norte",
            new PortalEndpoint(PortalSpaceKind.World, "", new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(1, 1)));
        world.AddPortal(portal);

        var snapshot = GlobalProjector.Build(world);

        Assert.Equal(portal, Assert.Single(snapshot.Portals));
    }

    [Fact]
    public void Build_excludes_a_portal_that_never_touches_the_World_scope()
    {
        var (world, city, _, _) = MakeWorldWithCity();
        var otherCityId = world.NextCityId();
        world.AddPortal(new SpatialPortal(
            "portal-city-to-city", "Passagem Interna",
            new PortalEndpoint(PortalSpaceKind.City, city.Id.ToString(), new CellCoord(0, 0)),
            new PortalEndpoint(PortalSpaceKind.City, otherCityId.ToString(), new CellCoord(1, 1))));

        var snapshot = GlobalProjector.Build(world);

        Assert.Empty(snapshot.Portals);
    }

    private static long CityPopulationQuery(WorldState world, CityId cityId) =>
        LivingWorld.Simulation.CityPopulationQuery.Population(world, cityId);
}
