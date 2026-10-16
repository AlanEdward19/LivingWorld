using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Trade;

public sealed record TradeDay(long Day, int ResourceId, long Quantity, long Value, long Purchases);

public sealed record WorkplaceTrades(long WorkplaceId, long FromDay, long ToDay, IReadOnlyList<TradeDay> Days);

/// <summary>Livro de trocas de uma oficina, dia a dia: o que ela vendeu de cada recurso, quanto valeu e em quantas compras.
/// Só leitura. Os dias já compactados em mês saem do livro diário (a janela de detalhe é
/// <c>WorldState.TradeLedgerDetailDays</c>); por padrão responde os últimos 30 dias.</summary>
public static class TradeEndpoints
{
    public const int DefaultWindowDays = 30;

    public static void MapTradeEndpoints(this WebApplication app, WorldHost host) =>
        app.MapGet("/workplaces/{id:long}/trades", (long id, long? fromDay, long? toDay) =>
        {
            var world = host.Current;
            var workplace = world.FindWorkplace(new WorkplaceId(id));
            if (workplace is null) return Results.NotFound($"oficina {id} não existe");

            long today = world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay;
            long to = toDay ?? today;
            long from = fromDay ?? Math.Max(0, to - DefaultWindowDays + 1);
            if (from < 0 || to < from) return Results.BadRequest("fromDay deve ser >= 0 e <= toDay");

            var days = world.TradesOf(workplace.Id, from, to)
                .OrderBy(e => e.Period).ThenBy(e => e.ResourceId)
                .Select(e => new TradeDay(e.Period, e.ResourceId, e.Quantity, e.Value, e.Purchases))
                .ToArray();
            return Results.Ok(new WorkplaceTrades(id, from, to, days));
        })
        .Produces<WorkplaceTrades>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest)
        .Produces<string>(StatusCodes.Status404NotFound);
}
