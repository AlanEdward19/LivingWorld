using System.Diagnostics;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Scenarios;
using Xunit.Abstractions;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#262: medida de custo do catálogo de humor num mundo grande (1000 NPCs, um mês, tempo de hora cheia): o mesmo mundo com as necessidades e a vida social,
/// com e sem o catálogo de momentos. Os momentos são preguiçosos (expiram na leitura, sem passo por tick) e o sorteio de desejos roda a cada poucas horas: o custo por NPC-tick (hora cheia, linha de base de ~3 µs) sobe pouco; o teto de 1,35 só pega regressão grossa (medido 1,13–1,16).</summary>
public class MoodCostTests(ITestOutputHelper output)
{
    private static double MicrosPerNpcTick(string[] extends, int population, int ticks)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(LivingWorld.Tests.Unit.Cities.SandboxFixtures.RepoRoot(), "scenarios", "periods", "medieval.json")))!.AsObject();
        root["InitialPopulation"] = population;
        root["Seed"] = 262;
        root["CatalogExtends"] = new JsonArray(extends.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray());
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var (world, clock) = loaded.Value;
        for (int i = 0; i < 24; i++) clock.Tick(world); // aquece
        int alive = world.Npcs.Count(n => n.IsAlive); // a população no começo da medida (sem comida de cenário os NPCs morrem em semanas: poucos dias bastam)
        GC.Collect();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < ticks; i++) clock.Tick(world);
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds * 1000.0 / ((double)ticks * alive);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_mood_catalog_does_not_raise_the_cost_per_npc_tick_in_a_thousand_npc_world()
    {
        var without = new List<double>();
        var with = new List<double>();
        for (int round = 0; round < 5; round++)
        {
            without.Add(MicrosPerNpcTick(["home-life", "social"], 1000, 24 * 4));
            with.Add(MicrosPerNpcTick(["home-life", "social", "moods"], 1000, 24 * 4));
        }

        double a = without.Skip(1).Order().ElementAt(1), b = with.Skip(1).Order().ElementAt(1); // a primeira rodada aquece o JIT
        string line = $"microsPerNpcTick sem humor {a:0.000} (rodadas {string.Join(", ", without.Select(v => v.ToString("0.000")))}) · com humor {b:0.000} (rodadas {string.Join(", ", with.Select(v => v.ToString("0.000")))}) · razão {b / a:0.000}";
        output.WriteLine(line);
        if (Environment.GetEnvironmentVariable("LW_MEASURE_OUT") is { Length: > 0 } path) File.AppendAllText(path, line + Environment.NewLine);
        Assert.True(b / a < 1.35, $"o catálogo de humor custou {b / a:0.000}× por NPC-tick");
    }
}
