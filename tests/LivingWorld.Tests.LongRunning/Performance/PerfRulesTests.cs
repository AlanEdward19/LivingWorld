using LivingWorld.Domain.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

public class PerfRulesTests
{
    [Fact]
    public void Create_succeeds_with_positive_fields()
    {
        var result = PerfRules.Create(1.0, 100, 2000, 5);
        Assert.True(result.IsSuccess);
        Assert.Equal(1.0, result.Value!.MaxMicrosPerAliveNpcTick);
        Assert.Equal(100, result.Value.MaxBytesAllocPerTick);
        Assert.Equal(2000, result.Value.MaxBytesPerAliveNpcPerYear);
        Assert.Equal(5, result.Value.ColdArchiveAfterYears);
    }

    [Fact]
    public void Create_rejects_non_positive_micros_per_alive_npc_tick()
    {
        Assert.False(PerfRules.Create(0, 100, 2000, 5).IsSuccess);
    }

    [Fact]
    public void Create_rejects_non_positive_bytes_alloc_per_tick()
    {
        Assert.False(PerfRules.Create(1.0, 0, 2000, 5).IsSuccess);
    }

    [Fact]
    public void Create_rejects_non_positive_bytes_per_alive_npc_per_year()
    {
        Assert.False(PerfRules.Create(1.0, 100, 0, 5).IsSuccess);
    }

    [Fact]
    public void Create_rejects_non_positive_cold_archive_after_years()
    {
        Assert.False(PerfRules.Create(1.0, 100, 2000, 0).IsSuccess);
    }
}
