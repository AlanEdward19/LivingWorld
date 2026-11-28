using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
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
    /// <summary>Nome do sistema dos despertares quando eram eventos do <see cref="EventScheduler"/>: só a leitura de
    /// snapshot antigo ainda o procura em <c>PendingEvents</c> (AD-129).</summary>
    public const string SystemName = "npc-wake";

    /// <summary>Monta o lote de quem acorda neste tick (instante <paramref name="nowMs"/>) a partir da fila de despertar do
    /// mundo (AD-129, ADR-0050).</summary>
    public static void PrepareWakeBatch(WorldState world, long nowMs) => world.PrepareNpcWakeBatch(nowMs);

    /// <summary><paramref name="ctx"/> fica na assinatura por simetria com os outros agendamentos do tick; a fila de
    /// despertar é do <see cref="WorldState"/> e não passa pelo <see cref="EventScheduler"/>.</summary>
    public static void ScheduleWake(WorldState world, TickContext ctx, long npcId, long targetTick)
    {
        _ = ctx;
        world.ReplaceNpcWake(npcId, targetTick);
    }

    /// <summary>Agenda wake imediato (próximo tick) para NPCs roteados pelo AttentionRouter
    /// que tenham Intent Active — dedupe via <see cref="WorldState.ReplaceNpcWake"/>.</summary>
    public static void ScheduleAttentionWakes(
        WorldState world, TickContext ctx, WorldEvent evt, AttentionRules rules, long now)
    {
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, rules);
        foreach (var npcId in routed.OrderBy(id => id.Value))
        {
            if (world.FindNpc(npcId) is not { IsAlive: true } npc) continue;
            // sem Intent válido, comportamento antigo (só threshold/fim de ação).
            if (npc.IntentStatus != IntentStatus.Active) continue;

            long wakeAt = ComputeNextWakeTick(
                npc, world.NeedsRules, world.ActionCatalog, now, world,
                eventDrivenWakeTick: now + 1);
            ScheduleWake(world, ctx, npcId.Value, wakeAt);
        }
    }

    public static long ComputeNextWakeTick(
        Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now, WorldState world,
        long? eventDrivenWakeTick = null, long? travelArrivalTick = null, LifeStage? lifeStage = null)
    {
        long next = long.MaxValue;
        var cause = WakeCause.Fallback;
        // Comer, comprar (ou ir comprar) atendem fome e sede; dormir (ou ir para a cama) atende o sono.
        var current = npc.CurrentAction;
        var purpose = current == ActionType.Travel ? npc.TravelPurpose : current;
        bool feeding = purpose is ActionType.Eat or ActionType.Buy;
        bool resting = purpose == ActionType.Sleep;
        // AD-187: a caminho da água (ida de água em curso, ida ou volta) e sem nada nas mãos, a decisão da hora só manda seguir, salvo a
        // fome à beira da morte: a necessidade já urgente não reacorda de hora em hora (despertar sem efeito); acorda na chegada, no
        // cruzamento que ainda vem e quando a fome chega ao ponto que mudaria a escolha. Quem leva algo acorda como antes (come do que
        // leva, devolve à despensa perto de casa)
        bool waterBound = current == ActionType.Travel && LivingWorld.Simulation.Economy.Supply.WaterFetch.OnTheWay(world, npc)
            && world.Invention is { } carry && !carry.CarriesOrHolds(npc.Id.Value);
        Consider(ref next, ref cause, NextNeedWake(npc.HungerNeed, now, needsRules.UrgencyThreshold, servedByCurrentAction: feeding || waterBound), WakeCause.NeedCrossing);
        if (waterBound)
            Consider(ref next, ref cause, NextThresholdCrossing(npc.HungerNeed, now, LivingWorld.Simulation.Economy.Supply.WaterFetch.StarvingDeficit), WakeCause.NeedCrossing);
        Consider(ref next, ref cause, NextNeedWake(npc.ThirstNeed, now, needsRules.UrgencyThreshold, servedByCurrentAction: feeding || waterBound), WakeCause.NeedCrossing);
        Consider(ref next, ref cause, NextNeedWake(npc.SleepNeed, now, needsRules.UrgencyThreshold, servedByCurrentAction: resting || waterBound), WakeCause.NeedCrossing);
        Consider(ref next, ref cause, waterBound ? NextNeedWake(npc.SocialNeed, now, needsRules.UrgencyThreshold, servedByCurrentAction: true)
            : NextSocialWake(npc.SocialNeed, now, needsRules.UrgencyThreshold), WakeCause.NeedCrossing);

        if (npc.ExtraNeeds is not null || npc.BodyTask is { Action: LivingWorld.Simulation.Objects.ObjectUsage.Action or LivingWorld.Simulation.Objects.SocialInteractions.Action } || world.ObjectCatalog?.Social is not null) // #255: o fim do uso e o cruzamento das necessidades do catálogo
            Consider(ref next, ref cause, WorldMillis.ToHourCeiling(LivingWorld.Simulation.Objects.HomeLife.NextWakeMs(world, npc, WorldMillis.FromHours(now))), WakeCause.NeedCrossing);

        // Viagem com chegada conhecida termina na chegada, não na duração máxima de Travel: depois dessa duração o
        // "fim de ação" já vencido acordava o viajante de hora em hora até chegar, sem nada para concluir (#152).
        bool arrivalPending = travelArrivalTick is { } pendingArrival && pendingArrival > now;
        if (npc.CurrentAction is { } action && !arrivalPending)
        {
            long actionEnd = npc.ActionStartedAtTick + LivingWorld.Simulation.Objects.ObjectUse.DurationHours(world, npc, action, catalog);
            Consider(ref next, ref cause, actionEnd, WakeCause.ActionEnd);
        }

        // AD-187: a virada da rotina também não muda a ida de água (vem antes de tudo que não é urgente)
        if (!waterBound) Consider(ref next, ref cause, NextRoutineBoundaryWake(npc, catalog, now, world, lifeStage), WakeCause.RoutineBoundary);

        // Quem está a caminho acorda ao chegar, não ao fim da duração máxima da ação: sem isso toda viagem
        // de 1–2 h só terminava às 4 h, e o trajeto comia o dia.
        if (travelArrivalTick is { } arrival)
            Consider(ref next, ref cause, arrival, WakeCause.TravelArrival);

        if (eventDrivenWakeTick is { } attentionTick)
            Consider(ref next, ref cause, attentionTick, WakeCause.Attention);

        if (next == long.MaxValue) next = now + 1;
        if (next <= now)
        {
            next = now + 1;
            cause = cause == WakeCause.NeedCrossing ? WakeCause.NeedAlreadyUrgent : WakeCause.Overdue;
        }
        world.WakeCounters.RecordScheduled(cause, npc.CurrentAction);

        double reaction = AttributeMechanic.ReactionSpeedMultiplier(world, npc);
        if (reaction > 1)
        {
            long span = next - now;
            next = now + Math.Max(1L, (long)Math.Ceiling(span / reaction));
        }

        return next;
    }

    /// <summary>Mesmo resultado de <c>Math.Min</c> em cadeia, guardando quem deu o mínimo (empate: o primeiro).</summary>
    private static void Consider(ref long next, ref WakeCause cause, long candidate, WakeCause candidateCause)
    {
        if (candidate >= next) return;
        next = candidate;
        cause = candidateCause;
    }

    /// <summary>Próxima fronteira da rotina (troca de ação), menos o tempo de deslocamento quando a nova
    /// ação é ir trabalhar ou dormir em outro lugar: quem sai de casa para chegar às 6 h acorda a tempo.</summary>
    private static long NextRoutineBoundaryWake(
        Npc npc, ActionCatalog catalog, long now, WorldState world, LifeStage? knownStage)
    {
        var date = world.CurrentDate;
        var stage = knownStage ?? world.LifeStageRules.LifeStageOf(npc.AgeYears(date));
        var profession = npc.Profession == ProfessionType.None ? (ProfessionType?)null : npc.Profession;
        int hoursPerDay = date.Calendar.HoursPerDay;
        int hour = date.Hour;

        int untilChange = RoutineBoundaries.HoursUntilChange(catalog, profession, stage, hour, hoursPerDay);
        var upcoming = RoutineBoundaries.RoutineAt(catalog, profession, stage, (hour + untilChange) % hoursPerDay, hoursPerDay);
        // Quem já está a caminho da próxima ação da rotina não precisa sair mais cedo de novo: sem isto, um trajeto
        // maior que o tempo até a fronteira acordava o NPC a cada hora só para confirmar a mesma viagem (#152).
        bool alreadyHeadingThere = npc.CurrentAction == ActionType.Travel && npc.TravelPurpose == upcoming;
        long lead = alreadyHeadingThere ? 0 : BehaviorDecisionSystem.CommuteLeadHours(world, npc, upcoming);
        return now + Math.Max(1, untilChange - lead);
    }

    public static void RescheduleAfterHour(
        WorldState world, TickContext ctx, Npc npc, NeedsRules needsRules, ActionCatalog catalog, long now,
        long? travelArrivalTick = null, LifeStage? lifeStage = null)
    {
        if (world.FineTime)
        {
            // AD-197c: a chegada e o fim da ação caem no instante certo (a fase da hora em que a viagem saiu)
            long? arrivalMs = travelArrivalTick is { } arrival ? arrival * WorldMillis.PerHour + npc.ActionPhaseMs : null;
            RescheduleAtMs(world, npc, needsRules, catalog, arrivalMs, lifeStage);
            return;
        }
        ScheduleWake(world, ctx, npc.Id.Value, ComputeNextWakeTick(
            npc, needsRules, catalog, now, world, travelArrivalTick: travelArrivalTick, lifeStage: lifeStage));
    }

    /// <summary>AD-197h: reagenda no passo fino com a chegada já em ms (<c>TravelArrivalMs</c>).</summary>
    public static void RescheduleAtMs(WorldState world, Npc npc, NeedsRules needsRules, ActionCatalog catalog, long? travelArrivalMs,
        LifeStage? lifeStage = null) =>
        world.ReplaceNpcWakeAtMs(npc.Id.Value,
            ComputeNextWakeMs(npc, needsRules, catalog, world.CurrentDate.TotalMilliseconds, world, travelArrivalMs, lifeStage));

    /// <summary>AD-197c: o mesmo despertar de <see cref="ComputeNextWakeTick"/> no passo fino, em milissegundos: fim de ação e
    /// chegada no instante exato (com a fase da hora em que começaram), necessidade no instante em que cruza o limiar, virada da
    /// rotina na hora cheia do calendário. Atrasado ou sem candidato, uma hora depois (a cadência de antes).</summary>
    public static long ComputeNextWakeMs(
        Npc npc, NeedsRules needsRules, ActionCatalog catalog, long nowMs, WorldState world, long? travelArrivalMs = null,
        LifeStage? lifeStage = null)
    {
        const long H = WorldMillis.PerHour;
        long now = WorldMillis.ToHourFloor(nowMs);
        long next = long.MaxValue;
        var cause = WakeCause.Fallback;
        var current = npc.CurrentAction;
        var purpose = current == ActionType.Travel ? npc.TravelPurpose : current;
        bool feeding = purpose is ActionType.Eat or ActionType.Buy;
        bool resting = purpose == ActionType.Sleep;
        bool waterBound = current == ActionType.Travel && LivingWorld.Simulation.Economy.Supply.WaterFetch.OnTheWay(world, npc)
            && world.Invention is { } carry && !carry.CarriesOrHolds(npc.Id.Value);
        double urgency = needsRules.UrgencyThreshold;
        Consider(ref next, ref cause, NeedWakeMs(npc.HungerNeed, nowMs, urgency, feeding || waterBound), WakeCause.NeedCrossing);
        if (waterBound)
            Consider(ref next, ref cause, CrossingMs(npc.HungerNeed, nowMs, LivingWorld.Simulation.Economy.Supply.WaterFetch.StarvingDeficit), WakeCause.NeedCrossing);
        Consider(ref next, ref cause, NeedWakeMs(npc.ThirstNeed, nowMs, urgency, feeding || waterBound), WakeCause.NeedCrossing);
        Consider(ref next, ref cause, NeedWakeMs(npc.SleepNeed, nowMs, urgency, resting || waterBound), WakeCause.NeedCrossing);
        long social = CrossingMs(npc.SocialNeed, nowMs, urgency);
        Consider(ref next, ref cause, waterBound ? NeedWakeMs(npc.SocialNeed, nowMs, urgency, true)
            : social > nowMs ? social
            : npc.SocialNeed.ValueAtMs(nowMs) <= BehaviorDecisionSystem.SocialCriticalNeed ? nowMs + SocialRewakeHours * H : long.MaxValue,
            WakeCause.NeedCrossing);

        if (npc.ExtraNeeds is not null || npc.BodyTask is { Action: LivingWorld.Simulation.Objects.ObjectUsage.Action or LivingWorld.Simulation.Objects.SocialInteractions.Action } || world.ObjectCatalog?.Social is not null) // #255: o fim do uso e o cruzamento das necessidades do catálogo
            Consider(ref next, ref cause, LivingWorld.Simulation.Objects.HomeLife.NextWakeMs(world, npc, nowMs), WakeCause.NeedCrossing);

        bool arrivalPending = travelArrivalMs is { } pendingArrival && pendingArrival > nowMs;
        if (npc.CurrentAction is { } action && !arrivalPending)
            Consider(ref next, ref cause, npc.ActionStartedAtMs + LivingWorld.Simulation.Objects.ObjectUse.DurationMs(world, npc, action, catalog),
                WakeCause.ActionEnd); // AD-197h: a duração em ms (a mesma conta da conclusão)
        if (!waterBound)
            Consider(ref next, ref cause, NextRoutineBoundaryWake(npc, catalog, now, world, lifeStage) * H, WakeCause.RoutineBoundary);
        if (travelArrivalMs is { } arrival)
            Consider(ref next, ref cause, arrival, WakeCause.TravelArrival);

        if (next == long.MaxValue) next = nowMs + H;
        if (next <= nowMs)
        {
            next = nowMs + H;
            cause = cause == WakeCause.NeedCrossing ? WakeCause.NeedAlreadyUrgent : WakeCause.Overdue;
        }
        world.WakeCounters.RecordScheduled(cause, npc.CurrentAction);

        double reaction = AttributeMechanic.ReactionSpeedMultiplier(world, npc);
        if (reaction > 1) next = nowMs + Math.Max(1L, (long)Math.Ceiling((next - nowMs) / reaction));
        return next;
    }

    private static long NeedWakeMs(LazyNeed need, long nowMs, double urgencyThreshold, bool servedByCurrentAction)
    {
        long crossing = CrossingMs(need, nowMs, urgencyThreshold);
        return servedByCurrentAction && crossing <= nowMs ? long.MaxValue : crossing;
    }

    /// <summary>Instante (ms) em que a necessidade cruza o limiar de urgência; agora, se já cruzou.</summary>
    private static long CrossingMs(LazyNeed need, long nowMs, double urgencyThreshold)
    {
        if (need.DecayRatePerHour <= 0) return long.MaxValue;
        double value = need.ValueAtMs(nowMs);
        double target = 100 - urgencyThreshold;
        if (value <= 0 || value <= target) return nowMs;
        return nowMs + Math.Max(1L, (long)Math.Ceiling((value - target) / need.DecayRatePerHour * WorldMillis.PerHour));
    }

    /// <summary>Despertar por necessidade. Quem já está fazendo o que atende a necessidade (comer, dormir) não a
    /// reavalia a cada hora enquanto a ação não termina — a ação em curso é a resposta, e o fim dela acorda o
    /// NPC. Uma necessidade que ainda vai cruzar o limiar continua acordando-o no cruzamento.</summary>
    private static long NextNeedWake(LazyNeed need, long nowTick, double urgencyThreshold, bool servedByCurrentAction)
    {
        long crossing = NextThresholdCrossing(need, nowTick, urgencyThreshold);
        return servedByCurrentAction && crossing <= nowTick ? long.MaxValue : crossing;
    }

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
        if (need.DecayRatePerHour <= 0) return long.MaxValue;
        double value = need.ValueAt(nowTick);
        if (value <= 0) return nowTick;
        if (100 - value > urgencyThreshold) return nowTick;

        double target = 100 - urgencyThreshold;
        if (value <= target) return nowTick;
        long delta = (long)Math.Ceiling((value - target) / need.DecayRatePerHour);
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
