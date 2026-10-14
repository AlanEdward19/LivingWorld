using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Exposição do mês (Fase 21b), derivada do que já existe: a criança convive com ao menos um adulto vivo do próprio domicílio
/// (<c>Household.Members</c>, Fase 7). Sem domicílio ou sem adulto, não há cuidador e não há exposição. É binária de propósito:
/// graus de exposição (fala dirigida, brincadeira, ensino) dependem de ações de rotina que ainda não existem e não entram aqui.</summary>
public static class ExposureAccumulator
{
    public static bool HasCaregiver(WorldState world, Npc child)
    {
        if (child.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return false;
        foreach (var memberId in household.Members)
        {
            if (memberId == child.Id) continue;
            if (world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            if (world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) == LifeStage.Adult) return true;
        }
        return false;
    }
}
