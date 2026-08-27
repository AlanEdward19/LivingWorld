using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Reavalia estágios de evolução por idade e/ou uso (EVO-01..03, EVO-05). Estágio 0 permanece
/// nos efeitos base do descritor; limiares com idade+uso exigem ambos (AND estrito).
/// </summary>
public sealed class ExtraordinaryPowerStageSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryPowerStage";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var carrier in world.ExtraordinaryCarriers
                     .OrderBy(item => item.CarrierId.Value)
                     .ToList())
        {
            if (world.FindNpc(carrier.CarrierId) is not { IsAlive: true } npc) continue;

            int stageIndex = ResolveCarrierStageIndex(world, npc, carrier);
            if (stageIndex == carrier.CurrentStageIndex) continue;

            world.UpsertExtraordinaryCarrier(carrier with { CurrentStageIndex = stageIndex });
        }
    }

    internal static int ResolveCarrierStageIndex(
        WorldState world, Npc npc, ExtraordinaryCarrierState carrier)
    {
        int ageYears = npc.AgeYears(world.CurrentDate);
        int highest = 0;
        foreach (string powerId in carrier.PowerIds)
        {
            var descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
                candidate => string.Equals(candidate.Id, powerId, StringComparison.Ordinal));
            if (descriptor is null) continue;
            highest = Math.Max(highest, ResolveStageIndex(descriptor, ageYears, carrier.UseCount));
        }
        return highest;
    }

    internal static int ResolveStageIndex(PowerDescriptor descriptor, int ageYears, int useCount)
    {
        if (descriptor.Stages is not { Count: > 0 }) return 0;

        int highest = 0;
        for (int i = 0; i < descriptor.Stages.Count; i++)
        {
            if (IsStageMet(descriptor.Stages[i], ageYears, useCount))
                highest = i + 1;
        }
        return highest;
    }

    internal static bool IsStageMet(PowerEvolutionStage stage, int ageYears, int useCount)
    {
        if (stage.AgeThreshold is int ageRequired && ageYears < ageRequired) return false;
        if (stage.UseCountThreshold is int useRequired && useCount < useRequired) return false;
        return stage.AgeThreshold is not null || stage.UseCountThreshold is not null;
    }

    internal static IReadOnlyList<string> EffectiveEffects(
        PowerDescriptor descriptor, int ageYears, int useCount)
    {
        int stageIndex = ResolveStageIndex(descriptor, ageYears, useCount);
        if (stageIndex == 0 || descriptor.Stages is null)
            return descriptor.Effects;
        return descriptor.Stages[stageIndex - 1].EffectTokens;
    }
}
