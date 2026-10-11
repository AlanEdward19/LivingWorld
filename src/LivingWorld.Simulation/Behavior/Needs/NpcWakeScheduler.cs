using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Agenda acordar NPC no tick em que a ação termina ou uma necessidade cruza limiar.
/// Também agenda wakes event-driven via
/// <see cref="AttentionRouter"/>.</summary>
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

    /// <summary>Agenda wake imediato (próximo tick) para NPCs roteados pelo AttentionRouter
    /// que tenham Intent Active — dedupe via <see cref="WorldState.ReplaceNpcWake"/>.</summary>
    public static void ScheduleAttentionWakes(
        WorldState world, TickContext ctx, WorldEvent evt, AttentionRules rules, long now)
    {
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, rules);
        foreach (var npcId in routed.OrderBy(id => id.Value))
        {
            if (world.FindNpc(npcId) is not { IsAlive: true } npc) continue;
            // COH-44: sem Intent válido, comportamento antigo (só threshold/fim de ação).
            if (npc.IntentStatus != IntentStatus.Active) continue;

            long wakeAt = ComputeNextWakeTick(
                npc, world.NeedsRules, world.ActionCatalog, now, world,
                eventDrivenWakeTick: now + 1);
            ScheduleWake(world, ctx, npcId.Value, wakeAt);
        }
    }

    public static long ComputeNextWakeTick(
        Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now, WorldState world,
        long? eventDrivenWakeTick = null)
    {
        long next = long.MaxValue;
        next = Math.Min(next, NextThresholdCrossing(npc.HungerNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextThresholdCrossing(npc.ThirstNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextThresholdCrossing(npc.SleepNeed, now, needsRules.UrgencyThreshold));
        next = Math.Min(next, NextSocialWake(npc.SocialNeed, now, needsRules.UrgencyThreshold));

        if (npc.CurrentAction is { } action)
        {
            long actionEnd = npc.ActionStartedAtTick + catalog.MaxDurationHours[action];
            next = Math.Min(next, actionEnd);
        }

        next = Math.Min(next, NextRoutineBoundaryWake(npc, catalog, now, world));

        if (eventDrivenWakeTick is { } attentionTick)
            next = Math.Min(next, attentionTick);

        if (next == long.MaxValue) next = now + 1;
        if (next <= now) next = now + 1;

        double reaction = AttributeMechanic.ReactionSpeedMultiplier(world, npc);
        if (reaction > 1)
        {
            long span = next - now;
            next = now + Math.Max(1L, (long)Math.Ceiling(span / reaction));
        }

        return next;
    }

    /// <summary>Próxima fronteira da rotina (troca de ação), menos o tempo de deslocamento quando a nova
    /// ação é ir trabalhar ou dormir em outro lugar: quem sai de casa para chegar às 6 h acorda a tempo.</summary>
    private static long NextRoutineBoundaryWake(Npc npc, ActionCatalog catalog, long now, WorldState world)
    {
        var date = world.CurrentDate;
        var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(date));
        var profession = npc.Profession == ProfessionType.None ? (ProfessionType?)null : npc.Profession;
        int hoursPerDay = date.Calendar.HoursPerDay;
        int hour = date.Hour;

        int untilChange = RoutineBoundaries.HoursUntilChange(catalog, profession, stage, hour, hoursPerDay);
        var upcoming = catalog.RoutineOf(profession, stage, (hour + untilChange) % hoursPerDay);
        long lead = BehaviorDecisionSystem.CommuteLeadHours(world, npc, upcoming);
        return now + Math.Max(1, untilChange - lead);
    }

    public static void RescheduleAfterHour(
        WorldState world, TickContext ctx, Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now) =>
        ScheduleWake(world, ctx, npc.Id.Value, ComputeNextWakeTick(npc, needsRules, catalog, now, world));

    /// <summary>Solidão não é necessidade de sobrevivência: enquanto não for crítica, não obriga o NPC a
    /// redecidir toda hora (quem se sente sozinho às 15 h não larga o turno; socializa na janela da rotina).
    /// Reavaliar a cada hora por causa dela era o maior gerador de despertares e quebrava o ritmo do dia.</summary>
    private static long NextSocialWake(LazyNeed social, long nowTick, double urgencyThreshold)
    {
        long crossing = NextThresholdCrossing(social, nowTick, urgencyThreshold);
        if (crossing > nowTick) return crossing;
        return social.ValueAt(nowTick) <= BehaviorDecisionSystem.SocialCriticalNeed
            ? nowTick + SocialRewakeHours
            : long.MaxValue;
    }

    /// <summary>Quem está muito sozinho repensa isso de tempos em tempos, não a cada hora: solidão varia
    /// em dias. Medido (<c>docs/audits/realism-cadence.md</c>): 3 h deixa trabalho, sono e latência
    /// das necessidades idênticos e tira ~20% dos despertares.</summary>
    private const long SocialRewakeHours = 3;

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
            nextTicks[i] = ComputeNextWakeTick(list[i], needsRules, catalog, now, world));

        foreach (var (npc, index) in list.Select((npc, index) => (npc, index)).OrderBy(t => t.npc.Id.Value))
            ScheduleWake(world, ctx, npc.Id.Value, nextTicks[index]);
    }
}
