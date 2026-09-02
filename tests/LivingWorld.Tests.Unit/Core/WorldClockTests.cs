using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Core;

public class WorldClockTests
{
    [Fact]
    public void Yearly_system_runs_exactly_10_times_over_3650_daily_ticks()
    {
        // Calendário desta cena: 1 hora/dia (tick == dia), 73 dias/mês x 5 meses = 365 dias/ano.
        var calendar = new WorldCalendar(HoursPerDay: 1, DaysPerMonth: 73, MonthsPerYear: 5);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var systems = new ISimulationSystem[]
        {
            new ExampleCounterSystem(TickFrequency.Daily),
            new ExampleCounterSystem(TickFrequency.Yearly),
        };
        var clock = new WorldClock(systems);

        clock.Run(world, ticks: 3650);

        Assert.Equal(3650, world.ExampleTickCounts[TickFrequency.Daily]);
        Assert.Equal(10, world.ExampleTickCounts[TickFrequency.Yearly]);
    }

    [Fact]
    public void Hourly_system_runs_every_tick()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 1);
        clock.Run(world, ticks: 500);

        Assert.Equal(500, world.ExampleTickCounts[TickFrequency.Hourly]);
    }

    [Fact]
    public void Systems_run_in_declared_order_not_registration_accident()
    {
        var order = new List<string>();
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var systems = new ISimulationSystem[]
        {
            new RecordingSystem("second", TickFrequency.Hourly, order),
            new RecordingSystem("first", TickFrequency.Hourly, order),
        };
        var clock = new WorldClock(systems);

        clock.Tick(world);

        Assert.Equal(["second", "first"], order);
    }

    [Fact]
    public void Event_scheduled_for_a_future_tick_fires_exactly_at_that_tick()
    {
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var fired = new List<long>();
        var scheduler = new SelfSchedulingSystem(targetTick: 5, fired, rescheduleForever: false);
        var clock = new WorldClock([scheduler]);

        clock.Run(world, ticks: 10);

        Assert.Equal([5], fired);
    }

    [Fact]
    public void Tick_that_never_converges_aborts_naming_the_culprit_system()
    {
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var fired = new List<long>();
        var system = new SelfSchedulingSystem(targetTick: 1, fired, rescheduleForever: true);
        var clock = new WorldClock([system], maxIterationsPerTick: 10);

        // Precisa chegar ao tick 1 primeiro.
        var ex = Assert.Throws<TickBudgetExceededException>(() => clock.Run(world, ticks: 2));
        Assert.Equal(system.Name, ex.SystemName);
    }

    /// <summary>COH-63 / doc#81: ciclo de PRODUÇÃO A→B→A no mesmo tick — guard de
    /// <see cref="WorldClock"/> (<c>maxIterationsPerTick</c>) já cobre; distinto do
    /// <see cref="CausalChainTooDeepException"/> de proveniência (T4).</summary>
    [Fact]
    public void Production_cycle_A_B_A_aborts_deterministically_naming_culprit()
    {
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var a = new PingPongSystem("system-A", peer: "system-B");
        var b = new PingPongSystem("system-B", peer: "system-A");
        var clock = new WorldClock([a, b], maxIterationsPerTick: 8);

        var first = Assert.Throws<TickBudgetExceededException>(() => clock.Run(world, ticks: 1));
        Assert.Contains(first.SystemName, new[] { a.Name, b.Name });
        Assert.Contains("8", first.Message, StringComparison.Ordinal);

        // Mesma seed / mesmo setup → mesmo culpado (determinístico).
        var world2 = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var second = Assert.Throws<TickBudgetExceededException>(
            () => new WorldClock([a, b], maxIterationsPerTick: 8).Run(world2, ticks: 1));
        Assert.Equal(first.SystemName, second.SystemName);
        Assert.Equal(first.Message, second.Message);
    }

    private sealed class RecordingSystem(string name, TickFrequency frequency, List<string> order) : ISimulationSystem
    {
        public string Name => name;
        public TickFrequency Frequency => frequency;
        public void Tick(WorldState world, TickContext ctx) => order.Add(name);
    }

    private sealed class SelfSchedulingSystem(long targetTick, List<long> fired, bool rescheduleForever) : ISimulationSystem
    {
        public string Name => "self-scheduling";
        public TickFrequency Frequency => TickFrequency.Hourly;

        public void Tick(WorldState world, TickContext ctx)
        {
            if (ctx.CurrentTick == targetTick)
                ctx.ScheduleEvent(targetTick, Name);
        }

        public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
        {
            fired.Add(evt.TargetTick);
            if (rescheduleForever)
                ctx.ScheduleEvent(evt.TargetTick, Name);
        }
    }

    /// <summary>No Tick agenda o peer no mesmo tick; no HandleEvent reagenda o peer —
    /// produz ciclo A→B→A… até o iteration budget.</summary>
    private sealed class PingPongSystem(string name, string peer) : ISimulationSystem
    {
        public string Name => name;
        public TickFrequency Frequency => TickFrequency.Hourly;

        public void Tick(WorldState world, TickContext ctx) =>
            ctx.ScheduleEvent(ctx.CurrentTick, peer);

        public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt) =>
            ctx.ScheduleEvent(ctx.CurrentTick, peer);
    }
}
