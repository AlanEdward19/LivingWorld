using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.History;

/// <summary>COH-04: EventLogRecord persiste EventId/CauseEventId/SourceSystem nullable ao lado
/// de Sequence (sem Sqlite:Autoincrement — ADR-0002).</summary>
public class EventLogRecordTests
{
    [Fact]
    public void Round_trip_preserves_causal_provenance_fields()
    {
        using var context = OpenDb();
        var repository = new SqliteWorldRepository(context);
        var events = new List<WorldEvent>
        {
            new(10, WorldEventKind.ExtraordinaryUseAttempted, "attempt", EventId: 0,
                CauseEventId: null, SourceSystem: "ExtraordinaryInvocationEngine"),
            new(10, WorldEventKind.ExtraordinaryCostPaid, "cost", EventId: 1,
                CauseEventId: 0, SourceSystem: "ExtraordinaryInvocationEngine"),
        };

        repository.SaveSnapshotWithEvents(
            BranchId.Root, tick: 10, json: "{}", canonicalHash: "c", volatileHash: "v", events);

        var loaded = repository.LoadEvents(BranchId.Root);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(0, loaded[0].EventId);
        Assert.Null(loaded[0].CauseEventId);
        Assert.Equal("ExtraordinaryInvocationEngine", loaded[0].SourceSystem);
        Assert.Equal(1, loaded[1].EventId);
        Assert.Equal(0, loaded[1].CauseEventId);
        Assert.Equal(0, loaded[0].Sequence);
        Assert.Equal(1, loaded[1].Sequence);
    }

    [Fact]
    public void Nullable_provenance_columns_accept_legacy_null_rows()
    {
        using var context = OpenDb();
        context.EventLog.Add(new EventLogRecord
        {
            BranchId = BranchId.Root.Value,
            Tick = 1,
            Sequence = 0,
            Kind = WorldEventKind.Death.ToString(),
            Payload = "9",
            EventId = null,
            CauseEventId = null,
            SourceSystem = null,
        });
        context.SaveChanges();

        var loaded = Assert.Single(new SqliteWorldRepository(context).LoadEvents(BranchId.Root));
        Assert.Null(loaded.EventId);
        Assert.Null(loaded.CauseEventId);
        Assert.Null(loaded.SourceSystem);
        Assert.Equal("Death", loaded.Kind);
    }

    [Fact]
    public void Sequence_assignment_stays_repo_owned_not_database_autoincrement()
    {
        using var context = OpenDb();
        var repository = new SqliteWorldRepository(context);
        var events = new List<WorldEvent>
        {
            new(5, WorldEventKind.Birth, "a", EventId: 10, SourceSystem: "natality"),
            new(5, WorldEventKind.Birth, "b", EventId: 11, SourceSystem: "natality"),
            new(6, WorldEventKind.Death, "c", EventId: 12, SourceSystem: "mortality"),
        };

        repository.SaveSnapshotWithEvents(
            BranchId.Root, tick: 6, json: "{}", canonicalHash: "c", volatileHash: "v", events);

        var loaded = repository.LoadEvents(BranchId.Root);
        Assert.Equal([0, 1, 0], loaded.Select(e => e.Sequence).ToArray());
        Assert.DoesNotContain(
            "Autoincrement",
            File.ReadAllText(Path.Combine(
                FindRepoRoot(),
                "src/LivingWorld.Infrastructure/Migrations/20260826020000_AddEventLogCausalProvenance.cs")),
            StringComparison.OrdinalIgnoreCase);
    }

    private static WorldDbContext OpenDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        return context;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
