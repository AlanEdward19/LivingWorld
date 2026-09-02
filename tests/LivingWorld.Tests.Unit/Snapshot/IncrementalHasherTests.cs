using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

public class IncrementalHasherTests
{
    [Fact]
    public void Incremental_matches_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 11, initialPopulation: 10);
        clock.Run(world, 100);
        Assert.True(IncrementalHasher.MatchesCanonical(world));
    }

    [Fact]
    public void Incremental_matches_canonical_hash_after_many_ticks()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 100);
        clock.Run(world, 12 * 30 * 24);
        Assert.True(IncrementalHasher.MatchesCanonical(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), IncrementalHasher.Compute(world, useCache: false));
    }
}
