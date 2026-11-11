using System.Text.Json;

namespace LivingWorld.Tests.Unit.Population;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PopulationBaselineCollection
{
    public const string Name = "Population baseline";
}

/// <summary>R3 (rules/eval-criteria.md): faixa de população final vem de 20 seeds gravados, não
/// de chute. <c>tests/baselines/population.json</c> guarda a contagem final de cada seed;
/// reprova fora de [min × 0.8, max × 1.2] do que está gravado — tolerância, não igualdade
/// exata, porque ajuste fino da tabela de vida não deveria quebrar o gate por 1 NPC.</summary>
[Collection(PopulationBaselineCollection.Name)]
[Trait("Category", "Scenario")]
public class PopulationBaselineTests
{
    private static readonly string BaselinePath = Path.Combine(FindRepoRoot(), "tests", "baselines", "population.json");

    // A vila padrão das seeds 1..20 no ano 10 vem da corrida compartilhada (ADR-0044): mesmo mundo que antes rodava aqui.
    private static int[] FinalPopulations() => DefaultVillageDecade.Runs.Select(r => r.Alive[DefaultVillageDecade.Years]).ToArray();

    [Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]
    public void ZZZ_record_baseline()
    {
        var counts = FinalPopulations();
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(counts, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Final_population_over_20_seeds_stays_within_80_to_120_percent_of_the_recorded_baseline()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(File.ReadAllText(BaselinePath))!;
        int min = baseline.Min();
        int max = baseline.Max();

        var actual = FinalPopulations();
        for (int index = 0; index < actual.Length; index++)
            Assert.True(
                actual[index] >= (int)(min * 0.8) && actual[index] <= (int)(max * 1.2),
                $"seed={index + 1}, actual={actual[index]}, expected=[{(int)(min * 0.8)}, {(int)(max * 1.2)}]");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
