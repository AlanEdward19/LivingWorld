using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Treasury;

/// <summary>taxes-treasury C6-C7: <c>GET /cities/{id}/treasury</c> — saldo e receita e gasto por mês.</summary>
public class TreasuryEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public TreasuryEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private Guid SeedCityWithLedger()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var city = new City(world.NextCityId(), new CellCoord(7, 7), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        long month = world.Calendar.HoursPerMonth;
        world.RecordTreasury(city.Id, tick: 5, revenue: 40, spending: 0);
        world.RecordTreasury(city.Id, tick: 9, revenue: 10, spending: 25);
        world.RecordTreasury(city.Id, tick: 3 * month + 1, revenue: 7, spending: 0);
        return city.Id.Value;
    }

    [Fact]
    public async Task Interval_returns_the_months_with_summed_revenue_and_spending()
    {
        var id = SeedCityWithLedger();

        var response = await _factory.CreateClient().GetAsync($"/cities/{id}/treasury?fromDay=0&toDay=29");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var months = body.GetProperty("months").EnumerateArray().ToArray();
        Assert.Single(months); // o mês 3 fica fora do intervalo
        Assert.Equal(0, months[0].GetProperty("month").GetInt64());
        Assert.Equal(50, months[0].GetProperty("revenue").GetInt64());
        Assert.Equal(25, months[0].GetProperty("spending").GetInt64());
        Assert.True(body.TryGetProperty("balance", out _));
    }

    [Fact]
    public async Task Inverted_interval_is_rejected_with_400_and_an_unknown_city_is_404()
    {
        var id = SeedCityWithLedger();
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/cities/{id}/treasury?fromDay=20&toDay=5")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/cities/{Guid.NewGuid()}/treasury")).StatusCode);
    }
}
