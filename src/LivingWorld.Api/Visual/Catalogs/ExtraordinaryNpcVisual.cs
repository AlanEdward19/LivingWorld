using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Api.Visual.Catalogs;

public sealed record ExtraordinaryNeedSubstitutionVisual(
    string ReplacesNeed, int ResourceId, long UnitsPerUse);

/// <summary>Projeção genérica do estado extraordinário; não interpreta ids nem nomes de poder.</summary>
public sealed record ExtraordinaryNpcVisual(
    IReadOnlyList<string> PowerIds,
    bool IsManifested,
    string ManifestationState,
    double ScaleMultiplier,
    string SkinTint,
    string MovementTrail,
    ExtraordinaryNeedSubstitutionVisual? NeedSubstitution,
    double SenescenceRateMultiplier,
    bool CanFly,
    double SpeedMultiplier);

public static class ExtraordinaryNpcVisualProjector
{
    public static ExtraordinaryNpcVisual? Build(WorldState world, NpcId npcId)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npcId);
        if (carrier is null) return null;
        var npc = world.FindNpc(npcId);
        var locomotion = npc is null
            ? new ExtraordinaryLocomotionProfile(false, false, 1)
            : ExtraordinaryLocomotion.Resolve(world, npc);

        var need = carrier.NeedSubstitution is { } substitution
            ? new ExtraordinaryNeedSubstitutionVisual(
                substitution.ReplacesNeed, substitution.Resource.Id, substitution.UnitsPerUse)
            : null;
        return new ExtraordinaryNpcVisual(
            carrier.PowerIds, carrier.IsManifested, carrier.ManifestationState,
            carrier.Appearance.ScaleMultiplier, carrier.Appearance.SkinTint, carrier.Appearance.MovementTrail,
            need, carrier.SenescenceRateMultiplier, locomotion.CanFly, locomotion.SpeedMultiplier);
    }
}
