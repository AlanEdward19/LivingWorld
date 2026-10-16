using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Exposição do mês (Fase 21b), derivada do que já existe: a criança convive com ao menos um adulto vivo do próprio domicílio
/// (<c>Household.Members</c>, Fase 7). Sem domicílio ou sem adulto, não há cuidador e não há exposição. É binária de propósito:
/// graus de exposição (fala dirigida, brincadeira, ensino) dependem de ações de rotina que ainda não existem e não entram aqui.
/// A 21c lê também <b>quem</b> são: o idioma de cada cuidador alimenta a fluência da criança.</summary>
public static class ExposureAccumulator
{
    /// <summary>Idiomas (cultura) de cada adulto vivo do domicílio, um por cuidador, em ordem de id. Vazio = sem exposição.</summary>
    public static IReadOnlyList<int> CaregiverLanguages(WorldState world, Npc child)
    {
        if (child.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return [];
        List<int>? languages = null;
        foreach (var memberId in household.Members.OrderBy(m => m.Value))
        {
            if (memberId == child.Id) continue;
            if (world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            if (world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            (languages ??= []).Add(OntogenyMechanic.SpokenCulture(member).Id);
        }
        return languages ?? [];
    }

    public static bool HasCaregiver(WorldState world, Npc child) => CaregiverLanguages(world, child).Count > 0;
}
