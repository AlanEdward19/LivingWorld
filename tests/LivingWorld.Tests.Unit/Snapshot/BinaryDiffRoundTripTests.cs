using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>Fase 28, T19 (CMP-04/05): round-trip binário do diff real após simulação longa.</summary>
public class BinaryDiffRoundTripTests
{
    private const long OneYearInHours = 12 * 30 * 24;
    private const long TenYearsInHours = 10 * OneYearInHours;

    private static WorldState Clone(WorldState world) =>
        WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

    private static WorldState RoundTripDelta(WorldState current, WorldState baseline, IReadOnlySet<long> dirtyNpcIds)
    {
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteDelta(current, baseline, dirtyNpcIds, ms);
        ms.Position = 0;
        return writer.ReadAndApply(ms, baseline);
    }

    [Fact]
    public void Delta_round_trip_after_10_years_preserves_byte_identical_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 101, initialPopulation: 12);
        var baseline = Clone(world);
        clock.Run(world, TenYearsInHours);

        var dirty = world.Npcs.Select(n => n.Id.Value).ToHashSet();
        var restored = RoundTripDelta(world, baseline, dirty);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Delta_round_trip_after_10_years_incremental_hash_matches_recomputed_from_scratch()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 202, initialPopulation: 20);
        var baseline = Clone(world);
        clock.Run(world, TenYearsInHours);

        var dirty = world.Npcs.Select(n => n.Id.Value).ToHashSet();
        var restored = RoundTripDelta(world, baseline, dirty);

        Assert.True(IncrementalHasher.MatchesCanonical(restored));
        Assert.Equal(
            IncrementalHasher.Compute(restored, useCache: false),
            WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Full_round_trip_after_10_years_preserves_byte_identical_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 303, initialPopulation: 15);
        clock.Run(world, TenYearsInHours);

        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteFull(world, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, world);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        Assert.True(IncrementalHasher.MatchesCanonical(restored));
    }

    [Fact]
    public void Chained_delta_round_trips_over_10_years_remain_byte_identical()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 404, initialPopulation: 10);
        var baseline = Clone(world);
        var checkpointTicks = TenYearsInHours / 4;

        for (int i = 0; i < 4; i++)
        {
            clock.Run(world, checkpointTicks);
            var dirty = world.Npcs.Select(n => n.Id.Value).ToHashSet();
            var restored = RoundTripDelta(world, baseline, dirty);
            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
            baseline = Clone(world);
        }
    }
}
