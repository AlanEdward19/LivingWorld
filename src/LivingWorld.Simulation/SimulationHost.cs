namespace LivingWorld.Simulation;

/// <summary>Controles do hospedeiro (task 6): pausa, velocidade, avanço rápido. Nada aqui é
/// estado do mundo — por isso não aparece em <see cref="WorldState"/> nem no snapshot.</summary>
public sealed class SimulationHost(WorldClock clock, WorldState world)
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
    public void FastForward(long ticks) => clock.Run(world, ticks);
}
