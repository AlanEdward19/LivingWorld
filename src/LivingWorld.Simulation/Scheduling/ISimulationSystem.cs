using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>Um sistema de simulação. Registrado por frequência (rules/implementation.md);
/// não chama outro sistema direto — comunica por evento agendado (<see cref="TickContext"/>).</summary>
public interface ISimulationSystem
{
    string Name { get; }
    TickFrequency Frequency { get; }

    void Tick(WorldState world, TickContext ctx);

    /// <summary>Chamado quando um evento agendado por este sistema (via <see cref="ScheduledEvent.SystemName"/>)
    /// vence. Sem-op por padrão — nem todo sistema agenda eventos futuros.</summary>
    void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
    }
}
