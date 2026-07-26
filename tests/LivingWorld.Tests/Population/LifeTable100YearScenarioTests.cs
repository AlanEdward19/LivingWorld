using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>R2 (rules/eval-criteria.md): horizonte longo (100 anos) fica fora do gate padrão —
/// roda só com <c>--filter Category=Scenario</c> (nightly). Critério: a tabela de vida não
/// trunca cedo — em 100 anos, ao menos 1 NPC atinge 90% da longevidade máxima do cenário.</summary>
[Trait("Category", "Scenario")]
public class LifeTable100YearScenarioTests
{
    [Fact]
    public void At_least_one_npc_reaches_90_percent_of_max_longevity_over_100_years()
    {
        const long oneHundredYears = 100 * 12 * 30 * 24;
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        clock.Run(world, oneHundredYears);

        int threshold = (int)(ScenarioRunner.DefaultPopulationRules.LifeTable.MaxLongevityYears * 0.9);
        var maxAgeReached = world.Npcs.Max(n => n.AgeYears(world.CurrentDate));

        Assert.True(maxAgeReached >= threshold,
            $"maior idade observada em 100 anos foi {maxAgeReached}, esperado >= {threshold} (90% de {ScenarioRunner.DefaultPopulationRules.LifeTable.MaxLongevityYears})");
    }
}
