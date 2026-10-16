using System.Text.Json.Nodes;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#47 fatia 2 (ADR-0037): pontos de uso com ação e pose, e o servidor dizendo qual objeto cada ocupante usa.</summary>
public class ObjectUseTests
{
    private static JsonObject Root(Action<JsonObject>? tweak = null)
    {
        var root = new JsonObject();
        DefaultObjects.AddTo(root);
        tweak?.Invoke(root);
        return root;
    }

    private static JsonObject Bed(JsonObject root) => (JsonObject)((JsonArray)root["Objects"]!).Single(o => (string)o!["id"]! == "bed")!;

    private static JsonObject BedPoint(JsonObject root) => (JsonObject)((JsonArray)Bed(root)["usePoints"]!)[0]!;

    private static WorldState WorldWith(ObjectCatalog? catalog) => ScenarioRunner.Create(seed: 13, initialPopulation: 0, objectCatalog: catalog).World;

    private static Building AddBuilding(WorldState world, int typeId)
    {
        var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null, aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, typeId, completedAtTick: 0);
        world.AddBuilding(building);
        return building;
    }

    private static Npc Enter(WorldState world, Building building, string name, ActionType? action)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), name, Sex.Female, WorldDate.Epoch(new WorldCalendar(24, 30, 12)), new CultureId(1), new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0), city: building.City);
        world.AddNpc(npc);
        var door = BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells.Single(c => c.Material == BuildingMaterial.Door).Cell;
        Assert.True(InteriorEntry.TryEnter(world, npc, building.Id, FloorLevel.Ground, door).IsSuccess);
        if (action is { } a) npc.SetCurrentAction(a, 0);
        return npc;
    }

    private static string? Using(WorldState world, Building building, Npc npc) =>
        InteriorProjector.Build(world, building.Id).Value!.Occupants.Single(o => o.Npc == npc.Id).UsingObject;

    [Fact]
    public void A_use_point_keeps_its_action_pose_offset_and_facing()
    {
        var point = ObjectCatalogScenarioLoader.Load(Root()).Value!.Find("bed")!.UsePoints.Single();

        Assert.Equal("lie", point.Name);
        Assert.Equal((int)ActionType.Sleep, point.Action);
        Assert.Equal("sleep", point.Pose);
        Assert.Equal([0.0, 0.05, 0.3], point.Offset);
        Assert.Equal(0.0, point.Facing);
    }

    [Fact]
    public void A_use_point_without_action_or_pose_is_valid_and_has_neither()
    {
        var catalog = ObjectCatalogScenarioLoader.Load(Root(r =>
        {
            BedPoint(r).Remove("action");
            BedPoint(r).Remove("pose");
        })).Value!;

        var point = catalog.Find("bed")!.UsePoints.Single();
        Assert.Null(point.Action);
        Assert.Null(point.Pose);
    }

    [Fact]
    public void An_unknown_action_id_or_pose_is_refused_with_its_path()
    {
        var action = ObjectCatalogScenarioLoader.Load(Root(r => BedPoint(r)["action"] = 99));
        var textAction = ObjectCatalogScenarioLoader.Load(Root(r => BedPoint(r)["action"] = "sleep"));
        var pose = ObjectCatalogScenarioLoader.Load(Root(r => BedPoint(r)["pose"] = "levitate"));
        var textPose = ObjectCatalogScenarioLoader.Load(Root(r => BedPoint(r)["pose"] = 3));

        Assert.Contains("usePoints[0].action", action.Error);
        Assert.Contains("usePoints[0].action", textAction.Error);
        Assert.Contains("usePoints[0].pose", pose.Error);
        Assert.Contains("usePoints[0].pose", textPose.Error);
    }

    [Fact]
    public void The_pose_vocabulary_is_the_one_the_client_animation_catalog_knows()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "client", "godot", "Scenes", "animation_catalog.gd"));
        var line = source.Split('\n').Single(l => l.StartsWith("const POSES"));
        var clientPoses = System.Text.RegularExpressions.Regex.Matches(line, "\"([a-z]+)\"").Select(m => m.Groups[1].Value);

        Assert.Equal(clientPoses.Order(), ObjectPoses.Known.Order());
    }

    [Fact]
    public void Use_points_survive_the_snapshot_round_trip()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        var point = restored.ObjectCatalog!.Find("bed")!.UsePoints.Single();
        Assert.Equal((int)ActionType.Sleep, point.Action);
        Assert.Equal("sleep", point.Pose);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void A_sleeping_occupant_of_a_house_uses_the_bed_and_an_eating_one_the_table()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var house = AddBuilding(world, typeId: 3);
        var sleeper = Enter(world, house, "ana", ActionType.Sleep);
        var eater = Enter(world, house, "bia", ActionType.Eat);

        Assert.Equal("bed", Using(world, house, sleeper));
        Assert.Equal("table", Using(world, house, eater));
    }

    [Fact]
    public void An_action_no_furniture_serves_or_no_action_at_all_uses_nothing()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var house = AddBuilding(world, typeId: 3);
        var walker = Enter(world, house, "ana", ActionType.Travel);
        var idle = Enter(world, house, "bia", null);

        Assert.Null(Using(world, house, walker));
        Assert.Null(Using(world, house, idle));
    }

    [Fact]
    public void Only_the_furniture_of_the_building_type_counts()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var shop = AddBuilding(world, typeId: 1);
        var sleeper = Enter(world, shop, "ana", ActionType.Sleep);
        var clerk = Enter(world, shop, "bia", ActionType.Work);

        Assert.Null(Using(world, shop, sleeper));
        Assert.Equal("counter", Using(world, shop, clerk));
    }

    [Fact]
    public void The_delta_source_uses_the_furniture_of_the_building_type_not_a_fixed_one()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var shop = AddBuilding(world, typeId: 1);
        var sleeper = Enter(world, shop, "ana", ActionType.Sleep);
        var clerk = Enter(world, shop, "bia", ActionType.Work);

        var occupants = InteriorProjector.BuildOccupants(world, shop.Id)!;

        Assert.Null(occupants.Single(o => o.Npc == sleeper.Id).UsingObject);
        Assert.Equal("counter", occupants.Single(o => o.Npc == clerk.Id).UsingObject);
    }

    [Fact]
    public void The_first_piece_in_furnishing_order_that_serves_the_action_wins()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root(r => ((JsonObject)r["Furnishings"]!)["3"] = new JsonArray("anvil", "counter"))).Value!);
        var house = AddBuilding(world, typeId: 3);
        var worker = Enter(world, house, "ana", ActionType.Work);

        Assert.Equal("anvil", Using(world, house, worker));
    }

    [Fact]
    public void A_world_without_Objects_reports_no_object_in_use_and_omits_the_field()
    {
        var world = WorldWith(null);
        var house = AddBuilding(world, typeId: 3);
        var sleeper = Enter(world, house, "ana", ActionType.Sleep);

        var snapshot = InteriorProjector.Build(world, house.Id).Value!;

        Assert.Null(Using(world, house, sleeper));
        Assert.DoesNotContain("usingObject", System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void The_json_the_client_reads_names_the_object_in_use()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var house = AddBuilding(world, typeId: 3);
        Enter(world, house, "ana", ActionType.Sleep);

        var json = System.Text.Json.JsonSerializer.SerializeToNode(
            InteriorProjector.Build(world, house.Id).Value!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Equal("bed", (string)json["occupants"]![0]!["usingObject"]!);
        var point = json["objects"]!["catalog"]!.AsArray().First(o => (string)o!["id"]! == "bed")!["usePoints"]![0]!;
        Assert.Equal("sleep", (string)point["pose"]!);
        Assert.Equal(1, (int)point["action"]!);
        Assert.Equal(0.3, (double)point["offset"]![2]!);
    }

    [Fact]
    public void BuildOccupants_the_delta_source_also_names_the_object_in_use()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root()).Value!);
        var house = AddBuilding(world, typeId: 3);
        var sleeper = Enter(world, house, "ana", ActionType.Sleep);

        var occupants = InteriorProjector.BuildOccupants(world, house.Id)!;

        Assert.Equal("bed", occupants.Single(o => o.Npc == sleeper.Id).UsingObject);
    }

    [Fact]
    public void Changing_only_the_object_in_use_counts_as_a_move_in_the_delta()
    {
        var before = new InteriorOccupant(new NpcId(1), FloorLevel.Ground, new CellCoord(1, 1), ActionType.Work, null, "anvil");
        var after = before with { UsingObject = "counter" };

        var diff = InteriorOccupancyDiffer.Diff([before], [after]);

        Assert.Single(diff.Moved);
        Assert.Empty(InteriorOccupancyDiffer.Diff([before], [before]).Moved);
    }

    [Fact]
    public void A_use_point_without_an_action_attracts_nobody()
    {
        var world = WorldWith(ObjectCatalogScenarioLoader.Load(Root(r => BedPoint(r).Remove("action"))).Value!);
        var house = AddBuilding(world, typeId: 3);
        var sleeper = Enter(world, house, "ana", ActionType.Sleep);

        Assert.Null(Using(world, house, sleeper));
    }

    [Theory]
    [InlineData("bed", "lie", 1, "sleep", 0.0, 0.05, 0.3, 0.0)]
    [InlineData("table", "eat", 0, "eat", 0.0, 0.0, 0.45, 180.0)]
    [InlineData("bench", "sit", 3, "socialize", 0.0, 0.0, 0.3, 180.0)]
    [InlineData("anvil", "hammer", 2, "work", 0.0, 0.0, 0.3, 180.0)]
    [InlineData("counter", "serve", 2, "work", 0.0, 0.0, -0.35, 0.0)]
    public void Each_default_use_point_has_its_declared_name_action_pose_offset_and_facing(
        string id, string name, int action, string pose, double x, double y, double z, double facing)
    {
        var point = ObjectCatalogScenarioLoader.Load(Root()).Value!.Find(id)!.UsePoints.Single();

        Assert.Equal(name, point.Name);
        Assert.Equal(action, point.Action);
        Assert.Equal(pose, point.Pose);
        Assert.Equal([x, y, z], point.Offset);
        Assert.Equal(facing, point.Facing);
    }

    [Fact]
    public void The_default_use_points_serve_the_actions_their_pieces_are_for()
    {
        var catalog = ObjectCatalogScenarioLoader.Load(Root()).Value!;

        Assert.Equal((int)ActionType.Eat, catalog.Find("table")!.UsePoints.Single().Action);
        Assert.Equal((int)ActionType.Work, catalog.Find("anvil")!.UsePoints.Single().Action);
        Assert.Equal((int)ActionType.Work, catalog.Find("counter")!.UsePoints.Single().Action);
        Assert.Equal((int)ActionType.Socialize, catalog.Find("bench")!.UsePoints.Single().Action);
        Assert.Empty(catalog.Find("crate")!.UsePoints);
    }
}
