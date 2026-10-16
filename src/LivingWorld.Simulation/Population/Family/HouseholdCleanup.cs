using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Dissolução de <see cref="Household"/> vazio — extraído de <see cref="NpcDeath.Apply"/>
/// para reuso por morte, casamento e redistribuição de órfãos.</summary>
public static class HouseholdCleanup
{
    /// <summary>Se <paramref name="household"/> ainda tem membros, não faz nada. Caso contrário,
    /// registra estoque residual como <see cref="WorldEventKind.ResourceLost"/>, remove o household
    /// do mundo e limpa referências órfãs em NPCs.</summary>
    public static void DissolveIfEmpty(WorldState world, TickContext ctx, Household household)
    {
        if (!household.IsEmpty) return;

        foreach (var (resource, amount) in household.Stock)
            if (amount > 0)
                ctx.LogEvent(WorldEventKind.ResourceLost, $"{household.Id.Value}|{resource.Id}|{amount}", sourceSystem: "HouseholdCleanup");

        world.RemoveHousehold(household.Id);
        foreach (var member in world.Npcs)
            if (member.Household == household.Id)
                member.LeaveHousehold(world.CurrentDate);
    }
}
