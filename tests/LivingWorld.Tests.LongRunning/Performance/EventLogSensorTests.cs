using System.Text;
using System.Text.Json;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Performance;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>event-log-size-budget: bytes do event log persistido (tipo mais payload, como o sensor de bytes por NPC) por NPC
/// vivo por ano, por tipo de evento, no fixture de 1.000 NPCs com snapshot mensal. Teto da Fase 9: 50 B/NPC vivo/ano; o
/// baseline por tipo fica em <c>tests/baselines/event-log-bytes.json</c> (110% do total). O que não é ruído sai da
/// compactação byte a byte igual ao que o sistema emitiu.</summary>
[Collection(ScalePerformanceCollection.Name)]
public class EventLogSensorTests
{
    private const double BudgetBytesPerNpcPerYear = 50.0;
    private const long YearTicks = 12L * 30 * 24;
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");

    private sealed class TeeSink(IWorldEventSink first, List<WorldEvent> raw) : IWorldEventSink
    {
        public void Record(WorldEvent evt)
        {
            raw.Add(evt);
            first.Record(evt);
        }
    }

    private sealed record Measured(
        Dictionary<string, double> BytesPerNpcPerYearByKind, double Total, List<WorldEvent> Raw,
        List<(string Kind, string Payload, string? Source)> Persisted);

    private static Measured Measure(int population)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();

        var repository = new SqliteWorldRepository(context);
        var buffer = new BufferingWorldEventSink();
        var raw = new List<WorldEvent>();
        var (world, baseClock) = ScaleScenarioFixture.CreateWorld(42, population);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new TeeSink(buffer, raw));
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: world.Calendar.HoursPerMonth);

        long aliveNpcTicks = 0;
        for (long t = 0; t < YearTicks; t++)
        {
            aliveNpcTicks += world.AliveNpcIndex.Alive.Count;
            clock.Tick(world);
            if (world.CurrentDate.TotalHours % world.Calendar.HoursPerMonth == 0)
                runner.Snapshot(world, buffer);
        }

        double npcYears = aliveNpcTicks / (double)YearTicks;
        var persisted = repository.LoadEvents(BranchId.Root).Select(e => (e.Kind, e.Payload, e.SourceSystem)).ToList();
        var byKind = new Dictionary<string, double>();
        foreach (var (kind, payload, _) in persisted)
            byKind[kind] = byKind.GetValueOrDefault(kind) + (Encoding.UTF8.GetByteCount(kind) + Encoding.UTF8.GetByteCount(payload)) / npcYears;
        return new Measured(byKind, byKind.Values.Sum(), raw, persisted);
    }

    [Fact]
    public void Compacted_event_log_stays_under_the_phase_9_budget_and_the_versioned_baseline_at_1000_npcs()
    {
        var measured = Measure(ScaleScenarioFixture.PopulationSmall);

        Assert.True(measured.Total <= BudgetBytesPerNpcPerYear,
            $"event log compactado: {measured.Total:F1} B/NPC/ano passa do teto de {BudgetBytesPerNpcPerYear} (por tipo: {string.Join(", ", measured.BytesPerNpcPerYearByKind.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value:F1}"))})");

        var baseline = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(Path.Combine(BaselinesDir, "event-log-bytes.json")))!;
        Assert.True(measured.Total <= 1.10 * baseline.Values.Sum(),
            $"event log {measured.Total:F1} B/NPC/ano passa de 110% do baseline versionado ({baseline.Values.Sum():F1})");
    }

    [Fact(Skip = "Regravar: dotnet test --filter ZZZ_record_event_log_baseline")]
    public void ZZZ_record_event_log_baseline()
    {
        var measured = Measure(ScaleScenarioFixture.PopulationSmall);
        File.WriteAllText(
            Path.Combine(BaselinesDir, "event-log-bytes.json"),
            JsonSerializer.Serialize(measured.BytesPerNpcPerYearByKind.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2)),
                new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Compaction_keeps_every_non_noise_event_byte_for_byte_and_keeps_the_noise_totals()
    {
        var measured = Measure(ScaleScenarioFixture.PopulationSmall);

        // o log persistido sai ordenado por tick (e, no mesmo tick, pela ordem de emissão): ordenação estável sobre o bruto
        var rawKept = measured.Raw.Where(e => !EventLogCompactor.IsNoise(e.Kind)).OrderBy(e => e.Tick).Select(e => (e.Kind.ToString(), e.Payload)).ToList();
        var persistedKept = measured.Persisted.Where(e => e.Source != EventLogCompactor.AggregateSource).Select(e => (e.Kind, e.Payload)).ToList();
        int firstDiff = Enumerable.Range(0, Math.Min(rawKept.Count, persistedKept.Count)).FirstOrDefault(i => rawKept[i] != persistedKept[i], -1);
        Assert.True(rawKept.Count == persistedKept.Count && firstDiff < 0,
            $"bruto {rawKept.Count} contra persistido {persistedKept.Count}; primeira diferença no índice {firstDiff}: bruto [{string.Join(" ; ", rawKept.Skip(Math.Max(0, firstDiff - 1)).Take(4))}] persistido [{string.Join(" ; ", persistedKept.Skip(Math.Max(0, firstDiff - 1)).Take(4))}]");

        foreach (var kind in new[] { WorldEventKind.ResourceLost, WorldEventKind.WageUnpaid, WorldEventKind.PlantMatured })
        {
            long rawCount = measured.Raw.Count(e => e.Kind == kind);
            long aggregatedCount = measured.Persisted
                .Where(e => e.Source == EventLogCompactor.AggregateSource && e.Kind == kind.ToString())
                .Sum(e => long.Parse(e.Payload.Split('|')[0]));
            Assert.Equal(rawCount, aggregatedCount);
        }

        Assert.Contains(measured.Persisted, e => e.Source == EventLogCompactor.AggregateSource);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }
}
