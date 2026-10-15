using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.World;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

/// <summary>#46 fatia 2: a aparência autorada sai por <c>GET /npcs/{id}</c> e pelas projeções do mundo; sem o bloco
/// <c>Appearance</c> o campo nem aparece (contrato dos mundos de antes intacto). Factory própria: troca o mundo do host.</summary>
public class AppearanceEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public AppearanceEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, long AnaId)> CreateWorld(bool withBlock)
    {
        var scenario = WorldCreateEndpointsTests.FullValidScenario(seed: 31);
        scenario["InitialPopulation"] = 5;
        if (withBlock) scenario["Appearance"] = JsonNode.Parse("{\"Heritability\":0.6}");
        string look = withBlock
            ? ",\"Appearance\":{\"SkinTone\":12,\"HairTone\":34,\"HairCurl\":56,\"Build\":78},\"Skills\":{\"0\":40}"
            : "";
        scenario["AuthoredHouseholds"] = JsonNode.Parse(
            "[{\"Members\":[{\"Name\":\"Ana\",\"Sex\":\"Female\",\"AgeYears\":30,\"ProfessionId\":1" + look + "}]}]");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario.ToJsonString(), "Aparência"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var host = _factory.Services.GetRequiredService<WorldHost>();
        return (client, host.Current.Npcs.Single(n => n.Name == "Ana").Id.Value);
    }

    [Fact]
    public async Task Inspection_returns_the_authored_looks_and_skills()
    {
        var (client, id) = await CreateWorld(withBlock: true);

        var npc = JsonNode.Parse(await client.GetStringAsync($"/npcs/{id}"))!;

        var look = npc["appearance"]!;
        Assert.Equal(12, look["skinTone"]!.GetValue<int>());
        Assert.Equal(34, look["hairTone"]!.GetValue<int>());
        Assert.Equal(56, look["hairCurl"]!.GetValue<int>());
        Assert.Equal(78, look["build"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_city_projection_carries_the_looks_for_the_client_to_dress_the_body()
    {
        var (client, id) = await CreateWorld(withBlock: true);
        var world = JsonNode.Parse(await client.GetStringAsync("/visual/subscribe?scope=World&mode=Spectator"))!;
        string cityId = world["payload"]!["cities"]![0]!["id"]!["value"]!.ToString();

        var city = JsonNode.Parse(await client.GetStringAsync($"/visual/subscribe?scope=City&refId={cityId}&mode=Spectator"))!;

        var ana = city["payload"]!["livingState"]!["npcs"]!.AsArray().Single(n => n!["id"]!["value"]!.GetValue<long>() == id)!;
        Assert.Equal(12, ana["appearance"]!["skinTone"]!.GetValue<int>());
    }

    [Fact]
    public async Task Without_the_period_block_the_field_is_absent_everywhere()
    {
        var (client, id) = await CreateWorld(withBlock: false);

        Assert.DoesNotContain("appearance", await client.GetStringAsync($"/npcs/{id}"));
        var world = JsonNode.Parse(await client.GetStringAsync("/visual/subscribe?scope=World&mode=Spectator"))!;
        string cityId = world["payload"]!["cities"]![0]!["id"]!["value"]!.ToString();
        Assert.DoesNotContain("appearance", await client.GetStringAsync($"/visual/subscribe?scope=City&refId={cityId}&mode=Spectator"));
        Assert.DoesNotContain("appearance", world.ToJsonString());
    }
}
