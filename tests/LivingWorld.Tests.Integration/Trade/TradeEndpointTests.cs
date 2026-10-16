using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Trade;

/// <summary>trade-ledger C9-C11: <c>GET /workplaces/{id}/trades</c> — livro diario de uma oficina.</summary>
public class TradeEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public TradeEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private long SeedWorkplaceWithTrades()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(0, 0), 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        world.RecordTrade(workplace.Id, new ResourceType(2), day: 12, quantity: 4, value: 20);
        world.RecordTrade(workplace.Id, new ResourceType(1), day: 10, quantity: 3, value: 9);
        world.RecordTrade(workplace.Id, new ResourceType(1), day: 10, quantity: 2, value: 6);
        world.RecordTrade(workplace.Id, new ResourceType(1), day: 50, quantity: 1, value: 3);
        return workplace.Id.Value;
    }

    [Fact]
    public async Task Interval_returns_the_days_in_order_with_summed_purchases()
    {
        long id = SeedWorkplaceWithTrades();

        var response = await _factory.CreateClient().GetAsync($"/workplaces/{id}/trades?fromDay=0&toDay=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var days = body.GetProperty("days").EnumerateArray().ToArray();
        Assert.Equal(2, days.Length); // o dia 50 fica fora do intervalo
        Assert.Equal(10, days[0].GetProperty("day").GetInt64());
        Assert.Equal(1, days[0].GetProperty("resourceId").GetInt32());
        Assert.Equal(5, days[0].GetProperty("quantity").GetInt64());
        Assert.Equal(15, days[0].GetProperty("value").GetInt64());
        Assert.Equal(2, days[0].GetProperty("purchases").GetInt64());
        Assert.Equal(12, days[1].GetProperty("day").GetInt64());
    }

    [Fact]
    public async Task Inverted_interval_is_rejected_with_400()
    {
        long id = SeedWorkplaceWithTrades();

        var response = await _factory.CreateClient().GetAsync($"/workplaces/{id}/trades?fromDay=20&toDay=5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_workplace_is_404()
    {
        var response = await _factory.CreateClient().GetAsync("/workplaces/999999/trades");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
