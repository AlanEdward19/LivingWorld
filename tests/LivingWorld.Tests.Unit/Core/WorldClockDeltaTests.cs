using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Core;

/// <summary>ADR-0050 (AD-197b): o tick avança Δ milissegundos e os sistemas rodam ao cruzar a fronteira do calendário, não a cada tick.</summary>
public class WorldClockDeltaTests
{
    private static readonly TickFrequency[] Frequencies =
        [TickFrequency.Hourly, TickFrequency.Daily, TickFrequency.Midshift, TickFrequency.Monthly, TickFrequency.Yearly];

    private static (WorldState World, WorldClock Clock, Dictionary<TickFrequency, long> Runs) CounterWorld()
    {
        var calendar = new WorldCalendar(HoursPerDay: 24, DaysPerMonth: 2, MonthsPerYear: 2);
        var world = new WorldState(
            calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var runs = Frequencies.ToDictionary(f => f, _ => 0L);
        return (world, new WorldClock(Frequencies.Select(f => (ISimulationSystem)new Counter(f, runs)).ToArray()), runs);
    }

    private sealed class Counter(TickFrequency frequency, Dictionary<TickFrequency, long> runs) : ISimulationSystem
    {
        public string Name => $"counter-{frequency}";
        public TickFrequency Frequency => frequency;
        public void Tick(WorldState world, TickContext ctx) => runs[frequency]++;
    }

    [Theory]
    [InlineData(60_000, 5)]
    [InlineData(1_000, 5)]
    [InlineData(900_000, 5)]
    [InlineData(50, 1)] // o tick fino do jogador (~50 ms), um dia
    public void Boundary_systems_run_the_same_number_of_times_with_a_smaller_delta(int delta, int days)
    {
        var (hourly, hourlyClock, hourlyRuns) = CounterWorld();
        var (fine, fineClock, fineRuns) = CounterWorld();

        hourlyClock.Run(hourly, 24 * days);
        fineClock.Run(fine, 24L * days * (WorldMillis.PerHour / delta), delta);

        Assert.Equal(
            (hourly.CurrentDate, string.Join(',', hourlyRuns.Values)),
            (fine.CurrentDate, string.Join(',', fineRuns.Values)));
    }

    [Fact]
    public void Tick_inside_the_hour_advances_only_the_clock()
    {
        var (world, clock, runs) = CounterWorld();

        clock.Run(world, 59, 60_000);

        Assert.Equal((59 * 60_000L, 0L), (world.CurrentDate.TotalMilliseconds, runs[TickFrequency.Hourly]));
    }

    [Fact]
    public void Tick_that_would_pass_the_full_hour_stops_on_it()
    {
        var (world, clock, runs) = CounterWorld();
        clock.Tick(world, 1);

        clock.Tick(world, WorldMillis.PerHour);

        Assert.Equal((3_600_000L, 1L), (world.CurrentDate.TotalMilliseconds, runs[TickFrequency.Hourly]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(7_200_000)]
    public void Delta_that_does_not_divide_the_hour_is_refused(int delta)
    {
        var (world, clock, _) = CounterWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(world, delta));
    }
}
