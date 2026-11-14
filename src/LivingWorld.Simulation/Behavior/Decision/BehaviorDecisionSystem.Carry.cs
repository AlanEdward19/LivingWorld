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
        if (!world.InventionOn || world.Invention!.Carried.Count == 0 || !world.Invention.CarriesAnything(npc.Id.Value)) return;
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && !PersonalCarry.PantryOutOfReach(world, npc, home))
        {
            PersonalCarry.Unpack(world, npc, home); // em casa (a despensa ao alcance da hora)
            return;
        }
        PersonalCarry.ConsumeFromPack(world, npc, now, CriticalDeficit);
    }

    /// <summary>Ao sair de casa (a viagem começa na porta): estima quanto tempo fica fora (ida, o que vai fazer lá e volta) e leva o
    /// que a sede e a fome vão pedir. Quem saiu para comer em casa (estava longe) não arruma nada.</summary>
    private static void PackOnDeparture(WorldState world, Npc npc, MarketIndex marketIndex, long now)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence) return; // sem despensa em casa, levar não muda nada
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        if (npc.TravelPurpose == ActionType.Eat || PersonalCarry.PantryOutOfReach(world, npc, home)) return; // só sai de casa quem está nela
        if (TravelDestinationOf(world, npc, marketIndex) is not { } destination || destination == npc.CurrentLocation) return;
        long leg = Mobility.TravelTicks(world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc));
        long stay = npc.TravelPurpose is { } purpose
            ? Objects.ObjectUse.DurationHours(world, npc, purpose, world.ActionCatalog)
            : 0;
        PersonalCarry.PackForTrip(world, npc, home, 2 * leg + stay, now);
    }

    /// <summary>Comer precisa da despensa: longe de casa, sem nada no que leva que sirva e fora do próprio local de trabalho (onde a
    /// oficina dá a ração), a pessoa volta para comer. Devolve a casa quando é preciso voltar.</summary>
    private static CellCoord? EatDestinationOf(WorldState world, Npc npc)
    {
        if (!world.InventionOn || npc.Household is not { } h || world.FindHousehold(h) is not { } home) return null;
        long now = world.CurrentDate.TotalHours;
        if (PersonalCarry.PackServes(world, npc, now, world.NeedsRules.UrgencyThreshold)) return null;
        if (npc.Employer is { } employer && world.FindWorkplace(employer) is { } work && work.Location == npc.CurrentLocation)
        {
            int urgency = world.NeedsRules.UrgencyThreshold;
            var econ = world.EconomyRules;
            bool thirsty = 100 - npc.ThirstAt(now) > urgency, hungry = 100 - npc.HungerAt(now) > urgency;
            if ((thirsty && work.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) > 0)
                || (hungry && work.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) > 0))
                return null; // a oficina dá a ração
        }
        return PersonalCarry.PantryOutOfReach(world, npc, home) ? home.Location : null;
    }

    /// <summary>Voltar para comer é o tempo perdido que o problema de carregar sente.</summary>
    private static void FeltHomeForMeal(WorldState world, Npc npc)
    {
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        world.InventionTally.HomeForMeal++;
        PersonalCarry.FeltAway(world, npc, Mobility.TravelTicks(world, npc, npc.CurrentLocation, home.Location, 1.0));
    }
}
