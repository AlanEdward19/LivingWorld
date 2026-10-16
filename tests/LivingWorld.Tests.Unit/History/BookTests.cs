using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T12: <see cref="Book"/> e <see cref="BookOperations"/> (HIST-09 AC1, AC2).</summary>
public class BookTests
{
    private static readonly HistoryRules Rules = HistoryRules.Default;

    private static (WorldState world, Fact fact, ReportState report, Book book) Sample()
    {
        var (world, _) = ScenarioRunner.Create(11, historyRules: Rules);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], npc.City, 0.8, "1|2");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(report);
        var book = new Book(
            world.NextBookIdAndAdvance(),
            report.Id,
            CopyOfBookId: null,
            Lost: false,
            LostAtTick: null,
            RediscoveredAtTick: null);
        world.AddBook(book);
        return (world, fact, report, book);
    }

    [Fact]
    public void Copy_creates_new_book_with_scribe_error_and_copy_chain()
    {
        var (world, fact, report, book) = Sample();
        _ = DistortionEngine.DistanceFromFact(report, fact, Rules, world.Rng, world);

        var result = BookOperations.Copy(world, book, Rules, nowTick: 30);

        Assert.True(result.IsSuccess);
        var (copyBook, copyReport) = result.Value!;
        Assert.Equal(book.Id, copyBook.CopyOfBookId);
        Assert.Equal(TransmissionMediumType.Book, copyReport.Medium);
        Assert.Equal(report.HopCount + 1, copyReport.HopCount);
        Assert.Contains(world.Books, b => b.Id == copyBook.Id);
        Assert.NotNull(world.FindReport(copyReport.Id));
    }

    [Fact]
    public void MarkLost_keeps_row_readable_and_sets_lost_flags()
    {
        var sink = new BufferingWorldEventSink();
        var (world, _, _, book) = Sample();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);

        var result = BookOperations.MarkLost(world, book.Id, ctx);

        Assert.True(result.IsSuccess);
        var lost = world.FindBook(book.Id)!;
        Assert.True(lost.Lost);
        Assert.Equal(ctx.CurrentTick, lost.LostAtTick);
        Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.BookLost);
    }

    [Fact]
    public void Book_digest_is_identical_across_two_separate_processes()
    {
        var a = RunDigestInSeparateProcess(42);
        var b = RunDigestInSeparateProcess(42);
        Assert.Equal(a, b);
    }

    private static string RunDigestInSeparateProcess(ulong seed) =>
        WorkersProcess.RunDigest($"history-book-digest {seed}");
}
