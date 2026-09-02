using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.Infrastructure;

public class EventLogKindEncodingTests
{
    [Fact]
    public void Encode_first_occurrence_stores_literal_kind()
    {
        var pool = new StringInternPool();

        string encoded = EventLogKindEncoding.Encode("Birth", pool);

        Assert.Equal("Birth", encoded);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Encode_repeated_kind_stores_interned_reference()
    {
        var pool = new StringInternPool();

        Assert.Equal("Birth", EventLogKindEncoding.Encode("Birth", pool));
        string encoded = EventLogKindEncoding.Encode("Birth", pool);

        Assert.Equal($"{EventLogKindEncoding.InternedPrefix}0", encoded);
    }

    [Fact]
    public void Decode_legacy_plain_kind_returns_unchanged_and_seeds_pool()
    {
        var pool = new StringInternPool();

        string decoded = EventLogKindEncoding.Decode("Death", pool);

        Assert.Equal("Death", decoded);
        Assert.Equal(1, pool.Count);
        Assert.Equal("Death", pool.Resolve(0));
    }

    [Fact]
    public void Decode_interned_kind_resolves_from_seeded_pool()
    {
        var pool = new StringInternPool();
        EventLogKindEncoding.SeedPool(pool, ["Birth"]);

        string decoded = EventLogKindEncoding.Decode($"{EventLogKindEncoding.InternedPrefix}0", pool);

        Assert.Equal("Birth", decoded);
    }

    [Fact]
    public void SeedPool_rebuilds_dictionary_from_mixed_stored_kinds()
    {
        var pool = new StringInternPool();
        EventLogKindEncoding.SeedPool(
            pool,
            ["Birth", $"{EventLogKindEncoding.InternedPrefix}0", "Death"]);

        Assert.Equal(2, pool.Count);
        Assert.Equal("Birth", EventLogKindEncoding.Decode($"{EventLogKindEncoding.InternedPrefix}0", pool));
        Assert.Equal("Death", EventLogKindEncoding.Decode("Death", pool));
    }

    [Fact]
    public void Repository_round_trip_interns_repeated_kinds_on_disk()
    {
        using var context = OpenDb();
        var repository = new SqliteWorldRepository(context);
        var events = new List<WorldEvent>
        {
            new(1, WorldEventKind.Birth, "a", EventId: 1),
            new(1, WorldEventKind.Birth, "b", EventId: 2),
            new(2, WorldEventKind.Death, "c", EventId: 3),
        };

        repository.SaveSnapshotWithEvents(
            BranchId.Root, tick: 2, json: "{}", canonicalHash: "c", volatileHash: "v", events);

        var stored = context.EventLog
            .OrderBy(e => e.Tick)
            .ThenBy(e => e.Sequence)
            .Select(e => e.Kind)
            .ToArray();

        Assert.Equal("Birth", stored[0]);
        Assert.Equal($"{EventLogKindEncoding.InternedPrefix}0", stored[1]);
        Assert.Equal("Death", stored[2]);

        var loaded = repository.LoadEvents(BranchId.Root);
        Assert.Equal(["Birth", "Birth", "Death"], loaded.Select(e => e.Kind).ToArray());
    }

    [Fact]
    public void Repository_loads_pre_interning_rows_without_mutation()
    {
        using var context = OpenDb();
        context.EventLog.Add(new EventLogRecord
        {
            BranchId = BranchId.Root.Value,
            Tick = 5,
            Sequence = 0,
            Kind = WorldEventKind.Marriage.ToString(),
            Payload = "1|2",
            EventId = 10,
        });
        context.SaveChanges();

        var loaded = Assert.Single(new SqliteWorldRepository(context).LoadEvents(BranchId.Root));

        Assert.Equal("Marriage", loaded.Kind);
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
}
