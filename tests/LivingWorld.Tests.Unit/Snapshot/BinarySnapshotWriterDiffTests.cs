using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

public class BinarySnapshotWriterDiffTests
{
    private static WorldState Clone(WorldState world) =>
        WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

    private static (WorldState baseline, WorldState current, long npcId) MutateFirstNpcHealth(
        ulong seed,
        int newHealth)
    {
        var (world, _) = ScenarioRunner.Create(seed: seed, initialPopulation: 5);
        var baseline = Clone(world);
        var npc = world.Npcs[0];
        npc.SetHealth(newHealth);
        return (baseline, world, npc.Id.Value);
    }

    private static string ReadDeltaJson(MemoryStream ms)
    {
        ms.Position = 0;
        var magic = new byte[8];
        ms.ReadExactly(magic);
        Assert.Equal("LWSNAP1\0"u8.ToArray(), magic);
        Assert.Equal(1, ms.ReadByte());

        var countBuf = new byte[4];
        ms.ReadExactly(countBuf);
        int count = BitConverter.ToInt32(countBuf);
        for (int i = 0; i < count; i++)
        {
            var idBuf = new byte[8];
            ms.ReadExactly(idBuf);
        }

        ms.ReadExactly(countBuf);
        int jsonLen = BitConverter.ToInt32(countBuf);
        var jsonBytes = new byte[jsonLen];
        ms.ReadExactly(jsonBytes);
        return Encoding.UTF8.GetString(jsonBytes);
    }

    [Fact]
    public void Delta_round_trip_preserves_canonical_hash()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 9, newHealth: 42);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, baseline);
        Assert.Equal(WorldSnapshot.CanonicalHash(current), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Delta_contains_only_changed_npc_fields()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 11, newHealth: 55);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);

        var json = JsonNode.Parse(ReadDeltaJson(ms))!.AsObject();
        var diffNpc = json["Npcs"]!.AsArray().Single()!.AsObject();

        Assert.True(diffNpc.ContainsKey("Id"));
        Assert.True(diffNpc.ContainsKey("Health"));
        Assert.False(diffNpc.ContainsKey("Name"));
        Assert.False(diffNpc.ContainsKey("BirthDate"));
        Assert.False(diffNpc.ContainsKey("Skills"));
    }

    [Fact]
    public void Delta_is_smaller_than_full_npc_entity()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 13, newHealth: 60);
        var writer = new BinarySnapshotWriter();

        using var deltaMs = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, deltaMs);
        var deltaJson = ReadDeltaJson(deltaMs);
        var deltaNpc = JsonNode.Parse(deltaJson)!["Npcs"]!.AsArray().Single()!.ToJsonString();

        var fullNpc = JsonNode.Parse(WorldSnapshot.Serialize(current))!["Npcs"]!.AsArray()
            .First(n => n!["Id"]!["Value"]!.GetValue<long>() == npcId)!
            .ToJsonString();

        Assert.True(deltaNpc.Length < fullNpc.Length);
    }

    [Fact]
    public void ReadAndApply_keeps_unchanged_npcs_from_baseline()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 17, newHealth: 33);
        var unchangedNpc = baseline.Npcs[1];
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, baseline);

        Assert.Equal(unchangedNpc.Health, restored.Npcs[1].Health);
        Assert.Equal(unchangedNpc.Name, restored.Npcs[1].Name);
        Assert.Equal(33, restored.Npcs[0].Health);
    }

    [Fact]
    public void Delta_new_npc_without_baseline_includes_all_fields()
    {
        var (world, _) = ScenarioRunner.Create(seed: 19, initialPopulation: 3);
        var baselineJson = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        baselineJson["Npcs"]!.AsArray().RemoveAt(0);
        var baseline = WorldSnapshot.Deserialize(baselineJson.ToJsonString());
        var newborn = world.Npcs[0];

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { newborn.Id.Value }, ms);

        var diffNpc = JsonNode.Parse(ReadDeltaJson(ms))!["Npcs"]!.AsArray().Single()!.AsObject();
        var fullNpc = JsonNode.Parse(WorldSnapshot.Serialize(world))!["Npcs"]!.AsArray()
            .First(n => n!["Id"]!["Value"]!.GetValue<long>() == newborn.Id.Value)!
            .AsObject();

        foreach (var prop in fullNpc)
            Assert.True(diffNpc.ContainsKey(prop.Key));
    }

    [Fact]
    public void Delta_handles_multiple_dirty_npcs_with_independent_field_diffs()
    {
        var (world, _) = ScenarioRunner.Create(seed: 23, initialPopulation: 5);
        var baseline = Clone(world);
        world.Npcs[0].SetHealth(10);
        world.Npcs[2].SetHealth(20);
        var dirty = new HashSet<long> { world.Npcs[0].Id.Value, world.Npcs[2].Id.Value };

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, dirty, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, baseline);

        Assert.Equal(10, restored.Npcs[0].Health);
        Assert.Equal(20, restored.Npcs[2].Health);
        Assert.Equal(baseline.Npcs[1].Health, restored.Npcs[1].Health);
    }

    [Fact]
    public void Delta_binary_envelope_uses_magic_and_marker_one()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 29, newHealth: 44);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);

        ms.Position = 0;
        var magic = new byte[8];
        ms.ReadExactly(magic);
        Assert.Equal("LWSNAP1\0"u8.ToArray(), magic);
        Assert.Equal(1, ms.ReadByte());
    }

    [Fact]
    public void Delta_dirty_set_lists_all_dirty_npc_ids()
    {
        var (world, _) = ScenarioRunner.Create(seed: 31, initialPopulation: 4);
        var baseline = Clone(world);
        var id0 = world.Npcs[0].Id.Value;
        var id2 = world.Npcs[2].Id.Value;
        world.Npcs[0].SetHealth(5);
        world.Npcs[2].SetHealth(6);

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { id0, id2 }, ms);

        ms.Position = 8;
        Assert.Equal(1, ms.ReadByte());
        var countBuf = new byte[4];
        ms.ReadExactly(countBuf);
        int count = BitConverter.ToInt32(countBuf);
        Assert.Equal(2, count);

        var ids = new List<long>();
        for (int i = 0; i < count; i++)
        {
            var idBuf = new byte[8];
            ms.ReadExactly(idBuf);
            ids.Add(BitConverter.ToInt64(idBuf));
        }

        Assert.Contains(id0, ids);
        Assert.Contains(id2, ids);
    }

    [Fact]
    public void Delta_omits_unchanged_npc_from_payload_array()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 37, newHealth: 48);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);

        var npcs = JsonNode.Parse(ReadDeltaJson(ms))!["Npcs"]!.AsArray();
        Assert.Single(npcs);
        Assert.Equal(npcId, npcs[0]!["Id"]!["Value"]!.GetValue<long>());
    }

    [Fact]
    public void Delta_applies_world_level_changes_from_current_state()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 41, initialPopulation: 3);
        var baseline = Clone(world);
        clock.Run(world, 10);
        var npcId = world.Npcs[0].Id.Value;
        world.Npcs[0].SetHealth(25);

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { npcId }, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, baseline);

        Assert.Equal(world.CurrentDate.TotalHours, restored.CurrentDate.TotalHours);
        Assert.Equal(25, restored.Npcs[0].Health);
    }

    [Fact]
    public void Delta_empty_field_diff_when_dirty_npc_has_no_changes()
    {
        var (world, _) = ScenarioRunner.Create(seed: 43, initialPopulation: 3);
        var baseline = Clone(world);
        var npcId = world.Npcs[0].Id.Value;

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { npcId }, ms);

        var diffNpc = JsonNode.Parse(ReadDeltaJson(ms))!["Npcs"]!.AsArray().Single()!.AsObject();
        Assert.True(diffNpc.ContainsKey("Id"));
        Assert.False(diffNpc.ContainsKey("Health"));
    }

    [Fact]
    public void Delta_round_trip_after_simulation_ticks_matches_full_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 47, initialPopulation: 8);
        clock.Run(world, 50);
        var baseline = Clone(world);
        clock.Run(world, 30);

        var dirty = world.Npcs.Select(n => n.Id.Value).ToHashSet();

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, dirty, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, baseline);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }
}
