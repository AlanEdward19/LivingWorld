using System.Text.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using LivingWorld.Tests.Baselines;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T20: cânone fica no teto declarado (<see cref="HistoryRules.CanonSizePerCommunity"/>)
/// em 50/100/200 anos sem tendência de crescimento; bytes/relato retido dentro do baseline de 20
/// seeds (HIST-08 AC3, AC4).</summary>
public class CanonBoundedOverTimeTests
{
    // WorldCalendar default do cenário (ScenarioRunner.DefaultCalendar): 24h/dia, 30 dias/mês, 12
    // meses/ano — usado só para converter "ano N" em tick, sem rodar o clock hora a hora.
    private const long TicksPerYear = 24 * 30 * 12;

    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void Canon_size_stays_at_ceiling_across_horizons_with_no_growth_trend(int years)
    {
        var rules = RulesFor(canonSize: 25);
        var (world, _) = ScenarioRunner.Create(1, historyRules: rules);
        var city = EnsureCity(world, world.Npcs[0].City);

        AdmitOneReportPerYear(world, city, rules, years);

        Assert.Equal(rules.CanonSizePerCommunity, city.CanonSlots.Count);
    }

    [Fact]
    public void Bytes_per_retained_report_matches_baseline_for_twenty_seeds()
    {
        var actual = new Dictionary<int, double>();
        for (int seed = 0; seed < 20; seed++)
            actual[seed] = BytesPerReportAtTenYears((ulong)seed);

        BaselineFixture.AssertMatches(BaselinesDir, "canon-bytes-per-report", actual);
    }

    [Fact(Skip = "Regravar: dotnet test --filter Record_canon_bytes_per_report_baseline")]
    public void Record_canon_bytes_per_report_baseline()
    {
        var actual = new Dictionary<int, double>();
        for (int seed = 0; seed < 20; seed++)
            actual[seed] = BytesPerReportAtTenYears((ulong)seed);

        BaselineFixture.Record(BaselinesDir, "canon-bytes-per-report", actual);
    }

    private static HistoryRules RulesFor(int canonSize) => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: canonSize,
        mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1,
        transmissibilityWeight: 0,
        recencyWeight: 1).Value!;

    private static double BytesPerReportAtTenYears(ulong seed)
    {
        var rules = RulesFor(canonSize: 25);
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var city = EnsureCity(world, world.Npcs[0].City);

        AdmitOneReportPerYear(world, city, rules, years: 10);

        if (city.CanonSlots.Count == 0)
            return 0;

        long totalBytes = city.CanonSlots.Sum(r => JsonSerializer.SerializeToUtf8Bytes(r).Length);
        return (double)totalBytes / city.CanonSlots.Count;
    }

    private static void AdmitOneReportPerYear(WorldState world, City city, HistoryRules rules, int years)
    {
        for (int year = 1; year <= years; year++)
        {
            long tick = year * TicksPerYear;
            double weight = 0.1 + year % 7 * 0.1; // varia determinístico, sem tendência de subida
            var report = new ReportState(
                world.NextReportIdAndAdvance(), new FactId(year), city.Id,
                TransmissionMediumType.OralTradition, HopCount: 0, Weight: weight,
                CreatedAtTick: tick, LastHopTick: tick);
            CanonSlotManager.Admit(city, report, rules, nowTick: tick);
        }
    }

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
