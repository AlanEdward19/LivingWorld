namespace LivingWorld.Domain.Shared;

/// <summary>Conta de tempo de mundo em milissegundos inteiros (ADR-0050, decisão do dono de 2026-11-23: ms, para o tick fino de
/// ~50 ms com jogador caber no motor). A hora do calendário tem sempre 3.600.000 ms; o que muda por cenário é quantas horas tem o
/// dia, o mês e o ano (<see cref="WorldCalendar"/>).</summary>
public static class WorldMillis
{
    public const int PerSecond = 1000;
    public const int PerHour = 3_600_000;

    /// <summary>Duração de tick sem jogador (e a única de produção até a AD-197c): uma hora de mundo.</summary>
    public const int HourTick = PerHour;

    /// <summary>Começo da hora <paramref name="hours"/> em ms. Sem <c>checked</c>: está no caminho quente (todo agendamento e despertar)
    /// e só estoura em 2,5 trilhões de horas.</summary>
    public static long FromHours(long hours) => hours * PerHour;

    /// <summary>Hora em que o instante cai, com piso também para instantes antes da época.</summary>
    public static long ToHourFloor(long ms) => ms >= 0 ? ms / PerHour : (ms + 1) / PerHour - 1;

    /// <summary>Hora cheia que fecha o intervalo onde o instante cai: <c>(h-1)·hora &lt; ms ≤ h·hora</c>. Um tick nunca atravessa
    /// a hora (<c>WorldClock</c>), então tudo que vence num tick cai na mesma hora de fecho.</summary>
    public static long ToHourCeiling(long ms) => ms > 0 ? (ms - 1) / PerHour + 1 : -(-ms / PerHour);

    /// <summary>Δ aceito pelo relógio: divide a hora (1 ms a 3.600.000 ms; 50 ms divide), para que o tick caia exatamente em toda
    /// hora cheia.</summary>
    public static bool IsValidTickDelta(int deltaMs) => deltaMs is >= 1 and <= PerHour && PerHour % deltaMs == 0;
}
