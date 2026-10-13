using System.Reflection;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>snapshot-positional-binary: o codec posicional devolve o mesmo mundo (hash canônico) e os mesmos bytes ao
/// recodificar, cobre toda propriedade persistível do mundo, e o diff aplicado à base reconstrói a árvore atual.</summary>
public class PositionalSnapshotTests
{
    private const long TwoYears = 2 * 12 * 30 * 24;

    [Fact]
    public void Codec_round_trips_many_seeds_with_equal_hash_and_equal_bytes()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (world, clock) = ScenarioRunner.Create(seed: seed, initialPopulation: 12);
            clock.Run(world, TwoYears / 4);

            var bytes = PositionalSnapshot.EncodeFull(world, out _);
            var restored = PositionalSnapshot.DecodeFull(bytes);

            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
            Assert.Equal(
                WorldSnapshot.VolatileHash(WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world))), WorldSnapshot.VolatileHash(restored));
            var body = bytes.AsSpan(9);
            Assert.Equal(body.ToArray(), PositionalTreeCodec.Encode(PositionalTreeCodec.Decode(body))); // a recodificação não muda um byte
        }
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Codec_round_trips_twenty_seeds_after_ten_years()
    {
        Parallel.For(1, 21, seed =>
        {
            var (world, clock) = ScenarioRunner.Create(seed: (ulong)seed, initialPopulation: 12);
            clock.Run(world, 10 * 12 * 30 * 24);

            var restored = PositionalSnapshot.DecodeFull(PositionalSnapshot.EncodeFull(world, out _));

            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        });
    }

    [Fact]
    public void Every_persistable_world_property_is_in_the_encoded_tree()
    {
        var (world, _) = ScenarioRunner.Create(seed: 7, initialPopulation: 8);
        var tree = WorldSnapshot.BuildPersistentTree(world);

        var expected = WorldSnapshot.ReflectedProperties
            .Where(p => p.GetCustomAttribute<EphemeralAttribute>() is null)
            .Select(p => p.Name);

        Assert.All(expected, name => Assert.True(tree.ContainsKey(name), $"{name} ficou de fora do snapshot"));
    }

    [Fact]
    public void Same_seed_and_ticks_write_identical_bytes()
    {
        static byte[] Run()
        {
            var (w, c) = ScenarioRunner.Create(seed: 5, initialPopulation: 20);
            c.Run(w, 24 * 90);
            return PositionalSnapshot.EncodeFull(w, out _);
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Encoded_bytes_are_far_smaller_than_the_json_snapshot()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 9, initialPopulation: 40);
        clock.Run(world, 24 * 60);

        long json = System.Text.Encoding.UTF8.GetByteCount(WorldSnapshot.Serialize(world));
        long binary = PositionalSnapshot.EncodeFull(world, out _).Length;

        Assert.True(binary * 2 <= json, $"binário {binary} B contra JSON {json} B");
    }

    [Fact]
    public void Delta_applied_to_the_base_rebuilds_the_current_world_across_a_year()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 13, initialPopulation: 30);
        clock.Run(world, 24 * 30);
        var baseBytes = PositionalSnapshot.EncodeFull(world, out var snapshotBase);

        for (int step = 0; step < 4; step++)
        {
            clock.Run(world, 24 * 90);
            var delta = PositionalSnapshot.EncodeDelta(world, snapshotBase);
            var restored = PositionalSnapshot.DecodeDelta(delta, baseBytes);

            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
            Assert.Equal(
                WorldSnapshot.VolatileHash(WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world))), WorldSnapshot.VolatileHash(restored));
        }
    }

    [Fact]
    public void Delta_is_smaller_than_a_full_snapshot_when_little_changed()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 17, initialPopulation: 60);
        clock.Run(world, 24 * 30);
        var full = PositionalSnapshot.EncodeFull(world, out var snapshotBase);
        clock.Run(world, 24);

        Assert.True(PositionalSnapshot.EncodeDelta(world, snapshotBase).Length < full.Length);
    }

    [Fact]
    public void A_snapshot_base_only_fits_its_own_world_and_later_ticks()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 19, initialPopulation: 6);
        clock.Run(world, 48);
        PositionalSnapshot.EncodeFull(world, out var snapshotBase);
        var (other, _) = ScenarioRunner.Create(seed: 19, initialPopulation: 6);

        Assert.False(snapshotBase.Fits(world)); // no próprio tick da base o diff sobrescreveria a base gravada
        clock.Run(world, 24);
        Assert.True(snapshotBase.Fits(world));
        Assert.False(snapshotBase.Fits(other));
    }

    [Fact]
    public void Invalid_or_mismatched_blocks_are_rejected()
    {
        var (world, _) = ScenarioRunner.Create(seed: 3, initialPopulation: 4);
        var full = PositionalSnapshot.EncodeFull(world, out var snapshotBase);
        var delta = PositionalSnapshot.EncodeDelta(world, snapshotBase);

        Assert.Throws<InvalidDataException>(() => PositionalSnapshot.DecodeFull("not a snapshot at all"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => PositionalSnapshot.DecodeFull(delta));
        Assert.Throws<InvalidDataException>(() => PositionalSnapshot.DecodeDelta(full, full));
    }
}
