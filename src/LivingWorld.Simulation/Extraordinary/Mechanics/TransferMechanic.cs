using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

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

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        NpcStatAccessors.All
            .Select(stat => new ExtraordinaryPrimitive(
                KeyPrefix + stat.Key, $"{KeyPrefix}{stat.Key}:<n>[:from-target]", false, "target"))
            .Append(new ExtraordinaryPrimitive(
                KeyPrefix + "lifespan-years", "transfer.lifespan-years:<anos>[:from-target]", false, "target"))
            .ToList();

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        bool fromTarget = declaration.EndsWith(":from-target", StringComparison.Ordinal);
        string token = fromTarget
            ? declaration[..^":from-target".Length]
            : declaration;
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);

        if (parsed.Value.Key == "transfer.lifespan-years")
            return PrepareLifespan(ctx, declaration, fromTarget, parsed.Value.Amount);

        if (!NpcStatAccessors.TryGet(parsed.Value.Key, KeyPrefix, out var stat))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");

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

    private static Result<PreparedMutation?> PrepareLifespan(
        ExtraordinaryMechanicContext ctx, string declaration, bool fromTarget, int years)
    {
        var donor = fromTarget ? ctx.Target : ctx.Carrier;
        var recipient = fromTarget ? ctx.Carrier : ctx.Target;
        var donorDeath = FindAgeDeath(ctx.World, donor);
        var recipientDeath = FindAgeDeath(ctx.World, recipient);
        if (donorDeath is null || recipientDeath is null || !donor.IsAlive || !recipient.IsAlive)
            return Result<PreparedMutation?>.Fail("Effects: NPC ausente ou morto");

        long hoursPerYear = ctx.World.Calendar.HoursPerYear;
        long current = ctx.Tick.CurrentTick;
        if (donorDeath.TargetTick - current < years * hoursPerYear)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            long moved = ExtraordinaryMechanicSupport.ScaledAmount(years, resolution) * hoursPerYear;
            RescheduleAgeDeath(ctx.Tick, donorDeath, donorDeath.TargetTick - moved, current);
            RescheduleAgeDeath(ctx.Tick, recipientDeath, recipientDeath.TargetTick + moved, current);
        }));
    }

    private static ScheduledEvent? FindAgeDeath(WorldState world, Npc npc)
    {
        string payload = npc.Id.Value.ToString();
        foreach (var evt in world.PendingEvents)
        {
            if (evt.SystemName == MortalitySystem.SystemName && evt.Payload == payload)
                return evt;
        }

        return null;
    }

    private static void RescheduleAgeDeath(TickContext ctx, ScheduledEvent original, long tick, long current)
    {
        ctx.CancelEvent(original.Id);
        if (tick <= current)
            tick = current + 1;
        ctx.ScheduleEvent(tick, MortalitySystem.SystemName, original.Payload);
    }
}
