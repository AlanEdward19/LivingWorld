using System.Text.Json;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>ADR-0050 (AD-197b sobre a AD-197f): a disputa de vontade e o livro do estranhamento guardam os instantes em ms; na hora
/// cheia o save e o hash são os de antes (horas, e -1 para "nunca"), e a leitura do motor segue em horas.</summary>
public class PossessionMillisTests
{
    [Fact]
    public void Will_contest_on_the_full_hour_serializes_like_before_with_never_as_minus_one()
    {
        var contest = new WillContest(5 * 3_600_000L, 42.5, 7 * 3_600_000L, Controlled: true);

        Assert.Equal(
            "{\"SinceTick\":5,\"Resistance\":42.5,\"AtTick\":7,\"Controlled\":true,\"Struggle\":0,\"LastPersuadedTick\":-1,\"LastCheckTick\":-1}",
            JsonSerializer.Serialize(contest));
    }

    [Fact]
    public void Will_contest_reads_hours_and_keeps_the_sub_hour_instant()
    {
        var legacy = JsonSerializer.Deserialize<WillContest>(
            "{\"SinceTick\":5,\"Resistance\":1,\"AtTick\":7,\"Controlled\":true,\"LastPersuadedTick\":-1,\"LastCheckTick\":6}")!;
        var inside = new WillContest(5 * 3_600_000L + 50, 1, 7 * 3_600_000L, Controlled: false, LastCheckMs: 6 * 3_600_000L + 900);

        Assert.Equal(
            (5L, -1L, 6L, inside),
            (legacy.SinceTick, legacy.LastPersuadedTick, legacy.LastCheckTick, JsonSerializer.Deserialize<WillContest>(JsonSerializer.Serialize(inside))));
    }

    [Fact]
    public void Suspicion_keeps_the_hour_api_and_saves_hours_on_the_full_hour()
    {
        var s = new Suspicion { Observer = 1, Body = 2, Value = 3, Tick = 9 };

        Assert.Equal((9 * 3_600_000L, true), (s.AtMs, JsonSerializer.Serialize(s).Contains("\"Tick\":9", StringComparison.Ordinal)));
    }
}
