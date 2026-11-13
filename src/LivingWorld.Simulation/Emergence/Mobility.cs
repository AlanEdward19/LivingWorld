using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Emergence;

/// <summary>Consumidor causal da tecnologia de mobilidade: o eixo de velocidade (índice 0) das tecnologias que a cidade adotou
/// encurta todo deslocamento da cidade (<see cref="TravelTicks"/>), e com ele a despensa que a casa mantém e o tamanho da ida às compras.</summary>
public static class Mobility
{
    public static int BonusPct(WorldState world, CityId city)
    {
        if (world.Technologies.Count == 0) return 0;

        int best = 0;
        foreach (var tech in world.Technologies)
        {
            if (tech.Vector.Axes[0] <= best || !tech.AdoptedBy.Contains(city)) continue;
            best = tech.Vector.Axes[0];
        }
        return best * world.EmergenceRules.PaceBonusPctPerPoint;
    }

    /// <summary>Ponto único da duração de um deslocamento de NPC (trabalho, cama, mercado, visita, migração): custo do mapa
    /// escalado pelo corpo/poder, encurtado pela tecnologia que a cidade do NPC adotou. Local distinto custa ao menos 1 tick.</summary>
    public static long TravelTicks(WorldState world, Npc npc, CellCoord origin, CellCoord destination, double bodyMultiplier)
    {
        long ticks = WaterAware(world, npc.Household, origin, destination, bodyMultiplier)
            ?? TravelResolution.TicksBetween(world.Map, origin, destination, bodyMultiplier);
        return ticks == 0 ? 0 : Math.Max(1, Apply(ticks, BonusPct(world, npc.City)));
    }

    /// <summary>Trajeto de compra do domicílio (casa ↔ mercado): o mesmo custo de mapa da memória de trajetos, com a água do
    /// caminho e a embarcação da casa quando a invenção está ligada (AD-141), encurtado pela tecnologia da cidade.</summary>
    public static long HouseholdTicks(WorldState world, LivingWorld.Domain.Population.Family.Household household, CellCoord origin, CellCoord destination) =>
        Apply(WaterAware(world, household.Id, origin, destination, 1.0)
            ?? LivingWorld.Simulation.Economy.Market.TravelTicksCache.Between(world.Map, origin, destination), BonusPct(world, household.City));

    /// <summary>Ticks do trajeto com água no caminho (AD-141), ou nulo quando a invenção está desligada ou não há água: aí vale o
    /// custo de antes, byte a byte. A pé, a água custa o vau; com embarcação da casa, remar.</summary>
    private static long? WaterAware(WorldState world, HouseholdId? household, CellCoord origin, CellCoord destination, double bodyMultiplier)
    {
        if (!world.InventionOn || origin == destination) return null;
        int water = LivingWorld.Simulation.Invention.WaterCrossing.CellsBetween(world.Map, origin, destination);
        if (water == 0) return null;
        var rules = world.InventionRules!;
        int craft = household is { } h ? world.Invention!.BestCraftSpeed(h.Value, rules) : 0;
        double cost = LivingWorld.Domain.Geography.Spatial.MovementCost.Between(world.Map, origin, destination)
            + LivingWorld.Simulation.Invention.WaterCrossing.ExtraCost(world.Map, rules, water, craft);
        return Math.Max(1, (long)Math.Ceiling(Math.Max(0, cost) * Math.Max(0.1, bodyMultiplier)));
    }

    public static long Apply(long ticks, int bonusPct) => bonusPct == 0 ? ticks : ticks * 100 / (100 + bonusPct);
}
