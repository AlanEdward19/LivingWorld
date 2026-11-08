using System.Text.Json.Nodes;
using LivingWorld.Workers;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatias B e C, gate de verdade: 20 seeds × 10 anos do cenário padrão pelo mesmo caminho do CLI. Termina
/// sem exceção, o JSON cumpre o contrato e a diversidade bate com <c>tests/baselines/civilization-diversity.json</c>.
/// Regravar o baseline é commit explícito: <c>bash scripts/civ-report.sh</c> e copiar <c>summary.diversity</c> para <c>Gate</c>
/// (com <c>Seeds</c>, <c>GateYears</c> e <c>FirstSeed</c> do padrão do script). Parte "cenário de referência" é da fatia D.</summary>
[Trait("Category", "Scenario")]
public class CivilizationDiversityBaselineTests
{
    private static readonly JsonNode Baseline = JsonNode.Parse(File.ReadAllText(
        Path.Combine(ObservatoryFixture.RepoRoot(), "tests", "baselines", "civilization-diversity.json")))!;

    private static readonly Lazy<string> ReportJson = new(() => CivilizationReportCommand.BuildJson(
        CivilizationReportTests.ScenarioPath(),
        Baseline["FirstSeed"]!.GetValue<ulong>(), Baseline["Seeds"]!.GetValue<int>(), Baseline["GateYears"]!.GetValue<int>()));

    [Fact]
    public void Twenty_seeds_for_ten_years_produce_a_report_that_matches_the_contract()
    {
        CivilizationReportTests.AssertMatchesContract(ReportJson.Value);
        var root = JsonNode.Parse(ReportJson.Value)!;
        Assert.Equal(Baseline["Seeds"]!.GetValue<int>(), root["seeds"]!.AsArray().Count);
        Assert.All(root["seeds"]!.AsArray(), s => Assert.Equal(Baseline["GateYears"]!.GetValue<int>(), s!["years"]!.AsArray().Count));
    }

    [Fact]
    public void Trajectory_diversity_of_twenty_seeds_matches_the_committed_baseline()
    {
        var actual = JsonNode.Parse(ReportJson.Value)!["summary"]!["diversity"]!;
        var gate = Baseline["Gate"]!;

        Assert.Equal(gate["Index"]!.GetValue<double>(), actual["index"]!.GetValue<double>(), 12);
        foreach (var (name, expected) in gate["ByMetric"]!.AsObject())
            Assert.Equal(expected!.GetValue<double>(), actual["byMetric"]![name]!.GetValue<double>(), 12);
    }

    [Fact]
    public void Different_seeds_of_the_default_scenario_tell_different_stories()
    {
        // Controle negativo: o mundo congelado dá 0 (TrajectoryDiversityTests); aqui o índice real tem de ficar acima dele.
        var diversity = JsonNode.Parse(ReportJson.Value)!["summary"]!["diversity"]!;
        Assert.True(diversity["index"]!.GetValue<double>() > 0);
        Assert.True(diversity["byMetric"]!["population"]!.GetValue<double>() > 0);
    }
}
