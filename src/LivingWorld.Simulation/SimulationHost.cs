namespace LivingWorld.Simulation;

/// <summary>Controles do hospedeiro (task 6): pausa, velocidade, avanço rápido. Nada aqui é
/// estado do mundo — por isso não aparece em <see cref="WorldState"/> nem no snapshot.</summary>
public sealed class SimulationHost(WorldHost host)
{
    public bool IsPaused { get; private set; }
    public double TicksPerSecond { get; private set; } = 1.0;

    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;

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
