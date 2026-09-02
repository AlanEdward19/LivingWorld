using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Transferência atômica genérica <c>transfer.&lt;atributo&gt;:&lt;magnitude&gt;</c>
/// (portador → alvo). Sufixo <c>:from-target</c> inverte a direção. Nenhum poder nomeado.
/// </summary>
public sealed class TransferMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "transfer.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

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

        string attribute = parsed.Value.Key switch
        {
            "transfer.health" => "health",
            "transfer.hunger" => "hunger",
            "transfer.thirst" => "thirst",
            "transfer.sleep" => "sleep",
            "transfer.social" => "social",
            _ => "",
        };
        if (attribute.Length == 0)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");

        var donor = fromTarget ? ctx.Target : ctx.Carrier;
        var recipient = fromTarget ? ctx.Carrier : ctx.Target;
        int amount = parsed.Value.Amount;
        long tick = ctx.Tick.CurrentTick;
        long available = Read(donor, attribute, tick);
        if (available < amount)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            int moved = ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution);
            Debit(donor, attribute, moved, tick);
            int room = 100 - (int)Read(recipient, attribute, tick);
            Credit(recipient, attribute, Math.Min(moved, Math.Max(0, room)), tick);
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

    private static long Read(Npc npc, string attribute, long tick) => attribute switch
    {
        "health" => npc.Health,
        "hunger" => npc.HungerAt(tick),
        "thirst" => npc.ThirstAt(tick),
        "sleep" => npc.SleepAt(tick),
        "social" => npc.SocialAt(tick),
        _ => 0,
    };

    private static void Debit(Npc npc, string attribute, int amount, long tick)
    {
        switch (attribute)
        {
            case "health": npc.SetHealth(npc.Health - amount); break;
            case "hunger": npc.SetHunger(npc.HungerAt(tick) - amount, tick); break;
            case "thirst": npc.SetThirst(npc.ThirstAt(tick) - amount, tick); break;
            case "sleep": npc.SetSleep(npc.SleepAt(tick) - amount, tick); break;
            case "social": npc.SetSocial(npc.SocialAt(tick) - amount, tick); break;
        }
    }

    private static void Credit(Npc npc, string attribute, int amount, long tick)
    {
        switch (attribute)
        {
            case "health":
                npc.SetHealth(ExtraordinaryMechanicSupport.ClampNeed((long)npc.Health + amount));
                break;
            case "hunger":
                npc.SetHunger(ExtraordinaryMechanicSupport.ClampNeed((long)npc.HungerAt(tick) + amount), tick);
                break;
            case "thirst":
                npc.SetThirst(ExtraordinaryMechanicSupport.ClampNeed((long)npc.ThirstAt(tick) + amount), tick);
                break;
            case "sleep":
                npc.SetSleep(ExtraordinaryMechanicSupport.ClampNeed((long)npc.SleepAt(tick) + amount), tick);
                break;
            case "social":
                npc.SetSocial(ExtraordinaryMechanicSupport.ClampNeed((long)npc.SocialAt(tick) + amount), tick);
                break;
        }
    }
}
