using System.Text.Json;
using LivingWorld.Domain.Population;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>ADR-0050 (AD-197b): a necessidade preguiçosa ancora em segundos e decai por hora de mundo; vale em qualquer instante,
/// e a forma serializada na hora cheia é a de antes.</summary>
public class LazyNeedSecondsTests
{
    [Fact]
    public void Value_inside_the_hour_decays_in_proportion_to_the_seconds_elapsed()
    {
        var need = LazyNeed.Initial(80, tick: 2, decayRatePerHour: 6);

        Assert.Equal(77, need.ValueAtSecond(2 * 3600 + 1800), 12);
    }

    [Fact]
    public void Value_at_the_full_hour_is_the_same_by_the_hour_and_by_the_second()
    {
        var need = LazyNeed.Initial(80, tick: 2, decayRatePerHour: 0.7).WithValueAtSecond(61.3, 5 * 3600);

        Assert.Equal(need.ValueAt(9), need.ValueAtSecond(9 * 3600));
    }

    [Fact]
    public void Full_hour_anchor_serializes_with_the_legacy_names_and_hours()
    {
        string json = JsonSerializer.Serialize(LazyNeed.Initial(50, tick: 3, decayRatePerHour: 2));

        Assert.Equal("{\"ValueAtLastEvent\":50,\"TickOfLastEvent\":3,\"DecayRatePerTick\":2}", json);
    }

    [Fact]
    public void Sub_hour_anchor_round_trips_through_json()
    {
        var need = LazyNeed.Initial(50, tick: 3, decayRatePerHour: 2).WithValueAtSecond(40, 3 * 3600 + 45);

        Assert.Equal(need, JsonSerializer.Deserialize<LazyNeed>(JsonSerializer.Serialize(need)));
    }
}
