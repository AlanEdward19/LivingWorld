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
        long ticks = TravelResolution.TicksBetween(world.Map, origin, destination, bodyMultiplier);
        return ticks == 0 ? 0 : Math.Max(1, Apply(ticks, BonusPct(world, npc.City)));
    }

    public static long Apply(long ticks, int bonusPct) => bonusPct == 0 ? ticks : ticks * 100 / (100 + bonusPct);
}
