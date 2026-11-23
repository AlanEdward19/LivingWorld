using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Core;

/// <summary>ADR-0050 (AD-197b): o tick avança Δ segundos e os sistemas rodam ao cruzar a fronteira do calendário, não a cada tick.</summary>
public class WorldClockDeltaTests
{
    private static readonly TickFrequency[] Frequencies =
        [TickFrequency.Hourly, TickFrequency.Daily, TickFrequency.Midshift, TickFrequency.Monthly, TickFrequency.Yearly];

    private static (WorldState World, WorldClock Clock) CounterWorld()
    {
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 2, MonthsPerYear: 2);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        return (world, new WorldClock(Frequencies.Select(f => (ISimulationSystem)new ExampleCounterSystem(f)).ToArray()));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(1)]
    [InlineData(900)]
    public void Boundary_systems_run_the_same_number_of_times_with_a_smaller_delta(int delta)
    {
        var (hourly, hourlyClock) = CounterWorld();
        var (fine, fineClock) = CounterWorld();

        hourlyClock.Run(hourly, 24 * 5);
        fineClock.Run(fine, 24 * 5 * (3600 / delta), delta);

        Assert.Equal(
            Frequencies.Select(f => hourly.ExampleTickCounts.GetValueOrDefault(f)),
            Frequencies.Select(f => fine.ExampleTickCounts.GetValueOrDefault(f)));
    }

    [Fact]
    public void Tick_inside_the_hour_advances_only_the_clock()
    {
        var (world, clock) = CounterWorld();

        clock.Run(world, 59, 60);

        Assert.Equal((59 * 60L, 0L), (world.CurrentDate.TotalSeconds, world.ExampleTickCounts.GetValueOrDefault(TickFrequency.Hourly)));
    }

    [Fact]
    public void Tick_that_would_pass_the_full_hour_stops_on_it()
    {
        var (world, clock) = CounterWorld();
        clock.Tick(world, 1);

        clock.Tick(world, 3600);

        Assert.Equal((3600L, 1L), (world.CurrentDate.TotalSeconds, world.ExampleTickCounts[TickFrequency.Hourly]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(7200)]
    public void Delta_that_does_not_divide_the_hour_is_refused(int delta)
    {
        var (world, clock) = CounterWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(world, delta));
    }
}
