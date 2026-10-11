using System.Diagnostics;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Performance;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>Medição antes/depois de mudanças de comportamento (não é gate). Rode com
/// <c>LW_MEASURE_OUT=arquivo dotnet test ... --filter MeasureHarness</c>: escreve µs/NPC-tick,
/// alocação, bytes de snapshot por NPC e a estabilidade da vila padrão (10 anos, 5 seeds).</summary>
[Collection(ScalePerformanceCollection.Name)]
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
public class MeasureHarness
{
    private sealed class CountingSink : IWorldEventSink
    {
        public Dictionary<WorldEventKind, int> Counts { get; } = [];
        public void Record(WorldEvent e) => Counts[e.Kind] = Counts.GetValueOrDefault(e.Kind) + 1;
    }

    [Fact]
    public void Measure()
    {
        var sb = new StringBuilder();
        // Perf: 1000 NPCs, 30 dias, melhor de 3 (ruído de máquina).
        double bestMicros = double.MaxValue, alloc = 0;
        for (int run = 0; run < 3; run++)
        {
            var (world, clock) = ScaleScenarioFixture.CreateWorld(42, 1000);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long alive = 0, before = GC.GetTotalAllocatedBytes(true);
            var sw = Stopwatch.StartNew();
            for (int t = 0; t < 720; t++) { alive += world.Npcs.Count(n => n.IsAlive); clock.Tick(world); }
            sw.Stop();
            bestMicros = Math.Min(bestMicros, sw.Elapsed.TotalMicroseconds / Math.Max(1, alive));
            alloc = (GC.GetTotalAllocatedBytes(true) - before) / 720.0;
            if (run == 2)
                sb.AppendLine($"scale1000 snapshotBytesPerAlive={Encoding.UTF8.GetByteCount(WorldSnapshot.Serialize(world)) / Math.Max(1, world.Npcs.Count(n => n.IsAlive))} alive={world.Npcs.Count(n => n.IsAlive)}");
        }
        sb.AppendLine($"scale1000 microsPerNpcTick={bestMicros:F3} allocPerTick={alloc:F0}");

        // Vila padrão: 100 NPCs, 10 anos.
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var (world, _) = ScenarioRunner.Create(seed);
            var sink = new CountingSink();
            var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
            var marks = new List<int>();
            var sw = Stopwatch.StartNew();
            for (int year = 1; year <= 10; year++)
            {
                clock.Run(world, 360 * 24);
                marks.Add(world.Npcs.Count(n => n.IsAlive));
            }
            sw.Stop();
            var living = world.Npcs.Where(n => n.IsAlive).ToList();
            double skill = living.Count == 0 ? 0 : living.Average(n => n.Skills.Values.Values.DefaultIfEmpty(0).Sum());
            sb.AppendLine($"village seed={seed} alive@1..10y=[{string.Join(",", marks)}] employed={living.Count(n => n.Employer is not null)} "
                + $"meanSkillSum={skill:F1} withMentor={living.Count(n => n.Mentor is not null)} ms={sw.ElapsedMilliseconds} "
                + $"events={string.Join(",", sink.Counts.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}"))}");
        }

        // Mundo "ao vivo": scenarios/default.json pelo ScenarioLoaderV2 (cidades ligadas), 10 anos × 3 seeds.
        string json = File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json"));
        for (int seed = 1; seed <= 3; seed++)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            node["Seed"] = seed;
            var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
            Assert.True(loaded.IsSuccess, loaded.Error);
            var (world, clock) = loaded.Value;
            var marks = new List<int>();
            var sw = Stopwatch.StartNew();
            for (int year = 1; year <= 10; year++)
            {
                clock.Run(world, 360 * 24);
                marks.Add(world.Npcs.Count(n => n.IsAlive));
            }
            sw.Stop();
            sb.AppendLine($"live seed={seed} alive@1..10y=[{string.Join(",", marks)}] cities={world.ActiveCities().Count()} buildings={world.Buildings.Count} workplaces={world.Workplaces.Count} ms={sw.ElapsedMilliseconds}");
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_MEASURE_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-measure.txt"), sb.ToString());
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
