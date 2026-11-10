using LivingWorld.Domain.Behavior;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>O que determinou o próximo despertar de um NPC (o menor dos candidatos de
/// <see cref="NpcWakeScheduler.ComputeNextWakeTick"/>; no empate vale o primeiro desta ordem).</summary>
public enum WakeCause
{
    /// <summary>Uma necessidade vai cruzar o limiar de urgência num tick futuro.</summary>
    NeedCrossing,

    /// <summary>Uma necessidade já está além do limiar e a ação em curso não a atende: reavalia na hora seguinte.</summary>
    NeedAlreadyUrgent,
    ActionEnd,
    RoutineBoundary,
    TravelArrival,
    Attention,

    /// <summary>O candidato vencedor (fim de ação, fronteira, chegada) já passou: reavalia na hora seguinte.</summary>
    Overdue,

    /// <summary>Nenhum candidato: reavalia na hora seguinte.</summary>
    Fallback,
}

/// <summary>Contadores de trabalho do despertar por evento (#152): quantos NPCs acordaram, quantos acordaram sem trocar de
/// ação e o que agendou cada despertar. Só leitura para sensores e perfil; nenhuma decisão lê isto, não entra no hash
/// nem no snapshot.</summary>
public sealed class NpcWakeCounters
{
    private static readonly int Causes = Enum.GetValues<WakeCause>().Length;

    // Uma coluna por ação em curso, mais a última para "sem ação".
    private static readonly int Actions = Enum.GetValues<ActionType>().Max(a => (int)a) + 2;

    private readonly long[] _scheduledBy = new long[Causes];
    private readonly long[] _scheduledByAction = new long[Causes * Actions];

    /// <summary>Despertares processados por <c>BehaviorDecisionSystem</c>.</summary>
    public long Wakes { get; private set; }

    /// <summary>Despertares em que a ação em curso continuou a mesma, sem ser concluída nem reiniciada.</summary>
    public long WakesWithoutEffect { get; private set; }

    public long ScheduledBy(WakeCause cause) => _scheduledBy[(int)cause];

    /// <summary>Agendamentos por causa e pela ação que o NPC tinha em curso (<c>null</c> = nenhuma).</summary>
    public long ScheduledBy(WakeCause cause, ActionType? during) => _scheduledByAction[Index(cause, during)];

    private static int Index(WakeCause cause, ActionType? during) =>
        (int)cause * Actions + (during is { } action ? (int)action : Actions - 1);

    internal void RecordWake(bool withoutEffect)
    {
        Wakes++;
        if (withoutEffect) WakesWithoutEffect++;
    }

    internal void RecordScheduled(WakeCause cause, ActionType? during)
    {
        _scheduledBy[(int)cause]++;
        _scheduledByAction[Index(cause, during)]++;
    }

    public void Reset()
    {
        Wakes = 0;
        WakesWithoutEffect = 0;
        Array.Clear(_scheduledBy);
        Array.Clear(_scheduledByAction);
    }
}
