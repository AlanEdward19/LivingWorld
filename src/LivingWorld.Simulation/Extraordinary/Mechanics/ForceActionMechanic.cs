using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class ForceActionMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.force-action";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        if (!Enum.IsDefined(typeof(ActionType), parsed.Value.Amount))
            return Result<PreparedMutation?>.Fail("Effects: npc.force-action exige um ActionType válido");
        var action = (ActionType)parsed.Value.Amount;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(
            declaration, _ => target.SetCurrentAction(action, ctx.Tick.CurrentTick)));
    }
}
