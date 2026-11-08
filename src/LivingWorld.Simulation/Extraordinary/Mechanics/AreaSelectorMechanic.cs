using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Marca o token de área no descritor; a expansão de alvos vive em <see cref="AreaTargetResolver"/>.</summary>
public sealed class AreaSelectorMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "area:";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
    [
        new("area:radius:", "area:radius:<n>", false, "area"),
        new("area:region:", "area:region:<id>", false, "area"),
    ];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Ok(null);
}
