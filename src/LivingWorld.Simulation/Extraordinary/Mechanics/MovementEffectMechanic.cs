using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Locomoção extraordinária é resolvida em <see cref="ExtraordinaryLocomotion"/>, não na invocação.
/// </summary>
public sealed class MovementEffectMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "movement.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>Parser único de <c>movement.flight:&lt;n&gt;</c> (lido por <see cref="GravityMechanic"/>).</summary>
    public static readonly ExtraordinaryPrimitive<double> Flight = new(
        "movement.flight", "movement.flight:<n>", false, "carrier", PrimitiveSyntax.PositiveNumber("movement.flight"));

    /// <summary>Parser único de <c>movement.speed-multiplier:&lt;n&gt;</c> (lido por <see cref="GravityMechanic"/>).</summary>
    public static readonly ExtraordinaryPrimitive<double> SpeedMultiplier = new(
        "movement.speed-multiplier", "movement.speed-multiplier:<n>", false, "carrier",
        PrimitiveSyntax.PositiveNumber("movement.speed-multiplier"));

    /// <summary>Qualquer <c>movement.*</c> a invocação aceita sem efeito (<c>ExtraordinaryLocomotion</c> o
    /// usa como ativador de custo de movimento): token tolerado, fora do catálogo (ADR-0040).</summary>
    public static readonly ExtraordinaryPrimitive<string> Tolerated = new(
        "movement.", [], PrimitiveSyntax.Anything());

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Flight, SpeedMultiplier, Tolerated];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Ok(null);
}
