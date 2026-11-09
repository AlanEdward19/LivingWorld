using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Custo <c>carrier.&lt;atributo&gt;:n</c> em qualquer atributo de <see cref="NpcStatAccessors"/>.
/// Subtrai sem clamp próprio (o setter do NPC limita).</summary>
public sealed class CarrierCostMechanic : ExtraordinaryMechanic
{
    public const string KeyPrefix = "carrier.";

    public override string Prefix => KeyPrefix;
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Cost;

    /// <summary>Parser único de <c>carrier.&lt;atributo&gt;:n</c> (carga e invocação).</summary>
    public static readonly ExtraordinaryPrimitive<(NpcStat Stat, int Amount)> Cost = new(
        KeyPrefix,
        NpcStatAccessors.All
            .Select(stat => new PrimitiveListing(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<n>", false, "carrier"))
            .ToList(),
        static token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Costs", allowSigned: false);
            if (!parsed.IsSuccess) return Result<(NpcStat, int)>.Fail(parsed.Error!);
            return NpcStatAccessors.TryGet(parsed.Value.Key, KeyPrefix, out var stat)
                ? Result<(NpcStat, int)>.Ok((stat, parsed.Value.Amount))
                : Result<(NpcStat, int)>.Fail($"Costs: alvo não suportado '{parsed.Value.Key}'");
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Cost];

    public override Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
    {
        if (!NpcStatAccessors.TryGet(key, KeyPrefix, out var stat))
            return Result<long>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<long>.Ok(stat.Read(ctx.Carrier, ctx.Tick.CurrentTick));
    }

    public override Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
    {
        var parsed = Cost.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation>.Fail(parsed.Error!);
        var stat = parsed.Value.Stat;
        var carrier = ctx.Carrier;
        long tick = ctx.Tick.CurrentTick;
        return Result<PreparedMutation>.Ok(new PreparedMutation(
            declaration, _ => stat.SubtractRaw(carrier, amount, tick)));
    }
}
