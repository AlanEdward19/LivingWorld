using System.Net;
using System.Net.Http.Json;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Fase 15, T5 (VTT-03): <c>GET /visual/subscribe</c> no escopo city entrega a projeção
/// de cidade — drill-down do mapa-múndi (T4) pra dentro de uma cidade específica pela mesma rota
/// genérica de subscribe (T3), sem endpoint dedicado por escopo.
/// Factory própria: o caso feliz chama <c>AddCity</c> no mundo canônico.</summary>
public class CityProjectionEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public CityProjectionEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Subscribing_to_an_existing_city_scope_returns_its_projection()
    {
        CityId cityId;
        using (var scope = _factory.Services.CreateScope())
        {
            var world = scope.ServiceProvider.GetRequiredService<WorldState>();
            var city = new City(world.NextCityId(), new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null,
                aggregatePool: AggregatePopulationPool.Empty);
            world.AddCity(city);
            cityId = city.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/visual/subscribe?scope=City&refId={cityId.Value}&mode=Spectator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var payload = json.GetProperty("payload");
        Assert.Equal(cityId.Value.ToString(), payload.GetProperty("id").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Subscribing_to_a_city_scope_that_does_not_exist_returns_a_null_payload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/visual/subscribe?scope=City&refId={Guid.NewGuid()}&mode=Spectator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("payload").ValueKind);
    }
}
