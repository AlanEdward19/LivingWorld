using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Multiplicadores físicos puros derivados de Height/Weight/MuscleMass
/// (Fase 16.3, COH-22/23) — mesmo shape de <see cref="AttributeMechanic"/>:
/// <c>WorldState × Npc → double</c>, neutro 1.0 quando <see cref="BodyRules.Enabled"/>
/// é falso.</summary>
/// <remarks>
/// Consumidores: trabalho (<see cref="WorkCapacityMultiplier"/>), deslocamento
/// (<see cref="MovementCostMultiplier"/>), combate
/// (<see cref="CombatOffenseMultiplier"/> / <see cref="CombatDamageTakenMultiplier"/>).
/// Equipment compatibility permanece FUTURE_DEPENDENCY (sem sistema de equipamento ainda).
/// </remarks>
public static class BodyMechanic
{
    /// <summary>Incremento diário de MuscleMass sob trabalho físico pesado sustentado
    /// (categoria SLOW, COH-24) — kg por dia de Work presente no workplace.</summary>
    public const double DailyWorkHardeningDelta = 0.05;

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

    /// <summary>Ofensa em combate — reusa a curva de MuscleMass de
    /// <see cref="WorkCapacityMultiplier"/> (mesmo atributo causal, outro consumidor).</summary>
    public static double CombatOffenseMultiplier(WorldState world, Npc npc) =>
        WorkCapacityMultiplier(world, npc);

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

    /// <summary>Dano recebido: massa/altura acima da média absorvem um pouco mais
    /// (multiplicador &lt; 1); abaixo da média sofrem mais. Neutro 1.0 se BodyRules off
    /// ou no mean. Clamp [0.5, 1.5].</summary>
    public static double CombatDamageTakenMultiplier(WorldState world, Npc npc)
    {
        var rules = world.BodyRules;
        if (!rules.Enabled)
            return 1.0;

        double weightMean = Math.Max(rules.WeightMean, 1e-9);
        double heightMean = Math.Max(rules.HeightMean, 1e-9);
        double weightFactor = 0.2 * ((npc.Weight - weightMean) / weightMean);
        double heightFactor = 0.1 * ((npc.Height - heightMean) / heightMean);
        return Math.Clamp(1.0 - weightFactor - heightFactor, 0.5, 1.5);
    }

    /// <summary>COH-24: incrementa <see cref="Npc.MuscleMass"/> lentamente após trabalho
    /// físico pesado, clampado em <see cref="BodyRules.MuscleMassMax"/>. No-op se BodyRules
    /// desabilitado ou já no teto.</summary>
    public static void ApplyWorkHardening(WorldState world, Npc npc)
    {
        var rules = world.BodyRules;
        if (!rules.Enabled)
            return;

        double next = Math.Min(npc.MuscleMass + DailyWorkHardeningDelta, rules.MuscleMassMax);
        if (next > npc.MuscleMass)
            npc.SetMuscleMass(next);
    }
}
