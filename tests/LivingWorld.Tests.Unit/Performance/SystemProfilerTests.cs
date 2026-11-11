using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.Unit.Performance;

/// <summary>#158 e #152 (item 1): o perfil por sistema e os contadores de despertar medem sem mudar o mundo.</summary>
public class SystemProfilerTests
{
    private const int Ticks = 72;

    [Fact]
    public void Profiled_clock_produces_the_same_world_as_the_plain_clock()
    {
        var (plain, plainClock) = ScenarioRunner.Create(42);
        var (profiled, profiledClock) = ScenarioRunner.Create(42);

        plainClock.Run(plain, Ticks);
        SystemProfiler.Measure(profiled, profiledClock, warmupTicks: 24, ticks: Ticks - 24);

        Assert.Equal(WorldSnapshot.CanonicalHash(plain), WorldSnapshot.CanonicalHash(profiled));
    }

    [Fact]
    public void Report_accounts_every_system_and_the_time_outside_them()
    {
        var (world, clock) = ScenarioRunner.Create(42);

        var report = SystemProfiler.Measure(world, clock, warmupTicks: 0, ticks: Ticks);

        Assert.Equal(clock.Systems.Select(s => s.Name), report.Systems.Select(s => s.Name));
        Assert.InRange(report.Systems.Sum(s => s.Milliseconds), 0, report.TotalMilliseconds);
    }

    [Fact]
    public void Wake_counters_count_each_processed_wake_and_what_scheduled_the_next_one()
    {
        var (world, clock) = ScenarioRunner.Create(42);
        world.WakeCounters.Reset();
        long processed = 0;

        for (int i = 0; i < Ticks; i++)
        {
            clock.Tick(world);
            processed += world.NpcWakeBatch.Count;
        }

        var counters = world.WakeCounters;
        long scheduled = Enum.GetValues<WakeCause>().Sum(counters.ScheduledBy);
        Assert.Equal((processed, true, true), (counters.Wakes, counters.WakesWithoutEffect <= counters.Wakes, scheduled >= counters.Wakes));
    }

    [Fact]
    public void Wake_counters_stay_out_of_the_canonical_and_volatile_hashes()
    {
        var (world, clock) = ScenarioRunner.Create(42);
        clock.Run(world, Ticks);
        var before = (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world));

        world.WakeCounters.Reset();

        Assert.Equal(before, (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world)));
    }
}
