using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Transmutação auditada <c>matter.transmute:&lt;origem&gt;:&lt;destino&gt;:&lt;taxa&gt;</c>
/// (ids de recurso do catálogo). Débito 1 unidade de origem; crédito <c>taxa</c> de destino.
/// </summary>
public sealed class MatterTransmuteMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "matter.transmute";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parts = declaration.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out int originId) || originId < 0
            || !int.TryParse(parts[2], out int destId) || destId < 0
            || !int.TryParse(parts[3], out int rate) || rate <= 0)
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
}
