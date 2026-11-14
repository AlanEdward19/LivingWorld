namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>R2 (rules/eval-criteria.md): horizonte longo (100 anos). Critério: a tabela de vida não trunca cedo — em 100 anos,
/// ao menos 1 NPC atinge 90% da longevidade máxima do cenário. A vila padrão da seed 42 vem da corrida compartilhada
/// <see cref="DefaultVillageSeed42"/> (ADR-0044): a maior idade só cresce (os mortos continuam em Npcs) e é amostrada a cada
/// 5 anos; a corrida segue até o ano 100 enquanto o teto não for atingido, como o laço antigo.</summary>
[Trait("Category", "Scenario")]
public class LifeTable100YearScenarioTests
{
    [Fact]
    public void At_least_one_npc_reaches_90_percent_of_max_longevity_over_100_years()
    {
        var run = DefaultVillageSeed42.Long;

        Assert.True(run.MaxAgeReached >= run.LifeTableThreshold,
            $"maior idade observada em 100 anos foi {run.MaxAgeReached}, esperado >= {run.LifeTableThreshold} (90% da longevidade máxima)");
    }
}
