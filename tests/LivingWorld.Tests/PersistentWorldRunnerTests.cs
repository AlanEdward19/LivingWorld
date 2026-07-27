using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests;

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
