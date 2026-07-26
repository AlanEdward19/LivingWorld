namespace LivingWorld.Domain;

/// <summary>Relógio do mundo (docs/domain/time-and-ticks.md): hora/dia/mês/ano do cenário,
/// nunca o relógio da máquina. Imutável; toda soma devolve uma nova data.</summary>
public readonly record struct WorldDate(WorldCalendar Calendar, long TotalHours) : IComparable<WorldDate>
{
    public int Hour => (int)(TotalHours % Calendar.HoursPerDay);
    public int Day => (int)((TotalHours / Calendar.HoursPerDay) % Calendar.DaysPerMonth);
    public int Month => (int)((TotalHours / Calendar.HoursPerMonth) % Calendar.MonthsPerYear);
    public long Year => TotalHours / Calendar.HoursPerYear;

    public static WorldDate Epoch(WorldCalendar calendar) => new(calendar, 0);

    public WorldDate AddHours(long hours) => new(Calendar, TotalHours + hours);
    public WorldDate AddDays(long days) => AddHours(days * Calendar.HoursPerDay);
    public WorldDate AddMonths(long months) => AddHours(months * Calendar.HoursPerMonth);
    public WorldDate AddYears(long years) => AddHours(years * Calendar.HoursPerYear);

    public int CompareTo(WorldDate other) => TotalHours.CompareTo(other.TotalHours);
    public static bool operator <(WorldDate a, WorldDate b) => a.TotalHours < b.TotalHours;
    public static bool operator >(WorldDate a, WorldDate b) => a.TotalHours > b.TotalHours;
    public static bool operator <=(WorldDate a, WorldDate b) => a.TotalHours <= b.TotalHours;
    public static bool operator >=(WorldDate a, WorldDate b) => a.TotalHours >= b.TotalHours;

    public override string ToString() => $"Y{Year}M{Month}D{Day}H{Hour}";
}
