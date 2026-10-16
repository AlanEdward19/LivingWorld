using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — destino da viagem por motivo (trabalho, sono, compra, mudança).
public sealed partial class BehaviorDecisionSystem
{
    private static CellCoord? WorkDestinationOf(WorldState world, Npc npc) =>
        npc.Employer is { } workplaceId ? world.FindWorkplace(workplaceId)?.Location : null;

    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location == npc.CurrentLocation) return null;
        return RestPlaceResolver.IsReachable(world.Map, npc.CurrentLocation, rest.Location) ? rest.Location : null;
    }

    private static CellCoord? BuyDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        NearestEssentialsMarket(world, npc, marketIndex)?.Location;

    /// <summary>Mercado mais próximo com comida ou água em estoque — quem vai comprar não vai à
    /// ferraria só porque ela fica mais perto.</summary>
    private static Workplace? NearestEssentialsMarket(WorldState world, Npc npc, MarketIndex marketIndex) =>
        marketIndex.NearestSelling(
            npc.CurrentLocation,
            new ResourceType(world.EconomyRules.FoodResourceId), new ResourceType(world.EconomyRules.WaterResourceId));

    private static CellCoord? TravelDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        RelocationDestinationOf(world, npc) is { } relocationDest
            ? relocationDest
            : PurposeDestinationOf(world, npc, marketIndex) is { } purposeDest
                ? purposeDest
                : SleepDestinationOf(world, npc) is { } sleepDest && sleepDest != npc.CurrentLocation
                    ? sleepDest
                    : WorkDestinationOf(world, npc) is { } workDest && workDest != npc.CurrentLocation
                        ? workDest
                        : BuyDestinationOf(world, npc, marketIndex);

    /// <summary>Destino do motivo da viagem (ir comprar, trabalhar ou dormir); nulo se o motivo é outro ou o
    /// NPC já está lá — e então vale a ordem de antes.</summary>
    private static CellCoord? PurposeDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        var destination = npc.TravelPurpose switch
        {
            ActionType.Buy => BuyDestinationOf(world, npc, marketIndex),
            ActionType.Work => WorkDestinationOf(world, npc),
            ActionType.Sleep => SleepDestinationOf(world, npc),
            _ => null,
        };
        return destination is { } cell && cell != npc.CurrentLocation ? cell : null;
    }

    private static CellCoord? RelocationDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId
        && world.FindHousehold(householdId) is { PendingRelocationCity: { } cityId }
        && world.FindCity(cityId) is { } city
            ? city.Location
            : null;
}
