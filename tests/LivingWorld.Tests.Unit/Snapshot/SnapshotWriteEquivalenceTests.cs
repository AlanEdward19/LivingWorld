using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>#156: o caminho rápido de gravação (o mundo serializado uma vez, codec e diff sobre o documento, bytes frios
/// em cache) grava exatamente os bytes e os hashes da definição de referência (<see cref="SnapshotWriteReference"/>).</summary>
public class SnapshotWriteEquivalenceTests
{
    private const long Day = 24, Month = 30 * Day, Year = 12 * Month;

    private static (WorldState World, WorldClock Clock) Village(ulong seed, long horizon)
    {
        var (world, clock) = ScenarioRunner.Create(seed: seed, initialPopulation: 12, appearanceRules: new AppearanceRules(0.6));
        clock.Run(world, horizon);
        return (world, clock);
    }

    [Theory]
    [InlineData(1UL, Day)]
    [InlineData(2UL, Year)]
    [InlineData(5UL, 3 * Year)]
    public void Full_block_equals_the_reference_block_byte_for_byte(ulong seed, long horizon)
    {
        var (world, _) = Village(seed, horizon);

        var block = PositionalSnapshot.EncodeFull(world, out _);
        var again = PositionalSnapshot.EncodeFull(world, out _); // segunda vez com o mapa e as regras vindos do cache

        var reference = SnapshotWriteReference.Block(0, SnapshotWriteReference.FullBody(world));
        Assert.Equal(reference, block);
        Assert.Equal(reference, again);
    }

    [Theory]
    [InlineData(3UL, Month)]
    [InlineData(4UL, 2 * Year)]
    public void Delta_equals_the_reference_delta_with_and_without_dirty_set_and_from_a_restored_base(ulong seed, long horizon)
    {
        var (world, clock) = Village(seed, horizon);
        var full = PositionalSnapshot.EncodeFull(world, out var fresh);
        var referenceBase = SnapshotWriteReference.Fingerprint(world);
        PositionalSnapshot.Decode(full, world.CurrentDate.TotalHours, null, _ => null, out var restored);
        clock.Run(world, Month);
        var dirty = world.Npcs.Where(n => n.Id.Value % 2 == 0).Select(n => n.Id.Value).ToHashSet();

        var reference = SnapshotWriteReference.Block(1, SnapshotWriteReference.DeltaBody(world, referenceBase, null));
        Assert.Equal(reference, PositionalSnapshot.EncodeDelta(world, fresh));
        Assert.Equal(reference, PositionalSnapshot.EncodeDelta(world, restored));
        Assert.Equal(
            SnapshotWriteReference.Block(1, SnapshotWriteReference.DeltaBody(world, referenceBase, dirty)),
            PositionalSnapshot.EncodeDelta(world, fresh, dirty));
    }

    [Fact]
    public void Delta_with_a_key_that_left_and_came_back_equals_the_reference_delta()
    {
        var (world, _) = Village(6, Year);
        PositionalSnapshot.EncodeFull(world, out var fresh);
        var referenceBase = SnapshotWriteReference.Fingerprint(world);
        var key = world.Relationships.Keys.First();
        world.RemoveRelationship(key);
        world.GetOrCreateRelationship(key, world.CurrentDate.TotalHours);

        var delta = PositionalSnapshot.EncodeDelta(world, fresh);

        Assert.True(PositionalTreeCodec.Decode(PositionalSnapshot.Body(delta))!.AsObject().ContainsKey("Moved")); // o caso existe
        Assert.Equal(SnapshotWriteReference.Block(1, SnapshotWriteReference.DeltaBody(world, referenceBase, null)), delta);
    }

    [Theory]
    [InlineData(7UL, Month)]
    [InlineData(8UL, 2 * Year)]
    public void Hashes_equal_the_reference_hashes_alone_and_inside_one_write(ulong seed, long horizon)
    {
        var (world, clock) = Village(seed, horizon);
        var first = PositionalSnapshot.Write(world, null);
        clock.Run(world, Month);
        var second = PositionalSnapshot.Write(world, first.NewBase);

        string canonical = SnapshotWriteReference.CanonicalHash(world), vol = SnapshotWriteReference.VolatileHash(world);
        Assert.Equal(
            [canonical, canonical, canonical, vol, vol],
            [second.CanonicalHash, WorldSnapshot.CanonicalHash(world), IncrementalHasher.Compute(world, useCache: false),
                second.VolatileHash, WorldSnapshot.VolatileHash(world)]);
    }

    [Fact]
    public void One_write_gives_the_same_block_as_encoding_alone()
    {
        var (world, clock) = Village(9, Year);
        var full = PositionalSnapshot.Write(world, null);
        var alone = PositionalSnapshot.EncodeFull(world, out _);
        clock.Run(world, Month);

        var delta = PositionalSnapshot.Write(world, full.NewBase);

        Assert.Equal(
            [alone, PositionalSnapshot.EncodeDelta(world, full.NewBase!)],
            [full.Block, delta.Block]);
    }

    [Fact]
    public void Block_from_the_fast_path_decodes_to_the_same_world()
    {
        var (world, clock) = Village(10, Year);
        var write = PositionalSnapshot.Write(world, null);
        clock.Run(world, Month);
        var delta = PositionalSnapshot.Write(world, write.NewBase);

        var restored = PositionalSnapshot.DecodeDelta(delta.Block, write.Block);

        Assert.Equal(delta.CanonicalHash, WorldSnapshot.CanonicalHash(restored));
    }
}
