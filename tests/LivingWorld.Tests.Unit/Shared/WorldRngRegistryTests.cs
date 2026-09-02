using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Shared;

public class WorldRngRegistryTests
{
    [Fact]
    public void Same_stream_key_persists_state_across_calls()
    {
        var registry = new WorldRngRegistry(seed: 42);
        var stream = registry.Stream("streamA");

        double first = stream.NextDouble();
        double second = registry.Stream("streamA").NextDouble();

        Assert.NotEqual(first, second); // mesmo stream avança, não reinicia a cada chamada
    }

    [Fact]
    public void Consuming_a_new_stream_does_not_move_other_streams()
    {
        var registryWithoutB = new WorldRngRegistry(seed: 42);
        double a1 = registryWithoutB.Stream("streamA").NextDouble();
        double c1 = registryWithoutB.Stream("streamC").NextDouble();

        var registryWithB = new WorldRngRegistry(seed: 42);
        double a2 = registryWithB.Stream("streamA").NextDouble();
        registryWithB.Stream("streamB").NextDouble(); // consome um stream novo no meio
        double c2 = registryWithB.Stream("streamC").NextDouble();

        Assert.Equal(a1, a2);
        Assert.Equal(c1, c2);
    }

    [Fact]
    public void Same_seed_produces_same_stream_sequence()
    {
        var r1 = new WorldRngRegistry(seed: 7).Stream("x");
        var r2 = new WorldRngRegistry(seed: 7).Stream("x");

        Assert.Equal(r1.NextDouble(), r2.NextDouble());
    }

    [Fact]
    public void Different_seeds_produce_different_stream_sequence()
    {
        var r1 = new WorldRngRegistry(seed: 7).Stream("x");
        var r2 = new WorldRngRegistry(seed: 8).Stream("x");

        Assert.NotEqual(r1.NextDouble(), r2.NextDouble());
    }

    [Fact]
    public void Snapshot_is_ordered_by_key_and_round_trips_through_reconstruction()
    {
        var registry = new WorldRngRegistry(seed: 42);
        registry.Stream("zeta").NextDouble();
        registry.Stream("alpha").NextDouble();

        var snapshot = registry.Snapshot();

        Assert.Equal(["alpha", "zeta"], snapshot.Select(s => s.Key));

        var restored = new WorldRngRegistry(seed: 42, snapshot);
        Assert.Equal(registry.Stream("alpha").NextDouble(), restored.Stream("alpha").NextDouble());
    }
}
