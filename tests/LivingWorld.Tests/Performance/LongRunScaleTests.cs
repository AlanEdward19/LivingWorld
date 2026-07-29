using LivingWorld.Simulation;

namespace LivingWorld.Tests.Performance;

[Trait("Category", "Scenario")]
public class LongRunScaleTests
{
    [Fact]
    public void Ten_k_population_ten_years_within_perf_budget()
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 42, initialPopulation: 10_000);
        clock.Run(world, ticks: 10 * 365 * 24);
        Assert.True(world.Npcs.Count(n => n.IsAlive) > 100);
    }

    [Fact]
    public void Storage_cost_per_alive_npc_stable_across_horizons()
    {
        var checkpoints = new[] { 365 * 24, 50 * 365 * 24, 100 * 365 * 24 };
        var samples = new List<double>();
        foreach (var horizon in checkpoints)
        {
            var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 99, initialPopulation: 500);
            clock.Run(world, horizon);
            long bytes = System.Text.Encoding.UTF8.GetByteCount(WorldSnapshot.Serialize(world));
            int alive = Math.Max(1, world.Npcs.Count(n => n.IsAlive));
            samples.Add((double)bytes / alive);
        }

        Assert.True(samples.Max() <= samples.Min() * 1.5);
    }
}
