using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Vínculo persistente entre duas partes: <c>bond.share:&lt;atributo&gt;[:&lt;proporção&gt;]</c>
/// e <c>bond.oath:&lt;consequência&gt;</c>, reavaliados no ciclo passivo.
/// </summary>
public sealed class BondMechanic : ExtraordinaryMechanic
{
    public const string SharePrefix = "bond.share:";
    public const string OathPrefix = "bond.oath:";

    public override string Prefix => "bond.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        NpcStatAccessors.All
            .Select(stat => new ExtraordinaryPrimitive(
                SharePrefix + stat.Key, $"{SharePrefix}{stat.Key}[:<proporção 1..100>]", false, "carrier"))
            .Append(new ExtraordinaryPrimitive(OathPrefix, "bond.oath:<token de efeito>", false, "carrier"))
            .ToList();

    internal static bool HasOathEffect(PowerDescriptor descriptor)
    {
        foreach (var effect in descriptor.Effects)
        {
            if (effect.StartsWith(OathPrefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration.StartsWith(SharePrefix, StringComparison.Ordinal))
            return PrepareShare(ctx, declaration);
        if (declaration.StartsWith(OathPrefix, StringComparison.Ordinal))
            return PrepareOath(ctx, declaration);
        return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
    }

    private static Result<PreparedMutation?> PrepareShare(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParseShare(declaration, out string attribute, out int proportion))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        _ = NpcStatAccessors.TryGet(attribute, out var stat);

        var world = ctx.World;
        var carrier = ctx.Carrier;
        var tick = ctx.Tick.CurrentTick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var state = CarrierState(world, carrier.Id);
            if (!TryLivingPartner(world, state, out var partner) || !carrier.IsAlive)
            {
                UndoBond(world, state);
                return;
            }

            long carrierValue = stat.Read(carrier, tick);
            long partnerValue = stat.Read(partner, tick);
            long gap = Math.Abs(carrierValue - partnerValue);
            int reflected = (int)(gap * proportion / 100);
            if (reflected == 0) return;
            if (carrierValue < partnerValue)
                stat.Write(partner, stat.Clamp(partnerValue - reflected), tick);
            else
                stat.Write(carrier, stat.Clamp(carrierValue - reflected), tick);
        }));
    }

    private static Result<PreparedMutation?> PrepareOath(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        string consequence = declaration[OathPrefix.Length..];
        if (consequence.Length == 0)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var descriptor = ctx.World.Extraordinary.Descriptors.FirstOrDefault(
            item => string.Equals(item.Id, ctx.Invocation.PowerId, StringComparison.Ordinal));
        if (descriptor is null)
            return Result<PreparedMutation?>.Fail("Extraordinary.Descriptors: poder ausente");

        var inner = ExtraordinaryMechanicRegistry.Default.Resolve(consequence);
        if (inner is null)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{consequence}'");

        var world = ctx.World;
        var carrier = ctx.Carrier;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            var state = CarrierState(world, carrier.Id);
            if (!TryLivingPartner(world, state, out var partner) || !carrier.IsAlive)
            {
                UndoBond(world, state);
                return;
            }

            string? condition = descriptor.ManifestationCondition;
            ApplyOathIfViolated(ctx, inner, consequence, carrier, condition, resolution);
            ApplyOathIfViolated(ctx, inner, consequence, partner, condition, resolution);
        }));
    }

    private static void ApplyOathIfViolated(
        ExtraordinaryMechanicContext ctx,
        IExtraordinaryMechanic mechanic,
        string consequence,
        Npc party,
        string? condition,
        ResolutionResult resolution)
    {
        if (ExtraordinaryManifestationCondition.IsMet(condition, ctx.World, party))
            return;

        var nested = ctx with { Carrier = party, Target = party, Kind = ExtraordinaryMechanicKind.Effect };
        var prepared = mechanic.PrepareEffect(nested, consequence);
        if (prepared is { IsSuccess: true, Value: { } mutation })
            mutation.Apply(resolution);
    }

    internal static bool TryParseShare(string declaration, out string attribute, out int proportion)
    {
        attribute = "";
        proportion = 100;
        if (!declaration.StartsWith(SharePrefix, StringComparison.Ordinal))
            return false;

        string spec = declaration[SharePrefix.Length..];
        if (spec.Length == 0) return false;
        int separator = spec.LastIndexOf(':');
        if (separator > 0
            && int.TryParse(spec[(separator + 1)..], out int parsed)
            && parsed is > 0 and <= 100
            && spec[(separator + 1)..].Equals(parsed.ToString(), StringComparison.Ordinal))
        {
            attribute = spec[..separator];
            proportion = parsed;
        }
        else
        {
            attribute = spec;
        }

        return NpcStatAccessors.TryGet(attribute, out _);
    }

    private static bool TryLivingPartner(
        WorldState world, ExtraordinaryCarrierState? state, out Npc partner)
    {
        partner = null!;
        if (state?.BondPartnerId is not { } partnerId)
            return false;
        if (world.FindNpc(partnerId) is not { IsAlive: true } found)
            return false;
        partner = found;
        return true;
    }

    private static ExtraordinaryCarrierState? CarrierState(WorldState world, NpcId id) =>
        world.CarrierOf(id);

    private static void UndoBond(WorldState world, ExtraordinaryCarrierState? state)
    {
        if (state is null) return;
        var partnerId = state.BondPartnerId;
        world.UpsertExtraordinaryCarrier(state with { BondPartnerId = null });
        if (partnerId is not { } id) return;
        var partnerState = CarrierState(world, id);
        if (partnerState is not null)
            world.UpsertExtraordinaryCarrier(partnerState with { BondPartnerId = null });
    }
}
