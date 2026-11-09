using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Transmutação auditada <c>matter.transmute:&lt;origem&gt;:&lt;destino&gt;:&lt;taxa&gt;</c>
/// (ids de recurso do catálogo). Débito 1 unidade de origem; crédito <c>taxa</c> de destino.
/// </summary>
public sealed class MatterTransmuteMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "matter.transmute";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
    [
        new("matter.transmute", "matter.transmute:<origem>:<destino>:<taxa>", false, "household", static token => TryParse(token, out _, out _, out _)),
    ];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParse(declaration, out int originId, out int destId, out int rate))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var home = ctx.Carrier.Household is { } householdId
            ? ctx.World.FindHousehold(householdId)
            : null;
        if (home is null)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var origin = new ResourceType(originId);
        var dest = new ResourceType(destId);
        const int originAmount = 1;
        if (home.Stock.GetValueOrDefault(origin) < originAmount)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        var tickCtx = ctx.Tick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            int debit = ExtraordinaryMechanicSupport.ScaledAmount(originAmount, resolution);
            long credit = (long)debit * rate;
            home.Withdraw(origin, debit);
            home.Deposit(dest, credit);
            long destroyedId = tickCtx.LogEvent(
                WorldEventKind.Destroyed, $"{origin.Id}|{debit}",
                sourceSystem: "MatterTransmuteMechanic");
            tickCtx.LogEvent(
                WorldEventKind.Minted, $"{dest.Id}|{credit}",
                sourceSystem: "MatterTransmuteMechanic", causeEventId: destroyedId);
        }));
    }

    private static bool TryParse(string declaration, out int originId, out int destId, out int rate)
    {
        originId = destId = rate = 0;
        var parts = declaration.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 4
            && string.Equals(parts[0], "matter.transmute", StringComparison.Ordinal)
            && int.TryParse(parts[1], out originId) && originId >= 0
            && int.TryParse(parts[2], out destId) && destId >= 0
            && int.TryParse(parts[3], out rate) && rate > 0;
    }
}
