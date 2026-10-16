using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>Fase 8, T7 (CITY-02, CITY-03, CITY-07, CITY-08): carga de CityRules/CityCatalog/
/// cidades iniciais do cenário — mesmo padrão de EconomyScenarioLoaderTests, campo obrigatório
/// ausente nomeia o campo.</summary>
public class CityScenarioLoaderTests
{
    private static JsonObject ValidRoot() => new()
    {
        ["CitiesEnabled"] = true,
        ["FoodShortageThreshold"] = 20.0,
        ["HousingShortageThreshold"] = 20.0,
        ["SecurityShortageThreshold"] = 20.0,
        ["EmigrationRatePerDeficitUnit"] = 0.1,
        ["MigrationEmploymentWeight"] = 1.0,
        ["MigrationFoodWeight"] = 1.0,
        ["MigrationSecurityWeight"] = 1.0,
        ["MigrationFamilyTiesWeight"] = 1.0,
        ["FoundingConcentrationThreshold"] = 0.5,
        ["FoundingResourceThreshold"] = 0.5,
        ["FoundingRouteThreshold"] = 0.5,
        ["FoundingDefensibilityThreshold"] = 0.5,
        ["FoundingLeadershipThreshold"] = 0.5,
        ["OrganizationTicks"] = 100,
        ["MaterializationIdleTicksBeforeEligible"] = 50,
        ["BuildingRecipes"] = new JsonObject
        {
            ["1"] = new JsonObject
            {
                ["Inputs"] = new JsonObject { ["1"] = 10 },
                ["TicksToBuild"] = 20,
                ["HousingCapacityProvided"] = 4,
            },
        },
        ["Cities"] = new JsonArray
        {
            new JsonObject
            {
                ["X"] = 3,
                ["Y"] = 4,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 10, ["WealthSum"] = 100, ["HealthSum"] = 90 },
            },
        },
    };

    [Fact]
    public void Happy_path_parses_rules_catalog_and_cities()
    {
        var result = CityScenarioLoader.Load(ValidRoot().ToJsonString());

        Assert.True(result.IsSuccess);
        var data = result.Value!;
        Assert.True(data.Rules.Enabled);
        Assert.Equal(20.0, data.Rules.FoodShortageThreshold);
        Assert.Equal(100, data.Rules.OrganizationTicks);
        Assert.Single(data.Catalog.BuildingRecipes);
        var city = Assert.Single(data.Cities);
        Assert.Equal(new CellCoord(3, 4), city.Location);
        Assert.Equal(new AggregatePopulationPool(10, 100, 90), city.AggregatePool);
    }

    [Fact]
    public void Missing_CitiesEnabled_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("CitiesEnabled");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("CitiesEnabled", result.Error);
    }

    [Fact]
    public void Missing_OrganizationTicks_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("OrganizationTicks");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("OrganizationTicks", result.Error);
    }

    [Fact]
    public void Missing_BuildingRecipes_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("BuildingRecipes");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("BuildingRecipes", result.Error);
    }

    [Fact]
    public void Missing_Cities_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("Cities");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Cities", result.Error);
    }

    [Fact]
    public void Missing_AggregatePool_on_a_city_fails_naming_the_field()
    {
        var root = ValidRoot();
        ((JsonObject)root["Cities"]![0]!).Remove("AggregatePool");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("AggregatePool", result.Error);
    }

    [Fact]
    public void Invalid_threshold_propagates_the_CityRules_validation_error()
    {
        var root = ValidRoot();
        root["FoodShortageThreshold"] = 200.0;

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("FoodShortageThreshold", result.Error);
    }
}
