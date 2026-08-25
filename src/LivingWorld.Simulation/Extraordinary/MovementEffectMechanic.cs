using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Locomoção extraordinária é resolvida em <see cref="ExtraordinaryLocomotion"/>, não na invocação.
/// </summary>
public sealed class MovementEffectMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "movement.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Ok(null);
}
