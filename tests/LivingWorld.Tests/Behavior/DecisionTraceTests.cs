using System.Reflection;
using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 T33 (COH-54): <see cref="DecisionTrace"/> via SelectByUtility.</summary>
public class DecisionTraceTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules UrgentRules() => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

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
            Neutral,
            CurrentAction: ActionType.Work);

    [Fact]
    public void SelectByUtility_trace_exposes_top_factors_and_alternatives()
    {
        var ctx = HungryCtx(beliefs: ["the market has food stock today"]);
        var decision = BehaviorDecisionSystem.SelectByUtility(
            ctx, UrgentRules(), EnabledEconomy(), continuityAction: null,
            wakeReason: WakeReason.UrgentNeed, previousIntent: ActionType.Work);

        var trace = decision.Trace;
        Assert.Equal(WakeReason.UrgentNeed, trace.WakeReason);
        Assert.Equal(ActionType.Work, trace.PreviousIntent);
        Assert.Equal(decision.Action, trace.Winner);
        Assert.True(trace.WinningUtility > double.NegativeInfinity);
        Assert.NotEmpty(trace.TopPressures);
        Assert.Contains(trace.TopPressures, p => p.Kind == PressureModel.AcquireFood);
        Assert.Contains(trace.KnownOpportunities, o => o.Kind == OpportunityModel.FoodAtMarket);
        Assert.NotEmpty(trace.TopPositiveFactors);
        Assert.NotEmpty(trace.TopNegativeFactors);
        Assert.NotEmpty(trace.KnownAlternatives);
        Assert.DoesNotContain(trace.Winner, trace.KnownAlternatives);
    }

    [Fact]
    public void DecisionTrace_type_has_no_Canonical_attributes()
    {
        var type = typeof(DecisionTrace);
        Assert.Null(type.GetCustomAttribute<CanonicalAttribute>());
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Assert.Null(prop.GetCustomAttribute<CanonicalAttribute>());
    }

    [Fact]
    public void Producing_DecisionTrace_does_not_change_canonical_hash()
    {
        var map = ScenarioRunner.DefaultMap(1);
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, UrgentRules(),
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EnabledEconomy());
        var npc = new Npc(
            new NpcId(1), "t", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
            new CellCoord(1, 1), null, null, null, 100, Neutral, ProfessionType.None,
            new CellCoord(1, 1), hunger: 15, thirst: 90, sleep: 80, social: 80);
        world.AddNpc(npc);

        var before = WorldSnapshot.CanonicalHash(world);
        var decisionCtx = DecisionContextBuilder.Build(world, npc, tick: 0);
        _ = BehaviorDecisionSystem.SelectByUtility(
            decisionCtx, UrgentRules(), world.EconomyRules, continuityAction: null);
        var after = WorldSnapshot.CanonicalHash(world);

        Assert.Equal(before, after);
        Assert.NotNull(decisionCtx);
    }

    [Fact]
    public void Trace_is_deterministic_for_same_context()
    {
        var ctx = HungryCtx();
        var a = BehaviorDecisionSystem.SelectByUtility(ctx, UrgentRules(), EnabledEconomy(), null);
        var b = BehaviorDecisionSystem.SelectByUtility(ctx, UrgentRules(), EnabledEconomy(), null);

        Assert.Equal(a.Trace.Winner, b.Trace.Winner);
        Assert.Equal(a.Trace.WinningUtility, b.Trace.WinningUtility);
        Assert.Equal(a.Trace.TopPressures.Select(p => p.Kind), b.Trace.TopPressures.Select(p => p.Kind));
        Assert.Equal(a.Trace.KnownAlternatives, b.Trace.KnownAlternatives);
        Assert.Equal(a.Trace.TopPositiveFactors, b.Trace.TopPositiveFactors);
    }
}
