using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — quando o NPC acorda de novo: urgência, antecipação do trajeto, chegada.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Tick em que a viagem em curso chega ao destino (mesma conta de <see cref="TryCompleteAction"/>);
    /// nulo se o NPC não está viajando ou se anda por poder extraordinário, que avança passo a passo.</summary>
    internal static long? TravelArrivalTick(WorldState world, Npc npc, MarketIndex marketIndex)
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
    internal static bool NeedsRestocking(WorldState world, Npc npc, MarketIndex marketIndex, bool duringWork = false)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || npc.Wallet.Amount < 1) return false;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return false;
        var provisioning = world.NeedsRules.Provisioning;

        long foodStock = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId));
        long waterStock = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId));
        // AD-148: com a banca de comida vazia há pouco, só a água pode levar ao mercado até a hora de voltar
        bool foodBlocked = FoodStallKnownEmpty(world, household);
        long scarcest = foodBlocked ? waterStock : Math.Min(foodStock, waterStock);
        int members = Math.Max(1, household.Members.Count);
        double hoard = -1;
        if (scarcest >= (long)members * MaxProvisionDays) // despensa cheia mesmo para o mercado mais longe
        {
            if (npc.Psyche is null) return false;
            hoard = PsycheMechanic.HoardFactor(world, npc, world.CurrentDate.TotalHours);
            if (scarcest >= members * MaxProvisionDays * hoard) return false;
        }

        var market = NearestProvisionMarket(world, household, marketIndex);
        if (market is null || npc.Wallet.Amount < CheapestEssentialPrice(economy, market)) return false;

        // AD-138: quem passou fome quer a despensa mais cheia
        if (hoard < 0) hoard = PsycheMechanic.HoardFactor(world, npc, world.CurrentDate.TotalHours);
        double target = ProvisionTargetAt(world, household, market) * hoard;
        if ((double)scarcest / members >= target * RestockShortfall) return false;
        // AD-148: no turno a compra espera o fim dele, a menos que a despensa não dure até lá; e uma pessoa da casa vai por todos
        if (duringWork && provisioning.RestockDuringWorkBelowDays >= 0 && (double)scarcest / members >= provisioning.RestockDuringWorkBelowDays)
        {
            world.PurchaseTally.SkippedDuringWork++;
            return false;
        }
        if (provisioning.OneShopperPerHousehold && HouseholdShopperOut(world, npc, household))
        {
            world.PurchaseTally.SkippedHouseholdCovered++;
            return false;
        }
        if (world.FamilyRules.Pressure?.TopUpPurchases != true && !provisioning.TopUp) return true;
        // AD-145: só vai se pode pagar o que está faltando. A compra completa a meta (não soma por cima), então com a água
        // cheia e a comida cara demais a ida não traz nada, e a pessoa ficava indo ao mercado sem parar em vez de viver.
        return (!foodBlocked && Affords(npc, market, economy.FoodResourceId, foodStock, members, target))
            || Affords(npc, market, economy.WaterResourceId, waterStock, members, target);
    }

    private static bool Affords(Npc npc, Workplace market, int resourceId, long stock, int members, double target) =>
        (double)stock / members < target * RestockShortfall
        && market.Prices.TryGetValue(new ResourceType(resourceId), out var price) && price > 0 && npc.Wallet.Amount >= price;

    /// <summary>Ação de quem não tem necessidade urgente: a da rotina, ou comprar quando a despensa baixa para a distância do mercado
    /// (à noite dorme) ou quando o choro de uma criança da casa não pode esperar (aí levanta mesmo de madrugada).</summary>
    internal static ActionType NonUrgentCandidate(
        WorldState world, Npc npc, ActionType routineAction, LifeStage stage, MarketIndex marketIndex) =>
        routineAction == ActionType.Sleep
            ? CaregiverHearsCry(world, npc, stage) ? ActionType.Buy : routineAction // só aqui o choro muda algo: fora do sono a despensa vazia já manda comprar
            : NeedsRestocking(world, npc, marketIndex, duringWork: routineAction == ActionType.Work) ? ActionType.Buy : routineAction;

    /// <summary>Alguém da casa (fora o próprio) está no mercado ou a caminho dele para comprar (AD-148).</summary>
    internal static bool HouseholdShopperOut(WorldState world, Npc npc, LivingWorld.Domain.Population.Family.Household household)
    {
        var members = household.Members; // índice, não foreach: o enumerador pela interface alocava a cada decisão
        for (int i = 0; i < members.Count; i++)
            if (members[i] is var memberId && memberId != npc.Id && world.FindNpc(memberId) is { IsAlive: true } member
                && (member.CurrentAction == ActionType.Buy || member.CurrentAction == ActionType.Travel && member.TravelPurpose == ActionType.Buy))
                return true;
        return false;
    }

    /// <summary>O que a decisão por urgência precisa saber da compra (AD-148): meta de dias da casa, preço de uma unidade de comida
    /// no mercado mais perto (0 com a regra desligada) e se alguém da casa já foi comprar.</summary>
    internal static (double TargetDays, long FoodPrice, bool ShopperOut, bool FoodStallEmpty) ProvisionViewFor(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return (ProvisionTargetDays, 0, false, false);
        var provisioning = world.NeedsRules.Provisioning;
        var market = NearestProvisionMarket(world, household, marketIndex);
        long price = provisioning.OnlyIfCanPay && market is not null
            && market.Prices.TryGetValue(new ResourceType(world.EconomyRules.FoodResourceId), out var p) ? p : 0;
        bool shopperOut = provisioning.OneShopperPerHousehold && HouseholdShopperOut(world, npc, household);
        return (ProvisionTargetAt(world, household, market), price, shopperOut, FoodStallKnownEmpty(world, household));
    }

    /// <summary>A casa achou a banca de comida vazia há pouco e ainda espera para voltar (AD-148).</summary>
    private static bool FoodStallKnownEmpty(WorldState world, LivingWorld.Domain.Population.Family.Household household) =>
        world.NeedsRules.Provisioning.FoodStallRetryHours > 0 && world.CurrentDate.TotalHours < household.FoodStallRetryTick;

    /// <summary>A rotina passa pelo humor (AD-138): no dia em que o luto ou a vergonha pesam, a pessoa não vai trabalhar ou não
    /// sai para conversar e fica onde está (em casa, quase sempre). Um sorteio por pessoa e por dia contra o recolhimento do
    /// momento, então o dia é inteiro (não foge do trabalho uma hora sim, outra não) e a falta cai com o luto. Calmo: nada.</summary>
    internal static ActionType MoodShapesRoutine(WorldState world, TickContext ctx, Npc npc, ActionType routine, long now)
    {
        if (routine is not (ActionType.Work or ActionType.Socialize)) return routine;
        long day = now / world.Calendar.HoursPerDay;
        if (routine == ActionType.Work)
        {
            routine = HormonesShapeWorkday(world, ctx, npc, now, day);
            if (routine != ActionType.Work) return routine;
        }
        if (npc.Psyche is not { } psyche || now >= psyche.CalmFromTick) return routine;
        double withdrawal = routine == ActionType.Work
            ? PsycheMechanic.WorkWithdrawal(world, npc, now)
            : PsycheMechanic.SocialWithdrawal(world, npc, now);
        if (withdrawal <= 0) return routine;
        string purpose = routine == ActionType.Work ? "mood-workday" : "mood-socialday";
        return ctx.StreamFor(purpose, unchecked(npc.Id.Value * 1_000_003L + day)).NextDouble() < withdrawal
            ? ActionType.Idle
            : routine;
    }

    /// <summary>O corpo na rotina (AD-139): no dia de cólica forte ela fica em casa (parte das mulheres, nos primeiros dias da
    /// menstruação) e o jovem no pico do impulso às vezes deixa o trabalho pelos pares. Um sorteio por pessoa e dia.</summary>
    private static ActionType HormonesShapeWorkday(WorldState world, TickContext ctx, Npc npc, long now, long day)
    {
        var rules = LivingWorld.Simulation.Population.Body.Endocrine.Rules(world);
        if (!rules.Enabled) return ActionType.Work;
        double age = (now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        if (age < rules.PubertyMinYears) return ActionType.Work;
        double cramps = npc.Sex == Sex.Female && age < 55 ? LivingWorld.Simulation.Population.Body.Endocrine.CrampWithdrawal(world, npc, now) : 0;
        double truancy = age < rules.MaturityAgeYears
            ? rules.TruancyShare * LivingWorld.Simulation.Population.Body.Endocrine.AdolescentDrive(world, npc, now)
            : 0;
        if (cramps <= 0 && truancy <= 0) return ActionType.Work;
        double u = ctx.StreamFor("body-workday", unchecked(npc.Id.Value * 1_000_003L + day)).NextDouble();
        if (u < cramps) return ActionType.Idle;
        return u < cramps + truancy ? ActionType.Socialize : ActionType.Work;
    }

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

        long ticks = Mobility.HouseholdTicks(world, household, household.Location, market.Location);
        return Math.Min(MaxProvisionDays, ProvisionTargetDays + 2.0 * ticks / world.CurrentDate.Calendar.HoursPerDay);
    }

    /// <summary>Menor preço positivo entre comida e água no mercado: com menos que isso na carteira a ida não compra nada.</summary>
    private static long CheapestEssentialPrice(EconomyRules economy, Workplace market)
    {
        long cheapest = long.MaxValue;
        ConsiderPrice(market, economy.FoodResourceId, ref cheapest);
        ConsiderPrice(market, economy.WaterResourceId, ref cheapest);
        return cheapest == long.MaxValue ? 0 : cheapest;
    }

    private static void ConsiderPrice(Workplace market, int resourceId, ref long cheapest)
    {
        if (market.Prices.TryGetValue(new ResourceType(resourceId), out var price) && price > 0 && price < cheapest)
            cheapest = price;
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
