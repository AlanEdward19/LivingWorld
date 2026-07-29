using LivingWorld.Simulation;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Snapshot;

public class IncrementalHasherTests
{
    [Fact]
    public void Incremental_matches_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 11, initialPopulation: 10);
        clock.Run(world, 100);
        Assert.True(IncrementalHasher.MatchesCanonical(world));
    }
}
