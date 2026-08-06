using LivingWorld.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T4: persistência de templates de período versionados. Mesmo padrão de
/// <c>HistorySnapshotReplayTests.OpenInMemoryDb</c> — sqlite <c>:memory:</c> real, migrado de
/// verdade, isolado por conexão nova a cada teste.</summary>
public class SqlitePeriodTemplateRepositoryTests
{
    private static PeriodTemplateRecord Template(string periodId = "medieval", int version = 1, string source = "external-ai") => new()
    {
        PeriodId = periodId,
        Version = version,
        PayloadJson = "{\"foo\":\"bar\"}",
        CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Source = source,
    };

    [Fact]
    public void Save_persists_a_new_template_and_round_trips_the_payload()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);

        var result = repository.Save(Template());

        Assert.True(result.IsSuccess, result.Error);
        var loaded = repository.Find("medieval", 1);
        Assert.NotNull(loaded);
        Assert.Equal("{\"foo\":\"bar\"}", loaded!.PayloadJson);
        Assert.Equal("external-ai", loaded.Source);
    }

    [Fact]
    public void Save_rejects_a_duplicate_PeriodId_and_Version_with_a_deterministic_conflict_error()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);
        repository.Save(Template());

        var result = repository.Save(Template());

        Assert.False(result.IsSuccess);
        Assert.Contains("medieval", result.Error);
        Assert.Contains("1", result.Error);
    }

    [Fact]
    public void Save_accepts_a_new_version_for_an_already_registered_PeriodId()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);
        repository.Save(Template(version: 1));

        var result = repository.Save(Template(version: 2));

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(repository.Find("medieval", 2));
    }

    [Fact]
    public void FindLatestVersion_returns_the_highest_version_for_the_period()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);
        repository.Save(Template(version: 1));
        repository.Save(Template(version: 2));
        repository.Save(Template(version: 3));

        var latest = repository.FindLatestVersion("medieval");

        Assert.NotNull(latest);
        Assert.Equal(3, latest!.Version);
    }

    [Fact]
    public void Find_returns_null_for_an_unregistered_PeriodId_or_version()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);

        Assert.Null(repository.Find("unknown", 1));
        Assert.Null(repository.FindLatestVersion("unknown"));
    }

    [Fact]
    public void ListLatestPerPeriod_returns_one_entry_per_distinct_PeriodId_at_its_highest_version()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqlitePeriodTemplateRepository(context);
        repository.Save(Template("medieval", 1));
        repository.Save(Template("medieval", 2));
        repository.Save(Template("prehistoric", 1));

        var catalog = repository.ListLatestPerPeriod();

        Assert.Equal(2, catalog.Count);
        Assert.Equal(2, catalog.Single(t => t.PeriodId == "medieval").Version);
        Assert.Equal(1, catalog.Single(t => t.PeriodId == "prehistoric").Version);
    }

    private static WorldDbContext OpenInMemoryDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
