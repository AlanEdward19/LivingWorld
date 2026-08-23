using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Infrastructure;

/// <summary>Digest determinístico dos sistemas de história da Fase 10 — usado pelos testes de
/// dois processos (mesmo padrão de <c>ScenarioRunner.RunAndHash</c>).</summary>
public static class HistorySimulationDigest
{
    public static string SignificanceDigest(ulong seed, long ticks, HistoryRules rules)
    {
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, ticks);

        var lines = sink.DrainAll()
            .OrderBy(e => e.Tick)
            .ThenBy(e => e.Kind)
            .ThenBy(e => e.Payload, StringComparer.Ordinal)
            .Select(e => $"{e.Tick}:{e.Kind}:{SignificanceCalculator.Compute(e, world, rules):F6}");
        return Hash(string.Join('\n', lines));
    }

    public static string LivingMemoryDigest(ulong seed, long ticks, HistoryRules rules)
    {
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, ticks);

        foreach (var evt in sink.DrainAll())
        {
            if (SignificanceCalculator.TryRecord(evt, world, rules).IsSuccess)
                continue;
        }

        var lines = world.Facts
            .OrderBy(f => f.Id.Value)
            .Select(f =>
            {
                bool open = LivingMemoryWindow.HasLivingWitness(f, world);
                var recall = LivingMemoryWindow.Recall(f, world);
                return $"{f.Id}:{open}:{recall.PerceivedSignificance:F6}:{recall.Payload}";
            });
        return Hash(string.Join('\n', lines));
    }

    public static string HistoryIndexDigest(ulong seed, long ticks, HistoryRules rules)
    {
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, ticks);

        foreach (var evt in sink.DrainAll())
            SignificanceCalculator.TryRecord(evt, world, rules);

        var index = HistoryIndex.RebuildFrom(world);
        var lines = new List<string>();
        for (int year = 0; year <= (int)(ticks / world.Calendar.HoursPerYear); year++)
        {
            foreach (var id in index.ByYear(year))
                lines.Add($"Y{year}:{id.Value}");
        }
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            foreach (var id in index.ByEntity(npc.Id))
                lines.Add($"E{npc.Id.Value}:{id.Value}");
        }
        foreach (var kind in Enum.GetValues<WorldEventKind>().OrderBy(k => k))
        {
            foreach (var id in index.ByKind(kind))
                lines.Add($"K{kind}:{id.Value}");
        }
        return Hash(string.Join('\n', lines));
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
