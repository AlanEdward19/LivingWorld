namespace LivingWorld.Domain.Shared;

/// <summary>Calendário de um cenário: nada no motor assume o calendário gregoriano
/// (docs/domain/time-and-ticks.md). A hora tem sempre 3.600 segundos de mundo; o tick avança Δ segundos (ADR-0050).</summary>
public sealed record WorldCalendar(int HoursPerDay, int DaysPerMonth, int MonthsPerYear)
{
    public long HoursPerMonth => (long)HoursPerDay * DaysPerMonth;
    public long HoursPerYear => HoursPerMonth * MonthsPerYear;
}
