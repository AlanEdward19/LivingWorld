using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Mata um NPC e limpa a referência de household — único ponto que faz as duas coisas
/// juntas, reusado por todo sistema que pode matar um NPC (<see cref="MortalitySystem"/>,
/// <see cref="NeedsDecaySystem"/>). Antes da Fase 4 só a mortalidade por idade matava NPC;
/// morte por fome sustentada chamava <c>Npc.Die</c> direto e pulava a limpeza de household —
/// achado pelo mesmo tipo de invariante que o sweep referencial (task 12, Fase 3) já cobria
/// para dissolução. Corrigido na raiz (função compartilhada) em vez de duplicar a limpeza em
/// cada sistema que mata NPC.</summary>
public static class NpcDeath
{
    public static void Apply(WorldState world, TickContext ctx, Npc npc, WorldEventKind kind)
    {
        npc.Die(world.CurrentDate);
        ctx.LogEvent(kind, npc.Id.Value.ToString());

        if (npc.Household is not { } householdId) return;

        var household = world.FindHousehold(householdId);
        household?.RemoveMember(npc.Id);
        if (household is not { IsEmpty: true }) return;

        // Fase 5 (T24, ECON-15): estoque residual do household dissolvido não pode só desaparecer
        // da conta — mesma disciplina de Workplace.Deposit (excedente é perda registrada, nunca
        // sumiço silencioso). Sem isso, a invariante de conservação de recurso vaza um pouco a
        // cada dissolução (achado rodando o cenário default de ponta a ponta em 10 anos).
        foreach (var (resource, amount) in household.Stock)
            if (amount > 0)
                ctx.LogEvent(WorldEventKind.ResourceLost, $"{household.Id.Value}|{resource.Id}|{amount}");

        world.RemoveHousehold(householdId);
        // Household deixou de existir — nenhuma referência pode sobreviver a ele, nem a de NPCs
        // mortos anteriormente que já tinham saído da lista de membros (sweep referencial, task 12).
        foreach (var member in world.Npcs)
            if (member.Household == householdId)
                member.LeaveHousehold(world.CurrentDate);
    }
}
