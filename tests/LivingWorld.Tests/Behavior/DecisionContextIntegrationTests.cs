using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 P1b T17 (COH-13..16): memória/relação/household divergem a decisão;
/// fatores vazios não quebram.</summary>
public class DecisionContextIntegrationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules UrgentRules() => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static ActionCatalog WorkRoutineCatalog() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 8,
            [ActionType.Work] = 8,
            [ActionType.Socialize] = 3,
            [ActionType.Travel] = 4,
            [ActionType.Idle] = 2,
            [ActionType.Buy] = 2,
        },
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static EconomyRules EnabledEconomy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc) Build(
        ulong seed, NeedsRules rules, ActionCatalog catalog,
        int hunger = 100, int thirst = 100, int sleep = 100, int social = 100,
        EconomyRules? economy = null)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, ScenarioRunner.DefaultLifeStageRules, economyRules: economy);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: thirst, sleep: sleep, social: social);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, npc);
        return (world, ctx, npc);
    }

    [Fact]
    public void Betrayal_memory_diverges_decision_toward_Travel()
    {
        var rules = UrgentRules();
        var catalog = WorkRoutineCatalog();

        var (worldPlain, ctxPlain, npcPlain) = Build(seed: 11, rules, catalog, hunger: 20);
        new BehaviorDecisionSystem().Tick(worldPlain, ctxPlain);
        Assert.Equal(ActionType.Eat, npcPlain.CurrentAction);

        var (worldMem, ctxMem, npcMem) = Build(seed: 11, rules, catalog, hunger: 20);
        worldMem.AddNpcMemory(
            npcMem.Id, MemoryCategory.Social, "foi traído por X na colheita", importance: 90, originTick: 1,
            participants: [npcMem.Id], location: npcMem.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);
        new BehaviorDecisionSystem().Tick(worldMem, ctxMem);

        Assert.Equal(ActionType.Travel, npcMem.CurrentAction);
        Assert.NotEqual(npcPlain.CurrentAction, npcMem.CurrentAction);
    }

    [Fact]
    public void High_trust_relationship_diverges_decision_toward_Socialize()
    {
        var rules = UrgentRules();
        var catalog = WorkRoutineCatalog();

        // Sleep urgente (déficit 80) vence Socialize baseline (50); bônus de confiança
        // (+40) sobe Socialize acima de Sleep.
        var (worldPlain, ctxPlain, npcPlain) = Build(seed: 22, rules, catalog, sleep: 20, social: 50);
        new BehaviorDecisionSystem().Tick(worldPlain, ctxPlain);
        Assert.Equal(ActionType.Sleep, npcPlain.CurrentAction);

        var (worldRel, ctxRel, npcRel) = Build(seed: 22, rules, catalog, sleep: 20, social: 50);
        var other = new Npc(
            new NpcId(2), "other", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-28), new CultureId(1),
            npcRel.CurrentLocation, null, null, null, 100, Neutral, ProfessionType.None, npcRel.CurrentLocation);
        worldRel.AddNpc(other);
        var rel = worldRel.GetOrCreateRelationship(new RelationshipKey(npcRel.Id, other.Id), now: 1);
        for (int i = 0; i < 45; i++)
            rel.ApplyEvent(RelationshipEventType.Cohabitation, ScenarioRunner.DefaultFamilyRules);
        Assert.True(rel.Trust >= 60);

        new BehaviorDecisionSystem().Tick(worldRel, ctxRel);
        Assert.Equal(ActionType.Socialize, npcRel.CurrentAction);
        Assert.NotEqual(npcPlain.CurrentAction, npcRel.CurrentAction);
    }

    [Fact]
    public void Household_stock_change_diverges_Eat_versus_Buy()
    {
        var rules = UrgentRules();
        var catalog = WorkRoutineCatalog();
        var economy = EnabledEconomy();
        var food = new ResourceType(1);

        ActionType Decide(bool withFood)
        {
            var (world, ctx, npc) = Build(seed: 33, rules, catalog, hunger: 10, economy: economy);
            var householdId = new HouseholdId(1);
            var stock = withFood
                ? new Dictionary<ResourceType, long> { [food] = 5 }
                : new Dictionary<ResourceType, long>();
            var household = new Household(householdId, npc.CurrentLocation, npc.Id, [npc.Id], stock: stock);
            world.AddHousehold(household);
            npc.JoinHousehold(householdId);
            new BehaviorDecisionSystem().Tick(world, ctx);
            return npc.CurrentAction!.Value;
        }

        var withStock = Decide(withFood: true);
        var withoutStock = Decide(withFood: false);

        Assert.Equal(ActionType.Eat, withStock);
        Assert.Equal(ActionType.Buy, withoutStock);
        Assert.NotEqual(withStock, withoutStock);
    }

    [Fact]
    public void Empty_memory_belief_relationship_factors_still_decide_without_error()
    {
        var rules = UrgentRules();
        var catalog = WorkRoutineCatalog();
        var (world, ctx, npc) = Build(seed: 44, rules, catalog, hunger: 5);

        var decisionCtx = DecisionContextBuilder.Build(world, npc, tick: 0);
        Assert.Empty(decisionCtx.RelevantMemories);
        Assert.Empty(decisionCtx.RelevantBeliefs);
        Assert.Empty(decisionCtx.KnownRelationships);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Eat, npc.CurrentAction);
    }
}
