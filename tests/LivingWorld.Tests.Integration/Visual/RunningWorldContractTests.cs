using System.Text.Json.Nodes;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-172: o cliente confere cada snapshot e cada delta contra as formas gravadas e descarta o que diverge. Um mundo
/// rodando (mais ticks do que os usados para gravar), com assinante nos escopos Mundo e Cidade: toda resposta cumpre a forma.</summary>
public class RunningWorldContractTests
{
    private static JsonNode Recorded(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return JsonNode.Parse(File.ReadAllText(Path.Combine(dir!, "tests", "contracts", file)))!;
    }

    [Fact]
    public void Every_snapshot_and_delta_of_a_running_world_matches_the_recorded_shapes()
    {
        var samples = RunningWorldSamples.Collect(RunningWorldSamples.RecordedTicks + 60);
        var checks = new (string File, IReadOnlyList<JsonNode> Items)[]
        {
            ("shape.world-subscribe.json", samples.WorldSnapshots),
            ("shape.city-subscribe.json", samples.CitySnapshots),
            ("shape.visual-delta.json", samples.Deltas),
        };

        var divergences = checks
            .SelectMany(check =>
            {
                var shape = Recorded(check.File);
                return check.Items.SelectMany((item, i) => ContractShapes.Divergences(item, shape, $"{check.File}#{i}"));
            })
            .ToList();

        Assert.True(samples.Deltas.Count >= 2 * RunningWorldSamples.RecordedTicks, $"poucos deltas: {samples.Deltas.Count}");
        Assert.True(divergences.Count == 0, $"{divergences.Count} divergências:\n{string.Join('\n', divergences.Distinct().Take(40))}");
    }
}
