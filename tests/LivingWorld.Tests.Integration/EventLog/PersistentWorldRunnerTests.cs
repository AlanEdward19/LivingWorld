using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Archive;
using LivingWorld.Simulation.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.EventLog;

/// <summary>Task 10: idempotência de replay — para cada snapshot em t ∈ {0, T/4, T/2, 3T/4},
/// reidratar e rodar até T produz o mesmo hash canônico da run contínua.</summary>
public class PersistentWorldRunnerTests
{
    private const ulong Seed = 7;
    private const long T = 400;

    [Theory]
    [InlineData(0)]
    [InlineData(T / 4)]
    [InlineData(T / 2)]
    [InlineData(3 * T / 4)]
    public void Resuming_from_a_snapshot_at_t_and_running_to_T_matches_the_continuous_hash_at_T(long snapshotTick)
    {
        var (continuousWorld, continuousClock) = ScenarioRunner.Create(Seed);
        continuousClock.Run(continuousWorld, T);
        string continuousHash = WorldSnapshot.CanonicalHash(continuousWorld);

        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 1);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, snapshotTick);
        runner.Snapshot(world, sink);

        var rehydrated = runner.LoadAt(snapshotTick)!;
        var resumeSink = new BufferingWorldEventSink();
        var resumeClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: resumeSink);
        resumeClock.Run(rehydrated, T - snapshotTick);

        Assert.Equal(continuousHash, WorldSnapshot.CanonicalHash(rehydrated));
    }

    [Fact]
    public void Loading_a_branch_with_no_snapshot_returns_null()
    {
        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 100);

        Assert.Null(runner.LoadLatest());
    }

    [Fact]
    public void Saving_a_new_world_at_tick_zero_replaces_higher_tick_snapshots_from_the_previous_world()
    {
        using var context = OpenInMemoryDb();
        var runner = new PersistentWorldRunner(
            new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 24);
        var sink = new BufferingWorldEventSink();

        var (oldWorld, oldClock) = ScenarioRunner.Create(seed: 1);
        oldClock.Run(oldWorld, 48);
        runner.Snapshot(oldWorld, sink);

        var (newWorld, _) = ScenarioRunner.Create(seed: 999);
        runner.Snapshot(newWorld, sink);

        var resumed = runner.LoadLatest();
        Assert.Equal((ulong)999, resumed!.Seed);
        Assert.Equal(0, resumed.CurrentDate.TotalHours);
    }

    [Fact]
    public void Disk_database_reopened_by_a_new_runner_resumes_the_last_saved_world()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"livingworld-continue-{Guid.NewGuid():N}.db");
        try
        {
            using (var first = OpenFileDb(dbPath))
            {
                var runner = new PersistentWorldRunner(
                    new SqliteWorldRepository(first), BranchId.Root, snapshotIntervalTicks: 24);
                var (created, clock) = ScenarioRunner.Create(seed: 8128);
                created.Rename("Vale persistido");
                clock.Run(created, 24);
                runner.Snapshot(created, new BufferingWorldEventSink());
            }

            using (var reopened = OpenFileDb(dbPath))
            {
                var continued = new PersistentWorldRunner(
                    new SqliteWorldRepository(reopened), BranchId.Root, snapshotIntervalTicks: 24).LoadLatest();

                Assert.Equal((ulong)8128, continued!.Seed);
                Assert.Equal("Vale persistido", continued.Name);
                Assert.Equal(24, continued.CurrentDate.TotalHours);
            }
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public void Cold_tier_archive_survives_a_snapshot_and_resume_cycle_through_a_real_coldtier_file()
    {
        string coldTierPath = Path.Combine(Path.GetTempPath(), $"livingworld-coldtier-{Guid.NewGuid():N}.bin");
        try
        {
            using var context = OpenInMemoryDb();
            var coldTier = new ColdTierPersistence(coldTierPath);
            var runner = new PersistentWorldRunner(
                new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 1, coldTier);
            var sink = new BufferingWorldEventSink();
            var (world, _) = ScenarioRunner.Create(Seed);

            // Arquivamento natural exige anos de simulação (rules.ColdArchiveAfterYears) — injeta
            // direto no arquivo frio pra testar só a fiação save/load, sem rodar T=anos de tick.
            world.ColdArchive.RestoreNpcSummaries(new Dictionary<long, ColdTierArchive.NpcSummary>
            {
                [1] = new(new NpcId(1), "Aldric", Sex.Male, WorldDate.Epoch(world.Calendar).AddYears(-60),
                    WorldDate.Epoch(world.Calendar).AddYears(-10), new CultureId(1), new ProfessionType(1)),
            });
            var archived = world.ColdArchive.AllNpcSummaries;
            runner.Snapshot(world, sink);

            var resumed = new PersistentWorldRunner(
                new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 1, coldTier).LoadLatest()!;

            Assert.Equal(archived.Count, resumed.ColdArchive.AllNpcSummaries.Count);
            foreach (var (id, summary) in archived)
                Assert.Equal(summary, resumed.ColdArchive.Lookup(id));
        }
        finally
        {
            File.Delete(coldTierPath);
        }
    }

    [Fact]
    public void Snapshot_without_a_coldtier_configured_behaves_exactly_as_before_this_change()
    {
        using var context = OpenInMemoryDb();
        var runner = new PersistentWorldRunner(new SqliteWorldRepository(context), BranchId.Root, snapshotIntervalTicks: 1);
        var sink = new BufferingWorldEventSink();
        var (world, clock) = ScenarioRunner.Create(Seed);
        clock.Run(world, T);

        runner.Snapshot(world, sink);
        var resumed = runner.LoadLatest()!;

        Assert.Empty(resumed.ColdArchive.AllNpcSummaries);
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

    private static WorldDbContext OpenFileDb(string path)
    {
        var options = new DbContextOptionsBuilder<WorldDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
