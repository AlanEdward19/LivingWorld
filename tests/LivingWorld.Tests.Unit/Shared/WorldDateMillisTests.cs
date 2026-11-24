using System.Text.Json;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Shared;

/// <summary>ADR-0050 (AD-197b): a data do mundo é guardada em milissegundos inteiros; a hora é derivada; a forma serializada de um
/// instante na hora cheia é a de antes (mesmo hash), e um save antigo em horas (ou na forma em segundos que existiu antes da troca
/// para ms) carrega convertido.</summary>
public class WorldDateMillisTests
{
    private static readonly WorldCalendar Calendar = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    [Fact]
    public void TotalHours_is_the_floor_of_milliseconds_also_before_the_epoch()
    {
        Assert.Equal(
            (1L, -1L, -2L),
            (WorldDate.FromMilliseconds(Calendar, 3_600_000 + 59).TotalHours, WorldDate.FromMilliseconds(Calendar, -1).TotalHours,
                WorldDate.FromMilliseconds(Calendar, -3_600_001).TotalHours));
    }

    [Fact]
    public void Calendar_fields_change_only_when_the_hour_turns()
    {
        var almost = WorldDate.FromHours(Calendar, 23).AddMilliseconds(3_599_999);
        var next = almost.AddMilliseconds(1);

        Assert.Equal((23, 0, 0, 1), (almost.Hour, almost.Day, next.Hour, next.Day));
    }

    [Fact]
    public void Serialized_full_hour_has_the_legacy_shape_without_the_fraction()
    {
        string json = JsonSerializer.Serialize(WorldDate.FromHours(Calendar, 30));
        Assert.Equal(
            "{\"Calendar\":{\"HoursPerDay\":24,\"DaysPerMonth\":30,\"MonthsPerYear\":12,\"HoursPerMonth\":720,\"HoursPerYear\":8640},"
            + "\"TotalHours\":30,\"Hour\":6,\"Day\":1,\"Month\":0,\"Year\":0}", json);
    }

    [Fact]
    public void Sub_hour_instant_round_trips_through_json()
    {
        var date = WorldDate.FromHours(Calendar, 30).AddMilliseconds(125_050);
        Assert.Equal(date, JsonSerializer.Deserialize<WorldDate>(JsonSerializer.Serialize(date)));
    }

    [Fact]
    public void Legacy_hours_and_the_seconds_form_load_as_milliseconds()
    {
        const string cal = "{\"HoursPerDay\":24,\"DaysPerMonth\":30,\"MonthsPerYear\":12}";
        var hours = JsonSerializer.Deserialize<WorldDate>("{\"Calendar\":" + cal + ",\"TotalHours\":30}");
        var seconds = JsonSerializer.Deserialize<WorldDate>("{\"Calendar\":" + cal + ",\"TotalHours\":30,\"SecondOfHour\":125}");

        Assert.Equal((30 * 3_600_000L, 30 * 3_600_000L + 125_000), (hours.TotalMilliseconds, seconds.TotalMilliseconds));
    }

    [Fact]
    public void ToString_keeps_the_hour_text_and_adds_the_time_inside_the_hour()
    {
        var hour = WorldDate.FromHours(Calendar, 30);
        Assert.Equal(
            ("Y0M0D1H6", "Y0M0D1H6m2s5", "Y0M0D1H6m2s5ms50"),
            (hour.ToString(), hour.AddMilliseconds(125_000).ToString(), hour.AddMilliseconds(125_050).ToString()));
    }
}
