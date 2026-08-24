using LivingWorld.Domain;

namespace LivingWorld.Simulation;

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
        WorldState world, TickContext ctx, ExtraordinaryInvocation invocation)
    {
        string prefix = Prefix(invocation);
        ctx.LogEvent(WorldEventKind.ExtraordinaryUseAttempted, $"{prefix}attempt");

        var prepared = Prepare(world, ctx, invocation);
        if (!prepared.IsSuccess)
            return Fail(ctx, prefix, prepared.Error!);

        var plan = prepared.Value!;
        foreach (var cost in plan.Costs)
        {
            cost.Apply(ResolutionResult.Success);
            ctx.LogEvent(WorldEventKind.ExtraordinaryCostPaid, $"{prefix}{cost.Token}");
        }

        if (plan.Resolution is ResolutionResult.Failure or ResolutionResult.CriticalFailure)
        {
            ApplyFailureModes(ctx, prefix, plan.FailureModes);
            return Fail(ctx, prefix, $"resolution:{plan.Resolution}");
        }

        foreach (var effect in plan.Effects)
        {
            effect.Apply(plan.Resolution);
            ctx.LogEvent(WorldEventKind.ExtraordinaryEffectApplied, $"{prefix}{effect.Token}");
        }
        if (plan.Resolution == ResolutionResult.PartialSuccess)
            ApplyFailureModes(ctx, prefix, plan.FailureModes);

        return Result<ExtraordinaryInvocationResult>.Ok(new ExtraordinaryInvocationResult(
            invocation.InvocationId, plan.Resolution, plan.Costs.Count, plan.Effects.Count));
    }

    private static Result<InvocationPlan> Prepare(
        WorldState world, TickContext ctx, ExtraordinaryInvocation invocation)
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
        var target = world.FindNpc(invocation.TargetId);
        if (carrier is null || !carrier.IsAlive)
            return Result<InvocationPlan>.Fail("CarrierId: NPC ausente ou morto");
        if (target is null || !target.IsAlive)
            return Result<InvocationPlan>.Fail("TargetId: NPC ausente ou morto");

        var effects = PrepareEffects(world, invocation, descriptor.Effects, target, ctx);
        if (!effects.IsSuccess) return Result<InvocationPlan>.Fail(effects.Error!);
        var costs = PrepareCosts(descriptor.Costs, carrier, world, ctx.CurrentTick);
        if (!costs.IsSuccess) return Result<InvocationPlan>.Fail(costs.Error!);
        var failureModes = PrepareFailureModes(descriptor.FailureModes, carrier);
        if (!failureModes.IsSuccess) return Result<InvocationPlan>.Fail(failureModes.Error!);

        var resolution = ResolveDeclaredOutcome(descriptor, invocation, carrier, target, ctx);
        if (!resolution.IsSuccess) return Result<InvocationPlan>.Fail(resolution.Error!);
        return Result<InvocationPlan>.Ok(new InvocationPlan(
            costs.Value!, effects.Value!, failureModes.Value!, resolution.Value));
    }

    private static Result<IReadOnlyList<PreparedMutation>> PrepareEffects(
        WorldState world,
        ExtraordinaryInvocation invocation,
        IReadOnlyList<string> declarations,
        Npc target,
        TickContext ctx)
    {
        var result = new List<PreparedMutation>();
        foreach (var declaration in declarations)
        {
            if (declaration.StartsWith("movement.", StringComparison.Ordinal)) continue;
            if (declaration.StartsWith("construct.create:", StringComparison.Ordinal))
            {
                var construct = PrepareConstruct(world, invocation, declaration, target, ctx);
                if (!construct.IsSuccess)
                    return Result<IReadOnlyList<PreparedMutation>>.Fail(construct.Error!);
                result.Add(construct.Value!);
                continue;
            }
            var parsed = ParseAmount(declaration, "Effects", allowSigned: true);
            if (!parsed.IsSuccess) return Result<IReadOnlyList<PreparedMutation>>.Fail(parsed.Error!);
            var (key, amount) = parsed.Value;
            Action<int>? apply = key switch
            {
                "npc.health" => value => target.SetHealth(ClampNeed((long)target.Health + value)),
                "npc.hunger" => value => target.SetHunger(ClampNeed((long)target.HungerAt(ctx.CurrentTick) + value), ctx.CurrentTick),
                "npc.thirst" => value => target.SetThirst(ClampNeed((long)target.ThirstAt(ctx.CurrentTick) + value), ctx.CurrentTick),
                "npc.sleep" => value => target.SetSleep(ClampNeed((long)target.SleepAt(ctx.CurrentTick) + value), ctx.CurrentTick),
                "npc.social" => value => target.SetSocial(ClampNeed((long)target.SocialAt(ctx.CurrentTick) + value), ctx.CurrentTick),
                _ => null,
            };
            if (apply is null)
                return Result<IReadOnlyList<PreparedMutation>>.Fail($"Effects: alvo não suportado '{key}'");
            result.Add(new PreparedMutation(declaration, resolution =>
                apply(resolution == ResolutionResult.PartialSuccess ? HalfAwayFromZero(amount) : amount)));
        }
        return Result<IReadOnlyList<PreparedMutation>>.Ok(result);
    }

    private static Result<PreparedMutation> PrepareConstruct(
        WorldState world,
        ExtraordinaryInvocation invocation,
        string declaration,
        Npc target,
        TickContext ctx)
    {
        var parts = declaration.Split(':', StringSplitOptions.TrimEntries);
        var dimensions = parts.Length == 5 ? parts[1].Split('x', StringSplitOptions.TrimEntries) : [];
        if (dimensions.Length != 2
            || !int.TryParse(dimensions[0], out int width) || width is < 1 or > 8
            || !int.TryParse(dimensions[1], out int height) || height is < 1 or > 8
            || !int.TryParse(parts[2], out int durability) || durability <= 0
            || !long.TryParse(parts[3], out long durationHours) || durationHours <= 0
            || string.IsNullOrWhiteSpace(parts[4]))
            return Result<PreparedMutation>.Fail(
                "Effects: construct.create exige 'LxA:durabilidade:horas:aparência' válida");

        var carrier = world.FindNpc(invocation.CarrierId)!;
        int directionX = Math.Sign(target.CurrentLocation.X - carrier.CurrentLocation.X);
        int directionY = Math.Sign(target.CurrentLocation.Y - carrier.CurrentLocation.Y);
        if (directionX == 0 && directionY == 0) directionX = 1;
        var origin = invocation.TargetCell ?? new CellCoord(
            target.CurrentLocation.X + directionX,
            target.CurrentLocation.Y + directionY);
        var footprint = Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width)
                .Select(x => new CellCoord(origin.X + x, origin.Y + y)))
            .ToList();
        if (footprint.Any(cell => !world.Map.TryGetCell(cell, out _)))
            return Result<PreparedMutation>.Fail("Effects: footprint do construto fora do mapa");
        if (world.ExtraordinaryConstructs.SelectMany(item => item.Footprint).Any(footprint.Contains))
            return Result<PreparedMutation>.Fail("Effects: footprint do construto já ocupado");
        if (footprint.Any(cell => IsBuildingCell(world, cell)))
            return Result<PreparedMutation>.Fail("Effects: footprint do construto ocupado por prédio");
        if (world.Npcs.Any(npc => npc.IsAlive && footprint.Contains(npc.CurrentLocation)))
            return Result<PreparedMutation>.Fail("Effects: footprint do construto ocupado por NPC");

        return Result<PreparedMutation>.Ok(new PreparedMutation(declaration, _ =>
        {
            long id = world.NextExtraordinaryConstructIdAndAdvance();
            var construct = new ExtraordinaryConstruct(
                id, invocation.CarrierId, invocation.PowerId, invocation.InvocationId,
                origin, footprint, durability, durability,
                ctx.CurrentTick, checked(ctx.CurrentTick + durationHours), parts[4]);
            world.AddExtraordinaryConstruct(construct);
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryConstructCreated,
                $"{invocation.CarrierId.Value}|{invocation.InvocationId}|{invocation.PowerId}|{id}");
        }));
    }

    private static Result<IReadOnlyList<PreparedMutation>> PrepareCosts(
        IReadOnlyList<string> declarations, Npc carrier, WorldState world, long tick)
    {
        var parsedCosts = new List<(string Declaration, string Key, int Amount)>();
        foreach (var declaration in declarations)
        {
            var parsed = ParseAmount(declaration, "Costs", allowSigned: false);
            if (!parsed.IsSuccess) return Result<IReadOnlyList<PreparedMutation>>.Fail(parsed.Error!);
            parsedCosts.Add((declaration, parsed.Value.Key, parsed.Value.Amount));
        }

        var home = carrier.Household is { } householdId ? world.FindHousehold(householdId) : null;
        foreach (var group in parsedCosts.GroupBy(cost => cost.Key, StringComparer.Ordinal))
        {
            long required = group.Sum(cost => (long)cost.Amount);
            long available = group.Key switch
            {
                "carrier.health" => carrier.Health,
                "carrier.hunger" => carrier.HungerAt(tick),
                "carrier.thirst" => carrier.ThirstAt(tick),
                "carrier.sleep" => carrier.SleepAt(tick),
                "carrier.social" => carrier.SocialAt(tick),
                _ when TryResource(group.Key, out var resource) && home is not null => home.Stock.GetValueOrDefault(resource),
                _ => -1,
            };
            if (available < 0)
                return Result<IReadOnlyList<PreparedMutation>>.Fail($"Costs: alvo não suportado '{group.Key}'");
            if (required > available)
                return Result<IReadOnlyList<PreparedMutation>>.Fail($"Costs[{group.Key}]: insuficiente");
        }

        var result = new List<PreparedMutation>();
        foreach (var (declaration, key, amount) in parsedCosts)
        {
            Action apply = key switch
            {
                "carrier.health" => () => carrier.SetHealth(carrier.Health - amount),
                "carrier.hunger" => () => carrier.SetHunger(carrier.HungerAt(tick) - amount, tick),
                "carrier.thirst" => () => carrier.SetThirst(carrier.ThirstAt(tick) - amount, tick),
                "carrier.sleep" => () => carrier.SetSleep(carrier.SleepAt(tick) - amount, tick),
                "carrier.social" => () => carrier.SetSocial(carrier.SocialAt(tick) - amount, tick),
                _ => () => home!.Withdraw(ResourceOf(key), amount),
            };
            result.Add(new PreparedMutation(declaration, _ => apply()));
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
                var parsed = ParseAmount(declaration, "FailureModes", allowSigned: false);
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

    private static bool IsBuildingCell(WorldState world, CellCoord cell)
    {
        foreach (var building in world.Buildings)
        {
            var position = building.Position;
            if (position is null)
            {
                if (world.FindCity(building.City) is not { } city) continue;
                var (bounds, _) = SpatialBoundsResolver.ResolveCity(
                    city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height);
                position = BuildingPlacementResolver.Resolve(building, city, world, bounds)?.Position;
            }
            if (position is null) continue;
            if (BuildingFootprintGenerator.Generate(building).Any(part =>
                    new CellCoord(position.Value.X + part.Cell.X, position.Value.Y + part.Cell.Y) == cell))
                return true;
        }
        return false;
    }

    private static Result<(string Key, int Amount)> ParseAmount(
        string declaration, string field, bool allowSigned)
    {
        int separator = declaration.LastIndexOf(':');
        if (separator <= 0 || separator == declaration.Length - 1
            || !int.TryParse(declaration[(separator + 1)..], out int amount)
            || amount == 0 || (!allowSigned && amount < 0))
            return Result<(string, int)>.Fail($"{field}: use 'alvo:magnitude' com magnitude {(allowSigned ? "não zero" : "positiva")}");
        return Result<(string, int)>.Ok((declaration[..separator], amount));
    }

    private static Result<ResolutionResult> ResolveDeclaredOutcome(
        PowerDescriptor descriptor, ExtraordinaryInvocation invocation, Npc carrier, Npc target, TickContext ctx)
    {
        if (descriptor.Reliability == "Guaranteed")
            return Result<ResolutionResult>.Ok(ResolutionResult.Success);
        if (descriptor.Reliability == "ResolutionCheck" && invocation.Resolution is { } resolution)
            return Result<ResolutionResult>.Ok(resolution);
        if (descriptor.Reliability != "ResolutionCheck")
            return Result<ResolutionResult>.Fail($"Reliability: valor inválido '{descriptor.Reliability}'");

        int largestMagnitude = descriptor.Effects
            .Select(effect => ParseAmount(effect, "Effects", allowSigned: true))
            .Where(parsed => parsed.IsSuccess)
            .Select(parsed => Math.Abs(parsed.Value.Amount))
            .DefaultIfEmpty(1)
            .Max();
        int difficulty = 10 + (int)Math.Ceiling(largestMagnitude / 10d)
            + Math.Clamp((100 - target.Health) / 20, 0, 5);
        int capacity = (int)Math.Clamp(
            Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20);
        string stream = $"extraordinary-resolution-{carrier.Id.Value}-{descriptor.Id}-{invocation.InvocationId}";
        return Result<ResolutionResult>.Ok(Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"), ctx.Rng(stream)));
    }

    private static bool IsAvailable(
        string mode, ExtraordinaryInvocationOrigin origin, bool isManifested) => mode switch
        {
            "Active" => origin == ExtraordinaryInvocationOrigin.Authored,
            "Passive" => origin == ExtraordinaryInvocationOrigin.Triggered,
            "Triggered" => origin == ExtraordinaryInvocationOrigin.Triggered,
            "Conditional" => origin == ExtraordinaryInvocationOrigin.Authored && isManifested,
            _ => false,
        };

    private static void ApplyFailureModes(
        TickContext ctx, string prefix, IReadOnlyList<PreparedFailureMode> failureModes)
    {
        foreach (var failureMode in failureModes)
        {
            failureMode.Apply();
            ctx.LogEvent(WorldEventKind.ExtraordinaryFailureApplied, $"{prefix}{failureMode.Token}");
        }
    }

    private static bool TryResource(string key, out ResourceType resource)
    {
        const string prefix = "household.resource.";
        bool valid = key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key[prefix.Length..], out int id) && id >= 0;
        resource = new ResourceType(valid ? int.Parse(key[prefix.Length..]) : 0);
        return valid;
    }

    private static ResourceType ResourceOf(string key)
    {
        _ = TryResource(key, out var resource);
        return resource;
    }

    private static int ClampNeed(long value) => (int)Math.Clamp(value, 0, 100);

    private static int HalfAwayFromZero(int value) =>
        value > 0 ? (value + 1) / 2 : (value - 1) / 2;

    private static Result<ExtraordinaryInvocationResult> Fail(TickContext ctx, string prefix, string error)
    {
        ctx.LogEvent(WorldEventKind.ExtraordinaryUseFailed, $"{prefix}{error}");
        return Result<ExtraordinaryInvocationResult>.Fail(error);
    }

    private static string Prefix(ExtraordinaryInvocation invocation) =>
        $"{invocation.CarrierId.Value}|{invocation.InvocationId}|{invocation.PowerId}|{invocation.TargetId.Value}|";

    private sealed record PreparedMutation(string Token, Action<ResolutionResult> Apply);
    private sealed record PreparedFailureMode(string Token, Action Apply);
    private sealed record InvocationPlan(
        IReadOnlyList<PreparedMutation> Costs,
        IReadOnlyList<PreparedMutation> Effects,
        IReadOnlyList<PreparedFailureMode> FailureModes,
        ResolutionResult Resolution);
}
