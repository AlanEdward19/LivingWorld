using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Periods;

public class ScenarioLoaderV2Tests
{
    private static JsonObject FullValidRoot()
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();

        root["EconomyEnabled"] = true;
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
        root["Workplaces"] = new JsonArray(new JsonObject
        {
            ["LocationTypeId"] = 1,
            ["X"] = 1,
            ["Y"] = 1,
            ["MaxVacancies"] = 3,
            ["Treasury"] = 0,
            ["Stock"] = new JsonObject(),
            ["Prices"] = new JsonObject(),
        });

        root["CitiesEnabled"] = true;
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
        root["Cities"] = new JsonArray(new JsonObject
        {
            ["X"] = 2,
            ["Y"] = 2,
            ["FoundedAtTick"] = 0,
            ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
        });

        return root;
    }

    [Fact]
    public void Happy_path_builds_world_with_population_behavior_economy_and_city_wired()
    {
        var root = FullValidRoot();

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;

        Assert.Equal(100, world.Npcs.Count);
        Assert.True(world.EconomyRules.Enabled);
        Assert.True(world.CityRules.Enabled);
        Assert.Single(world.Workplaces);
        // Bugfix real (usuário, 2026-08-13): a vila inicial (VillageX/Y = 5,5 em default.json)
        // não coincide com a cidade autorada (X/Y = 2,2, população 0) — antes desta correção a
        // população inicial nunca era vinculada a nenhuma cidade (Npc.City ficava default),
        // então sumia de toda projeção. Agora uma segunda cidade é fundada na própria vila,
        // e é ela que carrega a população real.
        Assert.Equal(2, world.Cities.Count);
        var homeCity = Assert.Single(world.Cities, c => c.Location == new CellCoord(5, 5));
        Assert.All(world.Npcs, npc => Assert.Equal(homeCity.Id, npc.City));
        Assert.All(world.Households, household => Assert.Equal(homeCity.Id, household.City));
    }

    [Fact]
    public void Invalid_period_definition_fails_without_building_a_partial_world()
    {
        var root = FullValidRoot();
        root.Remove("CitiesEnabled");

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("CitiesEnabled:", result.Error);
    }

    [Fact]
    public void World_runs_ticks_without_editing_any_cs_file_for_a_dynamics_declared_period()
    {
        var root = FullValidRoot();
        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 2.0 }),
        };

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);

        var (world, clock) = result.Value;
        for (int tick = 0; tick < 24; tick++)
            clock.Tick(world);

        Assert.True(world.Npcs.Count > 0);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
