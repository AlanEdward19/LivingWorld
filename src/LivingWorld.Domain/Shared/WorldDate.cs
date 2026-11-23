using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Relógio do mundo (docs/domain/time-and-ticks.md): hora/dia/mês/ano do cenário,
/// nunca o relógio da máquina. Imutável; toda soma devolve uma nova data. Guardado em milissegundos inteiros
/// (<see cref="TotalMilliseconds"/>, ADR-0050); <see cref="TotalHours"/> é a hora do calendário em que o instante cai (piso).</summary>
[JsonConverter(typeof(WorldDateJsonConverter))]
public readonly struct WorldDate : IComparable<WorldDate>, IEquatable<WorldDate>
{
    public const int MillisecondsPerHour = WorldMillis.PerHour;

    private WorldDate(WorldCalendar calendar, long totalMilliseconds)
    {
        Calendar = calendar;
        TotalMilliseconds = totalMilliseconds;
    }

    public WorldCalendar Calendar { get; }

    /// <summary>Milissegundos de mundo desde a época (negativo antes dela: quem nasceu antes do começo).</summary>
    public long TotalMilliseconds { get; }

    /// <summary>A hora do calendário em que o instante cai (piso, também antes da época).</summary>
    public long TotalHours => WorldMillis.ToHourFloor(TotalMilliseconds);

    /// <summary>Milissegundos passados desde o começo da hora (0 a 3.599.999).</summary>
    public int MillisecondOfHour => (int)(TotalMilliseconds - TotalHours * MillisecondsPerHour);

    public int Hour => (int)(TotalHours % Calendar.HoursPerDay);
    public int Day => (int)((TotalHours / Calendar.HoursPerDay) % Calendar.DaysPerMonth);
    public int Month => (int)((TotalHours / Calendar.HoursPerMonth) % Calendar.MonthsPerYear);
    public long Year => TotalHours / Calendar.HoursPerYear;

    public static WorldDate Epoch(WorldCalendar calendar) => new(calendar, 0);

    public static WorldDate FromMilliseconds(WorldCalendar calendar, long totalMilliseconds) => new(calendar, totalMilliseconds);

    /// <summary>O começo da hora <paramref name="totalHours"/> (o que era a data inteira antes do ADR-0050).</summary>
    public static WorldDate FromHours(WorldCalendar calendar, long totalHours) =>
        new(calendar, WorldMillis.FromHours(totalHours));

    public WorldDate AddMilliseconds(long milliseconds) => new(Calendar, TotalMilliseconds + milliseconds);
    public WorldDate AddHours(long hours) => new(Calendar, TotalMilliseconds + WorldMillis.FromHours(hours));
    public WorldDate AddDays(long days) => AddHours(days * Calendar.HoursPerDay);
    public WorldDate AddMonths(long months) => AddHours(months * Calendar.HoursPerMonth);
    public WorldDate AddYears(long years) => AddHours(years * Calendar.HoursPerYear);

    public int CompareTo(WorldDate other) => TotalMilliseconds.CompareTo(other.TotalMilliseconds);
    public static bool operator <(WorldDate a, WorldDate b) => a.TotalMilliseconds < b.TotalMilliseconds;
    public static bool operator >(WorldDate a, WorldDate b) => a.TotalMilliseconds > b.TotalMilliseconds;
    public static bool operator <=(WorldDate a, WorldDate b) => a.TotalMilliseconds <= b.TotalMilliseconds;
    public static bool operator >=(WorldDate a, WorldDate b) => a.TotalMilliseconds >= b.TotalMilliseconds;

    public bool Equals(WorldDate other) =>
        TotalMilliseconds == other.TotalMilliseconds && EqualityComparer<WorldCalendar>.Default.Equals(Calendar, other.Calendar);

    public override bool Equals(object? obj) => obj is WorldDate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Calendar, TotalMilliseconds);
    public static bool operator ==(WorldDate a, WorldDate b) => a.Equals(b);
    public static bool operator !=(WorldDate a, WorldDate b) => !a.Equals(b);

    /// <summary>Hora cheia: o texto de antes (<c>Y1M2D3H4</c>); dentro da hora acrescenta minuto, segundo e milissegundo.</summary>
    public override string ToString()
    {
        string hour = $"Y{Year}M{Month}D{Day}H{Hour}";
        int ms = MillisecondOfHour;
        if (ms == 0) return hour;
        int seconds = ms / WorldMillis.PerSecond;
        string text = $"{hour}m{seconds / 60}s{seconds % 60}";
        return ms % WorldMillis.PerSecond == 0 ? text : $"{text}ms{ms % WorldMillis.PerSecond}";
    }
}
