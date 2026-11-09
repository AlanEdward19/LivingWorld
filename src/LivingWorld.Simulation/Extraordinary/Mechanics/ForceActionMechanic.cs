using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class ForceActionMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.force-action";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>Parser único de <c>npc.force-action:&lt;ActionType&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<ActionType> Forced = new(
        "npc.force-action", "npc.force-action:<ActionType>", false, "target",
        static token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", allowSigned: false);
            if (!parsed.IsSuccess) return Result<ActionType>.Fail(parsed.Error!);
            return Enum.IsDefined(typeof(ActionType), parsed.Value.Amount)
                ? Result<ActionType>.Ok((ActionType)parsed.Value.Amount)
                : Result<ActionType>.Fail("Effects: npc.force-action exige um ActionType válido");
        },
        looseHead: true);

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Forced];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = Forced.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        var action = parsed.Value;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(
            declaration, _ => target.SetCurrentAction(action, ctx.Tick.CurrentTick)));
    }
}
