using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Necessidade com decaimento linear lazy — valor materializado só
/// em <see cref="ValueAt"/>, nunca escrito por tick. ADR-0050: a âncora é um instante em segundos
/// (<see cref="SecondOfLastEvent"/>) e a taxa é por hora de mundo (<see cref="DecayRatePerHour"/>), então o valor vale em
/// qualquer instante, com qualquer duração de tick. No save e no hash os nomes e a forma em horas são os de antes.
/// Os métodos com <c>tick</c> recebem a hora do calendário (sistemas de fronteira); os com <c>Second</c>, o instante.</summary>
public readonly record struct LazyNeed(
    double ValueAtLastEvent,
    [property: JsonPropertyName("TickOfLastEvent"), JsonConverter(typeof(HourCompatibleSecondsJsonConverter))] long SecondOfLastEvent,
    [property: JsonPropertyName("DecayRatePerTick")] double DecayRatePerHour)
{
    public const double Max = 100.0;

    /// <summary>A hora do calendário da última escrita (piso).</summary>
    [JsonIgnore]
    public long TickOfLastEvent => WorldSeconds.ToHourFloor(SecondOfLastEvent);

    /// <summary>Valor no começo da hora <paramref name="tick"/>. Âncora na hora cheia (o caso de todo sistema de fronteira): a
    /// conta direta em horas, sem passar por segundos (caminho quente, mesmo número).</summary>
    public double ValueAt(long tick)
    {
        long anchorHour = SecondOfLastEvent / WorldSeconds.PerHour;
        if (anchorHour * WorldSeconds.PerHour != SecondOfLastEvent) return ValueAtSecond(tick * WorldSeconds.PerHour);
        if (DecayRatePerHour <= 0)
            return Math.Clamp(ValueAtLastEvent, 0, Max);
        return Math.Clamp(ValueAtLastEvent - DecayRatePerHour * (tick - anchorHour), 0, Max);
    }

    /// <summary>Valor no instante <paramref name="second"/>. Horas decorridas = segundos / 3.600: exato quando a âncora e o
    /// instante caem na hora cheia (a conta de antes, byte a byte).</summary>
    public double ValueAtSecond(long second)
    {
        if (DecayRatePerHour <= 0)
            return Math.Clamp(ValueAtLastEvent, 0, Max);
        double value = ValueAtLastEvent - DecayRatePerHour * ElapsedHours(second - SecondOfLastEvent);
        return Math.Clamp(value, 0, Max);
    }

    /// <summary>Horas inteiras que a necessidade está em 0 na hora <paramref name="tick"/> — derivado da última escrita
    /// e da taxa (sem campo extra no estado). 0 se ainda não zerou ou se não decai. O zero é contado em horas inteiras depois
    /// da âncora, como sempre foi.</summary>
    public long TicksAtZero(long tick)
    {
        if (DecayRatePerHour <= 0 || ValueAt(tick) > 0) return 0;
        long zeroSecond = SecondOfLastEvent + WorldSeconds.FromHours((long)Math.Ceiling(ValueAtLastEvent / DecayRatePerHour));
        return Math.Max(0, WorldSeconds.ToHourFloor(WorldSeconds.FromHours(tick) - zeroSecond));
    }

    public LazyNeed WithValue(double value, long tick) => WithValueAtSecond(value, WorldSeconds.FromHours(tick));

    public LazyNeed WithValueAtSecond(double value, long second) =>
        new(Math.Clamp(value, 0, Max), second, DecayRatePerHour);

    public LazyNeed WithDecayRate(double decayRatePerHour, long tick) =>
        WithDecayRateAtSecond(decayRatePerHour, WorldSeconds.FromHours(tick));

    public LazyNeed WithDecayRateAtSecond(double decayRatePerHour, long second) =>
        new(ValueAtSecond(second), second, decayRatePerHour);

    public static LazyNeed Initial(int value, long tick, double decayRatePerHour) =>
        new(Math.Clamp(value, 0, Max), WorldSeconds.FromHours(tick), decayRatePerHour);

    /// <summary>Segundos em horas; na hora cheia a divisão inteira dá o mesmo número da conta antiga em ticks.</summary>
    private static double ElapsedHours(long seconds)
    {
        long hours = seconds / WorldSeconds.PerHour;
        return hours * WorldSeconds.PerHour == seconds ? hours : seconds / (double)WorldSeconds.PerHour;
    }
}
