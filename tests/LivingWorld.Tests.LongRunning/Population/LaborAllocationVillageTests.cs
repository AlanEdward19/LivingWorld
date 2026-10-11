using System.Text.Json;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>labor-allocation sobre a vila do gate (100 NPCs): a populacao do ano 10 nao cai abaixo de 90% do piso
/// gravado em <c>tests/baselines/gate-village-population.json</c> (medido depois da feature; antes dela as 5 seeds
/// somavam 373) e ninguem troca de emprego mais que 0,5 vez por ano em media.</summary>
public class LaborAllocationVillageTests
{
    private const long OneYearInHours = 12 * 30 * 24;

    private sealed class SwitchCounter : IWorldEventSink
    {
        public int Moves { get; private set; }

        public void Record(WorldEvent evt)
        {
            if (evt.Kind == WorldEventKind.Fired && evt.SourceSystem == "LaborAllocation") Moves++;
        }
    }

    [Fact]
    public void Gate_village_population_at_year_ten_stays_within_90_percent_of_the_floor()
    {
        var baseline = JsonSerializer.Deserialize<int[]>(File.ReadAllText(RepoPath("tests", "baselines", "gate-village-population.json")))!;
        int total = 0;
        for (int seed = 1; seed <= baseline.Length; seed++)
        {
            var (world, _) = ScenarioRunner.Create((ulong)seed);
            new WorldClock(ScenarioRunner.DefaultSystems()).Run(world, 10 * OneYearInHours);
            total += world.Npcs.Count(n => n.IsAlive);
        }

        Assert.True(total >= 0.9 * baseline.Sum(), $"populacao do ano 10 somada {total} < 90% de {baseline.Sum()}");
    }

    [Fact]
    public void Job_moves_stay_under_half_a_move_per_npc_per_year_in_the_gate_village()
    {
        double moves = 0, npcYears = 0;
        for (int seed = 1; seed <= 5; seed++)
        {
            var (world, _) = ScenarioRunner.Create((ulong)seed);
            var counter = new SwitchCounter();
            var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: counter);
            for (int year = 0; year < 3; year++)
            {
                clock.Run(world, OneYearInHours);
                npcYears += world.Npcs.Count(n => n.IsAlive);
            }

            moves += counter.Moves;
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
