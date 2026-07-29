using LivingWorld.Domain;

namespace LivingWorld.Simulation.Behavior;

/// <summary>Agenda acordar NPC no tick em que a ação termina ou uma necessidade cruza limiar
/// (Fase 9, PERF-08).</summary>
public static class NpcWakeScheduler
{
    public const string SystemName = "npc-wake";

    public static void PrepareWakeBatch(WorldState world, long tick)
    {
        world.ClearNpcWakeBatch();
        var due = world.Scheduler.PeekDue(tick);
        for (int i = due.Count - 1; i >= 0; i--)
        {
            var evt = due[i];
            if (evt.SystemName != SystemName) continue;
            world.Scheduler.Cancel(evt.Id);
            if (long.TryParse(evt.Payload, out var idValue)
                && world.FindNpc(new NpcId(idValue)) is { IsAlive: true } npc)
            {
                world.ClearNpcWakeEvent(idValue);
                world.AddNpcWake(npc);
            }
        }
    }

    public static void ScheduleWake(WorldState world, TickContext ctx, long npcId, long targetTick) =>
        world.ReplaceNpcWake(ctx, npcId, targetTick);

    public static long ComputeNextWakeTick(
        Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now)
    {
        long next = long.MaxValue;
        next = Math.Min(next, NextThresholdCrossing(npc.HungerNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextThresholdCrossing(npc.ThirstNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextThresholdCrossing(npc.SleepNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextThresholdCrossing(npc.SocialNeed, now, needsRules.UrgencyThreshold));

        if (npc.CurrentAction is { } action)
        {
            long actionEnd = npc.ActionStartedAtTick + catalog.MaxDurationHours[action];
            next = Math.Min(next, actionEnd);
        }

        if (next == long.MaxValue) next = now + 1;
        if (next <= now) next = now + 1;
        return next;
    }

    public static void RescheduleAfterHour(
        WorldState world, TickContext ctx, Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now) =>
        ScheduleWake(world, ctx, npc.Id.Value, ComputeNextWakeTick(npc, needsRules, catalog, now));

    public static long NextThresholdCrossing(LazyNeed need, long nowTick, double urgencyThreshold)
    {
        if (need.DecayRatePerTick <= 0) return long.MaxValue;
        double value = need.ValueAt(nowTick);
        if (value <= 0) return nowTick;
        if (100 - value > urgencyThreshold) return nowTick;

        double target = 100 - urgencyThreshold;
        if (value <= target) return nowTick;
        long delta = (long)Math.Ceiling((value - target) / need.DecayRatePerTick);
        return nowTick + Math.Max(1, delta);
    }

    public static void RescheduleBatchParallel(
        IReadOnlyList<Npc> npcs, WorldState world, TickContext ctx, NeedsRules needsRules, ActionCatalog catalog, long now, int partitionCount)
    {
        var list = npcs as List<Npc> ?? npcs.ToList();
        if (partitionCount <= 1 || list.Count < partitionCount)
        {
            foreach (var npc in list.OrderBy(n => n.Id.Value))
                RescheduleAfterHour(world, ctx, npc, needsRules, catalog, now);
            return;
        }

        var nextTicks = new long[list.Count];
        Parallel.For(0, list.Count, i =>
            nextTicks[i] = ComputeNextWakeTick(list[i], needsRules, catalog, now));

        foreach (var (npc, index) in list.Select((npc, index) => (npc, index)).OrderBy(t => t.npc.Id.Value))
            ScheduleWake(world, ctx, npc.Id.Value, nextTicks[index]);
    }
}
