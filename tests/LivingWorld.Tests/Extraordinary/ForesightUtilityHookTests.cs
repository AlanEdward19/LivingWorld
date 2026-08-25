using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ForesightUtilityHookTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void SelectByUtility_reduces_score_when_preview_predicts_failure()
    {
        var (world, _, npc) = BuildUrgentNpc(hunger: 10);
        var rules = MakeRules();
        long tick = 0;
        var badEatPreview = new Dictionary<ActionType, ResolutionResult>
        {
            [ActionType.Eat] = ResolutionResult.CriticalFailure,
        };

        var withoutPreview = BehaviorDecisionSystem.SelectByUtility(world, npc, rules, null, tick);
        var withPreview = BehaviorDecisionSystem.SelectByUtility(world, npc, rules, null, tick, badEatPreview);

        Assert.Equal(ActionType.Eat, withoutPreview);
        Assert.NotEqual(ActionType.Eat, withPreview);
    }

    [Fact]
    public void SelectByUtility_without_preview_matches_previous_behavior()
    {
        var (world, _, npc) = BuildUrgentNpc(hunger: 10);
        var rules = MakeRules();
        long tick = 0;

        var baseline = BehaviorDecisionSystem.SelectByUtility(world, npc, rules, null, tick);
        var explicitEmpty = BehaviorDecisionSystem.SelectByUtility(
            world, npc, rules, null, tick, ForesightMechanic.EmptyPreviews);
        var nullParam = BehaviorDecisionSystem.SelectByUtility(world, npc, rules, null, tick, null);

        Assert.Equal(baseline, explicitEmpty);
        Assert.Equal(baseline, nullParam);
    }

    [Fact]
    public void Carrier_with_bad_eat_preview_avoids_eat_while_identical_carrier_without_does_not()
    {
        var (worldA, _, npcA) = BuildUrgentNpc(hunger: 10, seed: 77);
        var (worldB, _, npcB) = BuildUrgentNpc(hunger: 10, seed: 77);
        var rules = MakeRules();
        long tick = 0;
        ForesightMechanic.EnsureTick(tick);
        ForesightMechanic.StorePreview(npcA.Id, tick, "Eat", ResolutionResult.CriticalFailure);

        var withForesight = BehaviorDecisionSystem.SelectByUtility(
            worldA, npcA, rules, null, tick, ForesightMechanic.PreviewsFor(npcA.Id, tick));
        var withoutForesight = BehaviorDecisionSystem.SelectByUtility(worldB, npcB, rules, null, tick);

        Assert.Equal(ActionType.Eat, withoutForesight);
        Assert.NotEqual(ActionType.Eat, withForesight);
    }

    private static NeedsRules MakeRules() => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildUrgentNpc(int hunger, ulong seed = 1)
    {
        var catalog = ActionCatalog.Create(
            new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2,
                [ActionType.Sleep] = 8,
                [ActionType.Work] = 8,
                [ActionType.Socialize] = 3,
                [ActionType.Travel] = 4,
                [ActionType.Idle] = 2,
                [ActionType.Buy] = 2,
            }, [], ActionType.Idle).Value!;
        var rules = MakeRules();
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: 100, sleep: 100, social: 100);
        world.AddNpc(npc);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        return (world, ctx, npc);
    }
}
