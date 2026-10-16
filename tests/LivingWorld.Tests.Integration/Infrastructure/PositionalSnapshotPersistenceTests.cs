using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.Infrastructure;

/// <summary>snapshot-positional-binary, S4: o runner grava base completa e diffs binários, reabre o mundo da cadeia
/// (base + diff) e continua com hash igual à execução contínua; linhas antigas só com JSON seguem legíveis.</summary>
public class PositionalSnapshotPersistenceTests
{
    private const ulong Seed = 11;

    private static WorldDbContext OpenInMemoryDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();
        return context;
    }

    [Fact]
    public void Saving_the_same_nonzero_tick_repeatedly_never_creates_a_self_referencing_delta()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, 24);
        var (world, clock) = ScenarioRunner.Create(Seed);
        clock.Run(world, 13);
        var sink = new BufferingWorldEventSink();
        string hash = WorldSnapshot.CanonicalHash(world);
        runner.Snapshot(world, sink);
        runner.Snapshot(world, sink);
        var row = Assert.Single(context.Snapshots);
        Assert.Null(row.BaseTick);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(new PersistentWorldRunner(repository, BranchId.Root, 24).LoadLatest()!));
    }

    [Fact]
    public void The_runner_writes_a_full_base_then_diffs_and_starts_a_new_base_after_twelve_diffs()
    {
        using var context = OpenInMemoryDb();
        var runner = new PersistentWorldRunner(new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 24);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);

        // A primeira contratação e o primeiro encontro no turno expandem o estado legitimamente.
        // Compara compactação incremental depois dessas duas fronteiras de bootstrap.
        clock.Run(world, 2 * world.Calendar.HoursPerDay);
        runner.Snapshot(world, sink);
        runner.Run(world, clock, sink, 24 * 15);

        var rows = context.Snapshots.OrderBy(s => s.Tick).ToList();
        Assert.Equal(16, rows.Count);
        Assert.All(rows, r => Assert.Equal(string.Empty, r.Json));
        Assert.All(rows, r => Assert.NotNull(r.Binary));
        Assert.Null(rows[0].BaseTick);
        Assert.All(rows.Skip(1).Take(12), r => Assert.Equal(rows[0].Tick, r.BaseTick));
        Assert.Null(rows[13].BaseTick); // 13ª gravação depois da base: base nova
        Assert.Equal(rows[13].Tick, rows[14].BaseTick);
        Assert.True(rows[1].Binary!.Length < rows[0].Binary!.Length);
    }

    [Fact]
    public void A_world_reopened_from_base_plus_diff_continues_with_the_continuous_hash()
    {
        const long snapshotAt = 24 * 7;
        const long later = 3650;

        var (continuous, continuousClock) = ScenarioRunner.Create(Seed);
        continuousClock.Run(continuous, snapshotAt + later);
        string expected = WorldSnapshot.CanonicalHash(continuous);

        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 24);
        runner.Snapshot(world, sink);
        runner.Run(world, clock, sink, snapshotAt);
        Assert.NotNull(context.Snapshots.Single(s => s.Tick == snapshotAt).BaseTick);

        // outro runner, sem nada em memória: só o que está no banco (base + diff)
        var reopened = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 24).LoadLatest()!;
        var resumeClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink());
        resumeClock.Run(reopened, later);

        Assert.Equal(expected, WorldSnapshot.CanonicalHash(reopened));
    }

    [Fact]
    public void A_runner_that_reopened_a_world_from_a_diff_starts_a_new_base_and_chains_diffs_on_it()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        var first = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 24);
        first.Snapshot(world, sink);
        first.Run(world, clock, sink, 24 * 3);

        var second = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 24);
        var reopened = second.LoadLatest()!;
        var reopenedSink = new BufferingWorldEventSink();
        var reopenedClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: reopenedSink);
        second.Run(reopened, reopenedClock, reopenedSink, 24 * 2);

        var rows = context.Snapshots.OrderBy(s => s.Tick).ToList();
        Assert.Null(rows[4].BaseTick); // primeira gravação depois de reabrir de um diff: base nova, a cadeia não cresce sem fim
        Assert.Equal(rows[4].Tick, rows[5].BaseTick);
        var again = second.LoadLatest()!;
        Assert.Equal(WorldSnapshot.CanonicalHash(reopened), WorldSnapshot.CanonicalHash(again));
    }

    [Fact]
    public void A_legacy_json_only_row_loads_through_the_json_reader_and_the_next_save_is_a_full_binary_base()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var (world, clock) = ScenarioRunner.Create(Seed);
        clock.Run(world, 48);
        repository.SaveSnapshotWithEvents(
            BranchId.Root, 48, WorldSnapshot.Serialize(world), WorldSnapshot.CanonicalHash(world),
            WorldSnapshot.VolatileHash(world), []);

        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 24);
        var loaded = runner.LoadLatest()!;
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(loaded));

        var sink = new BufferingWorldEventSink();
        var loadedClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        runner.Run(loaded, loadedClock, sink, 24);

        var newest = context.Snapshots.OrderBy(s => s.Tick).Last();
        Assert.Equal(72, newest.Tick);
        Assert.NotNull(newest.Binary);
        Assert.Null(newest.BaseTick);
    }

    [Fact]
    public void A_diff_row_loads_by_tick_through_its_base()
    {
        using var context = OpenInMemoryDb();
        var runner = new PersistentWorldRunner(new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 24);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        runner.Snapshot(world, sink);
        runner.Run(world, clock, sink, 24 * 4);

        var atDay3 = runner.LoadAt(24 * 3)!;
        var hashRow = context.Snapshots.Single(s => s.Tick == 24 * 3);
        Assert.NotNull(hashRow.BaseTick);
        Assert.Equal(hashRow.CanonicalHash, WorldSnapshot.CanonicalHash(atDay3));
    }

    private sealed class FlakyRepository(IWorldRepository inner) : IWorldRepository
    {
        public bool FailNext { get; set; }

        public void SaveSnapshotWithEvents(
            BranchId branch, long tick, string json, string canonicalHash, string volatileHash,
            IReadOnlyList<LivingWorld.Simulation.Core.WorldEvent> events, byte[]? binary = null, long? baseTick = null)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new IOException("disco cheio");
            }

            inner.SaveSnapshotWithEvents(branch, tick, json, canonicalHash, volatileHash, events, binary, baseTick);
        }

        public LivingWorld.Infrastructure.Records.WorldSnapshotRecord? LoadLatestSnapshot(BranchId branch) => inner.LoadLatestSnapshot(branch);

        public LivingWorld.Infrastructure.Records.WorldSnapshotRecord? LoadSnapshotAt(BranchId branch, long tick) => inner.LoadSnapshotAt(branch, tick);

        public IReadOnlyList<LivingWorld.Infrastructure.Records.EventLogRecord> LoadEvents(BranchId branch) => inner.LoadEvents(branch);
    }

    [Fact]
    public void A_failed_save_drops_the_base_so_the_next_save_is_a_full_block()
    {
        using var context = OpenInMemoryDb();
        var flaky = new FlakyRepository(new SqliteWorldRepository(context));
        var runner = new PersistentWorldRunner(flaky, BranchId.Root, snapshotIntervalTicks: 24);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        runner.Snapshot(world, sink);
        runner.Run(world, clock, sink, 24); // um diff gravado

        flaky.FailNext = true;
        clock.Run(world, 24);
        Assert.Throws<IOException>(() => runner.Snapshot(world, sink));
        clock.Run(world, 24);
        runner.Snapshot(world, sink);

        var newest = context.Snapshots.OrderBy(s => s.Tick).Last();
        Assert.Equal(72, newest.Tick);
        Assert.Null(newest.BaseTick);
    }

    [Fact]
    public void A_diff_whose_base_row_was_replaced_by_another_world_is_rejected_instead_of_decoding_wrong()
    {
        using var context = OpenInMemoryDb();
        var runner = new PersistentWorldRunner(new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 24);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        runner.Snapshot(world, sink);
        runner.Run(world, clock, sink, 24 * 2);

        var (other, _) = ScenarioRunner.Create(seed: 99);
        context.Snapshots.Single(s => s.Tick == 0).Binary = LivingWorld.Simulation.Snapshot.PositionalSnapshot.EncodeFull(other, out _);
        context.SaveChanges();

        Assert.Throws<InvalidDataException>(() => runner.LoadAt(24 * 2));
    }
}
