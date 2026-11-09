using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Locomoção extraordinária é resolvida em <see cref="ExtraordinaryLocomotion"/>, não na invocação.
/// </summary>
public sealed class MovementEffectMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "movement.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
    [
        new("movement.flight", "movement.flight:<n>", false, "carrier", PrimitiveSyntax.PositiveNumber("movement.flight")),
        new("movement.speed-multiplier", "movement.speed-multiplier:<n>", false, "carrier", PrimitiveSyntax.PositiveNumber("movement.speed-multiplier")),
    ];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Ok(null);
}
