using System.Text.Json.Nodes;
using LivingWorld.Simulation.Observation;
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

    private static SeedRun Known(ulong seed, params double[] foo) => new(seed, foo.Select((f, i) => new WorldYearSample(
        i + 1, (i + 1) * 100L,
        new Dictionary<string, double>
        {
            ["foo"] = f,
            ["population"] = f,
            ["cities_active"] = 1,
            ["gini"] = 0.5,
            ["treasury"] = 10,
            ["technologies"] = 0,
        },
        [new CitySample($"city-{seed}", new Dictionary<string, double> { ["bar"] = f * 2, ["population"] = f })])).ToList());

    [Fact]
    public void Summary_does_not_depend_on_the_input_order_of_the_runs()
    {
        // F8: a soma de ponto flutuante não é associativa; com 1e16, -1e16 e 1 a média muda conforme a ordem de soma.
        var runs = new[] { Known(1, 1e16), Known(2, -1e16), Known(3, 1) };

        string forward = CivilizationReport.ToJson("c.json", runs);
        string reversed = CivilizationReport.ToJson("c.json", runs.Reverse().ToArray());

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void Report_values_match_a_known_set_of_runs_field_by_field()
    {
        // Seeds fora de ordem e com tamanhos diferentes: 3 anos (seed 4), 2 anos (seeds 6 e 9). O último ano de cada uma é o que entra em `final`.
        var runs = new[] { Known(9, 5, 20), Known(4, 7, 999, 10), Known(6, 8, 0) };

        var root = JsonNode.Parse(CivilizationReport.ToJson("cenario.json", runs))!;

        Assert.Equal("cenario.json", root["scenario"]!.GetValue<string>());
        var seeds = root["seeds"]!.AsArray();
        Assert.Equal([4, 6, 9], seeds.Select(x => x!["seed"]!.GetValue<int>()));
        var seed4 = seeds[0]!["years"]!.AsArray();
        Assert.Equal(3, seed4.Count);
        Assert.Equal([1, 2, 3], seed4.Select(y => y!["year"]!.GetValue<int>()));
        Assert.Equal([100L, 200L, 300L], seed4.Select(y => y!["tick"]!.GetValue<long>()));
        Assert.Equal([7.0, 999.0, 10.0], seed4.Select(y => y!["world"]!["foo"]!.GetValue<double>()));
        var city = seed4[1]!["cities"]!.AsArray().Single()!;
        Assert.Equal("city-4", city["id"]!.GetValue<string>());
        Assert.Equal(1998.0, city["metrics"]!["bar"]!.GetValue<double>());

        var summary = root["summary"]!;
        Assert.Equal(3, summary["seeds"]!.GetValue<int>());
        Assert.Equal(2, summary["years"]!.GetValue<int>());
        var foo = summary["final"]!["foo"]!;
        Assert.Equal(0.0, foo["min"]!.GetValue<double>());
        Assert.Equal(10.0, foo["mean"]!.GetValue<double>());
        Assert.Equal(20.0, foo["max"]!.GetValue<double>());
        Assert.Equal(["cities_active", "foo", "gini", "population", "technologies", "treasury"], summary["final"]!.AsObject().Select(kv => kv.Key));

        var diversity = summary["diversity"]!;
        var expected = TrajectoryDiversity.Compute(runs);
        Assert.Equal(expected.Index, diversity["index"]!.GetValue<double>(), 12);
        Assert.True(diversity["index"]!.GetValue<double>() > 0);
        Assert.Equal(3, diversity["seeds"]!.GetValue<int>());
        Assert.Equal(2, diversity["years"]!.GetValue<int>());
        Assert.Equal(expected.ByMetric["population"], diversity["byMetric"]!["population"]!.GetValue<double>(), 12);
    }

    [Fact]
    public void Report_writes_non_finite_numbers_as_zero_and_an_empty_report_has_a_zeroed_summary()
    {
        var run = new SeedRun(1, [new WorldYearSample(1, 8640, new Dictionary<string, double>
            { ["a"] = double.NaN, ["b"] = double.PositiveInfinity, ["c"] = 2 }, [])]);

        var world = JsonNode.Parse(CivilizationReport.ToJson("x", [run]))!["seeds"]![0]!["years"]![0]!["world"]!;
        Assert.Equal(0.0, world["a"]!.GetValue<double>());
        Assert.Equal(0.0, world["b"]!.GetValue<double>());
        Assert.Equal(2.0, world["c"]!.GetValue<double>());

        var empty = JsonNode.Parse(CivilizationReport.ToJson("x", []))!["summary"]!;
        Assert.Equal(0, empty["seeds"]!.GetValue<int>());
        Assert.Equal(0, empty["years"]!.GetValue<int>());
        Assert.Empty(empty["final"]!.AsObject());
    }

    [Fact]
    public void Built_report_names_the_scenario_by_file_name_and_counts_seeds_and_years_from_the_arguments()
    {
        var root = JsonNode.Parse(CivilizationReportCommand.BuildJson(ScenarioPath(), 5, 3, 2))!;

        Assert.Equal("default.json", root["scenario"]!.GetValue<string>());
        Assert.Equal([5, 6, 7], root["seeds"]!.AsArray().Select(x => x!["seed"]!.GetValue<int>()));
        Assert.All(root["seeds"]!.AsArray(), x => Assert.Equal([1, 2], x!["years"]!.AsArray().Select(y => y!["year"]!.GetValue<int>())));
        Assert.Equal(2 * ScenarioRunnerHoursPerYear, root["seeds"]![0]!["years"]![1]!["tick"]!.GetValue<long>());
        Assert.Equal(3, root["summary"]!["seeds"]!.GetValue<int>());
        Assert.Equal(2, root["summary"]!["years"]!.GetValue<int>());
    }

    private static long ScenarioRunnerHoursPerYear => LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultCalendar.HoursPerYear;

    [Fact]
    public void Run_creates_the_output_directory_writes_the_same_json_and_returns_zero()
    {
        string dir = Path.Combine(Directory.CreateTempSubdirectory("civ-run").FullName, "novo", "sub");
        string output = Path.Combine(dir, "r.json");
        try
        {
            Assert.Equal(0, CivilizationReportCommand.Run(ScenarioPath(), 1, 1, 1, output));
            Assert.Equal(CivilizationReportCommand.BuildJson(ScenarioPath(), 1, 1, 1), File.ReadAllText(output));
        }
        finally
        {
            Directory.Delete(Path.GetFullPath(Path.Combine(dir, "..", "..")), recursive: true);
        }
    }
}
