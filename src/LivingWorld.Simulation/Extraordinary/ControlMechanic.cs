using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Possessão contínua e troca de corpo. Decisões do possuído delegam ao portador/regras
/// declaradas; o log causal atribui ações ao possuído. Identidade mutada via
/// <see cref="WorldEventKind.IdentityChanged"/>.
/// AD-071 (<c>docs/decisions-log.md</c>): resistência à possessão modula por
/// <see cref="Npc.Vitality"/> — atributo genético já causal (mortalidade/concepção), sem
/// campo novo.
/// </summary>
public sealed class ControlMechanic : ExtraordinaryMechanic
{
    /// <summary>Atributo de hospedeiro usado pela resistência à possessão (AD-071).</summary>
    public const string PossessionResistanceAttribute = nameof(Npc.Vitality);

    public const string PossessToken = "control.possess";
    public const string PossessPrefix = "control.possess:";
    public const string BodySwapToken = "control.body-swap";

    public override string Prefix => "control.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration == BodySwapToken)
            return PrepareBodySwap(ctx, declaration);
        if (declaration == PossessToken || declaration.StartsWith(PossessPrefix, StringComparison.Ordinal))
            return PreparePossess(ctx, declaration);
        return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
    }

    internal static bool IsPossessed(WorldState world, Npc npc)
    {
        if (!world.Extraordinary.Enabled) return false;
        var possessed = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (possessed?.PossessedBy is not { } ownerId) return false;
        var owner = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == ownerId);
        return owner is { IsManifested: true };
    }

    internal static bool TryDelegatedAction(
        WorldState world, Npc npc, bool justCompleted, out ActionType action)
    {
        action = default;
        if (!IsPossessed(world, npc)) return false;
        var possessed = world.ExtraordinaryCarriers.First(item => item.CarrierId == npc.Id);
        var ownerId = possessed.PossessedBy!.Value;
        var ownerState = world.ExtraordinaryCarriers.First(item => item.CarrierId == ownerId);
        if (!TrySequence(world, ownerState, out var sequence))
            return false;
        if (sequence.Count == 0)
        {
            action = world.FindNpc(ownerId)?.CurrentAction ?? ActionType.Idle;
            return true;
        }

        int index = 0;
        if (npc.CurrentAction is { } current)
        {
            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] == current)
                {
                    index = i;
                    break;
                }
            }
        }

        if (justCompleted)
            index = (index + 1) % sequence.Count;
        action = sequence[index];
        return true;
    }

    internal static void RevertIfCeased(
        WorldState world, TickContext ctx, Npc npc, ExtraordinaryCarrierState previous)
    {
        foreach (var other in world.ExtraordinaryCarriers.OrderBy(item => item.CarrierId.Value).ToList())
        {
            if (other.PossessedBy == npc.Id)
                world.UpsertExtraordinaryCarrier(other with { PossessedBy = null });
        }

        if (previous.BodySwapPartner is not { } partnerId) return;
        if (world.FindNpc(partnerId) is not { } partner) return;

        var mine = npc.Personality;
        npc.RewritePersonality(partner.Personality);
        partner.RewritePersonality(mine);
        ctx.LogEvent(WorldEventKind.IdentityChanged, $"{npc.Id.Value}|{partnerId.Value}|body-swap-revert", sourceSystem: "ControlMechanic");
        ClearSwap(world, npc.Id);
        ClearSwap(world, partnerId);
    }

    private static Result<PreparedMutation?> PreparePossess(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParsePossess(declaration, out _))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        if (ctx.Target.Id == ctx.Carrier.Id)
            return Result<PreparedMutation?>.Fail("Effects: control.possess exige alvo distinto");

        var world = ctx.World;
        var carrier = ctx.Carrier;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var state = CarrierOf(world, target);
            world.UpsertExtraordinaryCarrier(state with { PossessedBy = carrier.Id });
        }));
    }

    private static Result<PreparedMutation?> PrepareBodySwap(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (ctx.Target.Id == ctx.Carrier.Id)
            return Result<PreparedMutation?>.Fail("Effects: control.body-swap exige alvo distinto");

        var world = ctx.World;
        var carrier = ctx.Carrier;
        var target = ctx.Target;
        var tick = ctx.Tick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var carrierState = CarrierOf(world, carrier);
            if (carrierState.BodySwapPartner is not null) return;

            var mine = carrier.Personality;
            carrier.RewritePersonality(target.Personality);
            target.RewritePersonality(mine);
            world.UpsertExtraordinaryCarrier(carrierState with
            {
                BodySwapPartner = target.Id,
                ImpersonatingId = target.Id,
            });
            world.UpsertExtraordinaryCarrier(CarrierOf(world, target) with
            {
                BodySwapPartner = carrier.Id,
                ImpersonatingId = carrier.Id,
            });
            tick.LogEvent(WorldEventKind.IdentityChanged, $"{carrier.Id.Value}|{target.Id.Value}|body-swap", sourceSystem: "ControlMechanic");
        }));
    }

    internal static bool TryParsePossess(string declaration, out IReadOnlyList<ActionType> sequence)
    {
        sequence = [];
        if (declaration == PossessToken) return true;
        if (!declaration.StartsWith(PossessPrefix, StringComparison.Ordinal)) return false;
        var parts = declaration[PossessPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
        var list = new List<ActionType>(parts.Length);
        foreach (var part in parts)
        {
            if (!Enum.TryParse(part, ignoreCase: false, out ActionType parsed) || !Enum.IsDefined(parsed))
                return false;
            list.Add(parsed);
        }

        sequence = list;
        return list.Count > 0;
    }

    private static bool TrySequence(
        WorldState world, ExtraordinaryCarrierState owner, out IReadOnlyList<ActionType> sequence)
    {
        sequence = [];
        foreach (var powerId in owner.PowerIds)
        {
            var descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
                item => string.Equals(item.Id, powerId, StringComparison.Ordinal));
            if (descriptor is null) continue;
            foreach (var effect in descriptor.Effects)
            {
                if (TryParsePossess(effect, out sequence))
                    return true;
            }
        }

        return false;
    }

    private static void ClearSwap(WorldState world, NpcId id)
    {
        var state = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == id);
        if (state is null) return;
        world.UpsertExtraordinaryCarrier(state with { BodySwapPartner = null, ImpersonatingId = null });
    }

    internal static ExtraordinaryCarrierState CarrierOf(WorldState world, Npc npc) =>
        world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id)
        ?? new ExtraordinaryCarrierState(
            npc.Id, [], false, "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
