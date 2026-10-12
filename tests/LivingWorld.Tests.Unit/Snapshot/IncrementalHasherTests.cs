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
    [Trait("Category", "Scenario")]
    public void Incremental_matches_canonical_hash_after_many_ticks()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 100);
        clock.Run(world, 12 * 30 * 24);
        Assert.True(IncrementalHasher.MatchesCanonical(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), IncrementalHasher.Compute(world, useCache: false));
    }

    [Theory]
    [Trait("Category", "Scenario")]
    [InlineData(77UL, 12)]
    [InlineData(78UL, 30)]
    [InlineData(79UL, 60)]
    public void Incremental_hash_equals_the_from_scratch_hash_every_hundred_ticks_for_ten_years(ulong seed, int population)
    {
        var (world, clock) = ScenarioRunner.Create(seed: seed, initialPopulation: population);
        for (int tick = 0; tick < 10 * 12 * 30 * 24; tick += 100)
        {
            clock.Run(world, 100);
            Assert.Equal(IncrementalHasher.Compute(world, useCache: false), WorldSnapshot.CanonicalHash(world));
        }
    }
}
