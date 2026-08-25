using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Marca o token de área no descritor; a expansão de alvos vive em <see cref="AreaTargetResolver"/>.</summary>
public sealed class AreaSelectorMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "area:";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Ok(null);
}
