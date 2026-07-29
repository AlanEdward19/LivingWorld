namespace LivingWorld.Domain;

/// <summary>Necessidade com decaimento linear lazy (Fase 9, PERF-09) — valor materializado só
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

    public LazyNeed WithValue(double value, long tick) =>
        new(Math.Clamp(value, 0, Max), tick, DecayRatePerTick);

    public LazyNeed WithDecayRate(double decayRatePerTick, long tick) =>
        new(ValueAt(tick), tick, decayRatePerTick);

    public static LazyNeed Initial(int value, long tick, double decayRatePerTick) =>
        new(Math.Clamp(value, 0, Max), tick, decayRatePerTick);
}
