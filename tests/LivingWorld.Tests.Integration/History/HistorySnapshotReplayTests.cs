using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T21: snapshot em t, reidratar e reaplicar o log até T reproduz o mesmo
/// <c>Hash(world)</c> da execução contínua — mesmo padrão de <see cref="PersistentWorldRunnerTests"/>,
/// com Fact/ReportState(hop&gt;0)/Book já em jogo no ponto do snapshot (HIST-26).</summary>
public class HistorySnapshotReplayTests
{
    private const ulong Seed = 7;
    private const long SnapshotTick = 100;
    private const long T = 400;

    private static readonly HistoryRules Rules = HistoryRules.Default;

    [Fact]
    public void Resuming_from_a_snapshot_with_history_in_play_matches_the_continuous_hash_at_T()
    {
        var (continuousWorld, continuousClock) = ScenarioRunner.Create(Seed, historyRules: Rules);
        continuousClock.Run(continuousWorld, SnapshotTick);
        SeedHistory(continuousWorld, Rules, SnapshotTick);
        continuousClock.Run(continuousWorld, T - SnapshotTick);
        string continuousHash = WorldSnapshot.CanonicalHash(continuousWorld);

        using var context = OpenInMemoryDb();
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: 1);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(Seed, historyRules: Rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, SnapshotTick);
        SeedHistory(world, Rules, SnapshotTick);
        runner.Snapshot(world, sink);

        var rehydrated = runner.LoadAt(SnapshotTick)!;
        var resumeSink = new BufferingWorldEventSink();
        var resumeClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: resumeSink);
        resumeClock.Run(rehydrated, T - SnapshotTick);

        Assert.Equal(continuousHash, WorldSnapshot.CanonicalHash(rehydrated));
        var report = rehydrated.Facts.SelectMany(f => rehydrated.Cities
            .SelectMany(c => c.CanonSlots)
            .Where(r => r.OriginFactId == f.Id))
            .Single();
        Assert.True(report.HopCount > 0);
        Assert.Single(rehydrated.Books);
    }

    /// <summary>Semeia um Fact + ReportState(hop 1, via <see cref="DistortionEngine.AdvanceHop"/>)
    /// + Book no mesmo tick, em dois mundos deterministicamente idênticos (mesmo seed, mesmo
    /// número de ticks já rodados) — mesmo espírito de <c>HistoryBookDigest</c>.</summary>
    private static void SeedHistory(WorldState world, HistoryRules rules, long nowTick)
    {
        var npc = world.Npcs[0];
        var city = world.FindCity(npc.City) ?? EnsureCity(world, npc.City);

        var fact = new Fact(world.NextFactIdAndAdvance(), nowTick, WorldEventKind.Marriage, [npc.Id], npc.City, 0.9, "1|2");
        world.AddFact(fact);

        var report0 = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: 0, Weight: fact.Significance, CreatedAtTick: nowTick, LastHopTick: nowTick);
        var report = DistortionEngine.AdvanceHop(report0, fact, rules, world.Rng, world, nowTick);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, rules, nowTick);

        var book = new Book(world.NextBookIdAndAdvance(), report.Id, CopyOfBookId: null, Lost: false, LostAtTick: null, RediscoveredAtTick: null);
        world.AddBook(book);
    }

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
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
