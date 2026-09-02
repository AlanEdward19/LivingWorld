using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T13: <see cref="BookRediscoverySystem"/> (HIST-09 AC3).</summary>
public class BookRediscoverySystemTests
{
    private static readonly HistoryRules Rules = HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 2,
        mediumFidelityByType: HistoryRules.Default.MediumFidelityByType,
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1,
        transmissibilityWeight: 1,
        recencyWeight: 1).Value!;

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static (WorldState world, Book book, Fact fact, ReportState canonReport) PlantBookWithCanon(WorldState world)
    {
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Death, [npc.Id], npc.City, 0.95, npc.Id.Value.ToString());
        world.AddFact(fact);

        var archivedReport = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.OralTradition,
            HopCount: 1,
            Weight: 0.4,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(archivedReport);

        var book = new Book(
            world.NextBookIdAndAdvance(),
            archivedReport.Id,
            CopyOfBookId: null,
            Lost: false,
            LostAtTick: null,
            RediscoveredAtTick: null);
        world.AddBook(book);

        var canonReport = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.Song,
            HopCount: 3,
            Weight: 0.95,
            CreatedAtTick: 20,
            LastHopTick: 20);
        world.RegisterReport(canonReport);
        CanonSlotManager.Admit(city, canonReport, Rules, nowTick: 20);

        return (world, book, fact, canonReport);
    }

    [Fact]
    public void Lost_book_without_scheduled_event_stays_lost_after_ticks()
    {
        var (world, clock) = ScenarioRunner.Create(12, historyRules: Rules);
        var (_, book, _, _) = PlantBookWithCanon(world);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, null);
        BookOperations.MarkLost(world, book.Id, ctx);

        clock.Run(world, ticks: 500);

        var stillLost = world.FindBook(book.Id)!;
        Assert.True(stillLost.Lost);
        Assert.Null(stillLost.RediscoveredAtTick);
    }

    [Fact]
    public void Scheduled_rediscovery_event_restores_book_on_target_tick()
    {
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(13, historyRules: Rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        var (_, book, _, _) = PlantBookWithCanon(world);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        BookOperations.MarkLost(world, book.Id, ctx);

        long targetTick = world.CurrentDate.TotalHours + 10;
        BookRediscoverySystem.ScheduleRediscovery(book.Id, targetTick, ctx);

        long ticksToRun = targetTick - world.CurrentDate.TotalHours;
        clock.Run(world, ticksToRun);

        var restored = world.FindBook(book.Id)!;
        Assert.False(restored.Lost);
        Assert.NotNull(restored.RediscoveredAtTick);
        Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.BookRediscovered);
    }

    [Fact]
    public void Rediscovered_book_content_can_diverge_from_live_canon()
    {
        var (world, book, fact, canonReport) = PlantBookWithCanon(
            ScenarioRunner.Create(14, historyRules: Rules).World);

        var bookReport = world.FindReport(book.CarriesReportId)!;
        Assert.NotEqual(canonReport.HopCount, bookReport.HopCount);
        Assert.Equal(fact.Id, bookReport.OriginFactId);
        Assert.Equal(fact.Id, canonReport.OriginFactId);

        var ctx = new TickContext(world, world.Rng, world.Scheduler, null);
        BookOperations.MarkLost(world, book.Id, ctx);
        BookRediscoverySystem.OnRediscovered(world, ctx, book.Id);

        var city = world.FindCity(fact.Location!.Value)!;
        var liveCanon = city.CanonSlots.Single(r => r.OriginFactId == fact.Id);
        var rediscoveredReport = world.FindReport(world.FindBook(book.Id)!.CarriesReportId)!;

        Assert.Equal(canonReport.Id, liveCanon.Id);
        Assert.Equal(bookReport.Id, rediscoveredReport.Id);
        Assert.NotEqual(liveCanon.HopCount, rediscoveredReport.HopCount);
    }
}
