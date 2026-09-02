using System.Diagnostics;
using LivingWorld.Domain.History;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Baselines;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T4: <see cref="SignificanceCalculator"/> (HIST-01/HIST-02).</summary>
public class SignificanceCalculatorTests
{
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");

    [Fact]
    public void Compute_returns_same_value_for_same_event_and_world()
    {
        var (world, _) = ScenarioRunner.Create(5, historyRules: HistoryRules.Default);
        var evt = new WorldEvent(10, WorldEventKind.Death, "3");
        double first = SignificanceCalculator.Compute(evt, world, HistoryRules.Default);
        double second = SignificanceCalculator.Compute(evt, world, HistoryRules.Default);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TryRecord_creates_intact_fact_when_significance_meets_threshold()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var evt = new WorldEvent(5, WorldEventKind.Death, "1");

        var result = SignificanceCalculator.TryRecord(evt, world, HistoryRules.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(evt.Kind, result.Value!.Kind);
        Assert.Equal(evt.Payload, result.Value.Payload);
        Assert.Equal(evt.Tick, result.Value.Tick);
        Assert.True(result.Value.Significance >= HistoryRules.Default.SkeletonSignificanceThreshold);
    }

    [Fact]
    public void TryRecord_collapses_without_writing_when_significance_is_below_threshold()
    {
        var rules = HistoryRules.Create(
            enabled: true,
            skeletonSignificanceThreshold: 0.99,
            canonSizePerCommunity: 10,
            mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
            operatorProbability: HistoryRules.Default.OperatorProbability,
            importanceWeight: 1,
            transmissibilityWeight: 1,
            recencyWeight: 1).Value!;
        var (world, _) = ScenarioRunner.Create(1, historyRules: rules);
        var evt = new WorldEvent(5, WorldEventKind.CourtshipRejected, "1|2|3");

        var result = SignificanceCalculator.TryRecord(evt, world, rules);

        Assert.False(result.IsSuccess);
        Assert.Equal("collapsed", result.Error);
        Assert.Empty(world.Facts);
    }

    [Fact(Skip = "Regravar: dotnet test --filter Record_history_collapse_baseline")]
    public void Record_history_collapse_baseline() => RecordCollapseBaseline();

    [Fact]
    public void Collapse_rate_matches_baseline_for_twenty_seeds()
    {
        var rules = HistoryRules.Default;
        var actual = new Dictionary<int, double>();
        for (int seed = 0; seed < 20; seed++)
        {
            var sink = new BufferingWorldEventSink();
            var (world, _) = ScenarioRunner.Create((ulong)seed, historyRules: rules);
            var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
            clock.Run(world, 200);

            var events = sink.DrainAll();
            int below = events.Count(e => SignificanceCalculator.Compute(e, world, rules) < rules.SkeletonSignificanceThreshold);
            actual[seed] = events.Count == 0 ? 0 : (double)below / events.Count;
        }

        BaselineFixture.AssertMatches(BaselinesDir, "history-collapse", actual);
    }

    [Fact]
    public void Significance_digest_is_identical_across_two_separate_processes()
    {
        var a = RunDigestInSeparateProcess("history-significance-digest", 42, 300);
        var b = RunDigestInSeparateProcess("history-significance-digest", 42, 300);
        Assert.Equal(a, b);
    }

    internal static void RecordCollapseBaseline()
    {
        var rules = HistoryRules.Default;
        var actual = new Dictionary<int, double>();
        for (int seed = 0; seed < 20; seed++)
        {
            var sink = new BufferingWorldEventSink();
            var (world, _) = ScenarioRunner.Create((ulong)seed, historyRules: rules);
            var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
            clock.Run(world, 200);
            var events = sink.DrainAll();
            int below = events.Count(e => SignificanceCalculator.Compute(e, world, rules) < rules.SkeletonSignificanceThreshold);
            actual[seed] = events.Count == 0 ? 0 : (double)below / events.Count;
        }
        BaselineFixture.Record(BaselinesDir, "history-collapse", actual);
    }

    private static string RunDigestInSeparateProcess(string command, ulong seed, long ticks)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" {command} {seed} {ticks}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("falha ao iniciar processo");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"processo saiu com {process.ExitCode}: {error}");
        return output.Trim();
    }

    private static readonly string WorkersDllPath = FindWorkersDll();

    private static string FindWorkersDll()
    {
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";
        var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Workers", "bin", configuration, "net10.0", "LivingWorld.Workers.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException($"LivingWorld.Workers.dll não encontrado em {path}", path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
