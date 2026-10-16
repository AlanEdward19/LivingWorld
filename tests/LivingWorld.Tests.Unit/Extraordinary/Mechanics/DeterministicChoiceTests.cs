using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

/// <summary>T5 / EVO-16: hash(seed, npcId, salt) → [0,1) determinístico e uniforme.</summary>
public sealed class DeterministicChoiceTests
{
    [Fact]
    public void Same_seed_npcId_salt_always_produces_same_value()
    {
        const ulong seed = 42;
        var npcId = new NpcId(7);
        const string salt = "inheritance-occurs";

        double a = DeterministicChoice.InUnitInterval(seed, npcId, salt);
        double b = DeterministicChoice.InUnitInterval(seed, npcId, salt);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Value_is_in_half_open_unit_interval()
    {
        for (long id = 0; id < 1_000; id++)
        {
            double value = DeterministicChoice.InUnitInterval(99, new NpcId(id), "range-check");
            Assert.True(value is >= 0.0 and < 1.0, $"valor {value} fora de [0,1) para npcId={id}");
        }
    }

    [Fact]
    public void Different_inputs_change_the_value()
    {
        double baseline = DeterministicChoice.InUnitInterval(1, new NpcId(1), "salt-a");

        Assert.NotEqual(baseline, DeterministicChoice.InUnitInterval(2, new NpcId(1), "salt-a"));
        Assert.NotEqual(baseline, DeterministicChoice.InUnitInterval(1, new NpcId(2), "salt-a"));
        Assert.NotEqual(baseline, DeterministicChoice.InUnitInterval(1, new NpcId(1), "salt-b"));
    }

    [Fact]
    public void Large_npcId_sample_is_visually_uniform_across_unit_interval()
    {
        const int sampleSize = 10_000;
        const int bins = 10;
        var counts = new int[bins];

        for (long id = 0; id < sampleSize; id++)
        {
            double value = DeterministicChoice.InUnitInterval(12345, new NpcId(id), "uniformity");
            int bin = Math.Min(bins - 1, (int)(value * bins));
            counts[bin]++;
        }

        double expected = sampleSize / (double)bins;
        // Tolerância larga (~5σ binomial): rejeita só viés grosseiro, não flutuação normal.
        double minAllowed = expected * 0.85;
        double maxAllowed = expected * 1.15;

        for (int i = 0; i < bins; i++)
        {
            Assert.InRange(counts[i], minAllowed, maxAllowed);
        }
    }
}
