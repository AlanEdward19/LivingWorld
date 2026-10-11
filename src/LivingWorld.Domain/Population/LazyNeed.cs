namespace LivingWorld.Domain.Population;

/// <summary>Necessidade com decaimento linear lazy — valor materializado só
/// em <see cref="ValueAt"/>, nunca escrito por tick.</summary>
public readonly record struct LazyNeed(double ValueAtLastEvent, long TickOfLastEvent, double DecayRatePerTick)
{
    public const double Max = 100.0;

    public double ValueAt(long tick)
    {
        if (DecayRatePerTick <= 0)
            return Math.Clamp(ValueAtLastEvent, 0, Max);
        double value = ValueAtLastEvent - DecayRatePerTick * (tick - TickOfLastEvent);
        return Math.Clamp(value, 0, Max);
    }

    /// <summary>Ticks que a necessidade está em 0 em <paramref name="tick"/> — derivado da última escrita
    /// e da taxa (sem campo extra no estado). 0 se ainda não zerou ou se não decai.</summary>
    public long TicksAtZero(long tick)
    {
        if (DecayRatePerTick <= 0 || ValueAt(tick) > 0) return 0;
        long zeroTick = TickOfLastEvent + (long)Math.Ceiling(ValueAtLastEvent / DecayRatePerTick);
        return Math.Max(0, tick - zeroTick);
    }

    public LazyNeed WithValue(double value, long tick) =>
        new(Math.Clamp(value, 0, Max), tick, DecayRatePerTick);

    public LazyNeed WithDecayRate(double decayRatePerTick, long tick) =>
        new(ValueAt(tick), tick, decayRatePerTick);

    public static LazyNeed Initial(int value, long tick, double decayRatePerTick) =>
        new(Math.Clamp(value, 0, Max), tick, decayRatePerTick);
}
