using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Operações sobre <see cref="Book"/> (Fase 10, HIST-09) — cópia com erro de copista
/// e perda sem apagar a linha.</summary>
public static class BookOperations
{
    public static Result<(Book Copy, ReportState CopyReport)> Copy(
        WorldState world,
        Book source,
        HistoryRules rules,
        long nowTick)
    {
        if (!rules.Enabled)
            return Result<(Book, ReportState)>.Fail("history_disabled");

        var report = world.FindReport(source.CarriesReportId);
        if (report is null)
            return Result<(Book, ReportState)>.Fail("report_not_found");

        var fact = world.FindFact(report.OriginFactId);
        if (fact is null)
            return Result<(Book, ReportState)>.Fail("fact_not_found");

        var forCopy = report with { Medium = TransmissionMediumType.Book };
        var distorted = DistortionEngine.AdvanceHop(forCopy, fact, rules, world.Rng, world, nowTick);
        var copyReport = distorted with { Id = world.NextReportIdAndAdvance() };
        world.RegisterReport(copyReport);

        var copyBook = new Book(
            world.NextBookIdAndAdvance(),
            copyReport.Id,
            source.Id,
            Lost: false,
            LostAtTick: null,
            RediscoveredAtTick: null);
        world.AddBook(copyBook);

        return Result<(Book, ReportState)>.Ok((copyBook, copyReport));
    }

    public static Result<Book> MarkLost(WorldState world, BookId bookId, TickContext ctx)
    {
        var book = world.FindBook(bookId);
        if (book is null)
            return Result<Book>.Fail("book_not_found");

        if (book.Lost)
            return Result<Book>.Ok(book);

        var lost = book.MarkLost(ctx.CurrentTick);
        world.ReplaceBook(lost);
        ctx.LogEvent(WorldEventKind.BookLost, $"{bookId.Value}|{ctx.CurrentTick}", sourceSystem: "BookOperations");
        return Result<Book>.Ok(lost);
    }
}
