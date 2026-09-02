using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Sandbox;

/// <summary>Fase 28 T22 (SBX-02): o hash canônico do mundo principal permanece idêntico
/// antes/depois de qualquer uso do <see cref="DecisionSandbox"/> — 5 combinações de estímulo.</summary>
public sealed class DecisionSandboxIsolationTests
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
        HouseholdSnapshot? household = null,
        IReadOnlyDictionary<ActionType, ResolutionResult>? foresight = null) =>
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
            CurrentAction: ActionType.Work,
            ForesightPreviews: foresight);

    private static HouseholdSnapshot HouseholdWith(long food, long water = 5) =>
        new(
            new HouseholdId(1),
            new Dictionary<ResourceType, long>
            {
                [new ResourceType(1)] = food,
                [new ResourceType(2)] = water,
            },
            [new NpcId(1)]);

    private static void AssertSandboxIsolation(
        WorldState world,
        Action sandboxInvocation)
    {
        var canonicalBefore = WorldSnapshot.CanonicalHash(world);
        var incrementalBefore = IncrementalHasher.Compute(world, useCache: false);
        Assert.True(IncrementalHasher.MatchesCanonical(world));

        sandboxInvocation();

        Assert.Equal(canonicalBefore, WorldSnapshot.CanonicalHash(world));
        Assert.Equal(incrementalBefore, IncrementalHasher.Compute(world, useCache: false));
        Assert.True(IncrementalHasher.MatchesCanonical(world));
    }

    [Fact]
    public void SandboxIsolation_hungry_beliefs_do_not_change_world_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 28, initialPopulation: 10);
        clock.Run(world, 48);

        AssertSandboxIsolation(world, () =>
        {
            var ctx = HungryCtx(beliefs: ["the market has food stock today"]);
            _ = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());
        });
    }

    [Fact]
    public void SandboxIsolation_stocked_household_eat_stimulus_leaves_world_hash_unchanged()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 281, initialPopulation: 10);
        clock.Run(world, 24);

        AssertSandboxIsolation(world, () =>
        {
            var ctx = HungryCtx(household: HouseholdWith(food: 5));
            _ = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());
        });
    }

    [Fact]
    public void SandboxIsolation_empty_household_buy_stimulus_leaves_world_hash_unchanged()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 282, initialPopulation: 10);
        clock.Run(world, 24);

        AssertSandboxIsolation(world, () =>
        {
            var ctx = HungryCtx(household: HouseholdWith(food: 0, water: 0));
            _ = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());
        });
    }

    [Fact]
    public void SandboxIsolation_foresight_preview_stimulus_leaves_world_hash_unchanged()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 283, initialPopulation: 10);
        clock.Run(world, 72);

        var badEat = new Dictionary<ActionType, ResolutionResult>
        {
            [ActionType.Eat] = ResolutionResult.Failure,
        };

        AssertSandboxIsolation(world, () =>
        {
            var ctx = HungryCtx(foresight: badEat);
            _ = DecisionSandbox.Decide(ctx, UrgentRules(), EnabledEconomy());
        });
    }

    [Fact]
    public void SandboxIsolation_continuity_hysteresis_wake_stimulus_leaves_world_hash_unchanged()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 284, initialPopulation: 10);
        clock.Run(world, 36);

        AssertSandboxIsolation(world, () =>
        {
            var ctx = HungryCtx();
            var rules = UrgentRules(hysteresis: true);
            var economy = EnabledEconomy();
            var baseline = DecisionSandbox.Decide(ctx, rules, economy);
            _ = DecisionSandbox.Decide(
                ctx,
                rules,
                economy,
                new DecisionSandboxRequest(
                    ContinuityAction: baseline.Action,
                    WakeReason: WakeReason.UrgentNeed,
                    PreviousIntent: ActionType.Work));
        });
    }
}
