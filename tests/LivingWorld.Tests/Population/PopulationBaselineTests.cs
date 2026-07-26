using System.Text.Json;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>R3 (rules/eval-criteria.md): faixa de população final vem de 20 seeds gravados, não
/// de chute. <c>tests/baselines/population.json</c> guarda a contagem final de cada seed;
/// reprova fora de [min × 0.8, max × 1.2] do que está gravado — tolerância, não igualdade
/// exata, porque ajuste fino da tabela de vida não deveria quebrar o gate por 1 NPC.</summary>
public class PopulationBaselineTests
{
    private const long TenYearsInHours = 10 * 12 * 30 * 24;
    private static readonly string BaselinePath = Path.Combine(FindRepoRoot(), "tests", "baselines", "population.json");

    private static int FinalPopulation(ulong seed)
    {
        var (world, clock) = ScenarioRunner.Create(seed);
        clock.Run(world, TenYearsInHours);
        return world.Npcs.Count(n => n.IsAlive);
    }

    [Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]
    public void ZZZ_record_baseline()
    {
        var counts = Enumerable.Range(1, 20).Select(seed => FinalPopulation((ulong)seed)).ToArray();
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(counts, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Final_population_over_20_seeds_stays_within_80_to_120_percent_of_the_recorded_baseline()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(File.ReadAllText(BaselinePath))!;
        int min = baseline.Min();
        int max = baseline.Max();

        for (int seed = 1; seed <= 20; seed++)
        {
            int actual = FinalPopulation((ulong)seed);
            Assert.InRange(actual, (int)(min * 0.8), (int)(max * 1.2));
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
