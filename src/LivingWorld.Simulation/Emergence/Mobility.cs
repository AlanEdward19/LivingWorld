using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Emergence;

/// <summary>Consumidor causal da tecnologia de mobilidade: o eixo de velocidade (índice 0) das tecnologias que a cidade adotou
/// encurta o trajeto até o mercado, e com ele a despensa que a casa mantém e o tamanho da ida às compras.</summary>
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

    public static long Apply(long ticks, int bonusPct) => bonusPct == 0 ? ticks : ticks * 100 / (100 + bonusPct);
}
