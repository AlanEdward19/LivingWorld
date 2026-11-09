using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Efeito <c>npc.&lt;atributo&gt;:±n</c> em qualquer atributo de <see cref="NpcStatAccessors"/>,
/// com clamp na faixa do atributo.</summary>
public sealed class NpcStatMechanic : ExtraordinaryMechanic
{
    public const string KeyPrefix = "npc.";

    public override string Prefix => KeyPrefix;
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        NpcStatAccessors.All
            .Select(stat => new ExtraordinaryPrimitive(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<±n>", true, "target",
                PrimitiveSyntax.Amount(KeyPrefix + stat.Key, signed: true)))
            .ToList();

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: true);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        var (key, amount) = parsed.Value;
        if (!NpcStatAccessors.TryGet(key, KeyPrefix, out var stat))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{key}'");
        var target = ctx.Target;
        var tick = ctx.Tick.CurrentTick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
            stat.AddClamped(target, ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution), tick)));
    }
}
