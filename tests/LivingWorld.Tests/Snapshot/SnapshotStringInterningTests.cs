using System.Text.Json.Nodes;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Snapshot;

public class SnapshotStringInterningTests
{
    [Fact]
    public void Serialize_includes_StringTable_when_internable_strings_exist()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        clock.Run(world, 80);

        var json = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        Assert.NotNull(json["StringTable"]);
        Assert.IsType<JsonArray>(json["StringTable"]);
    }

    [Fact]
    public void Round_trip_preserves_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 7);
        clock.Run(world, 120);
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(rehydrated));
    }

    [Fact]
    public void Npcs_with_same_profession_share_intern_reference()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 13);
        clock.Run(world, 60);

        var json = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        var npcArray = json["Npcs"]!.AsArray();
        var refByProfessionId = new Dictionary<int, int>();

        for (int i = 0; i < world.Npcs.Count; i++)
        {
            int profId = world.Npcs[i].Profession.Id;
            int wireRef = npcArray[i]!["Profession"]!.GetValue<int>();
            if (refByProfessionId.TryGetValue(profId, out int existing))
                Assert.Equal(existing, wireRef);
            else
                refByProfessionId[profId] = wireRef;
        }

        Assert.NotEmpty(refByProfessionId);
    }

    [Fact]
    public void Round_trip_preserves_npc_profession_ids()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 21);
        clock.Run(world, 50);
        var professionsBefore = world.Npcs.Select(n => n.Profession.Id).ToArray();

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(professionsBefore, rehydrated.Npcs.Select(n => n.Profession.Id).ToArray());
    }

    [Fact]
    public void Round_trip_preserves_pending_event_system_names()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 33);
        clock.Run(world, 100);
        var namesBefore = world.PendingEvents.Select(e => e.SystemName).ToArray();

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(namesBefore, rehydrated.PendingEvents.Select(e => e.SystemName).ToArray());
    }

    [Fact]
    public void Legacy_snapshot_without_StringTable_still_deserializes()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 5);
        clock.Run(world, 30);
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var node = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        SnapshotStringInterning.Resolve(node);
        var legacyJson = node.ToJsonString();

        var rehydrated = WorldSnapshot.Deserialize(legacyJson);
        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(rehydrated));
    }

    [Fact]
    public void Interned_SystemName_values_are_numeric_not_literal_strings()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 99);
        clock.Run(world, 150);
        Assert.NotEmpty(world.PendingEvents);

        var json = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        var firstEvent = json["PendingEvents"]!.AsArray()[0]!.AsObject();
        var systemName = firstEvent["SystemName"]!;
        Assert.True(systemName is JsonValue value && value.TryGetValue<int>(out _));
    }
}
