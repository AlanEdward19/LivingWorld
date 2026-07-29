using LivingWorld.Simulation;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Snapshot;

public class BinarySnapshotWriterTests
{
    [Fact]
    public void Full_round_trip_preserves_canonical_hash()
    {
        var (world, _) = ScenarioRunner.Create(seed: 9, initialPopulation: 5);
        var writer = new BinarySnapshotWriter();
        using var ms = new MemoryStream();
        writer.WriteFull(world, ms);
        ms.Position = 0;
        var restored = writer.ReadAndApply(ms, world);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }
}
