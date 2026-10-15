using System.Net;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Emergence;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Forma congelada de <c>/cultures/{id}/technologies</c> (#59 fatia G). Factory própria: semeia uma tecnologia.</summary>
public class TechnologyContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public TechnologyContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Culture_technologies_shape_matches_the_committed_contract()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var npc = world.Npcs.First();
        var city = world.ActiveCities().First().Id;
        world.AddTechnology(new Technology(
            npc.Culture, EmergenceRules.MobilityCategory, new TechnologyVector([2, 4, 6]), 50, city, null, [city]));

        var response = await _factory.CreateClient().GetAsync($"/cultures/{npc.Culture.Id}/technologies?fromTick=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shape = ApiContractTests.ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()))!;
        shape[0]!["variationOfKey"] = "null"; // só as variações trazem a chave de origem
        ApiContractTests.AssertMatchesContract("shape.culture-technologies.json", shape);
    }
}
