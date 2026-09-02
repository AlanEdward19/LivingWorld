using LivingWorld.Domain;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>T3 — <see cref="Animal.Energy"/> como <see cref="LazyNeed"/> (pré-requisito REALISM-01).</summary>
public sealed class AnimalEnergyTests
{
    [Fact]
    public void Animal_energy_is_lazy_need_read_via_ValueAt()
    {
        var energy = LazyNeed.Initial(100, tick: 0, decayRatePerTick: 10);
        var animal = new Animal(new AnimalId(1), "wolf", new CellCoord(0, 0), true, null, energy);

        Assert.Equal(100, animal.Energy.ValueAt(0));
        Assert.Equal(50, animal.Energy.ValueAt(5));
        Assert.Equal(0, animal.Energy.ValueAt(20));

        var topped = animal with { Energy = animal.Energy.WithValue(80, tick: 5) };
        Assert.Equal(80, topped.Energy.ValueAt(5));
        Assert.Equal(50, animal.Energy.ValueAt(5));
    }
}
