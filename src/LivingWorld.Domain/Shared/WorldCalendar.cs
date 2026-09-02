namespace LivingWorld.Domain;

/// <summary>Calendário de um cenário: nada no motor assume o calendário gregoriano
/// (docs/domain/time-and-ticks.md). Um tick do motor equivale sempre a 1 hora do calendário.</summary>
public sealed record WorldCalendar(int HoursPerDay, int DaysPerMonth, int MonthsPerYear)
{
    public long HoursPerMonth => (long)HoursPerDay * DaysPerMonth;
    public long HoursPerYear => HoursPerMonth * MonthsPerYear;
}
