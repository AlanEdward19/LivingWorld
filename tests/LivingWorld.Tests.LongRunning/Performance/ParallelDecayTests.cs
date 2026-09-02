using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.LongRunning.Performance;

public class ParallelDecayTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void Parallel_reschedule_matches_sequential_hash(int partitions)
    {
        static string HashWithPartitions(int parts)
        {
            var (world, clock) = ScenarioRunner.Create(seed: 77, initialPopulation: 40);
            clock.Run(world, 48);
            var ctx = new TickContext(world, world.Rng, world.Scheduler);
            long now = world.CurrentDate.TotalHours;
            var npcs = world.AliveNpcIndex.Alive.ToList();
            NpcWakeScheduler.RescheduleBatchParallel(npcs, world, ctx, world.NeedsRules, world.ActionCatalog, now, parts);
            return WorldSnapshot.CanonicalHash(world);
        }

        var baseline = HashWithPartitions(1);
        Assert.Equal(baseline, HashWithPartitions(partitions));
    }
}
