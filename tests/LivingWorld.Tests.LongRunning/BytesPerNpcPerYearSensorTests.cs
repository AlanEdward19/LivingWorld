using System.Text;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.LongRunning;

/// <summary>Task 13: mede bytes de snapshot + event log por NPC/ano em 10 anos e reprova acima
/// do teto declarado no cenário (R3 — nenhum número mágico no teste, só em
/// <see cref="ScenarioRunner.DefaultMaxBytesPerNpcPerYear"/>, que espelha
/// scenarios/default.json).</summary>
public class BytesPerNpcPerYearSensorTests
{
    private const long Years = 10;
    private const long Ticks = Years * 12 * 30 * 24;

    [Fact]
    public void Ten_year_run_stays_under_the_scenario_declared_bytes_per_npc_per_year_cap()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();

        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: Ticks);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed: 42);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);

        runner.Run(world, clock, sink, Ticks);

        long snapshotBytes = Encoding.UTF8.GetByteCount(repository.LoadLatestSnapshot(BranchId.Root)!.Json);
        long eventBytes = repository.LoadEvents(BranchId.Root)
            .Sum(e => Encoding.UTF8.GetByteCount(e.Kind) + Encoding.UTF8.GetByteCount(e.Payload));

        double bytesPerNpcPerYear = (double)(snapshotBytes + eventBytes) / (world.Npcs.Count * Years);

        Assert.True(
            bytesPerNpcPerYear <= ScenarioRunner.DefaultMaxBytesPerNpcPerYear,
            $"{bytesPerNpcPerYear:F1} bytes/NPC/ano excede o teto de {ScenarioRunner.DefaultMaxBytesPerNpcPerYear} do cenário");
    }
}
