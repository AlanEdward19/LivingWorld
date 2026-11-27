using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#243: dono da capacidade BODY_TASK. A ação longa do corpo conduzido (cortar lenha...) rende item unidade a unidade: cada unidade
/// é um evento agendado no instante em que sai (<see cref="PlayerTasks.Settle"/>), também dentro da hora e nos ticks de uma hora do salto de
/// tempo. Nada por tick: sem ação em andamento, nada roda.</summary>
[LivingCapability("BODY_TASK", Events = [], Consumers = ["economy.stock", "nature.trees"])]
public sealed class BodyTaskSystem : ISimulationSystem
{
    public const string SystemName = "body-task";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (evt.Payload is not { } payload) return;
        var parts = payload.Split('|');
        if (parts.Length != 2 || !long.TryParse(parts[0], out long npcId) || !long.TryParse(parts[1], out long started)) return;
        if (world.FindNpc(new NpcId(npcId)) is { IsAlive: true } npc) PlayerTasks.Settle(world, ctx, npc, started);
    }
}
