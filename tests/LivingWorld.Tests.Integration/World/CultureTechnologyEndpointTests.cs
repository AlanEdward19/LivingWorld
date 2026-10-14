using System.Net;
using System.Net.Http.Json;
using LivingWorld.Domain.Emergence;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

/// <summary>emergence-first-slice, AC 6 e 7: <c>GET /cultures/{id}/technologies</c>.</summary>
public class CultureTechnologyEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public CultureTechnologyEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorldHost>().Current;
    }

    [Fact]
    public async Task Known_culture_returns_its_vectors_and_adoption()
    {
        var world = World();
        var npc = world.Npcs.First();
        var city = world.ActiveCities().First().Id;
        world.AddTechnology(new Technology(
            npc.Culture, EmergenceRules.MobilityCategory, new TechnologyVector([2, 4, 6]), 50, city, null, [city]));

        var response = await _factory.CreateClient().GetAsync($"/cultures/{npc.Culture.Id}/technologies?fromTick=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<TechnologyView>>();
        var view = Assert.Single(body!, t => t.Key == "2.4.6");
        Assert.Equal(1, view.AdoptedByCities);
        Assert.False(view.IsVariation);

        var later = await _factory.CreateClient().GetFromJsonAsync<List<TechnologyView>>($"/cultures/{npc.Culture.Id}/technologies?fromTick=51");
        Assert.DoesNotContain(later!, t => t.Key == "2.4.6");
    }

    [Fact]
    public async Task Unknown_culture_is_404()
    {
        var response = await _factory.CreateClient().GetAsync("/cultures/987654/technologies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
