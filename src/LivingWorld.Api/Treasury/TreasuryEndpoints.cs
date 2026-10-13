using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Treasury;

public sealed record TreasuryMonth(long Month, long Revenue, long Spending);

public sealed record CityTreasury(Guid CityId, long Balance, long FromDay, long ToDay, IReadOnlyList<TreasuryMonth> Months);

/// <summary>Tesouro de uma cidade: saldo atual e, mês a mês, o que entrou (imposto retido sobre salário, espólio sem
/// herdeiro) e o que saiu (material de obra). Só leitura; por padrão responde o último ano.</summary>
public static class TreasuryEndpoints
{
    public const int DefaultWindowDays = 360;

    public static void MapTreasuryEndpoints(this WebApplication app, WorldHost host) =>
        app.MapGet("/cities/{id:guid}/treasury", (Guid id, long? fromDay, long? toDay) =>
        {
            var world = host.Current;
            var city = world.FindCity(new CityId(id));
            if (city is null) return Results.NotFound($"cidade {id} não existe");

            long today = world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay;
            long to = toDay ?? today;
            long from = fromDay ?? Math.Max(0, to - DefaultWindowDays + 1);
            if (from < 0 || to < from) return Results.BadRequest("fromDay deve ser >= 0 e <= toDay");

            var months = world.TreasuryOf(city.Id, from, to)
                .Select(e => new TreasuryMonth(e.Month, e.Revenue, e.Spending))
                .ToArray();
            return Results.Ok(new CityTreasury(id, city.Treasury.Amount, from, to, months));
        })
        .Produces<CityTreasury>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest)
        .Produces<string>(StatusCodes.Status404NotFound);
}
