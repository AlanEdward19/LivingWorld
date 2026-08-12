using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests;

/// <summary>Fase 15.1, T42 (ADR-0017): <c>POST /worlds/create</c> passa a aceitar e persistir o
/// nome do mundo e a devolver identidade (<c>WorldId</c>, <c>Tick</c>, <c>InitialScope</c>) além
/// da contagem de NPCs já existente.</summary>
public class WorldCreateEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorldCreateEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static JsonObject FullValidScenario(ulong seed)
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();
        root["Seed"] = seed;

        root["EconomyEnabled"] = false;
        root["FoodResourceId"] = 1;
        root["WaterResourceId"] = 2;
        root["PriceSensitivity"] = 0.1;
        root["CapacityByResourceLocation"] = new JsonObject();
        root["SpoilagePerDayByResource"] = new JsonObject();
        root["WageByProfession"] = new JsonObject();
        root["PriceFloor"] = new JsonObject();
        root["PriceCeiling"] = new JsonObject();
        root["DemandBaselinePerNpc"] = new JsonObject();
        root["Recipes"] = new JsonObject();
        root["MarketLocationTypeIds"] = new JsonArray();
        root["LocationTypeByProfession"] = new JsonObject();
        root["Workplaces"] = new JsonArray();

        root["CitiesEnabled"] = false;
        root["FoodShortageThreshold"] = 0.1;
        root["HousingShortageThreshold"] = 0.1;
        root["SecurityShortageThreshold"] = 0.1;
        root["EmigrationRatePerDeficitUnit"] = 0.1;
        root["MigrationEmploymentWeight"] = 0.1;
        root["MigrationFoodWeight"] = 0.1;
        root["MigrationSecurityWeight"] = 0.1;
        root["MigrationFamilyTiesWeight"] = 0.1;
        root["FoundingConcentrationThreshold"] = 0.1;
        root["FoundingResourceThreshold"] = 0.1;
        root["FoundingRouteThreshold"] = 0.1;
        root["FoundingDefensibilityThreshold"] = 0.1;
        root["FoundingLeadershipThreshold"] = 0.1;
        root["OrganizationTicks"] = 1;
        root["MaterializationIdleTicksBeforeEligible"] = 1;
        root["BuildingRecipes"] = new JsonObject();
        root["Cities"] = new JsonArray();

        return root;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }

    [Fact]
    public async Task Create_with_valid_name_and_scenario_returns_identity_alongside_npc_count()
    {
        var client = _factory.CreateClient();
        var scenario = FullValidScenario(seed: 4242).ToJsonString();

        var response = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario, "Vale de Aster"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateWorldResponse>();
        Assert.NotNull(body);
        Assert.Equal("Vale de Aster", body!.Name);
        Assert.Equal(0, body.Tick);
        Assert.Equal("world", body.InitialScope);
        Assert.NotEqual(Guid.Empty, body.WorldId);
        Assert.Equal(WorldIdentity.WorldIdFor(4242), body.WorldId);
    }

    [Fact]
    public async Task Create_with_the_same_seed_and_name_twice_returns_the_same_WorldId()
    {
        var client = _factory.CreateClient();
        var scenario = FullValidScenario(seed: 777).ToJsonString();

        var first = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario, "Aldeia"));
        var second = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario, "Aldeia"));

        var firstBody = await first.Content.ReadFromJsonAsync<CreateWorldResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<CreateWorldResponse>();

        Assert.Equal(firstBody!.WorldId, secondBody!.WorldId);
    }

    [Fact]
    public async Task Create_with_a_different_seed_returns_a_different_WorldId()
    {
        var client = _factory.CreateClient();

        var a = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(FullValidScenario(1).ToJsonString(), "A"));
        var b = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(FullValidScenario(2).ToJsonString(), "B"));

        var bodyA = await a.Content.ReadFromJsonAsync<CreateWorldResponse>();
        var bodyB = await b.Content.ReadFromJsonAsync<CreateWorldResponse>();

        Assert.NotEqual(bodyA!.WorldId, bodyB!.WorldId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_with_blank_name_returns_400_and_does_not_replace_the_current_world(string blankName)
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var worldBefore = host.Current;

        var response = await client.PostAsJsonAsync(
            "/worlds/create", new CreateWorldRequest(FullValidScenario(99).ToJsonString(), blankName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Same(worldBefore, host.Current);
    }

    [Fact]
    public async Task Create_with_invalid_scenario_json_returns_400_and_does_not_replace_the_current_world()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var worldBefore = host.Current;

        var response = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest("{ not valid }", "Nome válido"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Same(worldBefore, host.Current);
    }
}
