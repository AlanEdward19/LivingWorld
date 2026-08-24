using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryCloseoutScenarioTests
{
    private const long HoursPerYear = 12 * 30 * 24;

    [Fact]
    public void Resolution_check_cost_is_identical_for_forced_success_and_failure_across_ten_seeds()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var success = World(seed, "ResolutionCheck", ["household.resource.9:2"], ["public-exposure"]);
            var failure = World(seed, "ResolutionCheck", ["household.resource.9:2"], ["public-exposure"]);

            var successResult = Invoke(success, 100 + (long)seed, ResolutionResult.Success);
            var failureResult = Invoke(failure, 100 + (long)seed, ResolutionResult.Failure);

            Assert.Equal((true, false, 3L, 3L),
                (successResult.IsSuccess, failureResult.IsSuccess,
                    success.Home.Stock[new ResourceType(9)], failure.Home.Stock[new ResourceType(9)]));
        }
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Guaranteed_costless_power_preserves_money_and_resources_at_every_tick_for_ten_years()
    {
        var setup = World(42, "Guaranteed", [], []);
        long initialStock = setup.Home.Stock[new ResourceType(9)];
        var initialMoney = (setup.World.MoneyMinted, setup.World.MoneyDestroyed,
            setup.World.Npcs.Sum(npc => npc.Wallet.Amount));
        var clock = new WorldClock([new ExtraordinaryStateSystem()]);

        for (long tick = 0; tick < 10 * HoursPerYear; tick++)
        {
            if (tick % HoursPerYear == 0)
                Assert.True(Invoke(setup, 200 + tick / HoursPerYear).IsSuccess);
            clock.Tick(setup.World);
            Assert.Equal(initialStock, setup.Home.Stock[new ResourceType(9)]);
            Assert.Equal(initialMoney, (setup.World.MoneyMinted, setup.World.MoneyDestroyed,
                setup.World.Npcs.Sum(npc => npc.Wallet.Amount)));
        }
    }

    [Fact]
    public void Every_declared_numeric_effect_is_the_exact_mutation_recorded_for_the_target()
    {
        var setup = World(
            42, "Guaranteed", [], [],
            ["npc.health:9", "npc.hunger:7", "npc.thirst:5", "npc.sleep:3", "npc.social:1"]);
        var sink = new RecordingSink();
        var result = ExtraordinaryInvocationEngine.Invoke(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler, sink),
            new ExtraordinaryInvocation(300, setup.Carrier.Id, "test-power", setup.Target.Id));

        Assert.Equal((true, 59, 57, 55, 53, 51),
            (result.IsSuccess, setup.Target.Health,
                setup.Target.HungerAt(0), setup.Target.ThirstAt(0),
                setup.Target.SleepAt(0), setup.Target.SocialAt(0)));
        Assert.Equal(
            setup.World.Extraordinary.Descriptors.Single().Effects,
            sink.Events.Where(evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied)
                .Select(evt => evt.Payload.Split('|')[^1]));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Enabled_extraordinary_state_changes_the_canonical_hash_after_ten_years()
    {
        var enabled = World(42, "Guaranteed", [], []);
        var disabledWorld = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: ExtraordinaryScenarioData.Disabled);

        new WorldClock([new ExtraordinaryStateSystem()]).Run(enabled.World, 10 * HoursPerYear);
        new WorldClock([]).Run(disabledWorld, 10 * HoursPerYear);

        Assert.NotEqual(
            WorldSnapshot.CanonicalHash(disabledWorld),
            WorldSnapshot.CanonicalHash(enabled.World));
    }

    private static Result<ExtraordinaryInvocationResult> Invoke(
        Setup setup, long id, ResolutionResult? resolution = null) =>
        ExtraordinaryInvocationEngine.Invoke(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler),
            new ExtraordinaryInvocation(id, setup.Carrier.Id, "test-power", setup.Target.Id, resolution));

    private static Setup World(
        ulong seed, string reliability, IReadOnlyList<string> costs,
        IReadOnlyList<string> failureModes, IReadOnlyList<string>? effects = null)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "source", effects ?? ["npc.health:1"], "Active", costs, reliability,
            failureModes, [], [], []);
        var carrierState = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrierState]);
        var carrier = Npc(1, 100, new HouseholdId(1));
        var target = Npc(2, 50, null);
        var home = new Household(
            new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return new Setup(world, carrier, target, home);
    }

    private static Npc Npc(long id, int value, HouseholdId? household) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, household, value,
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        ProfessionType.None, new CellCoord(0, 0), hunger: value, thirst: value, sleep: value,
        social: value);

    private sealed record Setup(WorldState World, Npc Carrier, Npc Target, Household Home);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
