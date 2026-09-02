using System.Globalization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Ponto único de aquisição e manifestação extraordinária. Regras são dados de cenário;
/// condições leem apenas relógio e estado canônico do portador.
/// </summary>
public sealed class ExtraordinaryStateSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryState";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var construct in world.ExtraordinaryConstructs
                     .Where(item => item.ExpiresAtTick <= ctx.CurrentTick)
                     .OrderBy(item => item.Id).ToList())
        {
            world.RemoveExtraordinaryConstruct(construct.Id);
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryConstructRemoved,
                $"{construct.CreatorId.Value}|{construct.SourceInvocationId}|{construct.Id}|expired", sourceSystem: SystemName);
        }

        ControlMechanic.ApplyPossessionResistance(world, ctx);

        foreach (var carrier in world.ExtraordinaryCarriers.OrderBy(item => item.CarrierId.Value).ToList())
        {
            if (world.FindNpc(carrier.CarrierId) is not { IsAlive: true } npc) continue;
            var resolved = Resolve(world, npc, carrier.PowerIds);
            if (carrier.IsManifested && !resolved.IsManifested)
            {
                if (carrier.PreAlterationTraits is { Count: > 0 } traits)
                    MindMechanic.RevertPreAlterationTraits(world, ctx, npc, traits);
                ControlMechanic.RevertIfCeased(world, ctx, npc, carrier);
                AppearanceMechanic.RevertIfCeased(world, ctx, npc, carrier);
                resolved = Resolve(world, npc, carrier.PowerIds) with { PreAlterationTraits = null };
            }
            if (resolved.IsManifested != carrier.IsManifested)
            {
                ctx.LogEvent(
                    resolved.IsManifested ? WorldEventKind.ExtraordinaryManifested : WorldEventKind.ExtraordinaryDormant,
                    $"{carrier.CarrierId.Value}|{string.Join(',', resolved.PowerIds)}|condition", sourceSystem: SystemName);
                if (resolved.IsManifested)
                    LogCulturalResponses(world, ctx, npc, resolved.PowerIds);
            }
            world.UpsertExtraordinaryCarrier(resolved);
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.Payload)) return;
        var parts = evt.Payload.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || parts[0] != "acquire" || !long.TryParse(parts[1], out long npcValue))
            return;

        var npcId = new NpcId(npcValue);
        string powerId = parts[2];
        string trigger = parts[3];
        var npc = world.FindNpc(npcId);
        var descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
            candidate => string.Equals(candidate.Id, powerId, StringComparison.Ordinal));
        var acquisitionRule = descriptor?.AcquisitionRules.FirstOrDefault(
            rule => MatchesTrigger(rule, trigger));
        if (npc is null || !npc.IsAlive || descriptor is null || acquisitionRule is null
            || !PassesAcquisitionRate(acquisitionRule, npc, powerId, evt.Id, ctx))
        {
            ctx.LogEvent(WorldEventKind.ExtraordinaryAcquisitionFailed, $"{npcValue}|{powerId}|{trigger}", sourceSystem: SystemName);
            return;
        }

        var existing = world.ExtraordinaryCarriers.FirstOrDefault(carrier => carrier.CarrierId == npcId);
        if (existing?.PowerIds.Contains(powerId, StringComparer.Ordinal) == true) return;

        var powerIds = (existing?.PowerIds ?? []).Append(powerId)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var resolved = Resolve(world, npc, powerIds);
        world.UpsertExtraordinaryCarrier(resolved);
        long acquiredId = ctx.LogEvent(
            WorldEventKind.ExtraordinaryAcquired, $"{npcValue}|{powerId}|event:{trigger}",
            sourceSystem: SystemName);
        if (resolved.IsManifested && existing?.IsManifested != true)
        {
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryManifested, $"{npcValue}|{powerId}|condition",
                sourceSystem: SystemName, causeEventId: acquiredId);
            LogCulturalResponses(world, ctx, npc, resolved.PowerIds);
        }
    }

    public static Result<ExtraordinaryCarrierState> GrantAuthored(
        WorldState world, TickContext ctx, NpcId npcId, string powerId)
    {
        if (!world.Extraordinary.Enabled)
            return Result<ExtraordinaryCarrierState>.Fail("Extraordinary.Enabled: false");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive)
            return Result<ExtraordinaryCarrierState>.Fail("NpcId: NPC ausente ou morto");
        if (!world.Extraordinary.Descriptors.Any(item => string.Equals(item.Id, powerId, StringComparison.Ordinal)))
            return Result<ExtraordinaryCarrierState>.Fail("PowerId: descritor ausente");

        var existing = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npcId);
        var ids = (existing?.PowerIds ?? []).Append(powerId)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var resolved = Resolve(world, npc, ids);
        world.UpsertExtraordinaryCarrier(resolved);
        ctx.LogEvent(WorldEventKind.ExtraordinaryAcquired, $"{npcId.Value}|{powerId}|authoring:web", sourceSystem: SystemName);
        return Result<ExtraordinaryCarrierState>.Ok(resolved);
    }

    public static Result<ExtraordinaryCarrierState?> RevokeAuthored(
        WorldState world, TickContext ctx, NpcId npcId, string powerId)
    {
        var npc = world.FindNpc(npcId);
        var existing = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npcId);
        if (npc is null || existing is null || !existing.PowerIds.Contains(powerId, StringComparer.Ordinal))
            return Result<ExtraordinaryCarrierState?>.Fail("PowerId: poder não pertence ao NPC");
        var ids = existing.PowerIds.Where(id => !string.Equals(id, powerId, StringComparison.Ordinal)).ToList();
        ExtraordinaryCarrierState? resolved = ids.Count == 0 ? null : Resolve(world, npc, ids);
        if (resolved is null) world.RemoveExtraordinaryCarrier(npcId);
        else world.UpsertExtraordinaryCarrier(resolved);
        ctx.LogEvent(WorldEventKind.ExtraordinaryRevoked, $"{npcId.Value}|{powerId}|authoring:web", sourceSystem: SystemName);
        return Result<ExtraordinaryCarrierState?>.Ok(resolved);
    }

    internal static ExtraordinaryCarrierState Resolve(WorldState world, Npc npc, IReadOnlyList<string> powerIds)
    {
        var active = world.Extraordinary.Descriptors
            .Where(descriptor => powerIds.Contains(descriptor.Id, StringComparer.Ordinal)
                && ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, npc))
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToList();
        var appearance = active.Select(descriptor => descriptor.Appearance).FirstOrDefault(value => value is not null);
        var need = active.Select(descriptor => descriptor.NeedSubstitution).FirstOrDefault(value => value is not null);
        double senescence = active.Count == 0 ? 1 : active.Min(descriptor => descriptor.SenescenceRateMultiplier);
        var existing = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        return new ExtraordinaryCarrierState(
            npc.Id,
            powerIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            active.Count > 0,
            active.Count > 0 ? "manifested" : "dormant",
            appearance is null
                ? new ExtraordinaryAppearanceState(1, "", "")
                : new ExtraordinaryAppearanceState(
                    appearance.ScaleMultiplier, appearance.SkinTint, appearance.MovementTrail),
            need,
            senescence,
            existing?.PreAlterationTraits,
            existing?.ForgottenFactIds,
            existing?.BondPartnerId,
            existing?.LuckCurseAmount ?? 0,
            existing?.LuckCurseUntilTick ?? 0,
            existing?.GravityTargetMultiplier ?? 1,
            existing?.ImplantedFactIds,
            existing?.DimensionalPocket,
            DimensionMechanic.PortalsStillActive(active) ? existing?.DimensionalPortals : null,
            existing?.PendingReincarnation,
            existing?.PossessedBy,
            existing?.BodySwapPartner,
            existing?.ImpersonatingId,
            existing?.UseCount ?? 0,
            existing?.CurrentStageIndex ?? 0);
    }

    private static bool MatchesTrigger(string rule, string trigger) =>
        string.Equals(rule, trigger, StringComparison.Ordinal)
        || string.Equals(rule, $"event:{trigger}", StringComparison.Ordinal)
        || TryParseRateRule(rule, out _, out string parsedTrigger)
            && string.Equals(parsedTrigger, trigger, StringComparison.Ordinal);

    private static bool PassesAcquisitionRate(
        string rule, Npc npc, string powerId, long eventId, TickContext ctx)
    {
        if (!TryParseRateRule(rule, out double baseRate, out _)) return true;
        double chance = Math.Clamp(baseRate * npc.RateGene.Value, 0, 1);
        return ctx.Rng($"extraordinary-acquisition-{npc.Id.Value}-{powerId}-{eventId}").NextDouble() < chance;
    }

    private static bool TryParseRateRule(string rule, out double rate, out string trigger)
    {
        rate = 0;
        trigger = "";
        var parts = rule.Split(':', 4, StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || parts[0] != "rate" || parts[2] != "event"
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out rate)
            || rate is < 0 or > 1 || string.IsNullOrWhiteSpace(parts[3]))
            return false;
        trigger = parts[3];
        return true;
    }

    private static void LogCulturalResponses(
        WorldState world, TickContext ctx, Npc carrier, IReadOnlyList<string> activePowerIds)
    {
        foreach (var response in ExtraordinaryCultureInterpreter.Responses(world, carrier, activePowerIds))
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryCulturalReaction,
                $"{carrier.Id.Value}|{response.CultureId}|{response.Manifestation}|{response.Response}", sourceSystem: SystemName);
    }
}
