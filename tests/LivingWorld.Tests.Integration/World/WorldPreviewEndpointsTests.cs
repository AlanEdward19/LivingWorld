using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests;

/// <summary>Fase 15.1, T43 (backend-gaps.md G2): <c>POST /worlds/preview</c> usa o mesmo
/// <see cref="MapScenarioLoader"/> que o create, sem tocar <see cref="WorldHost"/> nem persistir
/// nada, e produz exatamente a mesma geografia (hash espacial) que <c>POST /worlds/create</c>
/// produziria para a mesma seed. Factory própria: um dos testes chama create e faz
/// <c>WorldHost.Replace</c> — não pode compartilhar a coleção de endpoints.</summary>
public class WorldPreviewEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public WorldPreviewEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

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
    public async Task Preview_returns_dimensions_full_cell_grid_and_settlements()
    {
        var client = _factory.CreateClient();
        var scenario = FullValidScenario(seed: 555);
        int width = scenario["Width"]!.GetValue<int>();
        int height = scenario["Height"]!.GetValue<int>();

        var response = await client.PostAsJsonAsync("/worlds/preview", new PreviewWorldRequest(scenario.ToJsonString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PreviewWorldResponse>();
        Assert.NotNull(body);
        Assert.Equal(width, body!.Width);
        Assert.Equal(height, body.Height);
        Assert.Equal(width * height, body.Cells.Count);
        Assert.False(string.IsNullOrWhiteSpace(body.SpatialHash));
    }

    [Fact]
    public async Task Preview_with_invalid_scenario_json_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/worlds/preview", new PreviewWorldRequest("{ not valid }"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preview_does_not_touch_the_current_world_host()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var worldBefore = host.Current;

        await client.PostAsJsonAsync("/worlds/preview", new PreviewWorldRequest(FullValidScenario(556).ToJsonString()));

        Assert.Same(worldBefore, host.Current);
    }

    [Fact]
    public async Task Preview_spatial_hash_matches_the_map_of_the_world_created_from_the_same_scenario()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var scenario = FullValidScenario(seed: 4443).ToJsonString();

        var previewResponse = await client.PostAsJsonAsync("/worlds/preview", new PreviewWorldRequest(scenario));
        var previewBody = await previewResponse.Content.ReadFromJsonAsync<PreviewWorldResponse>();

        var createResponse = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario, "Prévia"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdMapHash = MapSpatialHash.Compute(host.Current.Map);

        Assert.Equal(createdMapHash, previewBody!.SpatialHash);
    }
}
