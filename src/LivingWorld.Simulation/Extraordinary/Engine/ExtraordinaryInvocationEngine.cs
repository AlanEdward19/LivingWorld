using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Engine;

public enum ExtraordinaryInvocationOrigin
{
    Triggered,
    Authored,
}

/// <summary>Pedido já identificado pelo autor causal; resolução incerta pertence ao motor.</summary>
public sealed record ExtraordinaryInvocation(
    long InvocationId,
    NpcId CarrierId,
    string PowerId,
    NpcId TargetId,
    ResolutionResult? Resolution = null,
    CellCoord? TargetCell = null,
    ExtraordinaryInvocationOrigin Origin = ExtraordinaryInvocationOrigin.Authored);

public sealed record ExtraordinaryInvocationResult(
    long InvocationId,
    ResolutionResult Resolution,
    int CostsPaid,
    int EffectsApplied);

/// <summary>
/// Aplica descritores somente por adaptadores de sistema conhecidos. Valida toda a operação antes
/// de debitar, cobra custos antes da resolução e nunca interpreta nomes de arquétipo.
/// </summary>
public static class ExtraordinaryInvocationEngine
{
    public static Result<ExtraordinaryInvocationResult> InvokeAuthored(
        WorldState world, TickContext ctx, NpcId carrierId, string powerId, NpcId targetId,
        CellCoord? targetCell = null, ResolutionResult? requestedResolution = null)
    {
        // Compatibilidade de contrato: resultados antigos ainda desserializam, mas não têm
        // autoridade. ResolutionCheck sempre é resolvido pelo motor no stream do portador.
        _ = requestedResolution;
        var result = Invoke(
            world, ctx,
            new ExtraordinaryInvocation(
                world.NextEventId, carrierId, powerId, targetId, null, targetCell,
                ExtraordinaryInvocationOrigin.Authored));
        // Rejeição de validação é zero-state. Uma resolução declarada como falha já pagou seus
        // custos e, portanto, é uma tentativa causal real que precisa reservar o id.
        if (result.IsSuccess || result.Error?.StartsWith("resolution:", StringComparison.Ordinal) == true)
            world.NextEventIdAndAdvance();
        return result;
    }

    public static Result<ExtraordinaryInvocationResult> Invoke(
        WorldState world, TickContext ctx, ExtraordinaryInvocation invocation,
        IExtraordinaryMechanicRegistry? registry = null,
        long? causeEventId = null)
    {
        const string source = "ExtraordinaryInvocationEngine";
        string prefix = Prefix(invocation);
        var attemptId = ctx.LogEvent(
            WorldEventKind.ExtraordinaryUseAttempted, $"{prefix}attempt", source, causeEventId);

        var prepared = Prepare(world, ctx, invocation, registry ?? ExtraordinaryMechanicRegistry.Default);
        if (!prepared.IsSuccess)
            return Fail(ctx, prefix, prepared.Error!, attemptId);

        var plan = prepared.Value!;
        long priorId = attemptId;
        foreach (var cost in plan.Costs)
        {
            cost.Apply(ResolutionResult.Success);
            priorId = ctx.LogEvent(
                WorldEventKind.ExtraordinaryCostPaid, $"{prefix}{cost.Token}", source, priorId);
        }

        if (plan.Resolution is ResolutionResult.Failure or ResolutionResult.CriticalFailure)
        {
            ApplyFailureModes(ctx, prefix, plan.FailureModes, attemptId);
            return Fail(ctx, prefix, $"resolution:{plan.Resolution}", attemptId);
        }

        foreach (var effect in plan.Effects)
        {
            effect.Apply(plan.Resolution);
            priorId = ctx.LogEvent(
                WorldEventKind.ExtraordinaryEffectApplied, $"{prefix}{effect.Token}", source, priorId);
        }
        PowerUseCounter.RecordSuccessfulUse(world, invocation.CarrierId);
        if (plan.Resolution == ResolutionResult.PartialSuccess)
            ApplyFailureModes(ctx, prefix, plan.FailureModes, attemptId);

        return Result<ExtraordinaryInvocationResult>.Ok(new ExtraordinaryInvocationResult(
            invocation.InvocationId, plan.Resolution, plan.Costs.Count, plan.Effects.Count));
    }

    private static Result<InvocationPlan> Prepare(
        WorldState world, TickContext ctx, ExtraordinaryInvocation invocation,
        IExtraordinaryMechanicRegistry registry)
    {
        if (!world.Extraordinary.Enabled)
            return Result<InvocationPlan>.Fail("Extraordinary.Enabled: false");

        var carrierState = world.ExtraordinaryCarriers.FirstOrDefault(c => c.CarrierId == invocation.CarrierId);
        if (carrierState is null || !carrierState.PowerIds.Contains(invocation.PowerId, StringComparer.Ordinal))
            return Result<InvocationPlan>.Fail("Carrier.PowerIds: poder não adquirido");

        var descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
            power => string.Equals(power.Id, invocation.PowerId, StringComparison.Ordinal));
        if (descriptor is null)
            return Result<InvocationPlan>.Fail("Extraordinary.Descriptors: poder ausente");
        if (descriptor.ManifestationCondition is not null && !carrierState.IsManifested)
            return Result<InvocationPlan>.Fail("Carrier.ManifestationState: poder não manifestado");
        if (!IsAvailable(descriptor.Mode, invocation.Origin, carrierState.IsManifested))
            return Result<InvocationPlan>.Fail(
                $"Mode: '{descriptor.Mode}' indisponível para origem {invocation.Origin}");

        var carrier = world.FindNpc(invocation.CarrierId);
        if (carrier is null || !carrier.IsAlive)
            return Result<InvocationPlan>.Fail("CarrierId: NPC ausente ou morto");

        var effectDeclarations = ExtraordinaryPowerStageSystem.EffectiveEffects(
            descriptor, carrier.AgeYears(world.CurrentDate), carrierState.UseCount);
        var effectTargets = ResolveEffectTargets(world, invocation, descriptor, carrier);
        if (!effectTargets.IsSuccess) return Result<InvocationPlan>.Fail(effectTargets.Error!);

        var primaryTarget = world.FindNpc(invocation.TargetId) ?? carrier;
        var mechanicCtx = new ExtraordinaryMechanicContext(
            world, ctx, invocation, carrier, primaryTarget, ExtraordinaryMechanicKind.Effect);
        var effects = PrepareEffectsForTargets(registry, mechanicCtx, effectDeclarations, effectTargets.Value!);
        if (!effects.IsSuccess) return Result<InvocationPlan>.Fail(effects.Error!);
        var costs = PrepareCosts(registry, mechanicCtx with { Kind = ExtraordinaryMechanicKind.Cost }, descriptor.Costs);
        if (!costs.IsSuccess) return Result<InvocationPlan>.Fail(costs.Error!);
        var failureModes = PrepareFailureModes(descriptor.FailureModes, carrier);
        if (!failureModes.IsSuccess) return Result<InvocationPlan>.Fail(failureModes.Error!);

        var resolution = ResolveDeclaredOutcome(
            world, descriptor, invocation, carrier, primaryTarget, ctx);
        if (!resolution.IsSuccess) return Result<InvocationPlan>.Fail(resolution.Error!);
        return Result<InvocationPlan>.Ok(new InvocationPlan(
            costs.Value!, effects.Value!, failureModes.Value!, resolution.Value));
    }

    private static Result<IReadOnlyList<Npc>> ResolveEffectTargets(
        WorldState world, ExtraordinaryInvocation invocation, PowerDescriptor descriptor, Npc carrier)
    {
        if (AreaTargetResolver.HasSelector(descriptor.Effects))
        {
            var ids = AreaTargetResolver.Resolve(world, carrier, descriptor.Effects);
            if (!ids.IsSuccess) return Result<IReadOnlyList<Npc>>.Fail(ids.Error!);
            var npcs = ids.Value!
                .Select(world.FindNpc)
                .Where(npc => npc is { IsAlive: true })
                .Cast<Npc>()
                .ToList();
            return Result<IReadOnlyList<Npc>>.Ok(npcs);
        }

        var target = world.FindNpc(invocation.TargetId);
        if (target is null || (!target.IsAlive && !target.IsGhost))
            return Result<IReadOnlyList<Npc>>.Fail("TargetId: NPC ausente ou morto");
        return Result<IReadOnlyList<Npc>>.Ok([target]);
    }

    private static Result<IReadOnlyList<PreparedMutation>> PrepareEffectsForTargets(
        IExtraordinaryMechanicRegistry registry,
        ExtraordinaryMechanicContext ctx,
        IReadOnlyList<string> declarations,
        IReadOnlyList<Npc> targets)
    {
        if (targets.Count == 0)
            return Result<IReadOnlyList<PreparedMutation>>.Ok([]);

        var result = new List<PreparedMutation>();
        foreach (var target in targets)
        {
            var perTarget = PrepareEffects(registry, ctx with { Target = target }, declarations);
            if (!perTarget.IsSuccess)
                return perTarget;
            result.AddRange(perTarget.Value!);
        }
        return Result<IReadOnlyList<PreparedMutation>>.Ok(result);
    }

    private static Result<IReadOnlyList<PreparedMutation>> PrepareEffects(
        IExtraordinaryMechanicRegistry registry,
        ExtraordinaryMechanicContext ctx,
        IReadOnlyList<string> declarations)
    {
        var result = new List<PreparedMutation>();
        foreach (var declaration in declarations)
        {
            var mechanic = registry.Resolve(declaration);
            if (mechanic is null)
            {
                var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: true);
                if (!parsed.IsSuccess)
                    return Result<IReadOnlyList<PreparedMutation>>.Fail(parsed.Error!);
                return Result<IReadOnlyList<PreparedMutation>>.Fail(
                    $"Effects: alvo não suportado '{parsed.Value.Key}'");
            }

            var prepared = PrepareMagnitudeMutation(
                ctx, registry, mechanic, declaration, ctx.Target, isCost: false);
            if (!prepared.IsSuccess)
                return Result<IReadOnlyList<PreparedMutation>>.Fail(prepared.Error!);
            if (prepared.Value is { } mutation)
                result.Add(mutation);
        }
        return Result<IReadOnlyList<PreparedMutation>>.Ok(result);
    }

    private static Result<PreparedMutation?> PrepareMagnitudeMutation(
        ExtraordinaryMechanicContext ctx,
        IExtraordinaryMechanicRegistry registry,
        IExtraordinaryMechanic mechanic,
        string declaration,
        Npc vulnerabilitySubject,
        bool isCost)
    {
        var scaled = ScaleTypedMagnitude(ctx, registry, declaration, vulnerabilitySubject, isCost);
        if (!scaled.IsSuccess)
            return Result<PreparedMutation?>.Fail(scaled.Error!);
        return mechanic.PrepareEffect(ctx, scaled.Value.Declaration);
    }

    private static Result<(string Declaration, string Key, int Amount)> ScaleTypedMagnitude(
        ExtraordinaryMechanicContext ctx,
        IExtraordinaryMechanicRegistry registry,
        string declaration,
        Npc vulnerabilitySubject,
        bool isCost)
    {
        string field = isCost ? "Costs" : "Effects";
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, field, allowSigned: !isCost);
        if (!parsed.IsSuccess)
        {
            if (isCost)
                return Result<(string, string, int)>.Fail(parsed.Error!);
            return Result<(string, string, int)>.Ok((declaration, declaration, 0));
        }

        if (!ExtraordinaryMechanicSupport.TrySplitTypedMagnitude(declaration, out string stripped, out string type))
            return Result<(string, string, int)>.Ok((declaration, parsed.Value.Key, parsed.Value.Amount));

        var strippedParsed = ExtraordinaryMechanicSupport.ParseAmount(stripped, field, allowSigned: !isCost);
        if (!strippedParsed.IsSuccess)
            return Result<(string, string, int)>.Ok((declaration, parsed.Value.Key, parsed.Value.Amount));

        bool accepted = isCost
            ? registry.Resolve(strippedParsed.Value.Key)?.CostAvailable(ctx, strippedParsed.Value.Key).IsSuccess == true
            : registry.Resolve(stripped)?.PrepareEffect(ctx, stripped).IsSuccess == true;
        if (!accepted)
            return Result<(string, string, int)>.Ok((declaration, parsed.Value.Key, parsed.Value.Amount));

        int factor = ResolveVulnerabilityFactor(ctx.World, vulnerabilitySubject, type);
        int amount = ExtraordinaryMechanicSupport.ApplyVulnerabilityFactor(strippedParsed.Value.Amount, factor);
        string scaledDeclaration = $"{strippedParsed.Value.Key}:{amount}";
        return Result<(string, string, int)>.Ok((scaledDeclaration, strippedParsed.Value.Key, amount));
    }

    private static int ResolveVulnerabilityFactor(WorldState world, Npc subject, string type)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == subject.Id);
        if (carrier is null)
            return 1;
        var tags = carrier.PowerIds
            .Select(id => world.Extraordinary.Descriptors.FirstOrDefault(
                power => string.Equals(power.Id, id, StringComparison.Ordinal)))
            .Where(power => power is not null)
            .SelectMany(power => power!.IntrinsicVulnerabilities);
        return ExtraordinaryMechanicSupport.VulnerabilityFactor(tags, type);
    }

    private static Result<IReadOnlyList<PreparedMutation>> PrepareCosts(
        IExtraordinaryMechanicRegistry registry,
        ExtraordinaryMechanicContext ctx,
        IReadOnlyList<string> declarations)
    {
        var parsedCosts = new List<(string Declaration, string Key, int Amount)>();
        foreach (var declaration in declarations)
        {
            var scaled = ScaleTypedMagnitude(ctx, registry, declaration, ctx.Carrier, isCost: true);
            if (!scaled.IsSuccess)
                return Result<IReadOnlyList<PreparedMutation>>.Fail(scaled.Error!);
            parsedCosts.Add(scaled.Value);
        }

        foreach (var group in parsedCosts.GroupBy(cost => cost.Key, StringComparer.Ordinal))
        {
            long required = group.Sum(cost => (long)cost.Amount);
            var mechanic = registry.Resolve(group.Key);
            if (mechanic is null)
                return Result<IReadOnlyList<PreparedMutation>>.Fail($"Costs: alvo não suportado '{group.Key}'");
            var available = mechanic.CostAvailable(ctx, group.Key);
            if (!available.IsSuccess)
                return Result<IReadOnlyList<PreparedMutation>>.Fail(available.Error!);
            if (required > available.Value)
                return Result<IReadOnlyList<PreparedMutation>>.Fail($"Costs[{group.Key}]: insuficiente");
        }

        var result = new List<PreparedMutation>();
        foreach (var (declaration, key, amount) in parsedCosts)
        {
            var mechanic = registry.Resolve(key)!;
            var prepared = mechanic.PrepareCost(ctx, declaration, amount);
            if (!prepared.IsSuccess)
                return Result<IReadOnlyList<PreparedMutation>>.Fail(prepared.Error!);
            result.Add(prepared.Value!);
        }
        return Result<IReadOnlyList<PreparedMutation>>.Ok(result);
    }

    private static Result<IReadOnlyList<PreparedFailureMode>> PrepareFailureModes(
        IReadOnlyList<string> declarations, Npc carrier)
    {
        var result = new List<PreparedFailureMode>();
        foreach (var declaration in declarations)
        {
            if (declaration.StartsWith("carrier.health:", StringComparison.Ordinal))
            {
                var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "FailureModes", allowSigned: false);
                if (!parsed.IsSuccess || parsed.Value.Key != "carrier.health")
                    return Result<IReadOnlyList<PreparedFailureMode>>.Fail(
                        parsed.Error ?? "FailureModes: carrier.health inválido");
                int amount = parsed.Value.Amount;
                result.Add(new PreparedFailureMode(
                    declaration, () => carrier.SetHealth(Math.Max(0, carrier.Health - amount))));
                continue;
            }
            result.Add(new PreparedFailureMode(declaration, () => { }));
        }
        return Result<IReadOnlyList<PreparedFailureMode>>.Ok(result);
    }

    private static Result<ResolutionResult> ResolveDeclaredOutcome(
        WorldState world, PowerDescriptor descriptor, ExtraordinaryInvocation invocation,
        Npc carrier, Npc target, TickContext ctx)
    {
        if (descriptor.Reliability == "Guaranteed")
            return Result<ResolutionResult>.Ok(ResolutionResult.Success);
        if (descriptor.Reliability == "ResolutionCheck" && invocation.Resolution is { } resolution)
            return Result<ResolutionResult>.Ok(resolution);
        if (descriptor.Reliability != "ResolutionCheck")
            return Result<ResolutionResult>.Fail($"Reliability: valor inválido '{descriptor.Reliability}'");

        int largestMagnitude = descriptor.Effects
            .Select(effect => ExtraordinaryMechanicSupport.ParseAmount(effect, "Effects", allowSigned: true))
            .Where(parsed => parsed.IsSuccess)
            .Select(parsed => Math.Abs(parsed.Value.Amount))
            .DefaultIfEmpty(1)
            .Max();
        int difficulty = 10 + (int)Math.Ceiling(largestMagnitude / 10d)
            + Math.Clamp((100 - target.Health) / 20, 0, 5);
        int capacity = LuckMechanic.AdjustCapacity(
            world, carrier, ctx.CurrentTick,
            (int)Math.Clamp(
                Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20));
        capacity = Math.Max(0, capacity + LuckMechanic.ManifestedCapacityBonus(world, target));
        string stream = $"extraordinary-resolution-{carrier.Id.Value}-{descriptor.Id}-{invocation.InvocationId}";
        return Result<ResolutionResult>.Ok(Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"), ctx.Rng(stream)));
    }

    /// <summary>Disponibilidade por Mode × origem × manifestação — compartilhado com
    /// <see cref="PowerOpportunityProvider"/> (COH-31).</summary>
    internal static bool IsAvailable(
        string mode, ExtraordinaryInvocationOrigin origin, bool isManifested) => mode switch
        {
            "Active" => origin == ExtraordinaryInvocationOrigin.Authored,
            "Passive" => origin == ExtraordinaryInvocationOrigin.Triggered,
            "Triggered" => origin == ExtraordinaryInvocationOrigin.Triggered,
            "Conditional" => origin == ExtraordinaryInvocationOrigin.Authored && isManifested,
            _ => false,
        };

    private static void ApplyFailureModes(
        TickContext ctx, string prefix, IReadOnlyList<PreparedFailureMode> failureModes, long causeEventId)
    {
        foreach (var failureMode in failureModes)
        {
            failureMode.Apply();
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryFailureApplied, $"{prefix}{failureMode.Token}",
                "ExtraordinaryInvocationEngine", causeEventId);
        }
    }

    private static Result<ExtraordinaryInvocationResult> Fail(
        TickContext ctx, string prefix, string error, long causeEventId)
    {
        ctx.LogEvent(
            WorldEventKind.ExtraordinaryUseFailed, $"{prefix}{error}",
            "ExtraordinaryInvocationEngine", causeEventId);
        return Result<ExtraordinaryInvocationResult>.Fail(error);
    }

    private static string Prefix(ExtraordinaryInvocation invocation) =>
        $"{invocation.CarrierId.Value}|{invocation.InvocationId}|{invocation.PowerId}|{invocation.TargetId.Value}|";

    private sealed record PreparedFailureMode(string Token, Action Apply);
    private sealed record InvocationPlan(
        IReadOnlyList<PreparedMutation> Costs,
        IReadOnlyList<PreparedMutation> Effects,
        IReadOnlyList<PreparedFailureMode> FailureModes,
        ResolutionResult Resolution);
}
