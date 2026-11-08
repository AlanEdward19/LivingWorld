using System.Text.Json.Nodes;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Emergence;

/// <summary>#114: depois de qualquer fusão de cidade (ADR-0019), nenhuma tecnologia lista como adotante uma cidade fundida
/// (lápide). Cenário vivo (<c>scenarios/default.json</c>, emergência e fundação ligadas), 10 anos, conferido a cada dia.</summary>
public class MergedCityAdoptionScenarioTests
{
    private const long TenYearsInHours = 10L * 12 * 30 * 24;

    private sealed class MergeSink : IWorldEventSink
    {
        public int Merges { get; private set; }
        public void Record(WorldEvent evt)
        {
            if (evt.Kind == WorldEventKind.CityMerged) Merges++;
        }
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void No_technology_is_adopted_by_a_merged_city_at_any_daily_sample_over_ten_years(int seed)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var world = loaded.Value.World;
        var sink = new MergeSink();
        var clock = new WorldClock(loaded.Value.Clock.Systems, sink: sink);

        for (long hour = 0; hour < TenYearsInHours; hour += 24)
        {
            clock.Run(world, 24);
            var tombstones = world.Cities.Where(c => c.MergedIntoCityId is not null).Select(c => c.Id).ToHashSet();
            foreach (var tech in world.Technologies)
                Assert.DoesNotContain(tech.AdoptedBy, tombstones.Contains);
        }

        // Garante que o caminho de fusão foi exercitado (a descoberta de tecnologia em si é rara nestes seeds; a prova
        // causal da transferência mora em SpatialSettlementFoundingSystemTests).
        Assert.True(sink.Merges > 0, "o cenário não fundiu nenhuma cidade — a asserção seria vácua");
    }
}
