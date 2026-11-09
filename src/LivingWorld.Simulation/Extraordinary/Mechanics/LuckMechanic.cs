using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Sorte genérica: <c>luck.capacity-bonus:&lt;n&gt;</c> na resolução;
/// <c>luck.curse:&lt;n&gt;[:&lt;ticks&gt;]</c> grava janela no alvo.
/// </summary>
public sealed class LuckMechanic : ExtraordinaryMechanic
{
    public const string BonusPrefix = "luck.capacity-bonus:";
    public const string CursePrefix = "luck.curse:";

    public override string Prefix => "luck.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>Parser único de <c>luck.capacity-bonus:&lt;n&gt;</c> (lido também por <c>CapacityBonus</c>).</summary>
    public static readonly ExtraordinaryPrimitive<int> CapacityBonusToken = new(
        BonusPrefix, "luck.capacity-bonus:<n>", false, "carrier",
        PrimitiveSyntax.From<int>(
            ParseCapacityBonus, static _ => "Effects: luck.capacity-bonus exige magnitude positiva"));

    /// <summary>Parser único de <c>luck.curse:&lt;n&gt;[:&lt;ticks&gt;]</c>.</summary>
    public static readonly ExtraordinaryPrimitive<(int Amount, long DurationTicks)> Curse = new(
        CursePrefix, "luck.curse:<n>[:<ticks>]", false, "target",
        static token => ParseCurse(token, out int amount, out long duration)
            ? Result<(int, long)>.Ok((amount, duration))
            : Result<(int, long)>.Fail("Effects: luck.curse exige 'luck.curse:<n>' ou 'luck.curse:<n>:<ticks>'"));

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [CapacityBonusToken, Curse];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration.StartsWith(BonusPrefix, StringComparison.Ordinal))
        {
            var bonus = CapacityBonusToken.Parse(declaration);
            return bonus.IsSuccess
                ? Result<PreparedMutation?>.Ok(null)
                : Result<PreparedMutation?>.Fail(bonus.Error!);
        }

        var curse = declaration.StartsWith(CursePrefix, StringComparison.Ordinal)
            ? Curse.Parse(declaration)
            : Result<(int, long)>.Fail("Effects: luck.curse exige 'luck.curse:<n>' ou 'luck.curse:<n>:<ticks>'");
        if (!curse.IsSuccess)
            return Result<PreparedMutation?>.Fail(curse.Error!);
        var (amount, durationTicks) = curse.Value;

        var world = ctx.World;
        var target = ctx.Target;
        long untilTick = checked(ctx.Tick.CurrentTick + durationTicks);
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var existing = world.ExtraordinaryCarriers
                .FirstOrDefault(item => item.CarrierId == target.Id);
            var next = existing is null
                ? new ExtraordinaryCarrierState(
                    target.Id, [], false, "dormant",
                    new ExtraordinaryAppearanceState(1, "", ""), null, 1,
                    LuckCurseAmount: amount, LuckCurseUntilTick: untilTick)
                : existing with { LuckCurseAmount = amount, LuckCurseUntilTick = untilTick };
            world.UpsertExtraordinaryCarrier(next);
        }));
    }

    internal static int AdjustCapacity(WorldState world, Npc carrier, long currentTick, int capacity)
    {
        int adjusted = capacity + CapacityBonus(world, carrier) - ActiveCurse(world, carrier, currentTick);
        return Math.Max(0, adjusted);
    }

    /// <summary>Bônus de um NPC que entra na rolagem como alvo, sem aplicar a maldição dele.</summary>
    internal static int ManifestedCapacityBonus(WorldState world, Npc npc) => CapacityBonus(world, npc);

    internal static bool TryParseCapacityBonus(string declaration, out int amount) =>
        CapacityBonusToken.TryParse(declaration, out amount);

    private static bool ParseCapacityBonus(string declaration, out int amount)
    {
        amount = 0;
        if (!declaration.StartsWith(BonusPrefix, StringComparison.Ordinal)) return false;
        string numeric = declaration[BonusPrefix.Length..];
        return int.TryParse(numeric, out amount) && amount > 0
            && numeric.Equals(amount.ToString(), StringComparison.Ordinal);
    }

    internal static bool TryParseCurse(string declaration, out int amount, out long durationTicks)
    {
        bool ok = Curse.TryParse(declaration, out var parsed);
        (amount, durationTicks) = parsed;
        return ok;
    }

    private static bool ParseCurse(string declaration, out int amount, out long durationTicks)
    {
        amount = 0;
        durationTicks = 0;
        if (!declaration.StartsWith(CursePrefix, StringComparison.Ordinal)) return false;
        var parts = declaration[CursePrefix.Length..].Split(':');
        if (parts.Length == 1
            && int.TryParse(parts[0], out amount) && amount > 0
            && parts[0].Equals(amount.ToString(), StringComparison.Ordinal))
        {
            durationTicks = 1;
            return true;
        }

        return parts.Length == 2
            && int.TryParse(parts[0], out amount) && amount > 0
            && parts[0].Equals(amount.ToString(), StringComparison.Ordinal)
            && long.TryParse(parts[1], out durationTicks) && durationTicks > 0
            && parts[1].Equals(durationTicks.ToString(), StringComparison.Ordinal);
    }

    private static int CapacityBonus(WorldState world, Npc carrier)
    {
        var state = world.CarrierOf(carrier.Id);
        if (state is not { IsManifested: true }) return 0;

        int bonus = 0;
        foreach (var descriptor in world.Extraordinary.Descriptors)
        {
            if (!state.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal)) continue;
            if (!ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, carrier))
                continue;
            foreach (var effect in descriptor.Effects)
            {
                if (TryParseCapacityBonus(effect, out int amount))
                    bonus += amount;
            }
        }

        return bonus;
    }

    private static int ActiveCurse(WorldState world, Npc carrier, long currentTick)
    {
        var state = world.CarrierOf(carrier.Id);
        if (state is null || state.LuckCurseUntilTick <= currentTick) return 0;
        return state.LuckCurseAmount;
    }
}
