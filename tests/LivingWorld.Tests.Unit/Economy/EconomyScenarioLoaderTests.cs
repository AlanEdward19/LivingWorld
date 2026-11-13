using System.Text.Json.Nodes;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>Fase 5, T13: carga de EconomyRules/EconomyCatalog/Workplaces iniciais do cenário —
/// mesmo padrão de BehaviorScenarioLoaderTests, campo obrigatório ausente nomeia o campo.</summary>
public class EconomyScenarioLoaderTests
{
    private static JsonObject ValidRoot() => new()
    {
        ["EconomyEnabled"] = true,
        ["FoodResourceId"] = 1,
        ["WaterResourceId"] = 2,
        ["PriceSensitivity"] = 0.5,
        ["CapacityByResourceLocation"] = new JsonObject { ["1,1"] = 100 },
        ["SpoilagePerDayByResource"] = new JsonObject { ["1"] = 0.1 },
        ["WageByProfession"] = new JsonObject { ["1"] = 10 },
        ["PriceFloor"] = new JsonObject { ["1"] = 1 },
        ["PriceCeiling"] = new JsonObject { ["1"] = 100 },
        ["DemandBaselinePerNpc"] = new JsonObject { ["1"] = 1.0 },
        ["Recipes"] = new JsonObject
        {
            ["1"] = new JsonObject
            {
                ["Inputs"] = new JsonObject(),
                ["Outputs"] = new JsonObject { ["1"] = 5 },
                ["RequiresCellResource"] = null,
                ["MaxWorkersPerCycle"] = 3,
            },
        },
        ["MarketLocationTypeIds"] = new JsonArray { 2 },
        ["LocationTypeByProfession"] = new JsonObject { ["1"] = 1 },
        ["Workplaces"] = new JsonArray
        {
            new JsonObject
            {
                ["LocationTypeId"] = 1,
                ["X"] = 5,
                ["Y"] = 5,
                ["MaxVacancies"] = 4,
                ["Treasury"] = 50,
                ["Stock"] = new JsonObject { ["1"] = 10 },
                ["Prices"] = new JsonObject(),
            },
        },
    };

    [Fact]
    public void Happy_path_parses_rules_catalog_and_workplaces()
    {
        var result = EconomyScenarioLoader.Load(ValidRoot().ToJsonString());

        Assert.True(result.IsSuccess);
        var data = result.Value!;
        Assert.True(data.Rules.Enabled);
        Assert.Equal(1, data.Rules.FoodResourceId);
        Assert.Single(data.Catalog.Recipes);
        Assert.Contains(2, data.Catalog.MarketLocationTypeIds);
        var workplace = Assert.Single(data.Workplaces);
        Assert.Equal(4, workplace.MaxVacancies);
        Assert.Equal(new Money(50), workplace.Treasury);
        Assert.Equal(10, workplace.Stock[new ResourceType(1)]);
    }

    [Fact]
    public void Missing_EconomyEnabled_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("EconomyEnabled");

        var result = EconomyScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("EconomyEnabled", result.Error);
    }

    [Fact]
    public void Missing_CapacityByResourceLocation_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("CapacityByResourceLocation");

        var result = EconomyScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("CapacityByResourceLocation", result.Error);
    }

    [Fact]
    public void Missing_Recipes_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("Recipes");

        var result = EconomyScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Recipes", result.Error);
    }

    [Fact]
    public void Missing_Workplaces_fails_naming_the_field()
    {
        var root = ValidRoot();
        root.Remove("Workplaces");

        var result = EconomyScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Workplaces", result.Error);
    }

    // AD-144: dono, ferramenta e regra de ofício são opcionais; sem eles o cenário carrega como antes.
    [Fact]
    public void Owner_tool_and_craft_rule_are_read_when_declared()
    {
        var root = ValidRoot();
        root["CraftLocationTypeIds"] = new JsonArray { 2 };
        root["SurplusDrawShare"] = 0.5;
        root["Recipes"]!["1"]!["Tool"] = new JsonObject { ["ResourceId"] = 4, ["KitPerWorker"] = 3.0, ["WearPerWorkerYear"] = 2.0, ["UntooledOutput"] = 0.6 };
        root["Workplaces"]![0]!["Owner"] = 7;

        var data = EconomyScenarioLoader.Load(root.ToJsonString()).Value!;

        Assert.Equal(("2", 4, 7L, 0.5), (string.Join(",", data.CraftLocationTypeIds), data.Catalog.Recipes[1].Tool!.ResourceId,
            data.Workplaces[0].Owner!.Value.Value, data.Rules.SurplusDrawShare));
    }

    [Fact]
    public void A_scenario_without_the_new_fields_has_no_owner_tool_or_draw()
    {
        var data = EconomyScenarioLoader.Load(ValidRoot().ToJsonString()).Value!;

        Assert.Equal((0, (object?)null, (object?)null, 0.0), (data.CraftLocationTypeIds.Count, (object?)data.Catalog.Recipes[1].Tool,
            (object?)data.Workplaces[0].Owner, data.Rules.SurplusDrawShare));
    }

    [Fact]
    public void The_old_levy_field_still_sets_the_draw()
    {
        var root = ValidRoot();
        root["SurplusLevyShare"] = 0.05;

        Assert.Equal(0.05, EconomyScenarioLoader.Load(root.ToJsonString()).Value!.Rules.SurplusDrawShare);
    }

    [Fact]
    public void An_invalid_tool_names_the_field()
    {
        var root = ValidRoot();
        root["Recipes"]!["1"]!["Tool"] = new JsonObject { ["ResourceId"] = 4 };

        Assert.Contains("Tool", EconomyScenarioLoader.Load(root.ToJsonString()).Error, StringComparison.Ordinal);
    }
}
