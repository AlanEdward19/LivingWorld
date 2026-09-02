using System.Text.Json;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T18 (LWV-04.5): o marcador de prédio concluído expõe
/// <c>location</c>/<c>locationIsDerived</c> no fio camelCase que o cliente da cidade consome —
/// mesma origem que <see cref="BuildingPlacementResolver"/>, nunca só o anel do front.</summary>
public class CityBuildingMarkerContractTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Marker_json_exposes_camelCase_location_and_locationIsDerived()
    {
        var marker = new CityBuildingMarker(
            new BuildingId(8), 2, new CellCoord(4, -2), LocationIsDerived: true, Orientation: 270);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(marker, Wire));
        var root = doc.RootElement;

        Assert.Equal(4, root.GetProperty("location").GetProperty("x").GetInt32());
        Assert.Equal(-2, root.GetProperty("location").GetProperty("y").GetInt32());
        Assert.True(root.GetProperty("locationIsDerived").GetBoolean());
        Assert.Equal(2, root.GetProperty("buildingTypeId").GetInt32());
        Assert.Equal(270, root.GetProperty("orientation").GetInt32());
    }

    [Fact]
    public void Authored_marker_json_sets_locationIsDerived_false()
    {
        var marker = new CityBuildingMarker(new BuildingId(3), 1, new CellCoord(9, 7), LocationIsDerived: false);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(marker, Wire));

        Assert.False(doc.RootElement.GetProperty("locationIsDerived").GetBoolean());
        Assert.Equal(9, doc.RootElement.GetProperty("location").GetProperty("x").GetInt32());
        Assert.Equal(7, doc.RootElement.GetProperty("location").GetProperty("y").GetInt32());
    }

    [Fact]
    public void Projector_marker_location_matches_BuildingPlacementResolver()
    {
        var world = ScenarioRunner.Create(seed: 18, initialPopulation: 1).World;
        var npc = world.Npcs.First();
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, -1, 0);
        world.AddBuilding(building);

        var marker = Assert.Single(CityProjector.Build(world, city.Id).Value!.Buildings);
        // dynamic-city-growth, T3: mesmos bounds que CityProjector.Build resolve internamente,
        // pra comparar com o mesmo contexto de ocupação.
        long population = CityPopulationQuery.Population(world, city.Id);
        var bounds = SpatialBoundsResolver.ResolveCity(city, population, world.Map.Width, world.Map.Height).Bounds;
        var resolved = BuildingPlacementResolver.Resolve(building, city, world, bounds);

        Assert.NotNull(resolved);
        Assert.Equal(resolved!.Value.Position, marker.Location);
        Assert.Equal(resolved.Value.IsDerived, marker.LocationIsDerived);
        Assert.Equal(resolved.Value.Orientation, marker.Orientation);
    }
}
