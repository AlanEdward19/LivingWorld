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
        if (ledger.Carried.Count == 0 && ledger.Held is null) return;
        bool carries = ledger.Carried.Count > 0 && ledger.CarriesAnything(npc.Id.Value);
        if (!carries && ledger.HeldBy(npc.Id.Value) == 0) return; // AD-157: quem só leva o recipiente vazio também o devolve
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && !PersonalCarry.PantryOutOfReach(world, npc, home))
        {
            PersonalCarry.Unpack(world, npc, home); // em casa (a despensa ao alcance da hora)
            return;
        }
        if (carries) PersonalCarry.ConsumeFromPack(world, npc, now, world.NeedsRules.UrgencyThreshold); // AD-157: come o lanche com fome, não só à beira do fim
    }

    /// <summary>Ao sair de casa (a viagem começa na porta): estima quanto tempo fica fora (ida, o que vai fazer lá e volta) e leva o
    /// que a sede e a fome vão pedir. Quem saiu para comer em casa (estava longe) não arruma nada.</summary>
    internal static void PackOnDeparture(WorldState world, Npc npc, MarketIndex marketIndex, long now)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence) return; // sem despensa em casa, levar não muda nada
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        if (npc.TravelPurpose == ActionType.Eat || PersonalCarry.PantryOutOfReach(world, npc, home)) return; // só sai de casa quem está nela
        if (TravelDestinationOf(world, npc, marketIndex) is not { } destination || destination == npc.CurrentLocation) return;
        if (!PersonalCarry.LeavesPantryBehind(world, npc, home, destination)) return; // AD-151: perto de casa não arruma nada
        long leg = Mobility.TravelTicks(world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc));
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
        }
        PersonalCarry.PackForTrip(world, npc, home, waterHours, foodHours, now);
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
    private static bool OffShiftFarFromPantry(WorldState world, Npc npc) =>
        world.InventionOn && world.InventionRules!.PantryNeedsPresence && npc.Household is { } h
        && world.FindHousehold(h) is { } home && PersonalCarry.PantryOutOfReach(world, npc, home)
        && SleepDestinationOf(world, npc) is { } bed && bed == home.Location;

    /// <summary>AD-157b: a ceia. Com a despensa em casa, quem vai dormir em casa e passaria a noite até a fome ou a sede urgente
    /// come antes, se a despensa tem com quê (como o desjejum antes de sair): sem isso acordava com fome crítica.</summary>
    private static bool SupperBeforeSleep(WorldState world, Npc npc, ActionCatalog catalog, long now)
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
        bool thirsty = npc.ThirstAt(now) - needs.ThirstDecayPerHour * night < line
            && home.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) > 0;
        if (!hungry && !thirsty) return false;
        world.InventionTally.SupperBeforeSleep++;
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
}
