using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Efeito <c>npc.&lt;atributo&gt;:±n</c> em qualquer atributo de <see cref="NpcStatAccessors"/>,
/// com clamp na faixa do atributo.</summary>
public sealed class NpcStatMechanic : ExtraordinaryMechanic
{
    public const string KeyPrefix = "npc.";

    public override string Prefix => KeyPrefix;
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>Parser único de <c>npc.&lt;atributo&gt;:±n</c> (carga e invocação).</summary>
    public static readonly ExtraordinaryPrimitive<(NpcStat Stat, int Amount)> Effect = new(
        KeyPrefix,
        NpcStatAccessors.All
            .Select(stat => new PrimitiveListing(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<±n>", true, "target"))
            .ToList(),
        static token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", allowSigned: true);
            if (!parsed.IsSuccess) return Result<(NpcStat, int)>.Fail(parsed.Error!);
            return NpcStatAccessors.TryGet(parsed.Value.Key, KeyPrefix, out var stat)
                ? Result<(NpcStat, int)>.Ok((stat, parsed.Value.Amount))
                : Result<(NpcStat, int)>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Effect];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = Effect.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        var (stat, amount) = parsed.Value;
        var target = ctx.Target;
        var tick = ctx.Tick.CurrentTick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
            stat.AddClamped(target, ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution), tick)));
    }
}
