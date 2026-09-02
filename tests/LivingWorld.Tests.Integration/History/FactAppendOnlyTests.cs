using LivingWorld.Domain.History;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Records;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.History;

/// <summary>Fase 10, T3: append-only real da tabela de fatos (HIST-02 AC2).</summary>
public class FactAppendOnlyTests
{
    [Fact]
    public void Direct_update_on_fact_log_is_rejected()
    {
        using var context = OpenInMemoryDb();
        InsertSampleFact(context);

        var ex = Assert.Throws<SqliteException>(() =>
            context.Database.ExecuteSqlRaw("UPDATE FactLog SET Payload = 'tampered' WHERE FactId = 1"));

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Direct_delete_on_fact_log_is_rejected()
    {
        using var context = OpenInMemoryDb();
        InsertSampleFact(context);

        var ex = Assert.Throws<SqliteException>(() =>
            context.Database.ExecuteSqlRaw("DELETE FROM FactLog WHERE FactId = 1"));

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void InsertSampleFact(WorldDbContext context)
    {
        context.FactLog.Add(new FactLogRecord
        {
            BranchId = 0,
            FactId = 1,
            Tick = 10,
            Kind = nameof(WorldEventKind.Death),
            Participants = "7",
            Significance = 0.9,
            Payload = "7",
        });
        context.SaveChanges();
    }

    private static WorldDbContext OpenInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
