using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — o que a pessoa leva consigo (AD-147): arrumar ao sair, beber do que leva, devolver em casa.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Antes de decidir: em casa, o que sobrou volta para a despensa; longe dela, com sede ou fome crítica, bebe e come do
    /// que leva antes de qualquer outra coisa (o corpo não espera a próxima escolha).</summary>
    private static void SettleCarried(WorldState world, Npc npc, long now)
    {
        if (!world.InventionOn) return;
        var ledger = world.Invention!;
        if (world.InventionRules!.PantryNeedsPresence && npc.CurrentAction == ActionType.Work) WorkBreakMeal(world, npc, now);
        if (!ledger.CarriesOrHolds(npc.Id.Value)) return; // AD-167: uma consulta no lugar de duas buscas (o caso comum: nada)
        bool carries = ledger.Carried.Count > 0 && ledger.CarriesAnything(npc.Id.Value);
        if (!carries && ledger.HeldBy(npc.Id.Value) == 0) return; // AD-157: quem só leva o recipiente vazio também o devolve
        // AD-185: a água da fonte só entra na despensa quando quem a leva chega em casa (perto não basta)
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && !PersonalCarry.PantryOutOfReach(world, npc, home)
            && !LivingWorld.Simulation.Economy.Supply.WaterFetch.HeadingHome(world, npc))
        {
            // AD-182 (08c item 111): o recipiente de quem está indo comprar perto de casa fica com ele até a compra acabar
            if (!carries && PersonalCarry.HoldsForMarket(world, npc, home)) return;
            PersonalCarry.Unpack(world, npc, home); // em casa (a despensa ao alcance da hora)
            return;
        }
        if (carries) PersonalCarry.ConsumeFromPack(world, npc, now, world.NeedsRules.UrgencyThreshold); // AD-157: come o lanche com fome, não só à beira do fim
    }

    /// <summary>AD-157b: a pausa do turno. No próprio local de trabalho, com a fome na hora da refeição
    /// (<see cref="PersonalCarry.MealTimeDeficit"/>), come do que levou ou a refeição do empregador ali mesmo, sem largar o
    /// trabalho nem viajar; a sede, da água do poço da propriedade. Antes só comia na urgência, e a fome sozinha só vencia crítica.</summary>
    private static void WorkBreakMeal(WorldState world, Npc npc, long now)
    {
        int meal = PersonalCarry.MealTimeDeficit;
        if (100 - npc.HungerAt(now) < meal && 100 - npc.ThirstAt(now) < meal) return; // o caso comum: sem fome nem sede na hora
        if (npc.Employer is not { } employer || world.FindWorkplace(employer) is not { } work || work.Location != npc.CurrentLocation) return;
        var econ = world.EconomyRules;
        if (100 - npc.HungerAt(now) >= meal && !PersonalCarry.ConsumeFromPack(world, npc, now, meal))
        {
            var food = new ResourceType(econ.FoodResourceId);
            if (TryRation(world, npc, food))
            {
                npc.SetHunger(Math.Max(npc.HungerAt(now), Objects.ObjectUse.ConsumeLevel(world, food, LivingWorld.Domain.Objects.ObjectEffectTargets.Hunger)), now);
                world.RecordResourceConsumed(food, 1);
                world.InventionTally.WorkBreakMeals++;
            }
        }
        if (100 - npc.ThirstAt(now) >= meal)
        {
            var water = new ResourceType(econ.WaterResourceId);
            if (TryRation(world, npc, water))
            {
                npc.SetThirst(Math.Max(npc.ThirstAt(now), Objects.ObjectUse.ConsumeLevel(world, water, LivingWorld.Domain.Objects.ObjectEffectTargets.Thirst)), now);
                world.RecordResourceConsumed(water, 1);
            }
        }
    }

    /// <summary>Ao sair de casa (a viagem começa na porta): estima quanto tempo fica fora (ida, o que vai fazer lá e volta) e leva o
    /// que a sede e a fome vão pedir. Quem saiu para comer em casa (estava longe) não arruma nada.</summary>
    /// <summary>AD-157b: a última saída arrumada (pessoa, tick, destino, ida), só memória do mesmo tick.</summary>
    [ThreadStatic] private static (long Npc, long Tick, CellCoord From, ActionType? Purpose, CellCoord Destination, long Leg) _departure;

    internal static void PackOnDeparture(WorldState world, Npc npc, MarketIndex marketIndex, long now)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence) return; // sem despensa em casa, levar não muda nada
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        if (npc.TravelPurpose == ActionType.Eat || PersonalCarry.PantryOutOfReach(world, npc, home)) return; // só sai de casa quem está nela
        if (TravelDestinationOf(world, npc, marketIndex) is not { } destination || destination == npc.CurrentLocation) return;
        // AD-157b: o destino e a ida ficam para a conta da chegada logo a seguir (TravelArrivalTick), que os refazia
        long leg = Mobility.TravelTicks(world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc));
        _departure = (npc.Id.Value, now, npc.CurrentLocation, npc.TravelPurpose, destination, leg);
        if (npc.TravelPurpose == ActionType.Buy) PersonalCarry.TakeCarrierToMarket(world, npc, home); // AD-182 (08c item 111)
        if (!PersonalCarry.LeavesPantryBehind(world, npc, home, destination)) return; // AD-151: perto de casa não arruma nada
        long stay = npc.TravelPurpose is { } purpose
            ? Objects.ObjectUse.DurationHours(world, npc, purpose, world.ActionCatalog)
            : 0;
        // AD-157 (backlog 08 item 67): quem vai ao próprio trabalho e lá ganha a refeição (e a água do poço) leva só para o caminho
        long margin = world.InventionRules.PackMarginHours; // AD-157: a folga do lanche
        long waterHours = 2 * leg + stay + margin, foodHours = waterHours;
        if (npc.TravelPurpose == ActionType.Work && npc.Employer is { } employer && world.FindWorkplace(employer) is { } work
            && work.Location == destination)
        {
            var (water, food) = WorkFeeds(world, npc, work);
            if (water) waterHours = 2 * leg + margin;
            if (food) foodHours = 2 * leg + margin;
            // AD-184: quem ajuda numa obra depois do turno leva água e lanche para as horas de lá (o patrão não dá na obra)
            if (LivingWorld.Simulation.Cities.Streets.SiteWork.HelpOf(world, npc) is not null)
            {
                long after = Math.Max(0, world.StreetRules.BuildDaylightEndHour - world.Calendar.HoursPerDay * 2 / 3);
                waterHours += after;
                foodHours += after;
            }
        }
        // AD-185: para a fonte o cântaro sai vazio (bebe-se lá); leva só a comida que a ida e a volta pedem
        if (LivingWorld.Simulation.Economy.Supply.WaterFetch.Outbound(world, npc)) waterHours = 0;
        PersonalCarry.PackForTrip(world, npc, home, waterHours, foodHours, now);
        // AD-179 (item 175): carregado, a ida demora mais; a chegada agendada usa a ida com a carga
        if (Mobility.LoadFactor(world, npc) != 1.0)
            _departure = (npc.Id.Value, now, npc.CurrentLocation, npc.TravelPurpose, destination,
                Mobility.TravelTicks(world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc)));
    }

    /// <summary>O que o local de trabalho dá a quem trabalha lá (AD-151): a água do poço da propriedade e a refeição por conta do
    /// salário, enquanto há no estoque e o salário a cobre.</summary>
    private static (bool Water, bool Food) WorkFeeds(WorldState world, Npc npc, LivingWorld.Domain.Economy.Workplace work)
    {
        var econ = world.EconomyRules;
        var foodType = new ResourceType(econ.FoodResourceId);
        bool water = work.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) > 0;
        bool food = work.Stock.GetValueOrDefault(foodType) > 0 && !econ.NoWorkMeal
            && (econ.FreeWorkRation || Economy.Labor.Payroll.BoardCharge(world, work, npc, foodType) >= 0);
        return (water, food);
    }

    /// <summary>Comer precisa da despensa: longe de casa, sem nada no que leva que sirva e fora do próprio local de trabalho (onde a
    /// oficina dá a ração), a pessoa volta para comer. Devolve a casa quando é preciso voltar.</summary>
    private static CellCoord? EatDestinationOf(WorldState world, Npc npc)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence) return null; // sem despensa em casa, come onde está
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return null;
        if (!PersonalCarry.PantryOutOfReach(world, npc, home)) return null; // a despensa está ao alcance (o caso comum)
        long now = world.CurrentDate.TotalHours;
        if (PersonalCarry.PackServes(world, npc, now, world.NeedsRules.UrgencyThreshold)) return null;
        if (npc.Employer is { } employer && world.FindWorkplace(employer) is { } work && work.Location == npc.CurrentLocation)
        {
            int urgency = world.NeedsRules.UrgencyThreshold;
            bool thirsty = 100 - npc.ThirstAt(now) > urgency, hungry = 100 - npc.HungerAt(now) > urgency;
            var (water, food) = WorkFeeds(world, npc, work);
            if ((thirsty && water) || (hungry && food))
                return null; // a oficina dá a refeição (a comida, por conta do salário: AD-151)
        }
        return home.Location;
    }

    /// <summary>AD-157b: à toa ou conversando longe da despensa (o turno acabou longe de casa): a viagem leva para a cama da casa.</summary>
    internal static bool OffShiftFarFromPantry(WorldState world, Npc npc) =>
        world.InventionOn && world.InventionRules!.PantryNeedsPresence && npc.Household is { } h
        && world.FindHousehold(h) is { } home && PersonalCarry.PantryOutOfReach(world, npc, home)
        && SleepDestinationOf(world, npc) is { } bed && bed == home.Location;

    /// <summary>AD-157b: a ceia. Com a despensa em casa, quem vai dormir em casa e chegaria à fome crítica na noite come antes; quem
    /// só chegaria à sede urgente bebe um copo (como o desjejum antes de sair): sem isso acordava com fome e sede.</summary>
    internal static bool SupperBeforeSleep(WorldState world, Npc npc, ActionCatalog catalog, long now)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence || npc.Household is not { } h
            || world.FindHousehold(h) is not { IsHomeless: false } home || npc.CurrentLocation != home.Location)
            return false;
        var needs = world.NeedsRules;
        var econ = world.EconomyRules;
        int line = 100 - needs.UrgencyThreshold;
        long night = Objects.ObjectUse.DurationHours(world, npc, ActionType.Sleep, catalog);
        bool hungry = npc.HungerAt(now) - needs.HungerDecayPerHour * night < line
            && home.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) > 0;
        bool thirsty = npc.ThirstAt(now) - needs.ThirstDecayPerHour * night < 100 - PersonalCarry.MealAheadDeficit
            && home.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) > 0;
        if (!hungry && !thirsty) return false;
        world.InventionTally.SupperBeforeSleep++;
        return true; // a ceia: comer e beber antes de deitar (beber sozinho deixava a fome sozinha, que perde para o resto até a crítica)
    }

    /// <summary>AD-157b: a hora da refeição em casa. Com a despensa em casa, quem está em casa sem urgência e com a fome perto da
    /// urgência (<see cref="PersonalCarry.MealTimeDeficit"/>) come agora, com a casa tendo comida: a fome sozinha só vencia o resto
    /// da escolha já crítica (a refeição de duas horas era passada com fome crítica).</summary>
    internal static bool MealTimeAtHome(WorldState world, Npc npc, ActionType candidate, long now)
    {
        // à toa ou conversando: a hora da refeição; deitado ou indo deitar: só a fome já urgente levanta para comer (a ceia cuida da
        // hora de deitar); o resto (trabalho, compra, cuidar) segue
        int threshold = candidate switch
        {
            ActionType.Idle or ActionType.Socialize => PersonalCarry.MealTimeDeficit,
            ActionType.Sleep => world.NeedsRules.UrgencyThreshold,
            _ => int.MaxValue,
        };
        if (threshold == int.MaxValue || 100 - npc.HungerAt(now) < threshold || !world.InventionOn || !world.InventionRules!.PantryNeedsPresence || npc.Household is not { } h
            || world.FindHousehold(h) is not { IsHomeless: false } home || npc.CurrentLocation != home.Location)
            return false;
        if (100 - npc.HungerAt(now) < threshold) return false;
        if (home.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)) <= 0) return false;
        world.InventionTally.MealTimeAtHome++;
        return true;
    }

    /// <summary>Voltar para comer é o tempo perdido que o problema de carregar sente.</summary>
    private static void FeltHomeForMeal(WorldState world, Npc npc)
    {
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        world.InventionTally.HomeForMeal++;
        // só medida (AD-157b): de onde sai a volta para comer
        var t = world.InventionTally;
        if (npc.Employer is { } e && world.FindWorkplace(e) is { } w && w.Location == npc.CurrentLocation) t.HomeForMealFromWork++;
        else if (world.Workplaces.Any(x => x.Location == npc.CurrentLocation)) t.HomeForMealFromOtherPlace++;
        else if (world.Households.Any(x => x.Location == npc.CurrentLocation)) t.HomeForMealFromOtherHome++;
        if (npc.CurrentAction == ActionType.Socialize) t.HomeForMealSocial++;
        else if (npc.CurrentAction == ActionType.Idle) t.HomeForMealIdle++;
        else if (npc.CurrentAction == ActionType.Travel) t.HomeForMealTravel++;
        if (100 - npc.ThirstAt(world.CurrentDate.TotalHours) > world.NeedsRules.UrgencyThreshold) t.HomeForMealThirsty++;
        PersonalCarry.FeltAway(world, npc, Mobility.TravelTicks(world, npc, npc.CurrentLocation, home.Location, 1.0));
    }

    /// <summary>AD-185: a sede urgente sem água no que leva nem em casa vai à fonte, se a fonte fica mais perto que o mercado mais
    /// perto com água ou se não há com que pagar a água dele. Marca a ida. Quem podia pagar e tem o mercado mais perto só fica com o
    /// mercado se a escolha foi comprar: a escolha que não vai ao mercado (à toa em casa) ia à fonte (antes morria de sede com moeda
    /// no bolso, o mercado mais perto e a escolha parada).</summary>
    private static bool ThirstGoesToSource(WorldState world, Npc npc, long now, MarketIndex marketIndex, ActionType candidate)
    {
        if (!LivingWorld.Simulation.Economy.Supply.WaterFetch.ThirstyWithoutWater(world, npc, now)) return false;
        var market = NearestEssentialsMarket(world, npc, marketIndex);
        long marketTicks = market is null ? long.MaxValue
            : Mobility.TravelTicks(world, npc, npc.CurrentLocation, market.Location, BodyMechanic.MovementCostMultiplier(world, npc));
        bool canPay = market is not null && market.Prices.TryGetValue(new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.WaterResourceId), out var price)
            && price > 0 && npc.Wallet.Amount >= price && candidate == ActionType.Buy;
        return LivingWorld.Simulation.Economy.Supply.WaterFetch.ThirstSendsToSource(world, npc, now, marketTicks, canPay);
    }
}
