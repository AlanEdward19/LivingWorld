using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class NpcStatMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: true);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        var (key, amount) = parsed.Value;
        var target = ctx.Target;
        var tick = ctx.Tick.CurrentTick;
        Action<int>? apply = key switch
        {
            "npc.health" => value => target.SetHealth(ExtraordinaryMechanicSupport.ClampNeed((long)target.Health + value)),
            "npc.hunger" => value => target.SetHunger(
                ExtraordinaryMechanicSupport.ClampNeed((long)target.HungerAt(tick) + value), tick),
            "npc.thirst" => value => target.SetThirst(
                ExtraordinaryMechanicSupport.ClampNeed((long)target.ThirstAt(tick) + value), tick),
            "npc.sleep" => value => target.SetSleep(
                ExtraordinaryMechanicSupport.ClampNeed((long)target.SleepAt(tick) + value), tick),
            "npc.social" => value => target.SetSocial(
                ExtraordinaryMechanicSupport.ClampNeed((long)target.SocialAt(tick) + value), tick),
            _ => null,
        };
        if (apply is null)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{key}'");
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
            apply(ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution))));
    }
}
