using System.Text.Json;
using LivingWorld.Domain.Population;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>ADR-0050 (AD-197b): a necessidade preguiçosa ancora em milissegundos e decai por hora de mundo; vale em qualquer instante,
/// e a forma serializada na hora cheia é a de antes.</summary>
public class LazyNeedSecondsTests
{
    [Fact]
    public void Value_inside_the_hour_decays_in_proportion_to_the_seconds_elapsed()
    {
        var need = LazyNeed.Initial(80, tick: 2, decayRatePerHour: 6);

        Assert.Equal(77, need.ValueAtMs(2 * 3_600_000 + 1_800_000), 12);
    }

    [Fact]
    public void Value_at_the_full_hour_is_the_same_by_the_hour_and_by_the_second()
    {
        var need = LazyNeed.Initial(80, tick: 2, decayRatePerHour: 0.7).WithValueAtMs(61.3, 5 * 3_600_000);

        Assert.Equal(need.ValueAt(9), need.ValueAtMs(9 * 3_600_000));
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
        var need = LazyNeed.Initial(50, tick: 3, decayRatePerHour: 2).WithValueAtMs(40, 3 * 3_600_000 + 45);

        Assert.Equal(need, JsonSerializer.Deserialize<LazyNeed>(JsonSerializer.Serialize(need)));
    }
}
