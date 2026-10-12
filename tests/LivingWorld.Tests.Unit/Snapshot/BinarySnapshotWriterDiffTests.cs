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

    /// <summary>A árvore do diff (Entities/Keys/Removed/Props/Order) tal como foi gravada.</summary>
    private static JsonObject ReadDelta(MemoryStream ms)
    {
        var data = ms.ToArray();
        Assert.True(PositionalSnapshot.IsDelta(data));
        return PositionalTreeCodec.Decode(data.AsSpan(9))!.AsObject();
    }

    private static JsonArray DeltaNpcs(MemoryStream ms) => ReadDelta(ms)["Entities"]!["Npcs"]!.AsArray();

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

        var diffNpc = DeltaNpcs(ms).Single()!.AsObject();

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
        var deltaNpc = DeltaNpcs(deltaMs).Single()!.ToJsonString();

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

        var diffNpc = DeltaNpcs(ms).Single()!.AsObject();
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

        var data = ms.ToArray();
        Assert.Equal("LWSNAP2\0"u8.ToArray(), data[..8]);
        Assert.Equal(1, data[8]);
    }

    [Fact]
    public void Delta_compares_only_the_dirty_npcs()
    {
        var (world, _) = ScenarioRunner.Create(seed: 31, initialPopulation: 4);
        var baseline = Clone(world);
        var id0 = world.Npcs[0].Id.Value;
        world.Npcs[0].SetHealth(5);
        world.Npcs[2].SetHealth(6); // mudou, mas não está no conjunto de sujos

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { id0 }, ms);

        var ids = DeltaNpcs(ms).Select(n => n!["Id"]!["Value"]!.GetValue<long>()).ToList();
        Assert.Equal([id0], ids);
    }

    [Fact]
    public void Delta_omits_unchanged_npc_from_payload_array()
    {
        var (baseline, current, npcId) = MutateFirstNpcHealth(seed: 37, newHealth: 48);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, new HashSet<long> { npcId }, ms);

        var npcs = DeltaNpcs(ms);
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
    public void Delta_has_no_entry_for_a_dirty_npc_that_did_not_change()
    {
        var (world, _) = ScenarioRunner.Create(seed: 43, initialPopulation: 3);
        var baseline = Clone(world);
        var npcId = world.Npcs[0].Id.Value;

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(world, baseline, new HashSet<long> { npcId }, ms);

        Assert.False(ReadDelta(ms)["Entities"]!.AsObject().ContainsKey("Npcs"));
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
