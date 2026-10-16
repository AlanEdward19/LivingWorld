using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
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

        long ticksNeeded = Mobility.TravelTicks(
            world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc));
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

    /// <summary>Quem tem dinheiro (pelo menos o preço de uma unidade) e vê a despensa da casa abaixo de metade da meta de
    /// dias (que cresce com a distância até o mercado) vai repor já, sem esperar a sede apertar: com o mercado a dias de
    /// caminho, esperar a sede urgente para só então partir era morrer na estrada. Rejeita barato quando a despensa está
    /// cheia. Sem estado: só lê o mundo, então reabrir o mundo no meio do dia decide igual à execução contínua.</summary>
    internal static bool NeedsRestocking(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || npc.Wallet.Amount < 1) return false;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return false;

        long scarcest = Math.Min(
            household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)),
            household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)));
        int members = Math.Max(1, household.Members.Count);
        if (scarcest >= (long)members * MaxProvisionDays) return false; // despensa cheia mesmo para o mercado mais longe

        var market = NearestProvisionMarket(world, household, marketIndex);
        if (market is null || npc.Wallet.Amount < CheapestEssentialPrice(economy, market)) return false;

        return (double)scarcest / members < ProvisionTargetAt(world, household, market) * RestockShortfall;
    }

    /// <summary>Ação de quem não tem necessidade urgente: a da rotina, ou comprar quando a despensa baixa para a distância do mercado
    /// (à noite dorme) ou quando o choro de uma criança da casa não pode esperar (aí levanta mesmo de madrugada).</summary>
    internal static ActionType NonUrgentCandidate(
        WorldState world, Npc npc, ActionType routineAction, LifeStage stage, MarketIndex marketIndex) =>
        routineAction == ActionType.Sleep
            ? CaregiverHearsCry(world, npc, stage) ? ActionType.Buy : routineAction // só aqui o choro muda algo: fora do sono a despensa vazia já manda comprar
            : NeedsRestocking(world, npc, marketIndex) ? ActionType.Buy : routineAction;

    /// <summary>Choro atendido (dívida da Fase 21): um adulto que está na mesma célula de uma criança do domicílio que chora — porque a despensa
    /// não tem comida ou água para ela — levanta e vai comprar, mesmo na hora de dormir. Só vale com carteira para pagar.</summary>
    internal static bool CaregiverHearsCry(WorldState world, Npc npc, LifeStage stage)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || !world.OntogenyRules.Enabled || stage == LifeStage.Child || npc.Wallet.Amount < 1) return false;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return false;
        if (household.Members.Count < 2) return false;

        bool pantryEmpty = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) < 1
            || household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) < 1;
        if (!pantryEmpty) return false;

        foreach (var memberId in household.Members)
            if (memberId != npc.Id && world.FindNpc(memberId) is { IsAlive: true, CurrentAction: ActionType.Cry } baby
                && baby.CurrentLocation == npc.CurrentLocation)
                return true;
        return false;
    }

    /// <summary>Meta de dias de despensa do domicílio do NPC (a padrão sem domicílio ou sem mercado).</summary>
    internal static double ProvisionTargetFor(WorldState world, Npc npc, MarketIndex marketIndex) =>
        npc.Household is { } householdId && world.FindHousehold(householdId) is { } household
            ? ProvisionTargetAt(world, household, NearestProvisionMarket(world, household, marketIndex))
            : ProvisionTargetDays;

    private static Workplace? NearestProvisionMarket(
        WorldState world, LivingWorld.Domain.Population.Family.Household household, MarketIndex marketIndex) =>
        marketIndex.NearestSelling(
            household.Location, new ResourceType(world.EconomyRules.FoodResourceId), new ResourceType(world.EconomyRules.WaterResourceId));

    private static double ProvisionTargetAt(
        WorldState world, LivingWorld.Domain.Population.Family.Household household, Workplace? market)
    {
        if (market is null) return ProvisionTargetDays;

        long ticks = Mobility.Apply(
            TravelTicksCache.Between(world.Map, household.Location, market.Location), Mobility.BonusPct(world, household.City));
        return Math.Min(MaxProvisionDays, ProvisionTargetDays + 2.0 * ticks / world.CurrentDate.Calendar.HoursPerDay);
    }

    /// <summary>Menor preço positivo entre comida e água no mercado: com menos que isso na carteira a ida não compra nada.</summary>
    private static long CheapestEssentialPrice(EconomyRules economy, Workplace market)
    {
        long cheapest = long.MaxValue;
        foreach (int id in new[] { economy.FoodResourceId, economy.WaterResourceId })
            if (market.Prices.TryGetValue(new ResourceType(id), out var price) && price > 0 && price < cheapest) cheapest = price;
        return cheapest == long.MaxValue ? 0 : cheapest;
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
            : Mobility.TravelTicks(world, npc, npc.CurrentLocation, destination,
                BodyMechanic.MovementCostMultiplier(world, npc));

    private static int HourAfter(WorldDate date, long hours) =>
        (int)((date.TotalHours + hours) % date.Calendar.HoursPerDay);
}
