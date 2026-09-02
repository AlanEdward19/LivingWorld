using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Systems;

/// <summary>
/// Reinvoca poderes <c>Mode=Passive</c> a cada tick Hourly enquanto manifestados.
/// Custo insuficiente pula o tick (log causal do motor) sem revogar o poder.
/// </summary>
public sealed class ExtraordinaryPassiveTickSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryPassiveTick";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.Extraordinary.Enabled) return;

        foreach (var carrierState in world.ExtraordinaryCarriers.OrderBy(item => item.CarrierId.Value).ToList())
        {
            if (!carrierState.IsManifested) continue;
            if (world.FindNpc(carrierState.CarrierId) is not { IsAlive: true } npc) continue;

            foreach (var powerId in carrierState.PowerIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                var descriptor = world.Extraordinary.Descriptors.FirstOrDefault(
                    item => string.Equals(item.Id, powerId, StringComparison.Ordinal));
                if (descriptor is null || descriptor.Mode != "Passive") continue;
                if (!ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, npc)
                    && !BondMechanic.HasOathEffect(descriptor))
                    continue;

                _ = ExtraordinaryInvocationEngine.Invoke(
                    world, ctx,
                    new ExtraordinaryInvocation(
                        world.NextEventIdAndAdvance(),
                        carrierState.CarrierId,
                        powerId,
                        carrierState.CarrierId,
                        Origin: ExtraordinaryInvocationOrigin.Triggered));
            }
        }
    }
}
