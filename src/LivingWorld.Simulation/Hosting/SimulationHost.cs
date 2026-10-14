using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Hosting;

/// <summary>Controles do hospedeiro: pausa, velocidade, avanço rápido. Nada aqui é
/// estado do mundo — por isso não aparece em <see cref="WorldState"/> nem no snapshot.</summary>
public sealed class SimulationHost(WorldHost host)
{
    /// <summary>Teto de uma pausa de admin: 30 dias simulados (ADR-0015, risco de admin distraído congelar o mundo).</summary>
    public const int MaxAdminPauseDays = 30;

    /// <summary>Teto padrão quando o admin não informa: 7 dias simulados.</summary>
    public const int DefaultAdminPauseDays = 7;

    public bool IsPaused { get; private set; }
    public double TicksPerSecond { get; private set; } = 1.0;

    /// <summary>Pausa de admin em vigor (quem, por quê, desde quando, até quantos ciclos do relógio); nulo na pausa comum.</summary>
    public AdminPauseState? AdminPause { get; private set; }

    /// <summary>Chamado quando o teto vence e a simulação retoma sozinha (a auditoria registra o fato).</summary>
    public Action<AdminPauseState>? OnPauseExpired { get; set; }

    private long _pausedCycles;

    public void Pause()
    {
        IsPaused = true;
        AdminPause = null;
    }

    public void Resume()
    {
        IsPaused = false;
        AdminPause = null;
        _pausedCycles = 0;
    }

    /// <summary>Pausa com teto: retoma sozinha depois de <see cref="AdminPauseState.MaxTicks"/> ciclos do relógio de tempo real
    /// (os ciclos que o mundo andaria, não fosse a pausa).</summary>
    public void PauseFor(string actor, string reason, long maxTicks, long worldTick)
    {
        IsPaused = true;
        _pausedCycles = 0;
        AdminPause = new AdminPauseState(actor, reason, worldTick, maxTicks);
    }

    /// <summary>Um ciclo do relógio de tempo real passou com a simulação pausada. Devolve verdadeiro se o teto venceu e retomou.</summary>
    public bool CountPausedCycle()
    {
        if (!IsPaused || AdminPause is not { } pause || ++_pausedCycles < pause.MaxTicks) return false;

        Resume();
        OnPauseExpired?.Invoke(pause);
        return true;
    }

    public void SetSpeed(double ticksPerSecond)
    {
        if (ticksPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Velocidade deve ser positiva.");
        TicksPerSecond = ticksPerSecond;
    }

    /// <summary>Avanço rápido: roda N ticks imediatamente, sem esperar tempo real e
    /// independente de pausa — é uma ação explícita do host, não o loop de tempo real.</summary>
    public void FastForward(long ticks) => host.Clock.Run(host.Current, ticks);

    /// <summary>Avança exatamente a duração de um ano do calendário do mundo atual.</summary>
    public void FastForwardOneYear() => FastForward(host.Current.Calendar.HoursPerYear);
}

/// <summary>Pausa global de admin (ADR-0015): estado do hospedeiro, fora do snapshot e do hash.</summary>
public sealed record AdminPauseState(string Actor, string Reason, long PausedAtTick, long MaxTicks);
