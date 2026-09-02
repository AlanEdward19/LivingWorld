using System.Text.Json.Nodes;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>Fase 13, T12 (PERIOD-22..23): catálogo ativo derivado de um <see
/// cref="PeriodDefinition"/> já validado — ids de profissão (bloco População) + ids de
/// habilidade referenciados em <c>Dynamics.SkillBiases</c>.</summary>
public class PeriodCatalogTests
{
    private static JsonObject FullValidRoot()
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

    [Fact]
    public void From_exposes_profession_ids_from_population_catalog()
    {
        var root = FullValidRoot(); // scenarios/default.json declara ProfessionIds = [1, 2]

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Equal([1, 2], catalog.ProfessionIds);
    }

    [Fact]
    public void From_exposes_distinct_sorted_skill_ids_from_dynamics_skill_biases()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["SkillBiases"] = new JsonArray(
                new JsonObject { ["SkillId"] = 7, ["Weight"] = 1.0 },
                new JsonObject { ["SkillId"] = 0, ["Weight"] = 2.0 },
                new JsonObject { ["SkillId"] = 7, ["Weight"] = 3.0 }),
        };

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Equal([0, 7], catalog.SkillIds);
    }

    [Fact]
    public void From_returns_empty_skill_ids_when_Dynamics_is_absent()
    {
        var root = FullValidRoot();

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Empty(catalog.SkillIds);
    }

    [Fact]
    public void From_exposes_skill_name_only_when_declared()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["SkillBiases"] = new JsonArray(
                new JsonObject { ["SkillId"] = 7, ["Weight"] = 1.0, ["Name"] = "Culinaria" },
                new JsonObject { ["SkillId"] = 0, ["Weight"] = 2.0 }),
        };

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Equal("Culinaria", catalog.SkillNames[7]);
        Assert.False(catalog.SkillNames.ContainsKey(0));
    }

    [Fact]
    public void From_exposes_profession_name_only_when_declared()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(
                new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 1.0, ["Name"] = "Ferreiro" }),
        };

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Equal("Ferreiro", catalog.ProfessionNames[1]);
        Assert.False(catalog.ProfessionNames.ContainsKey(2));
    }

    [Fact]
    public void From_exposes_descriptors_declared_in_the_scenario()
    {
        var root = FullValidRoot();
        root["Descriptors"] = new JsonObject
        {
            ["Terrain"] = new JsonArray(new JsonObject { ["Id"] = 1, ["Name"] = "Grama" }),
        };

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Equal("Grama", Assert.Single(catalog.Descriptors.Terrain).Name);
    }

    [Fact]
    public void From_returns_empty_descriptors_when_Descriptors_is_absent()
    {
        var root = FullValidRoot();

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Empty(catalog.Descriptors.Terrain);
        Assert.Empty(catalog.Descriptors.Action);
    }

    [Fact]
    public void From_returns_empty_name_dictionaries_when_Dynamics_is_absent()
    {
        var root = FullValidRoot();

        var definition = PeriodDefinitionValidator.Validate(root.ToJsonString()).Value!;
        var catalog = PeriodCatalog.From(definition);

        Assert.Empty(catalog.ProfessionNames);
        Assert.Empty(catalog.SkillNames);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
