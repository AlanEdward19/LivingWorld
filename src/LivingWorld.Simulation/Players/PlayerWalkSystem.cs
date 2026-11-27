using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#248: dono da capacidade BODY_WALK. O corpo conduzido com rumo mantido anda a cada tick fino (<see cref="PlayerWalk.Pump"/>),
/// sem depender da chegada de comando: o passo é um evento agendado por corpo, só enquanto o jogador segura o rumo. Sem jogador
/// andando, nada roda; o custo por NPC-tick não muda.</summary>
[LivingCapability("BODY_WALK", Events = [], Consumers = ["player.pose"])]
public sealed class PlayerWalkSystem : ISimulationSystem
{
    public string Name => PlayerWalk.PumpSystem;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (evt.Payload is not { } payload) return;
        var parts = payload.Split('|');
        if (parts.Length == 2 && long.TryParse(parts[0], out long npcId) && long.TryParse(parts[1], out long at)) PlayerWalk.Pump(world, ctx, npcId, at);
    }
}
