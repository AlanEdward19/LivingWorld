using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class CarrierCostMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "carrier.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Cost;

    public override Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
    {
        var carrier = ctx.Carrier;
        long tick = ctx.Tick.CurrentTick;
        long available = key switch
        {
            "carrier.health" => carrier.Health,
            "carrier.hunger" => carrier.HungerAt(tick),
            "carrier.thirst" => carrier.ThirstAt(tick),
            "carrier.sleep" => carrier.SleepAt(tick),
            "carrier.social" => carrier.SocialAt(tick),
            _ => -1,
        };
        return available < 0
            ? Result<long>.Fail($"Costs: alvo não suportado '{key}'")
            : Result<long>.Ok(available);
    }

    public override Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Costs", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation>.Fail(parsed.Error!);
        string key = parsed.Value.Key;
        var carrier = ctx.Carrier;
        long tick = ctx.Tick.CurrentTick;
        Action? apply = key switch
        {
            "carrier.health" => () => carrier.SetHealth(carrier.Health - amount),
            "carrier.hunger" => () => carrier.SetHunger(carrier.HungerAt(tick) - amount, tick),
            "carrier.thirst" => () => carrier.SetThirst(carrier.ThirstAt(tick) - amount, tick),
            "carrier.sleep" => () => carrier.SetSleep(carrier.SleepAt(tick) - amount, tick),
            "carrier.social" => () => carrier.SetSocial(carrier.SocialAt(tick) - amount, tick),
            _ => null,
        };
        if (apply is null)
            return Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<PreparedMutation>.Ok(new PreparedMutation(declaration, _ => apply()));
    }
}
