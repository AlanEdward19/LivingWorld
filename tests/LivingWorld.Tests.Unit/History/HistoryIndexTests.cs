using System.Diagnostics;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared.Baselines;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T17: <see cref="HistoryIndex"/> (HIST-20/21).</summary>
public class HistoryIndexTests
{
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;

    [Fact]
    public void ByYear_returns_facts_in_that_year_without_full_scan()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        int year = 1;
        long tickInYear = year * Calendar.HoursPerYear + 10;
        var fact = new Fact(new FactId(1), tickInYear, WorldEventKind.Death, [world.Npcs[0].Id], null, 0.9, "1");
        world.AddFact(fact);
        SeedNoiseFacts(world, count: 200, exclude: world.Npcs[0].Id);

        var ids = world.HistoryIndex.ByYear(year);

        Assert.Contains(fact.Id, ids);
        Assert.Equal(ids.Count, world.HistoryIndex.LastQueryReads);
    }

    [Fact]
    public void ByEntity_returns_facts_for_participant_without_full_scan()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var target = world.Npcs[0];
        world.AddFact(new Fact(new FactId(1), 10, WorldEventKind.Birth, [target.Id], null, 0.9, "1"));
        world.AddFact(new Fact(new FactId(2), 20, WorldEventKind.Marriage, [target.Id, world.Npcs[1].Id], null, 0.8, "1|2"));
        SeedNoiseFacts(world, count: 300, exclude: target.Id);

        var ids = world.HistoryIndex.ByEntity(target.Id);

        Assert.Equal(2, ids.Count);
        Assert.Equal(2, world.HistoryIndex.LastQueryReads);
    }

    [Fact]
    public void ByKind_returns_facts_of_kind_without_full_scan()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        world.AddFact(new Fact(new FactId(1), 10, WorldEventKind.Marriage, [world.Npcs[0].Id, world.Npcs[1].Id], null, 0.8, "1|2"));
        world.AddFact(new Fact(new FactId(2), 20, WorldEventKind.Marriage, [world.Npcs[2].Id, world.Npcs[3].Id], null, 0.8, "3|4"));
        SeedNoiseFacts(world, count: 300, exclude: world.Npcs[0].Id);

        var ids = world.HistoryIndex.ByKind(WorldEventKind.Marriage);

        Assert.Equal(2, ids.Count);
        Assert.Equal(2, world.HistoryIndex.LastQueryReads);
    }

    [Fact]
    public void RebuildFrom_reconstructs_index_after_rehydration()
    {
        var (world, _) = ScenarioRunner.Create(2, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        world.AddFact(new Fact(new FactId(1), 50, WorldEventKind.Birth, [npc.Id], null, 0.9, "1"));

        var rebuilt = HistoryIndex.RebuildFrom(world);

        Assert.Single(rebuilt.ByEntity(npc.Id));
    }

    [Fact(Skip = "Regravar: dotnet test --filter Record_history_index_baseline")]
    public void Record_history_index_baseline() => RecordIndexBaseline();

    [Fact]
    public void Query_reads_match_baseline_for_twenty_seeds()
    {
        var actual = MeasureReadsBySeed();
        BaselineFixture.AssertMatches(BaselinesDir, "history-index", actual);
    }

    [Fact]
    public void History_index_digest_is_identical_across_two_separate_processes()
    {
        var a = RunDigestInSeparateProcess(42, 300);
        var b = RunDigestInSeparateProcess(42, 300);
        Assert.Equal(a, b);
    }

    internal static void RecordIndexBaseline()
    {
        BaselineFixture.Record(BaselinesDir, "history-index", MeasureReadsBySeed());
    }

    private static Dictionary<int, int> MeasureReadsBySeed()
    {
        var actual = new Dictionary<int, int>();
        for (int seed = 0; seed < 20; seed++)
        {
            var (world, _) = ScenarioRunner.Create((ulong)seed, historyRules: HistoryRules.Default);
            var target = world.Npcs[0];
            world.AddFact(new Fact(new FactId(1), 100, WorldEventKind.Birth, [target.Id], null, 0.9, "1"));
            SeedNoiseFacts(world, count: 400, exclude: target.Id);
            world.HistoryIndex.ByEntity(target.Id);
            actual[seed] = world.HistoryIndex.LastQueryReads;
        }
        return actual;
    }

    private static void SeedNoiseFacts(WorldState world, int count, NpcId exclude)
    {
        for (int i = 0; i < count; i++)
        {
            var npc = world.Npcs[(i + 1) % world.Npcs.Count];
            if (npc.Id == exclude)
                npc = world.Npcs[(i + 2) % world.Npcs.Count];
            world.AddFact(new Fact(
                world.NextFactIdAndAdvance(),
                1000 + i,
                WorldEventKind.CourtshipRejected,
                [npc.Id],
                null,
                0.6,
                npc.Id.Value.ToString()));
        }
    }

    private static string RunDigestInSeparateProcess(ulong seed, long ticks)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{WorkersDllPath}\" history-index-digest {seed} {ticks}",
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
