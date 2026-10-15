using System.Net;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Formas congeladas de <c>/cities/{id}/treasury</c> e <c>/workplaces/{id}/trades</c> (#59 fatia F). Factory própria:
/// semeia cidade, oficina e livros no mundo.</summary>
public class EconomyContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public EconomyContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Treasury_and_trades_shapes_match_the_committed_contracts()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var city = new City(world.NextCityId(), new CellCoord(7, 7), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        world.RecordTreasury(city.Id, tick: 5, revenue: 40, spending: 0);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(0, 0), 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        world.RecordTrade(workplace.Id, new ResourceType(1), day: 0, quantity: 3, value: 9);
        var client = _factory.CreateClient();

        var treasury = await client.GetAsync($"/cities/{city.Id.Value}/treasury?fromDay=0&toDay=29");
        Assert.Equal(HttpStatusCode.OK, treasury.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.city-treasury.json", ApiContractTests.ShapeOf(JsonNode.Parse(await treasury.Content.ReadAsStringAsync())));

        var trades = await client.GetAsync($"/workplaces/{workplace.Id.Value}/trades?fromDay=0&toDay=29");
        Assert.Equal(HttpStatusCode.OK, trades.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.workplace-trades.json", ApiContractTests.ShapeOf(JsonNode.Parse(await trades.Content.ReadAsStringAsync())));
    }
}
