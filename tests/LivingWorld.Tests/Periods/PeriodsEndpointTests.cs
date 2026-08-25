using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T5 (PERIOD-07..10): <c>POST/GET /periods</c>, <c>GET /periods/{id}</c>.
/// Mesmo padrão de <see cref="LivingWorld.Tests.Cities.NpcEndpointTests"/> —
/// <see cref="WebApplicationFactory{TEntryPoint}"/> real, uma instância por classe (isolamento
/// de <c>world</c>/db do host, ver comentário em Program.cs).</summary>
[Collection(ApiEndpointCollection.Name)]
public class PeriodsEndpointTests
{
    private readonly LivingWorldApiFactory _factory;

    public PeriodsEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

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

    private static JsonObject Envelope(string periodId, int version, JsonObject definition, string source = "external-ai") => new()
    {
        ["PeriodId"] = periodId,
        ["Version"] = version,
        ["PeriodDefinition"] = definition,
        ["Source"] = source,
    };

    private static StringContent JsonBody(JsonObject body) =>
        new(body.ToJsonString(), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Post_periods_returns_201_and_registers_a_valid_period()
    {
        var client = _factory.CreateClient();
        // Id único por execução — evita 409 se ConnectionStrings__World apontar pra um db de
        // sessão anterior (run.cmd); o ModuleInitializer limpa isso, mas o id efêmero é cinto.
        string periodId = $"period-{Guid.NewGuid():N}";

        var response = await client.PostAsync("/periods", JsonBody(Envelope(periodId, 1, FullValidPeriodDefinition())));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(periodId, body);
    }

    [Fact]
    public async Task Post_periods_returns_400_with_field_path_for_an_invalid_definition()
    {
        var client = _factory.CreateClient();
        var definition = FullValidPeriodDefinition();
        definition.Remove("Width");

        var response = await client.PostAsync("/periods", JsonBody(Envelope("broken-period", 1, definition)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Width:", body);
    }

    [Fact]
    public async Task Post_periods_returns_409_for_a_duplicate_PeriodId_and_version()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/periods", JsonBody(Envelope("dup-period", 1, FullValidPeriodDefinition())));

        var response = await client.PostAsync("/periods", JsonBody(Envelope("dup-period", 1, FullValidPeriodDefinition())));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_periods_lists_a_registered_period()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/periods", JsonBody(Envelope("catalog-period", 1, FullValidPeriodDefinition())));

        var response = await client.GetAsync("/periods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("catalog-period", body);
    }

    // T44b: World Creator precisa saber o tamanho do mapa de um template antes de escolhê-lo, sem
    // baixar o `PeriodDefinition` inteiro primeiro (GET /periods/{id}).

    [Fact]
    public async Task Get_periods_includes_the_map_width_and_height_of_each_template()
    {
        var client = _factory.CreateClient();
        var definition = FullValidPeriodDefinition();
        definition["Width"] = 17;
        definition["Height"] = 23;
        await client.PostAsync("/periods", JsonBody(Envelope("sized-period", 1, definition)));

        var response = await client.GetAsync("/periods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PeriodSummaryResponse>>();
        var summary = Assert.Single(body!, p => p.PeriodId == "sized-period");
        Assert.Equal(17, summary.Width);
        Assert.Equal(23, summary.Height);
    }

    [Fact]
    public async Task Get_periods_by_id_returns_200_with_the_registered_definition()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/periods", JsonBody(Envelope("detail-period", 1, FullValidPeriodDefinition())));

        var response = await client.GetAsync("/periods/detail-period");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("detail-period", body);
    }

    [Fact]
    public async Task Get_periods_by_id_returns_404_for_an_unregistered_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/periods/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Fase 13, T12 (PERIOD-22..23): catálogo ativo de ids de um período registrado.

    [Fact]
    public async Task Get_periods_catalog_returns_200_with_profession_and_skill_ids()
    {
        var client = _factory.CreateClient();
        var definition = FullValidPeriodDefinition();
        definition["Dynamics"] = new JsonObject
        {
            ["SkillBiases"] = new JsonArray(new JsonObject { ["SkillId"] = 7, ["Weight"] = 1.0 }),
        };
        await client.PostAsync("/periods", JsonBody(Envelope("catalog-endpoint-period", 1, definition)));

        var response = await client.GetAsync("/periods/catalog-endpoint-period/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PeriodCatalogResponse>();
        Assert.NotNull(body);
        Assert.Equal("catalog-endpoint-period", body!.PeriodId);
        Assert.Equal([1, 2], body.ProfessionIds); // scenarios/default.json declara ProfessionIds = [1, 2]
        Assert.Equal([7], body.SkillIds);
    }

    // Fase 13, T14 (PERIOD-19, PERIOD-22..23): nome opcional de profissão/habilidade no catálogo.

    [Fact]
    public async Task Get_periods_catalog_returns_declared_names_and_omits_undeclared_ones()
    {
        var client = _factory.CreateClient();
        var definition = FullValidPeriodDefinition();
        definition["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 1.0, ["Name"] = "Ferreiro" }),
            ["SkillBiases"] = new JsonArray(new JsonObject { ["SkillId"] = 7, ["Weight"] = 1.0, ["Name"] = "Culinaria" }),
        };
        await client.PostAsync("/periods", JsonBody(Envelope("catalog-named-period", 1, definition)));

        var response = await client.GetAsync("/periods/catalog-named-period/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PeriodCatalogResponse>();
        Assert.NotNull(body);
        Assert.Equal("Ferreiro", body!.ProfessionNames[1]);
        Assert.False(body.ProfessionNames.ContainsKey(2)); // ProfessionId 2 existe no catálogo mas não tem Name declarado
        Assert.Equal("Culinaria", body.SkillNames[7]);
    }

    // Fase 15.1, T48 (backend-gaps.md G8): descritores legíveis por categoria no catálogo.

    [Fact]
    public async Task Get_periods_catalog_returns_declared_descriptors()
    {
        var client = _factory.CreateClient();
        var definition = FullValidPeriodDefinition();
        definition["Descriptors"] = new JsonObject
        {
            ["Terrain"] = new JsonArray(new JsonObject
            {
                ["Id"] = 1,
                ["Name"] = "Grama",
                ["Explanation"] = "Terreno fértil comum",
            }),
        };
        await client.PostAsync("/periods", JsonBody(Envelope("catalog-descriptors-period", 1, definition)));

        var response = await client.GetAsync("/periods/catalog-descriptors-period/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PeriodCatalogResponse>();
        Assert.NotNull(body);
        var terrain = Assert.Single(body!.Descriptors.Terrain);
        Assert.Equal("Grama", terrain.Name);
        Assert.Equal("Terreno fértil comum", terrain.Explanation);
        Assert.Empty(body.Descriptors.Biome); // não declarado, não inventado
    }

    [Fact]
    public async Task Get_periods_catalog_returns_404_for_an_unregistered_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/periods/does-not-exist/catalog");

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
