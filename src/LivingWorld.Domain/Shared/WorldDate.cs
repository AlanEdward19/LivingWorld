using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Relógio do mundo (docs/domain/time-and-ticks.md): hora/dia/mês/ano do cenário,
/// nunca o relógio da máquina. Imutável; toda soma devolve uma nova data. Guardado em segundos inteiros
/// (<see cref="TotalSeconds"/>, ADR-0050); <see cref="TotalHours"/> é a hora do calendário em que o instante cai (piso).</summary>
[JsonConverter(typeof(WorldDateJsonConverter))]
public readonly struct WorldDate : IComparable<WorldDate>, IEquatable<WorldDate>
{
    public const int SecondsPerHour = WorldSeconds.PerHour;

    private WorldDate(WorldCalendar calendar, long totalSeconds)
    {
        Calendar = calendar;
        TotalSeconds = totalSeconds;
        _totalHours = WorldSeconds.ToHourFloor(totalSeconds);
    }

    // A hora é lida em todo sistema e toda decisão: guardada junto, para não dividir a cada leitura (custo por NPC-tick).
    private readonly long _totalHours;

    public WorldCalendar Calendar { get; }

    /// <summary>Segundos de mundo desde a época (negativo antes dela: quem nasceu antes do começo).</summary>
    public long TotalSeconds { get; }

    /// <summary>A hora do calendário em que o instante cai (piso, também antes da época).</summary>
    public long TotalHours => _totalHours;

    /// <summary>Segundos passados desde o começo da hora (0 a 3.599).</summary>
    public int SecondOfHour => (int)(TotalSeconds - _totalHours * SecondsPerHour);

    public int Hour => (int)(TotalHours % Calendar.HoursPerDay);
    public int Day => (int)((TotalHours / Calendar.HoursPerDay) % Calendar.DaysPerMonth);
    public int Month => (int)((TotalHours / Calendar.HoursPerMonth) % Calendar.MonthsPerYear);
    public long Year => TotalHours / Calendar.HoursPerYear;

    public static WorldDate Epoch(WorldCalendar calendar) => new(calendar, 0);

    public static WorldDate FromSeconds(WorldCalendar calendar, long totalSeconds) => new(calendar, totalSeconds);

    /// <summary>O começo da hora <paramref name="totalHours"/> (o que era a data inteira antes do ADR-0050).</summary>
    public static WorldDate FromHours(WorldCalendar calendar, long totalHours) =>
        new(calendar, WorldSeconds.FromHours(totalHours));

    public WorldDate AddSeconds(long seconds) => new(Calendar, TotalSeconds + seconds);
    public WorldDate AddHours(long hours) => new(Calendar, TotalSeconds + WorldSeconds.FromHours(hours));
    public WorldDate AddDays(long days) => AddHours(days * Calendar.HoursPerDay);
    public WorldDate AddMonths(long months) => AddHours(months * Calendar.HoursPerMonth);
    public WorldDate AddYears(long years) => AddHours(years * Calendar.HoursPerYear);

    public int CompareTo(WorldDate other) => TotalSeconds.CompareTo(other.TotalSeconds);
    public static bool operator <(WorldDate a, WorldDate b) => a.TotalSeconds < b.TotalSeconds;
    public static bool operator >(WorldDate a, WorldDate b) => a.TotalSeconds > b.TotalSeconds;
    public static bool operator <=(WorldDate a, WorldDate b) => a.TotalSeconds <= b.TotalSeconds;
    public static bool operator >=(WorldDate a, WorldDate b) => a.TotalSeconds >= b.TotalSeconds;

    public bool Equals(WorldDate other) =>
        TotalSeconds == other.TotalSeconds && EqualityComparer<WorldCalendar>.Default.Equals(Calendar, other.Calendar);

    public override bool Equals(object? obj) => obj is WorldDate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Calendar, TotalSeconds);
    public static bool operator ==(WorldDate a, WorldDate b) => a.Equals(b);
    public static bool operator !=(WorldDate a, WorldDate b) => !a.Equals(b);

    /// <summary>Hora cheia: o texto de antes (<c>Y1M2D3H4</c>); dentro da hora acrescenta minuto e segundo.</summary>
    public override string ToString()
    {
        string hour = $"Y{Year}M{Month}D{Day}H{Hour}";
        int s = SecondOfHour;
        return s == 0 ? hour : $"{hour}m{s / 60}s{s % 60}";
    }
}
