using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Dissolução de <see cref="Household"/> vazio — extraído de <see cref="NpcDeath.Apply"/>
/// (Fase 7, T12) para reuso por morte, casamento e redistribuição de órfãos.</summary>
public static class HouseholdCleanup
{
    /// <summary>Se <paramref name="household"/> ainda tem membros, não faz nada. Caso contrário,
    /// registra estoque residual como <see cref="WorldEventKind.ResourceLost"/>, remove o household
    /// do mundo e limpa referências órfãs em NPCs (mesma disciplina de Fase 5/3).</summary>
    public static void DissolveIfEmpty(WorldState world, TickContext ctx, Household household)
    {
        if (!household.IsEmpty) return;

        foreach (var (resource, amount) in household.Stock)
            if (amount > 0)
                ctx.LogEvent(WorldEventKind.ResourceLost, $"{household.Id.Value}|{resource.Id}|{amount}");

        world.RemoveHousehold(household.Id);
        foreach (var member in world.Npcs)
            if (member.Household == household.Id)
                member.LeaveHousehold(world.CurrentDate);
    }
}
