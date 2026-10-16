using System.Text.Json.Nodes;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#47 fatia 1 (ADR-0034): formato de objeto no cenário, validado na borda, e interior servido pelo catálogo.</summary>
public class ObjectCatalogTests
{
    private static JsonObject Root(Action<JsonObject>? tweak = null)
    {
        var root = new JsonObject();
        DefaultObjects.AddTo(root);
        tweak?.Invoke(root);
        return root;
    }

    public static ObjectCatalog SampleCatalog() => ObjectCatalogScenarioLoader.Load(Root()).Value!;

    private static JsonObject FirstPart(JsonObject root) => (JsonObject)((JsonArray)((JsonObject)((JsonArray)root["Objects"]!)[0]!)["shape"]!)[0]!;

    [Fact]
    public void A_scenario_without_Objects_loads_no_catalog()
    {
        var result = ObjectCatalogScenarioLoader.Load(new JsonObject());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void The_default_objects_load_with_every_piece_the_client_used_to_hardcode()
    {
        var catalog = ObjectCatalogScenarioLoader.Load(Root()).Value!;

        Assert.Equal(9, catalog.Objects.Count);
        Assert.Equal(["bed", "table", "chest"], catalog.FurnishingFor(3));
        Assert.Equal(["table", "bench", "crate"], catalog.FurnishingFor(999));
    }

    [Fact]
    public void The_default_objects_match_the_assets_json_pieces_and_furnishings_exactly()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        var assets = JsonNode.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "client", "godot", "assets.json")))!.AsObject();
        var catalog = ObjectCatalogScenarioLoader.Load(Root()).Value!;

        var pieces = assets["pieces"]!.AsObject();
        Assert.Equal(pieces.Count, catalog.Objects.Count);
        foreach (var (id, spec) in pieces)
        {
            var part = catalog.Find(id)!.Shape.Single();
            Assert.Equal(spec!["size"]!.AsArray().Select(n => (double)n!).ToArray(), part.Size);
            Assert.Equal((string)spec["color"]!, part.Color);
            Assert.Equal((double)spec["size"]![1]! / 2.0, part.Offset[1]);
        }

        var furniture = assets["furniture"]!.AsObject();
        Assert.Equal(furniture.Count, catalog.Furnishings.Count);
        foreach (var (key, ids) in furniture)
            Assert.Equal(ids!.AsArray().Select(n => (string)n!).ToArray(), catalog.Furnishings[key]);
    }

    [Theory]
    [InlineData("size", 0.0, "Objects[0].shape[0].size[0]")]
    [InlineData("size", -1.0, "Objects[0].shape[0].size[0]")]
    public void A_non_positive_dimension_is_refused_with_its_path(string field, double value, string expectedPath)
    {
        var root = Root(r => ((JsonArray)FirstPart(r)[field]!)[0] = value);

        var result = ObjectCatalogScenarioLoader.Load(root);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedPath, result.Error);
    }

    [Fact]
    public void An_unknown_part_is_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(r => FirstPart(r)["shape"] = "torus"));

        Assert.False(result.IsSuccess);
        Assert.Contains("parte desconhecida", result.Error);
    }

    [Fact]
    public void An_unknown_format_version_is_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(r => r["objectsFormat"] = 2));

        Assert.False(result.IsSuccess);
        Assert.Contains("versão 2 desconhecida", result.Error);
    }

    [Fact]
    public void Objects_without_the_format_version_are_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(r => r.Remove("objectsFormat")));

        Assert.False(result.IsSuccess);
        Assert.Contains("objectsFormat", result.Error);
    }

    [Fact]
    public void A_furnishing_that_names_a_missing_object_is_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(r => ((JsonObject)r["Furnishings"]!)["3"] = new JsonArray("ghost")));

        Assert.False(result.IsSuccess);
        Assert.Contains("objeto 'ghost' não existe", result.Error);
    }

    [Fact]
    public void A_duplicate_id_is_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(r => ((JsonArray)r["Objects"]!).Add(((JsonArray)r["Objects"]!)[0]!.DeepClone())));

        Assert.False(result.IsSuccess);
        Assert.Contains("duplicado", result.Error);
    }

    [Fact]
    public void The_same_seed_and_slot_always_give_the_same_variation_and_seed_zero_never_varies()
    {
        Assert.Equal(ObjectVariation.Index(7, 3), ObjectVariation.Index(7, 3));
        Assert.All(Enumerable.Range(0, 20), slot => Assert.Equal(0, ObjectVariation.Index(0, slot)));
        Assert.Contains(Enumerable.Range(0, 20), slot => ObjectVariation.Index(7, slot) != 0);
    }

    [Fact]
    public void The_catalog_survives_a_snapshot_round_trip_with_the_same_canonical_hash()
    {
        var world = WorldWithCatalog();

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.ObjectCatalog!.Objects.Count, restored.ObjectCatalog!.Objects.Count);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void A_world_without_Objects_keeps_the_hash_of_before_and_the_catalog_changes_it_when_present()
    {
        var without = ScenarioRunner.Create(seed: 13, initialPopulation: 0).World;
        var with = WorldWithCatalog();

        Assert.Null(without.ObjectCatalog);
        Assert.DoesNotContain("ObjectCatalog", WorldSnapshot.Serialize(without));
        Assert.NotEqual(WorldSnapshot.CanonicalHash(without), WorldSnapshot.CanonicalHash(with));
    }

    [Fact]
    public void The_interior_snapshot_lists_the_catalog_furniture_of_the_building_type()
    {
        var world = WorldWithCatalog();
        var building = AddBuilding(world, typeId: 10);

        var snapshot = InteriorProjector.Build(world, building.Id).Value!;

        Assert.Equal(["bed", "bed", "table", "chest"], snapshot.Objects!.Furniture.Select(p => p.Id));
        Assert.Equal(9, snapshot.Objects.Catalog.Count);
    }

    [Fact]
    public void The_interior_snapshot_of_a_world_without_Objects_has_no_objects_field()
    {
        var world = ScenarioRunner.Create(seed: 13, initialPopulation: 0).World;
        var building = AddBuilding(world, typeId: 10);

        var snapshot = InteriorProjector.Build(world, building.Id).Value!;

        Assert.Null(snapshot.Objects);
        Assert.DoesNotContain("\"objects\"", System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void The_interior_snapshot_json_has_the_keys_the_client_reads()
    {
        var world = WorldWithCatalog();
        var building = AddBuilding(world, typeId: 3);

        var json = System.Text.Json.JsonSerializer.SerializeToNode(
            InteriorProjector.Build(world, building.Id).Value!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var objects = json["objects"]!;
        Assert.Equal(1, (int)objects["format"]!);
        Assert.Equal("bed", (string)objects["furniture"]![0]!["id"]!);
        Assert.Equal(0, (int)objects["furniture"]![0]!["variation"]!);
        var part = objects["catalog"]!.AsArray().First(o => (string)o!["id"]! == "bed")!["shape"]![0]!;
        Assert.Equal("box", (string)part["shape"]!);
        Assert.Equal(0.9, (double)part["size"]![2]!);
        Assert.Equal(0.1, (double)part["offset"]![1]!);
        Assert.Equal("d8cfb8", (string)part["color"]!);
    }

    [Fact]
    public void Furniture_variation_is_the_same_in_two_builds_and_stays_zero_for_seed_zero_pieces()
    {
        var world = WorldWithCatalog();
        var building = AddBuilding(world, typeId: 10);

        var first = InteriorProjector.Build(world, building.Id).Value!.Objects!.Furniture;
        var second = InteriorProjector.Build(world, building.Id).Value!.Objects!.Furniture;

        Assert.Equal(first, second);
        Assert.All(first, piece => Assert.Equal(0, piece.Variation));
    }

    private static Building AddBuilding(WorldState world, int typeId)
    {
        var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null, aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, typeId, completedAtTick: 0);
        world.AddBuilding(building);
        return building;
    }

    private static WorldState WorldWithCatalog()
    {
        return ScenarioRunner.Create(seed: 13, initialPopulation: 0, objectCatalog: SampleCatalog()).World;
    }
}
