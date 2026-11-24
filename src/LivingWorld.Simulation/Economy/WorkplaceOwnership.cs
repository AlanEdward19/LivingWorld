using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Economy;

/// <summary>Quem é dono das oficinas que já existem quando o mundo começa (AD-144). Regra do mundo, não nome escrito: a oficina
/// de ofício (tipo de local em <c>craftLocationTypeIds</c>, como a ferraria) é do mestre do ofício na cidade dela, o adulto
/// daquela profissão com mais habilidade no ofício (empate: o mais velho, depois o menor id), como a oficina de um mestre
/// artesão medieval. O que não é ofício (a terra da fazenda) fica sem dono: é terra comum do senhorio/comuna, e o excedente
/// dela vai ao tesouro da cidade. Sem ninguém da profissão, a oficina também fica sem dono. Dono declarado no cenário vence.</summary>
public static class WorkplaceOwnership
{
    public static void AssignFoundingMasters(WorldState world, IReadOnlyCollection<int> craftLocationTypeIds)
    {
        if (craftLocationTypeIds.Count == 0) return;
        var skills = ScenarioRunner.DefaultSkillsRules;
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (workplace.Owner is not null || !craftLocationTypeIds.Contains(workplace.LocationType.Id)) continue;

            Npc? master = null;
            double best = -1;
            foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
            {
                if (!npc.IsAlive || npc.City != workplace.City) continue;
                if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Child) continue;
                if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out var type)
                    || type != workplace.LocationType.Id) continue;

                double skill = skills.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType) ? npc.Skills.Get(skillType) : 0;
                bool better = skill > best || (skill == best && master is not null && npc.BirthDate.TotalHours < master.BirthDate.TotalHours);
                if (!better) continue;
                master = npc;
                best = skill;
            }

            workplace.AssignOwner(master?.Id);
        }
    }
}
