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
        var carrier = world.CarrierOf(npcId);
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
            need, carrier.SenescenceRateMultiplier, locomotion.CanFly, WireSpeed(locomotion.SpeedMultiplier));
    }

    /// <summary>
    /// JSON não representa infinito nem NaN (ADR-0040). Espelha o motor: infinito é orçamento sem limite
    /// (satura no maior finito, preservando a ordem) e NaN é orçamento 0 (o NPC não anda).
    /// </summary>
    private static double WireSpeed(double speed) =>
        double.IsNaN(speed) ? 0 : Math.Clamp(speed, 0, double.MaxValue);
}
