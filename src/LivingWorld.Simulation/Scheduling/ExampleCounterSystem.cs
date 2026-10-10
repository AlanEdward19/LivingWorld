using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Sistema placeholder/exemplo: só conta quantas vezes rodou em cada frequência.
/// Serve para provar o agendamento e o determinismo do scheduler.</summary>
public sealed class ExampleCounterSystem(TickFrequency frequency) : ISimulationSystem
{
    public string Name => $"example-counter-{frequency}";
    public TickFrequency Frequency => frequency;

    public void Tick(WorldState world, TickContext ctx) => world.IncrementExampleCount(frequency);
}
