using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Geography;

/// <summary>Fase 2: hash do mapa é função só da seed — igual para a mesma, diferente entre seeds.
/// O literal preso falha se a geração depender de processo/máquina.</summary>
public class MapHashTests
{
    private static string HashOf(ulong seed) => MapSpatialHash.Compute(ScenarioRunner.Create(seed).World.Map);

    [Fact]
    public void Same_seed_gives_same_map_hash_and_different_seed_differs()
    {
        Assert.Equal(HashOf(42), HashOf(42));
        Assert.NotEqual(HashOf(42), HashOf(43));
    }

    [Fact]
    public void Map_hash_for_seed_42_is_pinned()
    {
        Assert.Equal("292fb94f94d259b137c99dae50ec6a8c5ee938ce535ba0a49b1d66ac32e5b18f", HashOf(42));
    }
}
