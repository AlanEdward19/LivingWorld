using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Opportunity;

/// <summary>
/// Incrementa o contador de uso por invocação bem-sucedida (EVO-04). Falhas nunca contam.
/// </summary>
public static class PowerUseCounter
{
    public static void RecordSuccessfulUse(WorldState world, NpcId carrierId)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == carrierId);
        if (carrier is null) return;

        world.UpsertExtraordinaryCarrier(carrier with { UseCount = carrier.UseCount + 1 });
    }
}
