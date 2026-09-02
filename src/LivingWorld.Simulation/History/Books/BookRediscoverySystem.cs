using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.History.Books;

/// <summary>Redescoberta de livro perdido via evento agendado (Fase 10, HIST-09 AC3) — nunca
/// sorteio implícito por tick.</summary>
public sealed class BookRediscoverySystem : ISimulationSystem
{
    public const string SystemName = "history-book-rediscovery";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (!world.HistoryRules.Enabled) return;
        if (!long.TryParse(evt.Payload, out var bookIdValue)) return;
        OnRediscovered(world, ctx, new BookId(bookIdValue));
    }

    public static void ScheduleRediscovery(BookId bookId, long targetTick, TickContext ctx) =>
        ctx.ScheduleEvent(targetTick, SystemName, bookId.Value.ToString());

    public static Result<Book> OnRediscovered(WorldState world, TickContext ctx, BookId bookId)
    {
        var book = world.FindBook(bookId);
        if (book is null)
            return Result<Book>.Fail("book_not_found");

        if (!book.Lost)
            return Result<Book>.Fail("book_not_lost");

        var rediscovered = book.WithRediscovered(ctx.CurrentTick);
        world.ReplaceBook(rediscovered);
        ctx.LogEvent(
            WorldEventKind.BookRediscovered, $"{bookId.Value}|{ctx.CurrentTick}",
            sourceSystem: "BookRediscoverySystem");
        return Result<Book>.Ok(rediscovered);
    }
}
