using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>Fase 6, T13: rede de segurança de cobertura (mesmo padrão de
/// <c>PersonalityWeightingTests.Every_personality_trait_has_at_least_one_influence_table_entry</c>) —
/// reprova se alguma <c>ProfessionType</c> do cenário default (<see
/// cref="ScenarioRunner.DefaultPopulationCatalog"/>) ficar sem entrada em <see
/// cref="SkillsRules.SkillByProfession"/>, o que faria <c>SkillPracticeSystem</c>/
/// <c>SkillTeachingSystem</c> silenciosamente pular a profissão sem ganho de habilidade
/// nenhum.</summary>
public class SkillsRulesCoverageTests
{
    [Fact]
    public void Every_default_profession_has_a_skill_by_profession_entry()
    {
        var professionIds = ScenarioRunner.DefaultPopulationCatalog.ProfessionIds;
        Assert.NotEmpty(professionIds);

        foreach (var professionId in professionIds)
            Assert.True(
                ScenarioRunner.DefaultSkillsRules.SkillByProfession.ContainsKey(professionId),
                $"profissão {professionId} sem entrada em SkillsRules.SkillByProfession");
    }
}
