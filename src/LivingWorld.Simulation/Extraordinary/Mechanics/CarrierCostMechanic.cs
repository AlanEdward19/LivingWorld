using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Custo <c>carrier.&lt;atributo&gt;:n</c> em qualquer atributo de <see cref="NpcStatAccessors"/>.
/// Subtrai sem clamp próprio (o setter do NPC limita).</summary>
public sealed class CarrierCostMechanic : ExtraordinaryMechanic
{
    public const string KeyPrefix = "carrier.";

    public override string Prefix => KeyPrefix;
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Cost;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        NpcStatAccessors.All
            .Select(stat => new ExtraordinaryPrimitive(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<n>", false, "carrier"))
            .ToList();

    public override Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
    {
        if (!NpcStatAccessors.TryGet(key, KeyPrefix, out var stat))
            return Result<long>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<long>.Ok(stat.Read(ctx.Carrier, ctx.Tick.CurrentTick));
    }

    public override Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Costs", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation>.Fail(parsed.Error!);
        string key = parsed.Value.Key;
        if (!NpcStatAccessors.TryGet(key, KeyPrefix, out var stat))
            return Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{key}'");
        var carrier = ctx.Carrier;
        long tick = ctx.Tick.CurrentTick;
        return Result<PreparedMutation>.Ok(new PreparedMutation(
            declaration, _ => stat.SubtractRaw(carrier, amount, tick)));
    }
}
