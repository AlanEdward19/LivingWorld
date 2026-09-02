using System.Text.RegularExpressions;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.History;

/// <summary>COH-01/COH-03: LogEvent aditivo minta EventId e carrega SourceSystem/CauseEventId.</summary>
public class TickContextLogEventTests
{
    private sealed class CapturingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, TickContext Ctx, CapturingSink Sink) Build()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 42, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var sink = new CapturingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        return (world, ctx, sink);
    }

    [Fact]
    public void Legacy_LogEvent_wrapper_uses_Unknown_source_and_null_cause()
    {
        var (world, ctx, sink) = Build();

        var id = ctx.LogEvent(WorldEventKind.Death, "1");

        Assert.Equal(0, id);
        Assert.Equal(1, world.NextHistoryEventId);
        var evt = Assert.Single(sink.Events);
        Assert.Equal(0, evt.EventId);
        Assert.Null(evt.CauseEventId);
        Assert.Equal("Unknown", evt.SourceSystem);
        Assert.Equal(WorldEventKind.Death, evt.Kind);
        Assert.Equal("1", evt.Payload);
    }

    [Fact]
    public void Additive_LogEvent_mints_EventId_and_carries_cause_chain()
    {
        var (world, ctx, sink) = Build();

        var rootId = ctx.LogEvent(WorldEventKind.ExtraordinaryUseAttempted, "attempt", "ExtraordinaryInvocationEngine");
        var childId = ctx.LogEvent(
            WorldEventKind.ExtraordinaryCostPaid, "cost", "ExtraordinaryInvocationEngine", causeEventId: rootId);

        Assert.Equal(0, rootId);
        Assert.Equal(1, childId);
        Assert.Equal(2, world.NextHistoryEventId);
        Assert.Equal(2, sink.Events.Count);
        Assert.Null(sink.Events[0].CauseEventId);
        Assert.Equal(rootId, sink.Events[1].CauseEventId);
        Assert.Equal("ExtraordinaryInvocationEngine", sink.Events[0].SourceSystem);
    }

    [Fact]
    public void Same_seed_produces_identical_EventId_sequence()
    {
        static List<long> Run()
        {
            var world = new WorldState(
                ScenarioRunner.DefaultCalendar, seed: 7, ScenarioRunner.DefaultMap(1),
                ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
                ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
                ScenarioRunner.DefaultLifeStageRules);
            var sink = new CapturingSink();
            var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
            return
            [
                ctx.LogEvent(WorldEventKind.Birth, "a", "natality"),
                ctx.LogEvent(WorldEventKind.Death, "b", "mortality", causeEventId: 0),
            ];
        }

        Assert.Equal(Run(), Run());
    }

    /// <summary>Soft follow-up: Simulation call sites must pass explicit SourceSystem —
    /// only the TickContext 2-arg wrapper (legacy Unknown default) is allowlisted.</summary>
    [Fact]
    public void Simulation_LogEvent_call_sites_use_explicit_SourceSystem()
    {
        string repoRoot = FindRepoRoot();
        string simDir = Path.Combine(repoRoot, "src", "LivingWorld.Simulation");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(simDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            string text = File.ReadAllText(file);
            string rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            foreach (Match m in Regex.Matches(text, @"\.LogEvent\s*\("))
            {
                int open = m.Index + m.Length - 1;
                int end = FindMatchingParen(text, open);
                string call = text[m.Index..end];
                string prelude = text[Math.Max(0, m.Index - 100)..m.Index];
                if (prelude.Contains("public long LogEvent", StringComparison.Ordinal))
                    continue; // method definitions

                // Allowlist: TickContext 2-arg wrapper that deliberately defaults Unknown.
                if (rel.EndsWith("TickContext.cs", StringComparison.Ordinal)
                    && call.Contains("sourceSystem: \"Unknown\"", StringComparison.Ordinal))
                    continue;

                int commas = CountTopLevelCommas(call[(call.IndexOf('(') + 1)..^1]);
                bool hasExplicitSource =
                    call.Contains("sourceSystem", StringComparison.Ordinal)
                    || Regex.IsMatch(call, @",\s*""[A-Za-z][^""]*""\s*(,|\))")
                    || Regex.IsMatch(call, @",\s*source\s*(,|\))")
                    || Regex.IsMatch(call, @",\s*SystemName\s*(,|\))");

                if (commas <= 1 && !hasExplicitSource)
                {
                    int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    offenders.Add($"{rel}:{line}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "2-arg LogEvent call sites still defaulting SourceSystem=Unknown: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void High_traffic_paths_emit_non_Unknown_SourceSystem()
    {
        var (world, ctx, sink) = Build();
        world.Mint(ctx, new Money(5), "test-mint");
        _ = world.Destroy(ctx, new Money(1), "test-destroy");

        Assert.Contains(sink.Events, e =>
            e.Kind == WorldEventKind.Minted && e.SourceSystem == "WorldState");
        Assert.Contains(sink.Events, e =>
            e.Kind == WorldEventKind.Destroyed && e.SourceSystem == "WorldState");
        Assert.DoesNotContain(sink.Events, e => e.SourceSystem == "Unknown");
    }

    private static int FindMatchingParen(string text, int openIdx)
    {
        int i = openIdx + 1;
        int depth = 1;
        bool inStr = false;
        char strCh = '\0';
        while (i < text.Length && depth > 0)
        {
            char ch = text[i];
            if (inStr)
            {
                if (ch == '\\' && i + 1 < text.Length) { i += 2; continue; }
                if (ch == strCh) inStr = false;
                i++;
                continue;
            }
            if (ch is '"' or '\'') { inStr = true; strCh = ch; i++; continue; }
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            i++;
        }
        return i;
    }

    private static int CountTopLevelCommas(string args)
    {
        int d = 0, commas = 0;
        bool inStr = false;
        char strCh = '\0';
        for (int i = 0; i < args.Length; i++)
        {
            char ch = args[i];
            if (inStr)
            {
                if (ch == '\\' && i + 1 < args.Length) { i++; continue; }
                if (ch == strCh) inStr = false;
                continue;
            }
            if (ch is '"' or '\'') { inStr = true; strCh = ch; continue; }
            if (ch == '(') d++;
            else if (ch == ')') d--;
            else if (ch == ',' && d == 0) commas++;
        }
        return commas;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException(
            "LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
