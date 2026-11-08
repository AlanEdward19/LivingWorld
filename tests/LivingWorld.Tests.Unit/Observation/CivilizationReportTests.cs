using System.Text.Json.Nodes;
using LivingWorld.Workers;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia B: o JSON do <c>civ-report</c> cumpre o contrato congelado e não depende de paralelismo.</summary>
public class CivilizationReportTests
{
    internal static string ScenarioPath() => Path.Combine(ObservatoryFixture.RepoRoot(), "scenarios", "default.json");

    /// <summary>Forma do JSON: objeto vira chaves ordenadas, lista vira a forma do primeiro item, folha vira o nome do tipo.</summary>
    internal static JsonNode ShapeOf(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)ShapeOf(kv.Value)))),
        JsonArray a => a.Count == 0 ? new JsonArray() : new JsonArray(ShapeOf(a[0])),
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number => "number",
        JsonValue v when v.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => "boolean",
        _ => "string",
    };

    internal static void AssertMatchesContract(string json)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(ObservatoryFixture.RepoRoot(), "tests", "contracts", "shape.civ-report.json")))!;
        Assert.Equal(expected.ToJsonString(), ShapeOf(JsonNode.Parse(json)).ToJsonString());
    }

    [Fact]
    public void Report_json_matches_the_committed_contract()
    {
        AssertMatchesContract(CivilizationReportCommand.BuildJson(ScenarioPath(), firstSeed: 1, seeds: 2, years: 2));
    }

    [Fact]
    public void Report_is_byte_identical_whether_seeds_run_in_parallel_or_one_at_a_time()
    {
        string parallel = CivilizationReportCommand.BuildJson(ScenarioPath(), 1, 3, 1);
        string sequential = CivilizationReportCommand.BuildJson(ScenarioPath(), 1, 3, 1, degreeOfParallelism: 1);
        Assert.Equal(sequential, parallel);
    }

    [Fact]
    public void Report_lists_one_entry_per_seed_in_order_with_one_sample_per_year()
    {
        var root = JsonNode.Parse(CivilizationReportCommand.BuildJson(ScenarioPath(), 7, 3, 2))!;
        var seeds = root["seeds"]!.AsArray();
        Assert.Equal([7, 8, 9], seeds.Select(s => s!["seed"]!.GetValue<int>()));
        Assert.All(seeds, s => Assert.Equal([1, 2], s!["years"]!.AsArray().Select(y => y!["year"]!.GetValue<int>())));
    }

    [Fact]
    public void Different_seeds_give_different_series()
    {
        var seeds = JsonNode.Parse(CivilizationReportCommand.BuildJson(ScenarioPath(), 1, 3, 1))!["seeds"]!.AsArray();
        Assert.Equal(3, seeds.Select(s => s!["years"]!.ToJsonString()).Distinct().Count());
    }
}
