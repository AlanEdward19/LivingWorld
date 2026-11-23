namespace LivingWorld.Domain.Shared;

/// <summary>Conta de tempo de mundo em segundos inteiros (ADR-0050). A hora do calendário tem sempre 3.600 segundos; o que muda
/// por cenário é quantas horas tem o dia, o mês e o ano (<see cref="WorldCalendar"/>).</summary>
public static class WorldSeconds
{
    public const int PerHour = 3600;

    /// <summary>Duração de tick sem jogador (e a única de produção até a AD-197c): uma hora de mundo.</summary>
    public const int HourTick = PerHour;

    public static long FromHours(long hours) => checked(hours * PerHour);

    /// <summary>Hora em que o instante cai, com piso também para instantes antes da época.</summary>
    public static long ToHourFloor(long seconds)
    {
        if (seconds >= 0) return seconds / PerHour;
        long q = seconds / PerHour;
        return q * PerHour == seconds ? q : q - 1;
    }

    /// <summary>Hora cheia que fecha o intervalo onde o instante cai: <c>(h-1)·3600 &lt; s ≤ h·3600</c>. Um tick nunca atravessa
    /// a hora (<c>WorldClock</c>), então tudo que vence num tick cai na mesma hora de fecho.</summary>
    public static long ToHourCeiling(long seconds) => -ToHourFloor(-seconds);

    /// <summary>Δ aceito pelo relógio: divide a hora (1 s a 3.600 s), para que o tick caia exatamente em toda hora cheia e o
    /// despertar se arredonde ao passo sem conta de ponto flutuante nova.</summary>
    public static bool IsValidTickDelta(int deltaSeconds) => deltaSeconds is >= 1 and <= PerHour && PerHour % deltaSeconds == 0;
}
