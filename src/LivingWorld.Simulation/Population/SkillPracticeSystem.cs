using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Ganho de habilidade por prática no trabalho (Fase 6, T8, SKILL-03) — único ponto
/// que lê <see cref="Npc.RateGene"/> pra fonte <see cref="SkillGainSource.Practice"/>. <c>Daily</c>
/// (design.md): roda logo depois de <see cref="EmploymentSystem"/> e antes de <see
/// cref="ProductionSystem"/>, pra que a produção do mesmo dia já leia a habilidade
/// atualizada.</summary>
public sealed class SkillPracticeSystem : ISimulationSystem
{
    public const string SystemName = "population-skill-practice";

    private readonly SkillsRules _rules;

    public SkillPracticeSystem(SkillsRules rules) => _rules = rules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    /// <summary>NPC empregado, em ação Work, presente no próprio <see cref="Workplace"/> ganha a
    /// habilidade mapeada pela profissão (<see cref="SkillsRules.SkillByProfession"/>). Sem
    /// mapeamento, sem emprego, ação diferente de Work, ou ausente do local — sem-op, sem
    /// exceção (mesmo padrão de <see cref="ProductionSystem"/> pulando Workplace sem recipe).</summary>
    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (!npc.IsAlive) continue;
            if (npc.Employer is not { } employerId) continue;
            if (npc.CurrentAction != ActionType.Work) continue;
            if (world.FindWorkplace(employerId) is not { } workplace) continue;
            if (npc.CurrentLocation != workplace.Location) continue;
            if (!_rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) continue;

            double gain = _rules.Gain(npc.Skills.Get(skillType), SkillGainSource.Practice, npc.RateGene.Value);
            npc.GainSkill(skillType, gain, _rules.Cap);
        }
    }
}
