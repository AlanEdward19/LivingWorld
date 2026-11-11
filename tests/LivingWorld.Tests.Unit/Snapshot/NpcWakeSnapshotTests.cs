using System.Text.Json.Nodes;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>Despertares pendentes no snapshot (AD-129): estado canônico próprio, reidratado igual ao mundo que seguiu
/// rodando, e snapshot antigo (despertares em <c>PendingEvents</c>) continua abrindo.</summary>
public class NpcWakeSnapshotTests
{
    private const long SplitTick = 200;
    private const long Extra = 300;

    private static WorldState Run(ulong seed, long ticks)
    {
        var (world, clock) = ScenarioRunner.Create(seed: seed, initialPopulation: 40);
        clock.Run(world, ticks);
        return world;
    }

    private static string ContinueAndHash(WorldState world, long ticks)
    {
        new WorldClock(ScenarioRunner.DefaultSystems()).Run(world, ticks);
        return WorldSnapshot.CanonicalHash(world);
    }

    [Fact]
    public void Same_seed_and_ticks_give_the_same_canonical_hash()
    {
        Assert.Equal(WorldSnapshot.CanonicalHash(Run(21, SplitTick)), WorldSnapshot.CanonicalHash(Run(21, SplitTick)));
    }

    [Fact]
    public void Different_seeds_give_different_pending_wakes()
    {
        Assert.NotEqual(Run(21, SplitTick).PendingNpcWakes, Run(22, SplitTick).PendingNpcWakes);
    }

    [Fact]
    public void Reopening_a_positional_snapshot_keeps_running_like_the_continuous_world()
    {
        var continuous = Run(31, SplitTick);
        var reopened = PositionalSnapshot.DecodeFull(PositionalSnapshot.EncodeFull(continuous, out _));

        Assert.Equal(ContinueAndHash(continuous, Extra), ContinueAndHash(reopened, Extra));
    }

    [Fact]
    public void Reopening_a_json_snapshot_keeps_running_like_the_continuous_world()
    {
        var continuous = Run(32, SplitTick);
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(continuous));

        Assert.Equal(ContinueAndHash(continuous, Extra), ContinueAndHash(reopened, Extra));
    }

    [Fact]
    public void A_wake_moved_after_reopening_no_longer_fires_at_its_old_tick()
    {
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(Run(33, SplitTick)));
        var wake = reopened.PendingNpcWakes[0];

        NpcWakeScheduler.ScheduleWake(reopened, new TickContext(reopened, reopened.Rng, reopened.Scheduler), wake.NpcId, wake.TargetTick + 3);

        Assert.DoesNotContain(wake.NpcId, reopened.PeekDueNpcWakes(wake.TargetTick));
    }

    [Fact]
    public void A_legacy_snapshot_with_wakes_in_pending_events_keeps_running_like_the_continuous_world()
    {
        var continuous = Run(34, SplitTick);
        var legacy = WorldSnapshot.FromNode(ToLegacy(WorldSnapshot.BuildPersistentTree(continuous)));

        Assert.Equal(ContinueAndHash(continuous, Extra), ContinueAndHash(legacy, Extra));
    }

    [Fact]
    public void A_legacy_snapshot_moves_its_wakes_out_of_the_event_scheduler()
    {
        var continuous = Run(35, SplitTick);
        var legacy = WorldSnapshot.FromNode(ToLegacy(WorldSnapshot.BuildPersistentTree(continuous)));

        Assert.Equal(
            (string.Join(';', continuous.PendingNpcWakes), 0),
            (string.Join(';', legacy.PendingNpcWakes), legacy.PendingEvents.Count(e => e.SystemName == NpcWakeScheduler.SystemName)));
    }

    [Fact]
    public void A_legacy_snapshot_with_two_wakes_for_one_npc_keeps_the_latest()
    {
        var continuous = Run(36, SplitTick);
        var tree = ToLegacy(WorldSnapshot.BuildPersistentTree(continuous));
        var wake = continuous.PendingNpcWakes[0];
        tree["PendingEvents"]!.AsArray().Add(LegacyEvent(wake with { TargetTick = wake.TargetTick + 1, Sequence = -1 }));

        var legacy = WorldSnapshot.FromNode(tree);

        Assert.Equal(wake, legacy.PendingNpcWakes[0]);
    }

    /// <summary>Forma do snapshot anterior ao AD-129: cada despertar é um <see cref="ScheduledEvent"/> de
    /// <see cref="NpcWakeScheduler.SystemName"/> com o id do NPC em texto, e não existe <c>PendingNpcWakes</c>.</summary>
    private static JsonObject ToLegacy(JsonObject tree)
    {
        var events = tree["PendingEvents"]!.AsArray();
        foreach (var wake in tree["PendingNpcWakes"]!.AsArray())
            events.Add(LegacyEvent(new PendingNpcWake(
                wake!["NpcId"]!.GetValue<long>(), wake["TargetTick"]!.GetValue<long>(), wake["Sequence"]!.GetValue<long>())));
        tree.Remove("PendingNpcWakes");
        return tree;
    }

    private static JsonObject LegacyEvent(PendingNpcWake wake) => new()
    {
        ["Id"] = wake.Sequence,
        ["TargetTick"] = wake.TargetTick,
        ["SystemName"] = NpcWakeScheduler.SystemName,
        ["Payload"] = wake.NpcId.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
