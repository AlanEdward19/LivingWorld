using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class ForesightMechanicTests
{
    [Fact]
    public void Preview_reports_the_resolver_result_for_that_tick_and_seed_with_no_new_fact()
    {
        var treated = WorldWithPreview("check");
        var expectedWorld = WorldWithPreview("check");
        var sink = new RecordingSink();
        const long invocationId = 280;
        const string evento = "check";
        int factsBefore = treated.World.Facts.Count;
        int targetHealthBefore = treated.Target.Health;
        var expected = Resolver.Resolve(
            Difficulty(treated.Target, magnitude: 1),
            BaseCapacity(treated.Carrier),
            VarianceProfile.Dramatico("extraordinary"),
            expectedWorld.World.Rng.Stream(
                $"extraordinary-resolution-{treated.Carrier.Id.Value}-{evento}-{invocationId}"));

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, PreviewedResolution(sink, evento));
        Assert.Equal(factsBefore, treated.World.Facts.Count);
        Assert.DoesNotContain(sink.Events, evt => evt.Kind == WorldEventKind.FactRecorded);
        Assert.Equal(targetHealthBefore, treated.Target.Health);
    }

    [Fact]
    public void Preview_does_not_consume_the_live_resolution_stream()
    {
        var treated = WorldWithPreview("check");
        const long invocationId = 281;
        const string evento = "check";
        string streamKey = $"extraordinary-resolution-{treated.Carrier.Id.Value}-{evento}-{invocationId}";
        ulong stateBefore = treated.World.Rng.Stream(streamKey).State;

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(stateBefore, treated.World.Rng.Stream(streamKey).State);
    }

    [Fact]
    public void After_the_world_changes_a_later_resolve_follows_current_inputs_not_the_preview()
    {
        var world = WorldWithPreview("check");
        var sink = new RecordingSink();
        const long invocationId = 282;
        const string evento = "check";
        string streamKey = $"extraordinary-resolution-{world.Carrier.Id.Value}-{evento}-{invocationId}";
        int previewDifficulty = Difficulty(world.Target, magnitude: 1);
        int capacity = BaseCapacity(world.Carrier);

        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world.World, new TickContext(world.World, world.World.Rng, world.World.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, world.Carrier.Id, "test-power", world.Target.Id)).IsSuccess);
        var preview = PreviewedResolution(sink, evento);
        ulong streamState = world.World.Rng.Stream(streamKey).State;

        world.Target.SetHealth(0);
        int laterDifficulty = Difficulty(world.Target, magnitude: 1);
        var later = Resolver.Resolve(
            laterDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            new WorldRng(streamState));
        var replayPreview = Resolver.Resolve(
            previewDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
            new WorldRng(streamState));

        Assert.NotEqual(previewDifficulty, laterDifficulty);
        Assert.Equal(preview, replayPreview);
        Assert.Equal(
            Resolver.Resolve(
                laterDifficulty, capacity, VarianceProfile.Dramatico("extraordinary"),
                new WorldRng(streamState)),
            later);
        Assert.Empty(world.World.Facts);
    }

    [Fact]
    public void Default_registry_resolves_the_foresight_prefix()
    {
        Assert.IsType<ForesightMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("foresight.preview:check"));
    }

    /// <summary>REALISM-30: preview of an ActionType stays readable for the carrier on the
    /// current tick (volatile store — no Fact, no canonical WorldState mutation).</summary>
    [Fact]
    public void Preview_for_ActionType_persists_readable_on_current_tick_without_Fact()
    {
        var treated = WorldWithPreview(nameof(ActionType.Travel));
        var sink = new RecordingSink();
        const long invocationId = 290;
        int factsBefore = treated.World.Facts.Count;
        long tick = treated.World.CurrentDate.TotalHours;

        Assert.Same(
            ForesightMechanic.EmptyPreviews,
            ForesightMechanic.PreviewsFor(treated.World, treated.Carrier.Id, tick));

        var result = ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler, sink),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id));

        Assert.True(result.IsSuccess, result.Error);
        var expected = PreviewedResolution(sink, nameof(ActionType.Travel));
        var previews = ForesightMechanic.PreviewsFor(treated.World, treated.Carrier.Id, tick);
        Assert.True(previews.TryGetValue(ActionType.Travel, out var stored));
        Assert.Equal(expected, stored);
        Assert.Equal(factsBefore, treated.World.Facts.Count);
        Assert.Empty(ForesightMechanic.PreviewsFor(treated.World, treated.Target.Id, tick));
        Assert.Same(
            ForesightMechanic.EmptyPreviews,
            ForesightMechanic.PreviewsFor(treated.World, treated.Carrier.Id, tick + 1));
    }

    /// <summary>REALISM-32: sem preview no tick, decisão idêntica ao comportamento anterior.</summary>
    [Fact]
    public void SelectByUtility_without_foresight_preview_matches_prior_behavior()
    {
        var economy = NeutralEconomy();
        var rules = NeedsRules.Create(
            0, 0, 0, 0, urgencyThreshold: 70, maxActionSelectionSteps: 10,
            hysteresisEnabled: false, continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var baseCtx = new DecisionContext(
            new NpcId(1), 0,
            new NeedsSnapshot(15, 100, 100, 100),
            new BodySnapshot(1.7, 68, 28, 1, 1),
            null,
            Array.Empty<NpcMemory>(),
            Array.Empty<string>(),
            Array.Empty<RelationshipFact>(),
            Array.Empty<PowerOpportunity>(),
            personality,
            null);
        var withEmpty = baseCtx with { ForesightPreviews = ForesightMechanic.EmptyPreviews };

        var without = BehaviorDecisionSystem.SelectByUtility(baseCtx, rules, economy, null);
        var empty = BehaviorDecisionSystem.SelectByUtility(withEmpty, rules, economy, null);

        Assert.Equal(without.Action, empty.Action);
        Assert.Equal(without.Trace.WinningUtility, empty.Trace.WinningUtility);
        Assert.Equal(ActionType.Eat, without.Action);
        Assert.Same(ForesightMechanic.EmptyPreviews, withEmpty.ForesightPreviews);
    }

    /// <summary>REALISM-31: preview de desfecho ruim reduz utility da ação prevista.</summary>
    [Fact]
    public void SelectByUtility_with_Failure_foresight_on_Eat_avoids_Eat()
    {
        var economy = NeutralEconomy();
        var rules = NeedsRules.Create(
            0, 0, 0, 0, urgencyThreshold: 70, maxActionSelectionSteps: 10,
            hysteresisEnabled: false, continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var hungry = new DecisionContext(
            new NpcId(1), 0,
            new NeedsSnapshot(15, 100, 100, 100),
            new BodySnapshot(1.7, 68, 28, 1, 1),
            null,
            Array.Empty<NpcMemory>(),
            Array.Empty<string>(),
            Array.Empty<RelationshipFact>(),
            Array.Empty<PowerOpportunity>(),
            personality,
            null);
        var withBadEat = hungry with
        {
            ForesightPreviews = new Dictionary<ActionType, ResolutionResult>
            {
                [ActionType.Eat] = ResolutionResult.Failure,
            },
        };

        var baseline = BehaviorDecisionSystem.SelectByUtility(hungry, rules, economy, null);
        var foresight = BehaviorDecisionSystem.SelectByUtility(withBadEat, rules, economy, null);

        Assert.Equal(ActionType.Eat, baseline.Action);
        Assert.NotEqual(ActionType.Eat, foresight.Action);
        Assert.True(BehaviorDecisionSystem.ForesightUtilityFactor(ResolutionResult.Failure) < 1.0);
        Assert.True(BehaviorDecisionSystem.ForesightUtilityFactor(ResolutionResult.CriticalFailure) <
            BehaviorDecisionSystem.ForesightUtilityFactor(ResolutionResult.Failure));
    }

    /// <summary>Independent Test P2 Foresight: com preview de Failure em Eat, o portador
    /// escolhe alternativa com frequência maior que o NPC idêntico sem foresight (mesmos seeds).</summary>
    [Fact]
    public void Independent_foresight_avoids_bad_Eat_more_often_than_without_across_seeds()
    {
        var economy = NeutralEconomy();
        var rules = NeedsRules.Create(
            0, 0, 0, 0, urgencyThreshold: 70, maxActionSelectionSteps: 10,
            hysteresisEnabled: false, continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        int withoutAte = 0;
        int withAte = 0;
        const int trials = 40;
        for (int seed = 1; seed <= trials; seed++)
        {
            // Personalidade semeada: variação nos traços que modulam Eat vs outras ações.
            int risk = 20 + (seed * 7) % 61;
            int impulse = 20 + (seed * 11) % 61;
            var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, impulse, risk).Value!;
            var hungry = new DecisionContext(
                new NpcId(1), seed,
                new NeedsSnapshot(18, 100, 100, 100),
                new BodySnapshot(1.7, 68, 28, 1, 1),
                null,
                Array.Empty<NpcMemory>(),
                Array.Empty<string>(),
                Array.Empty<RelationshipFact>(),
                Array.Empty<PowerOpportunity>(),
                personality,
                null);
            var withPreview = hungry with
            {
                ForesightPreviews = new Dictionary<ActionType, ResolutionResult>
                {
                    [ActionType.Eat] = ResolutionResult.Failure,
                },
            };

            if (BehaviorDecisionSystem.SelectByUtility(hungry, rules, economy, null).Action == ActionType.Eat)
                withoutAte++;
            if (BehaviorDecisionSystem.SelectByUtility(withPreview, rules, economy, null).Action == ActionType.Eat)
                withAte++;
        }

        Assert.True(withoutAte > withAte,
            $"sem foresight Eat={withoutAte}/{trials}, com foresight Eat={withAte}/{trials}");
        Assert.Equal(0, withAte);
    }

    /// <summary>DecisionContextBuilder expõe foresight persistido no tick (AD-011).</summary>
    [Fact]
    public void DecisionContextBuilder_exposes_stored_foresight_preview_for_carrier()
    {
        var treated = WorldWithPreview(nameof(ActionType.Travel));
        const long invocationId = 291;
        long tick = treated.World.CurrentDate.TotalHours;

        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            treated.World, new TickContext(treated.World, treated.World.Rng, treated.World.Scheduler),
            new ExtraordinaryInvocation(invocationId, treated.Carrier.Id, "test-power", treated.Target.Id)).IsSuccess);

        var ctx = DecisionContextBuilder.Build(treated.World, treated.Carrier, tick);
        Assert.True(ctx.ForesightPreviews!.TryGetValue(ActionType.Travel, out var preview));
        Assert.Equal(
            ForesightMechanic.PreviewsFor(treated.World, treated.Carrier.Id, tick)[ActionType.Travel],
            preview);

        var other = DecisionContextBuilder.Build(treated.World, treated.Target, tick);
        Assert.Same(ForesightMechanic.EmptyPreviews, other.ForesightPreviews);
    }

    private static EconomyRules NeutralEconomy() => EconomyRules.Create(
        enabled: false, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static ResolutionResult PreviewedResolution(RecordingSink sink, string evento)
    {
        string marker = $"{ForesightMechanic.PreviewPrefix}{evento}|";
        var preview = Assert.Single(
            sink.Events,
            evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
                && evt.Payload.StartsWith(marker, StringComparison.Ordinal));
        return Enum.Parse<ResolutionResult>(preview.Payload[marker.Length..]);
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPreview(string evento)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", [$"{ForesightMechanic.PreviewPrefix}{evento}"], "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), 100);
        var target = Npc(new NpcId(2), 80);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc Npc(NpcId id, int health) => new(
        id, "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static int Difficulty(Npc target, int magnitude) =>
        10 + (int)Math.Ceiling(magnitude / 10d) + Math.Clamp((100 - target.Health) / 20, 0, 5);

    private static int BaseCapacity(Npc carrier) =>
        (int)Math.Clamp(Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
