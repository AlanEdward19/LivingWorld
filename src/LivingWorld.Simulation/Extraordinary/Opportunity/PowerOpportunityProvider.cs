using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;

namespace LivingWorld.Simulation.Extraordinary.Opportunity;

/// <summary>Expõe powers aplicáveis do portador como candidatos de utility (Fase 16.3 P1d,
/// COH-31/32). Filtra por Mode (<see cref="ExtraordinaryInvocationEngine.IsAvailable"/>)
/// e por estágio de evolução (16.2 <c>CurrentStageIndex</c>/<c>Stages</c>).</summary>
public static class PowerOpportunityProvider
{
    /// <summary>Origem padrão da decisão do Agent — só Modes Active/Conditional entram.</summary>
    public static ExtraordinaryInvocationOrigin DecisionOrigin { get; } =
        ExtraordinaryInvocationOrigin.Authored;

    /// <summary>Lista de oportunidades scoráveis para o NPC no tick. Vazia se sem carrier
    /// manifestado/aplicável; nunca lança — mechanic com falha isolada é omitido.</summary>
    public static IReadOnlyList<PowerOpportunity> ApplicableTo(
        WorldState world,
        Npc npc,
        long tick,
        IExtraordinaryMechanicRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(npc);
        _ = tick;

        if (!world.Extraordinary.Enabled)
            return Array.Empty<PowerOpportunity>();

        var carrier = world.ExtraordinaryCarriers
            .FirstOrDefault(c => c.CarrierId == npc.Id);
        if (carrier is null)
            return Array.Empty<PowerOpportunity>();

        registry ??= ExtraordinaryMechanicRegistry.Default;
        var results = new List<PowerOpportunity>();
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (string powerId in carrier.PowerIds)
        {
            PowerDescriptor? descriptor;
            try
            {
                descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
                    d => string.Equals(d.Id, powerId, StringComparison.Ordinal));
            }
            catch
            {
                continue;
            }

            if (descriptor is null) continue;

            if (!ExtraordinaryInvocationEngine.IsAvailable(
                    descriptor.Mode, DecisionOrigin, carrier.IsManifested))
                continue;

            IReadOnlyList<string> effects;
            try
            {
                effects = EffectsAtCarrierStage(descriptor, carrier.CurrentStageIndex);
            }
            catch
            {
                continue;
            }

            foreach (string effectToken in effects)
            {
                try
                {
                    if (registry.Resolve(effectToken) is null) continue;
                    if (!seenTokens.Add(effectToken)) continue;
                    results.Add(PowerOpportunity.FromDescriptor(descriptor, effectToken));
                }
                catch
                {
                    // Isola candidato com bug — resto do NPC continua (design Error Handling).
                }
            }
        }

        return results.Count == 0 ? Array.Empty<PowerOpportunity>() : results;
    }

    /// <summary>Efeitos liberados no estágio atual do carrier: estágio 0 = Effects base;
    /// estágio N&gt;0 = <c>Stages[N-1].EffectTokens</c> (mesma semântica de
    /// <see cref="ExtraordinaryPowerStageSystem.EffectiveEffects"/>).</summary>
    internal static IReadOnlyList<string> EffectsAtCarrierStage(
        PowerDescriptor descriptor, int currentStageIndex)
    {
        if (currentStageIndex <= 0 || descriptor.Stages is not { Count: > 0 })
            return descriptor.Effects;

        int stageOrdinal = Math.Min(currentStageIndex, descriptor.Stages.Count);
        return descriptor.Stages[stageOrdinal - 1].EffectTokens;
    }
}
