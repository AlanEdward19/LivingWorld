using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>Fase 28 T1 (COG-04, COG-20..23): <see cref="NpcCognitionLog"/> ring buffer + watchlist.</summary>
public class NpcCognitionLogTests
{
    private static readonly NpcId Npc = new(1);
    private static readonly NpcId OtherNpc = new(2);

    private static DecisionTrace Trace(ActionType winner = ActionType.Work, WakeReason wake = WakeReason.Scheduled) =>
        new(
            wake,
            PreviousIntent: ActionType.Sleep,
            TopPressures: [new Pressure("AcquireFood", 80, ["Hunger"])],
            KnownOpportunities: [new Opportunity("FoodAtMarket", 60)],
            winner,
            WinningUtility: 42.5,
            TopPositiveFactors: ["Hunger"],
            TopNegativeFactors: ["Distance"],
            BlockingFactors: [],
            KnownAlternatives: [ActionType.Sleep, ActionType.Socialize]);

    [Fact]
    public void Record_fifo_drops_oldest_when_window_exceeded()
    {
        var log = new NpcCognitionLog(windowSize: 3);

        for (long tick = 1; tick <= 4; tick++)
            log.Record(Npc, tick, Trace(winner: (ActionType)tick));

        var entries = log.RecentEntries(Npc, 10);
        Assert.Equal(3, entries.Count);
        Assert.Equal(2L, entries[0].Tick);
        Assert.Equal(3L, entries[1].Tick);
        Assert.Equal(4L, entries[2].Tick);
        Assert.Equal((ActionType)4, entries[2].Trace.Winner);
    }

    [Fact]
    public void Record_default_window_is_fifty_entries()
    {
        var log = new NpcCognitionLog();

        for (long tick = 1; tick <= 51; tick++)
            log.Record(Npc, tick, Trace());

        Assert.Equal(NpcCognitionLog.DefaultWindowSize, log.RecentEntries(Npc, 100).Count);
        Assert.Equal(2L, log.RecentEntries(Npc, 100)[0].Tick);
        Assert.Equal(51L, log.RecentEntries(Npc, 100)[^1].Tick);
    }

    [Fact]
    public void RecentEntries_returns_empty_for_unknown_npc()
    {
        var log = new NpcCognitionLog();
        Assert.Empty(log.RecentEntries(new NpcId(99), 10));
    }

    [Fact]
    public void RecentEntries_respects_requested_count()
    {
        var log = new NpcCognitionLog(windowSize: 10);

        for (long tick = 1; tick <= 5; tick++)
            log.Record(Npc, tick, Trace());

        var entries = log.RecentEntries(Npc, 2);
        Assert.Equal(2, entries.Count);
        Assert.Equal(4L, entries[0].Tick);
        Assert.Equal(5L, entries[1].Tick);
    }

    [Fact]
    public void MarkWatchlisted_is_not_retroactive_for_entries_before_from_tick()
    {
        var log = new NpcCognitionLog(windowSize: 3);

        for (long tick = 1; tick <= 6; tick++)
            log.Record(Npc, tick, Trace());

        log.MarkWatchlisted(Npc, fromTick: 5);

        for (long tick = 7; tick <= 8; tick++)
            log.Record(Npc, tick, Trace());

        var entries = log.RecentEntries(Npc, 20);
        Assert.Equal(4, entries.Count);
        Assert.Equal(4L, entries[0].Tick);
        Assert.DoesNotContain(entries, e => e.Tick is 1 or 2 or 3);
        Assert.Contains(entries, e => e.Tick == 5);
        Assert.Contains(entries, e => e.Tick == 8);
    }

    [Fact]
    public void MarkWatchlisted_retains_all_entries_from_mark_tick_forward()
    {
        var log = new NpcCognitionLog(windowSize: 3);

        log.MarkWatchlisted(Npc, fromTick: 10);

        for (long tick = 10; tick <= 20; tick++)
            log.Record(Npc, tick, Trace());

        var entries = log.RecentEntries(Npc, 50);
        Assert.Equal(11, entries.Count);
        Assert.Equal(10L, entries[0].Tick);
        Assert.Equal(20L, entries[^1].Tick);
    }

    [Fact]
    public void Unmark_preserves_accumulated_history()
    {
        var log = new NpcCognitionLog(windowSize: 3);

        log.MarkWatchlisted(Npc, fromTick: 1);
        for (long tick = 1; tick <= 7; tick++)
            log.Record(Npc, tick, Trace());

        log.Unmark(Npc);

        var beforeUnmark = log.RecentEntries(Npc, 20);
        Assert.Equal(7, beforeUnmark.Count);

        log.Record(Npc, 8, Trace());
        var afterRecord = log.RecentEntries(Npc, 20);
        Assert.Equal(7, afterRecord.Count);
        Assert.Equal(2L, afterRecord[0].Tick);
        Assert.Equal(8L, afterRecord[^1].Tick);
        Assert.False(log.IsWatchlisted(Npc));
    }

    [Fact]
    public void Mark_and_unmark_same_tick_preserves_only_existing_entries()
    {
        var log = new NpcCognitionLog(windowSize: 5);

        log.Record(Npc, 10, Trace());
        log.MarkWatchlisted(Npc, fromTick: 10);
        log.Unmark(Npc);

        Assert.Single(log.RecentEntries(Npc, 10));
        Assert.Equal(10L, log.RecentEntries(Npc, 10)[0].Tick);
    }

    [Fact]
    public void Retention_cost_is_bounded_for_unmarked_npcs_and_grows_only_for_watchlisted()
    {
        var log = new NpcCognitionLog(windowSize: 50);
        var watchlisted = new NpcId(50);

        for (long npc = 1; npc <= 200; npc++)
        {
            for (long tick = 1; tick <= 100; tick++)
                log.Record(new NpcId(npc), tick, Trace());
        }

        foreach (long npc in new long[] { 1, 50, 199 })
            Assert.Equal(50, log.RecentEntries(new NpcId(npc), 200).Count);

        log.MarkWatchlisted(watchlisted, fromTick: 100);
        for (long tick = 101; tick <= 250; tick++)
            log.Record(watchlisted, tick, Trace());

        Assert.Equal(50, log.RecentEntries(new NpcId(1), 300).Count);
        Assert.Equal(200, log.RecentEntries(watchlisted, 300).Count);
        Assert.True(log.IsWatchlisted(watchlisted));
    }

    [Fact]
    public void Entries_are_isolated_per_npc()
    {
        var log = new NpcCognitionLog(windowSize: 5);

        log.Record(Npc, 1, Trace(winner: ActionType.Work));
        log.Record(OtherNpc, 2, Trace(winner: ActionType.Sleep));

        Assert.Single(log.RecentEntries(Npc, 10));
        Assert.Equal(ActionType.Work, log.RecentEntries(Npc, 10)[0].Trace.Winner);
        Assert.Single(log.RecentEntries(OtherNpc, 10));
        Assert.Equal(ActionType.Sleep, log.RecentEntries(OtherNpc, 10)[0].Trace.Winner);
    }
}
