using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Relógio do mundo (docs/domain/time-and-ticks.md): hora/dia/mês/ano do cenário,
/// nunca o relógio da máquina. Imutável; toda soma devolve uma nova data. Guardado em milissegundos inteiros
/// (<see cref="TotalMilliseconds"/>, ADR-0050); <see cref="TotalHours"/> é a hora do calendário em que o instante cai (piso).</summary>
[JsonConverter(typeof(WorldDateJsonConverter))]
public readonly struct WorldDate : IComparable<WorldDate>, IEquatable<WorldDate>
{
    public const int MillisecondsPerHour = WorldMillis.PerHour;

    // O instante guardado como (hora << 22) | ms dentro da hora (3.600.000 < 2^22): a hora — lida em toda decisão e todo sistema —
    // sai com um deslocamento, sem divisão, e a data segue com 16 bytes. O deslocamento aritmético dá o piso também antes da época,
    // e a ordem do número guardado é a ordem do tempo.
    private const int MsBits = 22;
    private const long MsMask = (1L << MsBits) - 1;
    private readonly long _packed;

    private WorldDate(WorldCalendar calendar, long totalMilliseconds)
    {
        Calendar = calendar;
        long hours = WorldMillis.ToHourFloor(totalMilliseconds);
        _packed = (hours << MsBits) | (totalMilliseconds - hours * MillisecondsPerHour);
    }

    private WorldDate(WorldCalendar calendar, long hours, bool fullHour)
    {
        _ = fullHour;
        Calendar = calendar;
        _packed = hours << MsBits;
    }

    public WorldCalendar Calendar { get; }

    /// <summary>Milissegundos de mundo desde a época (negativo antes dela: quem nasceu antes do começo).</summary>
    public long TotalMilliseconds => TotalHours * MillisecondsPerHour + MillisecondOfHour;

    /// <summary>A hora do calendário em que o instante cai (piso, também antes da época).</summary>
    public long TotalHours => _packed >> MsBits;

    /// <summary>Milissegundos passados desde o começo da hora (0 a 3.599.999).</summary>
    public int MillisecondOfHour => (int)(_packed & MsMask);

    public int Hour => (int)(TotalHours % Calendar.HoursPerDay);
    public int Day => (int)((TotalHours / Calendar.HoursPerDay) % Calendar.DaysPerMonth);
    public int Month => (int)((TotalHours / Calendar.HoursPerMonth) % Calendar.MonthsPerYear);
    public long Year => TotalHours / Calendar.HoursPerYear;

    public static WorldDate Epoch(WorldCalendar calendar) => new(calendar, 0L, fullHour: true);

    public static WorldDate FromMilliseconds(WorldCalendar calendar, long totalMilliseconds) => new(calendar, totalMilliseconds);

    /// <summary>O começo da hora <paramref name="totalHours"/> (o que era a data inteira antes do ADR-0050).</summary>
    public static WorldDate FromHours(WorldCalendar calendar, long totalHours) => new(calendar, totalHours, fullHour: true);

    public WorldDate AddMilliseconds(long milliseconds) => new(Calendar, TotalMilliseconds + milliseconds);
    public WorldDate AddHours(long hours) =>
        MillisecondOfHour == 0 ? new(Calendar, TotalHours + hours, fullHour: true) : new(Calendar, TotalMilliseconds + WorldMillis.FromHours(hours));
    public WorldDate AddDays(long days) => AddHours(days * Calendar.HoursPerDay);
    public WorldDate AddMonths(long months) => AddHours(months * Calendar.HoursPerMonth);
    public WorldDate AddYears(long years) => AddHours(years * Calendar.HoursPerYear);

    public int CompareTo(WorldDate other) => _packed.CompareTo(other._packed);
    public static bool operator <(WorldDate a, WorldDate b) => a._packed < b._packed;
    public static bool operator >(WorldDate a, WorldDate b) => a._packed > b._packed;
    public static bool operator <=(WorldDate a, WorldDate b) => a._packed <= b._packed;
    public static bool operator >=(WorldDate a, WorldDate b) => a._packed >= b._packed;

    public bool Equals(WorldDate other) =>
        _packed == other._packed && EqualityComparer<WorldCalendar>.Default.Equals(Calendar, other.Calendar);

    public override bool Equals(object? obj) => obj is WorldDate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Calendar, _packed);
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
