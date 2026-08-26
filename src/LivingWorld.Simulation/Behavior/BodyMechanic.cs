using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Multiplicadores físicos puros derivados de Height/Weight/MuscleMass
/// (Fase 16.3, COH-22/23) — mesmo shape de <see cref="AttributeMechanic"/>:
/// <c>WorldState × Npc → double</c>, neutro 1.0 quando <see cref="BodyRules.Enabled"/>
/// é falso.</summary>
/// <remarks>
/// FUTURE_DEPENDENCY (COH-25): Height/Weight/MuscleMass ainda não têm consumidores em
/// equipment compatibility nem combat — documentado para auditoria P3 (T34), não
/// implementados como stub sem uso.
/// </remarks>
public static class BodyMechanic
{
    /// <summary>Capacidade de trabalho físico: cresce com <see cref="Npc.MuscleMass"/>
    /// acima da média do cenário; 1.0 no mean; neutro se BodyRules desabilitado.</summary>
    public static double WorkCapacityMultiplier(WorldState world, Npc npc)
    {
        var rules = world.BodyRules;
        if (!rules.Enabled)
            return 1.0;

        double mean = Math.Max(rules.MuscleMassMean, 1e-9);
        // Em mean → 1.0; ±100% da média → ±0.5 no fator (faixa típica ~0.5–1.5).
        return 1.0 + 0.5 * ((npc.MuscleMass - mean) / mean);
    }

    /// <summary>Custo de movimento: cresce com Weight/Height acima da média; 1.0 no mean;
    /// neutro se BodyRules desabilitado.</summary>
    public static double MovementCostMultiplier(WorldState world, Npc npc)
    {
        var rules = world.BodyRules;
        if (!rules.Enabled)
            return 1.0;

        double weightMean = Math.Max(rules.WeightMean, 1e-9);
        double heightMean = Math.Max(rules.HeightMean, 1e-9);
        double weightFactor = 0.3 * ((npc.Weight - weightMean) / weightMean);
        double heightFactor = 0.2 * ((npc.Height - heightMean) / heightMean);
        return Math.Max(0.1, 1.0 + weightFactor + heightFactor);
    }
}
