using LivingWorld.Domain;
using LivingWorld.Simulation;

using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 10: <see cref="NeedsDecaySystem"/> — decaimento das 4 necessidades
/// (NEEDS-01), objetivo inspecionável em déficit/zero (NEEDS-02/05) e morte por fome sustentada
/// (NEEDS-03). Determinismo obrigatório por sistema novo (rules/simulation-determinism.md).</summary>
public class NeedsDecaySystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules MakeRules(
        double hungerDecay = 0, double thirstDecay = 0, double sleepDecay = 0, double socialDecay = 0,
        int urgencyThreshold = 70) =>
        NeedsRules.Create(
            hungerDecay, thirstDecay, sleepDecay, socialDecay, urgencyThreshold,
            maxActionSelectionSteps: 10, hysteresisEnabled: false, continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildWorld(
        ulong seed, NeedsRules rules, int initialHunger = 100)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        var location = new CellCoord(1, 1);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            hunger: initialHunger);

        world.AddNpc(npc);
        npc.ConfigureNeedDecay(rules, world.CurrentDate.TotalHours);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        return (world, ctx, npc);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Lazy_need_decays_materialized_value_after_one_hour_without_eager_writes()
    {
        var rules = MakeRules(hungerDecay: 4, thirstDecay: 3, sleepDecay: 2, socialDecay: 1);
        var (world, _, npc) = BuildWorld(seed: 1, rules);
        var clock = new WorldClock([new NeedsDecaySystem()]);

        clock.Tick(world);
        long tick = world.CurrentDate.TotalHours;

        Assert.Equal(100 - (int)rules.HungerDecayPerHour, npc.HungerAt(tick));
        Assert.Equal(100 - (int)rules.ThirstDecayPerHour, npc.ThirstAt(tick));
        Assert.Equal(100 - (int)rules.SleepDecayPerHour, npc.SleepAt(tick));
        Assert.Equal(100 - (int)rules.SocialDecayPerHour, npc.SocialAt(tick));
    }

    [Fact]
    public void Need_decrement_below_zero_clamps_to_zero_instead_of_going_negative()
    {
        var rules = MakeRules(hungerDecay: 1000);
        var (world, _, npc) = BuildWorld(seed: 1, rules);
        var clock = new WorldClock([new NeedsDecaySystem()]);

        clock.Tick(world);

        Assert.Equal(0, npc.HungerAt(world.CurrentDate.TotalHours));
    }

    [Fact]
    public void HasUrgentNeed_is_true_once_a_need_hits_zero_regardless_of_the_urgency_threshold()
    {
        var rules = MakeRules(hungerDecay: 100, urgencyThreshold: 100);
        var (world, _, npc) = BuildWorld(seed: 1, rules);
        var clock = new WorldClock([new NeedsDecaySystem()]);

        Assert.False(npc.HasUrgentNeed(rules, world.CurrentDate.TotalHours));
        clock.Tick(world);
        long tick = world.CurrentDate.TotalHours;

        Assert.Equal(0, npc.HungerAt(tick));
        Assert.True(npc.HasUrgentNeed(rules, tick));
    }

    [Fact]
    public void HasUrgentNeed_is_true_when_a_need_deficit_exceeds_the_scenario_urgency_threshold()
    {
        var rules = MakeRules(urgencyThreshold: 50);
        var (_, _, npc) = BuildWorld(seed: 1, rules, initialHunger: 100 - rules.UrgencyThreshold - 1);
        npc.ConfigureNeedDecay(rules, 0);

        Assert.True(npc.HasUrgentNeed(rules, 0));
    }

    [Fact]
    public void HasUrgentNeed_is_false_when_no_need_deficit_exceeds_the_scenario_urgency_threshold()
    {
        var rules = MakeRules(urgencyThreshold: 50);
        var (_, _, npc) = BuildWorld(seed: 1, rules, initialHunger: 100 - rules.UrgencyThreshold);

        Assert.False(npc.HasUrgentNeed(rules));
    }

    [Fact]
    public void Hunger_never_decays_and_npc_never_starves_when_the_scenario_decay_rate_is_zero()
    {
        var rules = MakeRules(hungerDecay: 0);
        var (world, _, npc) = BuildWorld(seed: 1, rules, initialHunger: 0);
        var clock = new WorldClock([new NeedsDecaySystem()]);

        clock.Run(world, ticks: 500);

        Assert.Equal(0, npc.Hunger);
        Assert.True(npc.IsAlive);
    }

    [Fact]
    public void Npc_starves_to_death_within_X_to_Xplus1_ticks_after_hunger_first_hits_zero_with_starvation_logged()
    {
        var rules = MakeRules(hungerDecay: 7);
        var (world, _, npc) = BuildWorld(seed: 1, rules);
        var sink = new RecordingSink();
        var clock = new WorldClock([new NeedsDecaySystem()], sink: sink);
        long survivalTicks = (long)Math.Ceiling(100.0 / rules.HungerDecayPerHour);

        long? hungerZeroTick = null;
        long? deathTick = null;
        for (long tick = 1; tick <= survivalTicks * 2 + 5 && deathTick is null; tick++)
        {
            clock.Tick(world);
            if (hungerZeroTick is null && npc.HungerAt(world.CurrentDate.TotalHours) == 0) hungerZeroTick = tick;
            if (!npc.IsAlive) deathTick = tick;
        }

        Assert.NotNull(hungerZeroTick);
        Assert.NotNull(deathTick);
        Assert.InRange(deathTick.Value - hungerZeroTick.Value, survivalTicks, survivalTicks + 1);

        var starvation = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Starvation);
        Assert.Equal(npc.Id.Value.ToString(), starvation.Payload);
    }

    [Fact]
    public void Same_seed_produces_identical_world_hash_and_a_different_seed_diverges()
    {
        static string RunWithNeedsDecay(ulong seed)
        {
            var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 20);
            var clock = new WorldClock([new NeedsDecaySystem()]);
            clock.Run(world, ticks: 200);
            return WorldSnapshot.CanonicalHash(world);
        }

        string hashA = RunWithNeedsDecay(seed: 1);
        string hashB = RunWithNeedsDecay(seed: 1);
        string hashC = RunWithNeedsDecay(seed: 2);

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(hashA, hashC);
    }
}
