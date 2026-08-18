using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T6 (PERIOD-04..06, PERIOD-07..10): <c>POST /worlds/start</c> resolve um
/// template já cadastrado por <c>POST /periods</c> e inicializa mundo com a seed pedida.</summary>
public class WorldStartEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorldStartEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static JsonObject FullValidPeriodDefinition()
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();

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

    private static StringContent JsonBody(JsonObject body) =>
        new(body.ToJsonString(), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Start_returns_200_and_npc_count_for_a_registered_period()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/periods", JsonBody(new JsonObject
        {
            ["PeriodId"] = "start-period",
            ["Version"] = 1,
            ["PeriodDefinition"] = FullValidPeriodDefinition(),
            ["Source"] = "external-ai",
        }));

        var response = await client.PostAsJsonAsync("/worlds/start", new StartWorldRequest("start-period", 999));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StartWorldResponse>();
        Assert.NotNull(body);
        Assert.Equal(20, body!.NpcCount);
        Assert.Equal(999UL, body.Seed);
    }

    [Fact]
    public async Task Start_returns_404_for_an_unregistered_PeriodId()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/worlds/start", new StartWorldRequest("does-not-exist", 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
