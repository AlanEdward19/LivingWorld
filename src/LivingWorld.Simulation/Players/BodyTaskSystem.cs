using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#243: dono da capacidade BODY_TASK. A ação longa do corpo conduzido (cortar lenha...) rende item unidade a unidade (e, #255, o uso de objeto da casa: caminhar até a peça e a ação em fatias): cada unidade
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
        if (parts.Length == 3 && parts[2] == "u" && long.TryParse(parts[0], out long userId) && long.TryParse(parts[1], out long begun))
        {
            if (world.FindNpc(new NpcId(userId)) is { IsAlive: true } user) LivingWorld.Simulation.Objects.ObjectUsage.Step(world, ctx, user, begun); // #255: uso de objeto
            return;
        }

        if (parts.Length == 3 && parts[2] == "g" && long.TryParse(parts[0], out long walkerId) && long.TryParse(parts[1], out long began))
        {
            if (world.FindNpc(new NpcId(walkerId)) is { IsAlive: true } walker) PlayerGo.Step(world, ctx, walker, began); // #267: ir até
            return;
        }

        if (parts.Length == 3 && parts[2] == "d" && long.TryParse(parts[0], out long homeId) && int.TryParse(parts[1], out int parcel))
        {
            LivingWorld.Simulation.Objects.HomeFreight.Arrive(world, homeId, parcel); // #265: o entregador chegou com o móvel
            return;
        }

        if (parts.Length == 3 && parts[2] == "f" && long.TryParse(parts[0], out long driverId) && long.TryParse(parts[1], out long drove))
        {
            if (world.FindNpc(new NpcId(driverId)) is { IsAlive: true } driver) LivingWorld.Simulation.Objects.HomeFreight.DriveStep(world, ctx, driver, drove); // #265: o carro da casa anda
            return;
        }

        if (parts.Length == 3 && parts[2] == "s" && long.TryParse(parts[0], out long actorId) && long.TryParse(parts[1], out long met))
        {
            if (world.FindNpc(new NpcId(actorId)) is { IsAlive: true } actor) LivingWorld.Simulation.Objects.SocialInteractions.Step(world, ctx, actor, met); // #259: interação social
            return;
        }

        if (parts.Length != 2 || !long.TryParse(parts[0], out long npcId) || !long.TryParse(parts[1], out long started)) return;
        if (world.FindNpc(new NpcId(npcId)) is { IsAlive: true } npc) PlayerTasks.Settle(world, ctx, npc, started);
    }
}
