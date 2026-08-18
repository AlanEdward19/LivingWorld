using System.Text.Json.Nodes;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Tests.Periods;

public class PeriodDefinitionValidatorTests
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
    public void Happy_path_validates_all_sections_and_aggregates_period_definition()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 2.0 }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(20, result.Value!.Population.InitialPopulation);
        Assert.Single(result.Value!.Dynamics.ProfessionBiases);
    }

    [Fact]
    public void Missing_map_field_fails_with_map_loader_error()
    {
        var root = FullValidRoot();
        root.Remove("Width");

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Width:", result.Error);
    }

    [Fact]
    public void Missing_population_field_fails_with_population_loader_error()
    {
        var root = FullValidRoot();
        root.Remove("InitialPopulation");

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("InitialPopulation:", result.Error);
    }

    [Fact]
    public void Missing_behavior_field_fails_with_behavior_loader_error()
    {
        var root = FullValidRoot();
        root.Remove("HungerDecayPerHour");

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("HungerDecayPerHour:", result.Error);
    }

    [Fact]
    public void Missing_economy_field_fails_with_economy_loader_error()
    {
        var root = FullValidRoot();
        root.Remove("FoodResourceId");

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("FoodResourceId:", result.Error);
    }

    [Fact]
    public void Missing_city_field_fails_with_city_loader_error()
    {
        var root = FullValidRoot();
        root.Remove("CitiesEnabled");

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("CitiesEnabled:", result.Error);
    }

    [Fact]
    public void Malformed_dynamics_block_fails_with_dynamics_loader_error()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = -1.0 }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.ProfessionBiases[].Weight", result.Error);
    }

    [Fact]
    public void Profession_bias_referencing_id_outside_ProfessionIds_is_rejected()
    {
        var root = FullValidRoot();
        // scenarios/default.json declara ProfessionIds = [1, 2] (ver _professionComment)
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 999, ["Weight"] = 1.0 }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.ProfessionBiases[]: ProfessionId 999", result.Error);
    }

    [Fact]
    public void Transformation_rule_referencing_source_profession_id_outside_ProfessionIds_is_rejected()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Disappear",
                ["SourceProfessionIds"] = new JsonArray(999),
            }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Dynamics.TransformationRules[]: ProfessionId 999", result.Error);
    }

    [Fact]
    public void Transformation_rule_target_profession_id_does_not_need_to_pre_exist_in_ProfessionIds()
    {
        // Fase 13, T13: Emerge introduz uma profissão nova — exigir que o alvo já exista em
        // ProfessionIds tornaria a regra sem sentido (ela já estaria disponível pro sorteio
        // antes de "emergir").
        var root = FullValidRoot(); // ProfessionIds = [1, 2]
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Emerge",
                ["TargetProfessionIds"] = new JsonArray(999),
            }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public void Profession_bias_passes_when_ProfessionIds_is_unrestricted()
    {
        var root = FullValidRoot();
        (root["ProfessionIds"] as JsonArray)!.Clear();
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 999, ["Weight"] = 1.0 }),
        };

        var result = PeriodDefinitionValidator.Validate(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
