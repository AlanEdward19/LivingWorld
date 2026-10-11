using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>R2 (rules/eval-criteria.md): horizonte longo (100 anos) fica fora do gate padrão —
/// roda só com <c>--filter Category=Scenario</c> (nightly). Critério: a tabela de vida não
/// trunca cedo — em 100 anos, ao menos 1 NPC atinge 90% da longevidade máxima do cenário.</summary>
[Trait("Category", "Scenario")]
public class LifeTable100YearScenarioTests
{
    [Fact]
    public void At_least_one_npc_reaches_90_percent_of_max_longevity_over_100_years()
    {
        const int horizonYears = 100;
        const int stepYears = 5;
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        int threshold = (int)(ScenarioRunner.DefaultPopulationRules.LifeTable.MaxLongevityYears * 0.9);

        // A maior idade só cresce (os mortos continuam em Npcs): para assim que o critério é atendido,
        // sem simular os anos restantes. Falhar ainda custa os 100 anos completos.
        int maxAgeReached = 0;
        for (int year = 0; year < horizonYears && maxAgeReached < threshold; year += stepYears)
        {
            clock.Run(world, stepYears * 12L * 30 * 24);
            maxAgeReached = world.Npcs.Max(n => n.AgeYears(world.CurrentDate));
        }

        Assert.True(maxAgeReached >= threshold,
            $"maior idade observada em 100 anos foi {maxAgeReached}, esperado >= {threshold} (90% de {ScenarioRunner.DefaultPopulationRules.LifeTable.MaxLongevityYears})");
    }
}
