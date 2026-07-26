using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Sistema trivial (task 11): só conta quantas vezes rodou em cada frequência.
/// Prova agendamento e determinismo — descartável quando a Fase 3 chegar com sistemas reais.</summary>
public sealed class ExampleCounterSystem(TickFrequency frequency) : ISimulationSystem
{
    public string Name => $"example-counter-{frequency}";
    public TickFrequency Frequency => frequency;

    public void Tick(WorldState world, TickContext ctx) => world.IncrementExampleCount(frequency);
}
