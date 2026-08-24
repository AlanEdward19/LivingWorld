using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryInvocationEngineTests
{
    [Fact]
    public void Guaranteed_use_debits_cost_changes_only_declared_target_and_logs_causal_chain()
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"]);
        int carrierHealth = carrier.Health;
        int targetHunger = target.HungerAt(0);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(41, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((65, targetHunger, carrierHealth, 3L),
            (target.Health, target.HungerAt(0), carrier.Health, home.Stock[new ResourceType(9)]));
        Assert.Equal(
            [WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryCostPaid,
                WorldEventKind.ExtraordinaryEffectApplied],
            sink.Events.Select(evt => evt.Kind));
        Assert.All(sink.Events, evt => Assert.StartsWith("1|41|test-power|2|", evt.Payload));
    }

    [Fact]
    public void Invalid_or_unfunded_use_is_atomic_and_never_applies_the_effect()
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:6"]);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(42, carrier.Id, "test-power", target.Id));

        Assert.Equal((false, 50, 5L),
            (result.IsSuccess, target.Health, home.Stock[new ResourceType(9)]));
        Assert.Contains("insuficiente", result.Error, StringComparison.Ordinal);
        Assert.Equal(
            [WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryUseFailed],
            sink.Events.Select(evt => evt.Kind));
    }

    [Fact]
    public void Resolution_failure_still_pays_declared_cost_but_applies_no_effect()
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"], reliability: "ResolutionCheck");
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(
                43, carrier.Id, "test-power", target.Id, ResolutionResult.Failure));

        Assert.Equal((false, 50, 3L),
            (result.IsSuccess, target.Health, home.Stock[new ResourceType(9)]));
        Assert.Equal(
            [WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryCostPaid,
                WorldEventKind.ExtraordinaryFailureApplied, WorldEventKind.ExtraordinaryUseFailed],
            sink.Events.Select(evt => evt.Kind));
    }

    [Fact]
    public void Authored_resolution_is_deterministic_and_cannot_be_selected_by_the_caller()
    {
        var (first, firstCarrier, firstTarget, firstHome) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"], reliability: "ResolutionCheck");
        var (second, secondCarrier, secondTarget, secondHome) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"], reliability: "ResolutionCheck");

        var firstResult = ExtraordinaryInvocationEngine.InvokeAuthored(
            first, new TickContext(first, first.Rng, first.Scheduler),
            firstCarrier.Id, "test-power", firstTarget.Id, requestedResolution: ResolutionResult.CriticalFailure);
        var secondResult = ExtraordinaryInvocationEngine.InvokeAuthored(
            second, new TickContext(second, second.Rng, second.Scheduler),
            secondCarrier.Id, "test-power", secondTarget.Id, requestedResolution: ResolutionResult.CriticalSuccess);

        Assert.Equal(
            (firstResult.IsSuccess, firstResult.Value?.Resolution, firstResult.Error,
                firstTarget.Health, firstHome.Stock[new ResourceType(9)]),
            (secondResult.IsSuccess, secondResult.Value?.Resolution, secondResult.Error,
                secondTarget.Health, secondHome.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Guaranteed_use_does_not_consume_the_resolution_stream()
    {
        var (treated, carrier, target, _) = WorldWithPower(["npc.health:1"], []);
        var (control, _, _, _) = WorldWithPower(["npc.health:1"], []);
        const string stream = "extraordinary-resolution-1-test-power-99";

        ExtraordinaryInvocationEngine.Invoke(
            treated, new TickContext(treated, treated.Rng, treated.Scheduler),
            new ExtraordinaryInvocation(99, carrier.Id, "test-power", target.Id));

        Assert.Equal(
            new TickContext(control, control.Rng, control.Scheduler).Rng(stream).NextDouble(),
            new TickContext(treated, treated.Rng, treated.Scheduler).Rng(stream).NextDouble());
    }

    [Fact]
    public void Partial_success_applies_half_the_effect_but_the_full_cost()
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"], reliability: "ResolutionCheck");

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(46, carrier.Id, "test-power", target.Id, ResolutionResult.PartialSuccess));

        Assert.Equal((true, ResolutionResult.PartialSuccess, 58, 3L),
            (result.IsSuccess, result.Value!.Resolution, target.Health, home.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Resolution_check_matches_the_canonical_resolver_and_advances_exactly_one_causal_id()
    {
        var (world, carrier, target, _) = WorldWithPower(
            effects: ["npc.health:15"], costs: [], reliability: "ResolutionCheck");
        var (control, controlCarrier, controlTarget, _) = WorldWithPower(
            effects: ["npc.health:15"], costs: [], reliability: "ResolutionCheck");
        long invocationId = world.NextEventId;
        int difficulty = 10 + (int)Math.Ceiling(15 / 10d)
            + Math.Clamp((100 - controlTarget.Health) / 20, 0, 5);
        int capacity = (int)Math.Clamp(
            Math.Round(controlCarrier.Vitality / 10d + controlCarrier.RateGene.Value * 5d), 0, 20);
        var expected = Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            control.Rng.Stream($"extraordinary-resolution-{controlCarrier.Id.Value}-test-power-{invocationId}"));

        var result = ExtraordinaryInvocationEngine.InvokeAuthored(
            world, new TickContext(world, world.Rng, world.Scheduler),
            carrier.Id, "test-power", target.Id);

        Assert.Equal(expected, result.Value?.Resolution ?? ParseFailedResolution(result.Error));
        Assert.Equal(invocationId + 1, world.NextEventId);
    }

    [Fact]
    public void Partial_success_rounds_a_negative_odd_effect_away_from_zero()
    {
        var (world, carrier, target, _) = WorldWithPower(
            effects: ["npc.health:-15"], costs: [], reliability: "ResolutionCheck");

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(50, carrier.Id, "test-power", target.Id,
                ResolutionResult.PartialSuccess));

        Assert.Equal((true, 42), (result.IsSuccess, target.Health));
    }

    [Fact]
    public void Failed_resolution_applies_each_declared_consequence_to_the_carrier_and_log()
    {
        var (world, carrier, target, _) = WorldWithPower(
            effects: ["npc.health:15"], costs: [], reliability: "ResolutionCheck",
            failureModes: ["carrier.health:7", "public-exposure"]);
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(47, carrier.Id, "test-power", target.Id, ResolutionResult.Failure));

        Assert.Equal((false, 93, 50, 2),
            (result.IsSuccess, carrier.Health, target.Health,
                sink.Events.Count(evt => evt.Kind == WorldEventKind.ExtraordinaryFailureApplied)));
        Assert.Contains(sink.Events, evt => evt.Payload.EndsWith("carrier.health:7", StringComparison.Ordinal));
        Assert.Contains(sink.Events, evt => evt.Payload.EndsWith("public-exposure", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Passive")]
    [InlineData("Triggered")]
    public void Authored_use_rejects_modes_reserved_for_continuous_or_causal_systems(string mode)
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"], mode: mode);
        long nextEventId = world.NextEventId;

        var result = ExtraordinaryInvocationEngine.InvokeAuthored(
            world, new TickContext(world, world.Rng, world.Scheduler), carrier.Id, "test-power", target.Id);

        Assert.Equal((false, 50, 5L, nextEventId),
            (result.IsSuccess, target.Health, home.Stock[new ResourceType(9)], world.NextEventId));
        Assert.Contains("Mode", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Triggered_use_accepts_only_the_causal_system_origin()
    {
        var (world, carrier, target, _) = WorldWithPower(["npc.health:15"], [], mode: "Triggered");

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(48, carrier.Id, "test-power", target.Id,
                Origin: ExtraordinaryInvocationOrigin.Triggered));

        Assert.Equal((true, 65), (result.IsSuccess, target.Health));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Conditional")]
    public void Explicit_modes_reject_the_causal_system_origin(string mode)
    {
        var (world, carrier, target, home) = WorldWithPower(
            ["npc.health:15"], ["household.resource.9:2"], mode: mode);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(
                49, carrier.Id, "test-power", target.Id,
                Origin: ExtraordinaryInvocationOrigin.Triggered));

        Assert.Equal((false, 50, 5L),
            (result.IsSuccess, target.Health, home.Stock[new ResourceType(9)]));
        Assert.Contains("Mode", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Conditional_authored_use_requires_manifestation_and_dormant_rejection_is_zero_state()
    {
        var dormant = WorldWithPower(
            ["npc.health:15"], ["household.resource.9:2"], mode: "Conditional",
            manifested: false, manifestationCondition: "world:is-night");
        var manifested = WorldWithPower(
            ["npc.health:15"], ["household.resource.9:2"], mode: "Conditional",
            manifested: true, manifestationCondition: "world:is-night");
        string beforeHash = WorldSnapshot.CanonicalHash(dormant.World);
        long beforeEventId = dormant.World.NextEventId;

        var rejected = ExtraordinaryInvocationEngine.InvokeAuthored(
            dormant.World, new TickContext(dormant.World, dormant.World.Rng, dormant.World.Scheduler),
            dormant.Carrier.Id, "test-power", dormant.Target.Id);
        var accepted = ExtraordinaryInvocationEngine.InvokeAuthored(
            manifested.World, new TickContext(manifested.World, manifested.World.Rng, manifested.World.Scheduler),
            manifested.Carrier.Id, "test-power", manifested.Target.Id);

        Assert.Equal((false, 50, 5L, beforeEventId, beforeHash),
            (rejected.IsSuccess, dormant.Target.Health,
                dormant.Home.Stock[new ResourceType(9)], dormant.World.NextEventId,
                WorldSnapshot.CanonicalHash(dormant.World)));
        Assert.Equal((true, 65, 3L),
            (accepted.IsSuccess, manifested.Target.Health,
                manifested.Home.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Unsupported_target_fails_before_any_cost_or_mutation()
    {
        var (world, carrier, target, home) = WorldWithPower(
            effects: ["movement:construct"], costs: ["household.resource.9:2"]);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(44, carrier.Id, "test-power", target.Id));

        Assert.Equal((false, 50, 5L),
            (result.IsSuccess, target.Health, home.Stock[new ResourceType(9)]));
        Assert.Contains("Effects", result.Error, StringComparison.Ordinal);
        Assert.Equal(
            [WorldEventKind.ExtraordinaryUseAttempted, WorldEventKind.ExtraordinaryUseFailed],
            sink.Events.Select(evt => evt.Kind));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Paired_control_accounts_for_every_debited_resource_unit()
    {
        var (treated, carrier, target, treatedHome) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"]);
        var (_, _, controlTarget, controlHome) = WorldWithPower(
            effects: ["npc.health:15"], costs: ["household.resource.9:2"]);
        var ctx = new TickContext(treated, treated.Rng, treated.Scheduler);

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated, ctx, new ExtraordinaryInvocation(45, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        const long declaredCost = 2;
        Assert.Equal(controlHome.Stock[new ResourceType(9)],
            treatedHome.Stock[new ResourceType(9)] + declaredCost);
        Assert.Equal(controlTarget.Health + 15, target.Health);
    }

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs, string reliability = "Guaranteed",
        string mode = "Active", IReadOnlyList<string>? failureModes = null,
        bool manifested = true, string? manifestationCondition = null)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, mode, costs, reliability,
            failureModes ?? (reliability == "ResolutionCheck" ? ["no-effect"] : []), [], [], [],
            ManifestationCondition: manifestationCondition);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], manifested, manifested ? "active" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static ResolutionResult ParseFailedResolution(string? error) => error switch
    {
        "resolution:Failure" => ResolutionResult.Failure,
        "resolution:CriticalFailure" => ResolutionResult.CriticalFailure,
        _ => throw new Xunit.Sdk.XunitException($"Resultado inesperado: {error}"),
    };

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
