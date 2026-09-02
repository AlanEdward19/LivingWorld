using LivingWorld.Domain.Population;

namespace LivingWorld.Tests.LongRunning.Performance;

public class LazyNeedTests
{
    [Fact]
    public void ValueAt_clamps_to_zero_and_max()
    {
        var need = LazyNeed.Initial(100, tick: 0, decayRatePerTick: 10);
        Assert.Equal(0, need.ValueAt(20));
        Assert.Equal(100, need.ValueAt(0));
    }

    [Fact]
    public void ValueAt_crosses_urgency_threshold_at_expected_tick()
    {
        var need = LazyNeed.Initial(100, 0, decayRatePerTick: 2);
        Assert.True(need.ValueAt(10) <= 80);
        Assert.True(need.ValueAt(14) <= 72);
    }
}
