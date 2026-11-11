using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Transferência atômica genérica <c>transfer.&lt;atributo&gt;:&lt;magnitude&gt;</c>
/// (portador → alvo). Sufixo <c>:from-target</c> inverte a direção. Nenhum poder nomeado.
/// </summary>
public sealed class TransferMechanic : ExtraordinaryMechanic
{
    public const string KeyPrefix = "transfer.";

    public override string Prefix => KeyPrefix;
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    private const string FromTargetSuffix = ":from-target";
    private const string LifespanKey = KeyPrefix + "lifespan-years";

    /// <summary>Transferência lida de um token: <see cref="Stat"/> nulo = anos de vida.</summary>
    public readonly record struct Transfer(NpcStat? Stat, int Amount, bool FromTarget);

    /// <summary>Parser único de <c>transfer.&lt;atributo&gt;:n[:from-target]</c> e <c>transfer.lifespan-years</c>.
    /// Com o sufixo <c>:from-target</c> a forma tipada <c>chave:tipo:n</c> não vale (o motor não a reduz).</summary>
    public static readonly ExtraordinaryPrimitive<Transfer> Token = new(
        KeyPrefix,
        NpcStatAccessors.All
            .Select(stat => new PrimitiveListing(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<n>[:from-target]", false, "target"))
            .Append(new PrimitiveListing(
                LifespanKey, "transfer.lifespan-years:<anos>[:from-target]", false, "target"))
            .ToList(),
        static declaration =>
        {
            bool fromTarget = declaration.EndsWith(FromTargetSuffix, StringComparison.Ordinal);
            string token = fromTarget ? declaration[..^FromTargetSuffix.Length] : declaration;
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", allowSigned: false);
            if (!parsed.IsSuccess) return Result<Transfer>.Fail(parsed.Error!);
            if (parsed.Value.Key == LifespanKey)
                return Result<Transfer>.Ok(new Transfer(null, parsed.Value.Amount, fromTarget));
            return NpcStatAccessors.TryGet(parsed.Value.Key, KeyPrefix, out var stat)
                ? Result<Transfer>.Ok(new Transfer(stat, parsed.Value.Amount, fromTarget))
                : Result<Transfer>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Token];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = Token.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        bool fromTarget = parsed.Value.FromTarget;

        if (parsed.Value.Stat is not { } stat)
            return PrepareLifespan(ctx, declaration, fromTarget, parsed.Value.Amount);

        var donor = fromTarget ? ctx.Target : ctx.Carrier;
        var recipient = fromTarget ? ctx.Carrier : ctx.Target;
        int amount = parsed.Value.Amount;
        long tick = ctx.Tick.CurrentTick;
        long available = stat.Read(donor, tick);
        if (available < amount)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            int moved = ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution);
            stat.SubtractRaw(donor, moved, tick);
            int room = stat.Max - stat.Read(recipient, tick);
            stat.AddClamped(recipient, Math.Min(moved, Math.Max(0, room)), tick);
        }));
    }

    /// <summary>Anos de vida (AD-130): com a morte como risco contínuo não há data marcada para mover; o doador envelhece
    /// biologicamente <paramref name="years"/> anos e o receptor rejuvenesce o mesmo tanto (<see cref="Npc.LifespanOffsetHours"/>).
    /// O doador precisa ter esses anos antes do teto de longevidade.</summary>
    private static Result<PreparedMutation?> PrepareLifespan(
        ExtraordinaryMechanicContext ctx, string declaration, bool fromTarget, int years)
    {
        var donor = fromTarget ? ctx.Target : ctx.Carrier;
        var recipient = fromTarget ? ctx.Carrier : ctx.Target;
        if (donor is not { IsAlive: true } || recipient is not { IsAlive: true })
            return Result<PreparedMutation?>.Fail("Effects: NPC ausente ou morto");

        long hoursPerYear = ctx.World.Calendar.HoursPerYear;
        long current = ctx.Tick.CurrentTick;
        if (RemainingLifespanHours(ctx.World, donor, current) < years * hoursPerYear)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            long moved = ExtraordinaryMechanicSupport.ScaledAmount(years, resolution) * hoursPerYear;
            donor.ShiftLifespanOffset(moved);
            recipient.ShiftLifespanOffset(-moved);
        }));
    }

    /// <summary>Horas até o teto de longevidade da tábua, na idade biológica atual.</summary>
    internal static long RemainingLifespanHours(WorldState world, Npc npc, long tick) =>
        world.PopulationRules.LifeTable.MaxLongevityYears * world.Calendar.HoursPerYear
        - (tick - npc.BirthDate.TotalHours + npc.LifespanOffsetHours);
}
