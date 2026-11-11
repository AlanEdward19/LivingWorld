using System.Text.Json;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>labor-allocation sobre a vila do gate (100 NPCs): a populacao do ano 10 nao cai abaixo de 90% do piso
/// gravado em <c>tests/baselines/gate-village-population.json</c> (medido depois da feature; antes dela as 5 seeds
/// somavam 373) e ninguem troca de emprego mais que 0,5 vez por ano em media.</summary>
public class LaborAllocationVillageTests
{
    [Fact]
    public void Gate_village_population_at_year_ten_stays_within_90_percent_of_the_floor()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(File.ReadAllText(RepoPath("tests", "baselines", "gate-village-population.json")))!;
        // Vila padrão das seeds 1..N no ano 10: lida da corrida compartilhada do século (ADR-0044).
        int total = DefaultVillageCentury.Runs.Take(baseline.Length).Sum(r => r.Alive[10]);

        Assert.True(total >= 0.9 * baseline.Sum(), $"populacao do ano 10 somada {total} < 90% de {baseline.Sum()}");
    }

    [Fact]
    public void Job_moves_stay_under_half_a_move_per_npc_per_year_in_the_gate_village()
    {
        // Seeds 1..5, anos 1..3: as trocas (Fired de LaborAllocation) e os vivos de cada fim de ano vêm da corrida compartilhada.
        double moves = 0, npcYears = 0;
        foreach (var run in DefaultVillageCentury.Runs.Take(5))
        {
            for (int year = 1; year <= 3; year++) npcYears += run.Alive[year];
            moves += run.LaborMoves[3];
        }

        Assert.True(moves / npcYears <= 0.5, $"{moves / npcYears:F3} trocas por NPC por ano");
    }

    private static string RepoPath(params string[] tail)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine([dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado"), .. tail]);
    }
}
