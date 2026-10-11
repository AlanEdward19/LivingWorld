using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — quando o NPC acorda de novo: urgência, antecipação do trajeto, chegada.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Tick em que a viagem em curso chega ao destino (mesma conta de <see cref="TryCompleteAction"/>);
    /// nulo se o NPC não está viajando ou se anda por poder extraordinário, que avança passo a passo.</summary>
    private static long? TravelArrivalTick(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        if (npc.CurrentAction != ActionType.Travel) return null;
        if (TravelDestinationOf(world, npc, marketIndex) is not { } destination || destination == npc.CurrentLocation)
            return null;
        if (ExtraordinaryLocomotion.Resolve(world, npc).HasModifier) return null;

        long ticksNeeded = TravelResolution.TicksBetween(
            world.Map, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc));
        return npc.ActionStartedAtTick + ticksNeeded;
    }

    /// <summary>Necessidade urgente que faz o NPC largar a rotina: fome, sede e sono sempre; solidão só
    /// quando crítica — quem se sente um pouco sozinho no meio do turno continua a rotina do dia
    /// (e socializa na janela dela), como qualquer pessoa.</summary>
    private static bool HasUrgentNeedForDecision(Npc npc, NeedsRules rules, long now)
    {
        if (!npc.HasUrgentNeed(rules, now)) return false;
        return IsUrgent(npc.HungerAt(now), rules) || IsUrgent(npc.ThirstAt(now), rules)
            || IsUrgent(npc.SleepAt(now), rules) || npc.SocialAt(now) <= SocialCriticalNeed;
    }

    private static bool IsUrgent(int need, NeedsRules rules) => need == 0 || 100 - need > rules.UrgencyThreshold;

    /// <summary>Necessidade social abaixo da qual a solidão deixa de ser preferência e força a decisão.</summary>
    internal const int SocialCriticalNeed = 20;

    /// <summary>Rotina do horário atual, mas saindo cedo: quem tem de estar no trabalho (ou em casa para
    /// dormir) às X horas parte já na hora X menos o tempo de deslocamento, e não só quando o turno
    /// começa — senão cada trajeto empurra o dia para a frente e o ciclo deixa de ser de 24 h.</summary>
    private static ActionType RoutineWithCommuteLead(WorldState world, Npc npc, ActionCatalog catalog, LifeStage stage)
    {
        var profession = NoneIfSentinel(npc.Profession);
        var date = world.CurrentDate;
        int hoursPerDay = date.Calendar.HoursPerDay;
        var routine = RoutineBoundaries.RoutineAt(catalog, profession, stage, date.Hour, hoursPerDay);

        if (routine != ActionType.Work
            && WorkDestinationOf(world, npc) is { } workplace
            && LeadHoursTo(world, npc, workplace) is > 0 and var workLead
            && RoutineBoundaries.RoutineAt(catalog, profession, stage, HourAfter(date, workLead), hoursPerDay) == ActionType.Work)
            return ActionType.Work;

        if (routine != ActionType.Sleep && routine != ActionType.Work
            && SleepDestinationOf(world, npc) is { } bed
            && LeadHoursTo(world, npc, bed) is > 0 and var bedLead
            && RoutineBoundaries.RoutineAt(catalog, profession, stage, HourAfter(date, bedLead), hoursPerDay) == ActionType.Sleep)
            return ActionType.Sleep;

        return routine;
    }

    /// <summary>Horas de deslocamento até onde <paramref name="action"/> acontece (trabalho ou cama);
    /// 0 para as demais ações ou quando o NPC já está lá.</summary>
    internal static long CommuteLeadHours(WorldState world, Npc npc, ActionType action) => action switch
    {
        ActionType.Work when WorkDestinationOf(world, npc) is { } workplace => LeadHoursTo(world, npc, workplace),
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } bed => LeadHoursTo(world, npc, bed),
        _ => 0,
    };

    private static long LeadHoursTo(WorldState world, Npc npc, CellCoord destination) =>
        destination == npc.CurrentLocation
            ? 0
            : TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination,
                BodyMechanic.MovementCostMultiplier(world, npc));

    private static int HourAfter(WorldDate date, long hours) =>
        (int)((date.TotalHours + hours) % date.Calendar.HoursPerDay);
}
