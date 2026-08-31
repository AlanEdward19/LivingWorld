using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Sandbox;

/// <summary>Fase 28 T21 (SBX-01, SBX-03): sandbox isolado com <see cref="DecisionContext"/>
/// sintético — mesmo pipeline de <see cref="BehaviorDecisionSystem.SelectByUtility"/>.</summary>
public sealed class DecisionSandboxTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules UrgentRules(bool hysteresis = false) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: hysteresis,
        continuityBonus: 5, homelessSleepEfficiency: 0.5).Value!;

    private static EconomyRules EnabledEconomy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static DecisionContext HungryCtx(
        Personality? personality = null,
        IReadOnlyList<string>? beliefs = null,
        HouseholdSnapshot? household = null) =>
        new(
            new NpcId(1),
            Tick: 4,
            new NeedsSnapshot(Hunger: 15, Thirst: 90, Sleep: 80, Social: 80),
            new BodySnapshot(1.75, 70, 30, 1.0, 1.0),
            household,
            RelevantMemories: [],
            beliefs ?? [],
            KnownRelationships: [],
            PowerOpportunities: [],
            personality ?? Neutral,
            CurrentAction: ActionType.Work);

    [Fact]
    public void Synthetic_hungry_stimulus_produces_action_and_trace()
    {
        var ctx = HungryCtx(beliefs: ["the market has food stock today"]);
        var result = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());

        Assert.NotEqual(default(ActionType), result.Action);
        Assert.Equal(result.Action, result.Trace.Winner);
        Assert.True(result.Trace.WinningUtility > double.NegativeInfinity);
        Assert.NotEmpty(result.Trace.TopPressures);
        Assert.Contains(result.Trace.TopPressures, p => p.Kind == PressureModel.AcquireFood);
    }

    [Fact]
    public void Decide_does_not_mutate_WorldState_canonical_hash()
    {
        var map = ScenarioRunner.DefaultMap(1);
        var world = new WorldState(
            Calendar, seed: 42, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, UrgentRules(),
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EnabledEconomy());
        var npc = new Npc(
            new NpcId(1), "t", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
            new CellCoord(1, 1), null, null, null, 100, Neutral, ProfessionType.None,
            new CellCoord(1, 1), hunger: 15, thirst: 90, sleep: 80, social: 80);
        world.AddNpc(npc);

        var before = WorldSnapshot.CanonicalHash(world);
        _ = DecisionSandbox.Decide(HungryCtx(), UrgentRules(), EnabledEconomy());
        var after = WorldSnapshot.CanonicalHash(world);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Same_synthetic_stimulus_produces_identical_decision()
    {
        var ctx = HungryCtx(beliefs: ["the market has food stock today"]);
        var rules = UrgentRules();
        var economy = EnabledEconomy();
        var request = new DecisionSandboxRequest(
            ContinuityAction: null,
            WakeReason: WakeReason.UrgentNeed,
            PreviousIntent: ActionType.Work);

        var first = DecisionSandbox.Decide(ctx, rules, economy, request);
        var second = DecisionSandbox.Decide(ctx, rules, economy, request);

        Assert.Equal(first.Action, second.Action);
        Assert.Equal(first.Trace.WinningUtility, second.Trace.WinningUtility);
        Assert.Equal(first.Trace.TopPressures.Select(p => p.Kind), second.Trace.TopPressures.Select(p => p.Kind));
        Assert.Equal(first.Trace.KnownAlternatives, second.Trace.KnownAlternatives);
        Assert.Equal(first.Trace.TopPositiveFactors, second.Trace.TopPositiveFactors);
    }

    [Fact]
    public void Household_stock_changes_eat_versus_buy_winner()
    {
        var stocked = DecisionSandbox.Decide(
            HungryCtx(household: HouseholdWith(food: 5)), UrgentRules(), EnabledEconomy());
        var empty = DecisionSandbox.Decide(
            HungryCtx(household: HouseholdWith(food: 0, water: 0)), UrgentRules(), EnabledEconomy());

        Assert.Equal(ActionType.Eat, stocked.Action);
        Assert.Equal(ActionType.Buy, empty.Action);
    }

    private static HouseholdSnapshot HouseholdWith(long food, long water = 5) =>
        new(
            new HouseholdId(1),
            new Dictionary<ResourceType, long>
            {
                [new ResourceType(1)] = food,
                [new ResourceType(2)] = water,
            },
            [new NpcId(1)]);

    [Fact]
    public void Beliefs_surface_in_trace_opportunities_without_world_access()
    {
        var ctx = HungryCtx(beliefs: ["the market has food stock today"]);
        var result = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());

        Assert.Contains(result.Trace.KnownOpportunities, o => o.Kind == OpportunityModel.FoodAtMarket);
    }

    [Fact]
    public void Foresight_preview_affects_decision_without_world_rng()
    {
        var ctx = HungryCtx();
        var badEat = new Dictionary<ActionType, ResolutionResult>
        {
            [ActionType.Eat] = ResolutionResult.Failure,
        };
        var withForesight = ctx with { ForesightPreviews = badEat };

        var baseline = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());
        var foresight = DecisionSandbox.Decide(withForesight, UrgentRules(), EnabledEconomy());

        Assert.NotEqual(baseline.Action, foresight.Action);
        Assert.NotEqual(ActionType.Eat, foresight.Action);
    }

    [Fact]
    public void Continuity_bonus_can_change_winner_when_hysteresis_enabled()
    {
        var ctx = HungryCtx();
        var rules = UrgentRules(hysteresis: true);
        var economy = EnabledEconomy();

        var without = DecisionSandbox.Decide(ctx, rules, economy);
        var withContinuity = DecisionSandbox.Decide(
            ctx, rules, economy,
            new DecisionSandboxRequest(ContinuityAction: without.Action));

        Assert.Equal(without.Action, withContinuity.Action);
        Assert.True(withContinuity.Trace.WinningUtility >= without.Trace.WinningUtility);
    }
}
