using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 P1b (T13–T15, COH-11..14): builder de <see cref="DecisionContext"/>.</summary>
public class DecisionContextBuilderTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState BuildWorld()
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            bodyRules: BodyRules.Default);
    }

    private static Npc MakeNpc(
        WorldState world,
        long id = 1,
        HouseholdId? household = null,
        int hunger = 40,
        int thirst = 55,
        int sleep = 70,
        int social = 85,
        double height = 1.75,
        double weight = 72,
        double muscleMass = 30,
        ActionType? currentAction = ActionType.Work) =>
        new(
            new NpcId(id), "ctx", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
            new CellCoord(1, 1), null, null, household, health: 100, Personality, ProfessionType.None,
            new CellCoord(1, 1), hunger, thirst, sleep, social, currentAction: currentAction,
            height: height, weight: weight, muscleMass: muscleMass);

    [Fact]
    public void Build_snapshots_needs_body_personality_and_current_action()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, hunger: 40, thirst: 55, sleep: 70, social: 85,
            height: 1.80, weight: 80, muscleMass: 35, currentAction: ActionType.Idle);
        world.AddNpc(npc);
        const long tick = 12;

        var ctx = DecisionContextBuilder.Build(world, npc, tick);

        Assert.Equal(npc.Id, ctx.NpcId);
        Assert.Equal(tick, ctx.Tick);
        Assert.Equal(new NeedsSnapshot(40, 55, 70, 85), ctx.Needs);
        Assert.Equal(npc.Height, ctx.Body.Height);
        Assert.Equal(npc.Weight, ctx.Body.Weight);
        Assert.Equal(npc.MuscleMass, ctx.Body.MuscleMass);
        Assert.Equal(BodyMechanic.WorkCapacityMultiplier(world, npc), ctx.Body.WorkCapacityMultiplier);
        Assert.Equal(BodyMechanic.MovementCostMultiplier(world, npc), ctx.Body.MovementCostMultiplier);
        Assert.Equal(npc.Personality, ctx.Personality);
        Assert.Equal(ActionType.Idle, ctx.CurrentAction);
        Assert.Empty(ctx.RelevantMemories);
        Assert.Empty(ctx.RelevantBeliefs);
        Assert.Empty(ctx.KnownRelationships);
        Assert.Empty(ctx.PowerOpportunities);
    }

    [Fact]
    public void Build_without_household_sets_Household_null()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, household: null);
        world.AddNpc(npc);

        var ctx = DecisionContextBuilder.Build(world, npc, tick: 0);

        Assert.Null(ctx.Household);
    }

    [Fact]
    public void Build_with_household_snapshots_stock_and_members()
    {
        var world = BuildWorld();
        var householdId = new HouseholdId(1);
        var food = new ResourceType(1);
        var memberA = new NpcId(1);
        var memberB = new NpcId(2);
        var household = new Household(
            householdId, new CellCoord(1, 1), memberA, [memberA, memberB],
            stock: new Dictionary<ResourceType, long> { [food] = 7 });
        world.AddHousehold(household);

        var npc = MakeNpc(world, id: 1, household: householdId);
        world.AddNpc(npc);

        var ctx = DecisionContextBuilder.Build(world, npc, tick: 0);

        Assert.NotNull(ctx.Household);
        Assert.Equal(householdId, ctx.Household!.Id);
        Assert.Equal(7, ctx.Household.Stock[food]);
        Assert.Equal([memberA, memberB], ctx.Household.Members);
    }

    [Fact]
    public void Build_is_deterministic_for_same_world_npc_tick()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, hunger: 33, height: 1.66, weight: 61, muscleMass: 22);
        world.AddNpc(npc);

        var first = DecisionContextBuilder.Build(world, npc, tick: 5);
        var second = DecisionContextBuilder.Build(world, npc, tick: 5);

        Assert.Equal(first.NpcId, second.NpcId);
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Needs, second.Needs);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal(first.Personality, second.Personality);
        Assert.Equal(first.RelevantMemories.Select(m => m.Id), second.RelevantMemories.Select(m => m.Id));
        Assert.Equal(first.RelevantBeliefs, second.RelevantBeliefs);
    }

    [Fact]
    public void Build_without_memory_or_belief_returns_empty_lists()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world);
        world.AddNpc(npc);

        var ctx = DecisionContextBuilder.Build(world, npc, tick: 0);

        Assert.Empty(ctx.RelevantMemories);
        Assert.Empty(ctx.RelevantBeliefs);
    }

    [Fact]
    public void Build_includes_relevant_betrayal_memory_when_social_deficit_is_highest()
    {
        var world = BuildWorld();
        var npc = MakeNpc(world, hunger: 90, thirst: 90, sleep: 90, social: 10);
        world.AddNpc(npc);
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "foi traído por X na colheita", importance: 80, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        var ctx = DecisionContextBuilder.Build(world, npc, tick: 0);

        Assert.Contains(ctx.RelevantMemories, m => m.Content.Contains("traído", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_includes_city_beliefs_from_NpcBeliefQuery()
    {
        var historyRules = HistoryRules.Create(
            enabled: true,
            skeletonSignificanceThreshold: 0.5,
            canonSizePerCommunity: 10,
            mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
            {
                [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
            },
            operatorProbability: new Dictionary<DistortionOperator, double> { [DistortionOperator.Moralization] = 1.0 },
            importanceWeight: 1,
            transmissibilityWeight: 0,
            recencyWeight: 0).Value!;
        var (world, _) = ScenarioRunner.Create(3, historyRules: historyRules);
        var npc = world.Npcs[0];
        var city = world.FindCity(npc.City) ?? new City(npc.City, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        if (world.FindCity(npc.City) is null) world.AddCity(city);

        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id], city.Id, 0.8, "1|cause");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: 1, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, historyRules, nowTick: 20);

        var beliefsDirect = NpcBeliefQuery.BeliefsOf(world, npc.Id);
        var ctx = DecisionContextBuilder.Build(world, npc, tick: 0);

        Assert.NotEmpty(beliefsDirect);
        Assert.Equal(beliefsDirect, ctx.RelevantBeliefs);
    }
}
