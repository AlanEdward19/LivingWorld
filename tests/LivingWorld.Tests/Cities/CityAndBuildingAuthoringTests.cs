using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 15.1, T44 (backend-gaps.md): nome autorado/determinístico de cidade e prédios
/// autorados (id/tipo/posição/orientação) com validação de overlap/bounds/referência.</summary>
public class CityAndBuildingAuthoringTests
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
                ["Name"] = "Vale de Aster",
                ["X"] = 3,
                ["Y"] = 4,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 10, ["WealthSum"] = 100, ["HealthSum"] = 90 },
            },
        },
    };

    // --- Nome de cidade ---

    [Fact]
    public void Authored_city_name_is_parsed_from_the_scenario()
    {
        var result = CityScenarioLoader.Load(ValidRoot().ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Equal("Vale de Aster", result.Value!.Cities[0].Name);
    }

    [Fact]
    public void City_without_a_Name_field_parses_with_an_empty_name()
    {
        var root = ValidRoot();
        ((JsonObject)root["Cities"]![0]!).Remove("Name");

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Value!.Cities[0].Name);
    }

    [Fact]
    public void ScenarioLoaderV2_keeps_the_authored_city_name()
    {
        var scenario = FullScenarioWithCity(cityName: "Vale de Aster");

        var result = ScenarioLoaderV2.LoadWorld(scenario);

        Assert.True(result.IsSuccess);
        Assert.Equal("Vale de Aster", result.Value!.World.Cities[0].Name);
    }

    [Fact]
    public void ScenarioLoaderV2_generates_a_deterministic_non_empty_name_when_the_scenario_omits_one()
    {
        var scenarioA = FullScenarioWithCity(cityName: null);
        var scenarioB = FullScenarioWithCity(cityName: null);

        var resultA = ScenarioLoaderV2.LoadWorld(scenarioA);
        var resultB = ScenarioLoaderV2.LoadWorld(scenarioB);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        var nameA = resultA.Value!.World.Cities[0].Name;
        Assert.False(string.IsNullOrEmpty(nameA));
        Assert.Equal(nameA, resultB.Value!.World.Cities[0].Name); // mesma seed => mesmo nome gerado
    }

    // --- CityNameGenerator puro ---

    [Fact]
    public void CityNameGenerator_is_deterministic_for_the_same_seed()
    {
        var (worldA, _) = ScenarioRunner.Create(seed: 88);
        var (worldB, _) = ScenarioRunner.Create(seed: 88);

        Assert.Equal(CityNameGenerator.Generate(worldA), CityNameGenerator.Generate(worldB));
    }

    [Fact]
    public void CityNameGenerator_does_not_advance_the_city_founding_stream()
    {
        var (world, _) = ScenarioRunner.Create(seed: 89);
        var idBefore = world.NextCityId();

        var (worldSameSeed, _) = ScenarioRunner.Create(seed: 89);
        CityNameGenerator.Generate(worldSameSeed);
        var idAfterNaming = worldSameSeed.NextCityId();

        Assert.Equal(idBefore, idAfterNaming);
    }

    // --- Nome determinístico para cidade fundada pela simulação (SettlementFoundingSystem) ---

    [Fact]
    public void A_city_founded_by_the_simulation_gets_a_non_empty_deterministic_name()
    {
        var rules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
            foundingLeadershipThreshold: 0, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 24, ScenarioRunner.DefaultMap(24),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: rules);
        var motherCity = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(50, 500, 400));
        world.AddCity(motherCity);
        var system = new SettlementFoundingSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        system.Tick(world, ctx);
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, ctx, evt);

        var newCity = world.Cities.Single(c => c.Id != motherCity.Id);
        Assert.False(string.IsNullOrEmpty(newCity.Name));
    }

    // --- Prédios autorados: overlap / bounds / referência ---

    [Fact]
    public void Authored_building_is_parsed_with_position_and_orientation()
    {
        var root = ValidRoot();
        root["Buildings"] = new JsonArray
        {
            new JsonObject { ["CityIndex"] = 0, ["BuildingTypeId"] = 1, ["X"] = 5, ["Y"] = 5, ["Orientation"] = 90 },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString(), mapWidth: 10, mapHeight: 10);

        Assert.True(result.IsSuccess);
        var building = result.Value!.Buildings[0];
        Assert.Equal(0, building.CityIndex);
        Assert.Equal(1, building.BuildingTypeId);
        Assert.Equal(new CellCoord(5, 5), building.Position);
        Assert.Equal(90, building.Orientation);
    }

    [Fact]
    public void Two_authored_buildings_on_the_same_cell_fail_as_overlap()
    {
        var root = ValidRoot();
        root["Buildings"] = new JsonArray
        {
            new JsonObject { ["CityIndex"] = 0, ["BuildingTypeId"] = 1, ["X"] = 5, ["Y"] = 5 },
            new JsonObject { ["CityIndex"] = 0, ["BuildingTypeId"] = 1, ["X"] = 5, ["Y"] = 5 },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString(), mapWidth: 10, mapHeight: 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("ocupada", result.Error);
    }

    [Fact]
    public void Authored_building_outside_the_map_grid_fails_as_out_of_bounds()
    {
        var root = ValidRoot();
        root["Buildings"] = new JsonArray
        {
            new JsonObject { ["CityIndex"] = 0, ["BuildingTypeId"] = 1, ["X"] = 99, ["Y"] = 99 },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString(), mapWidth: 10, mapHeight: 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("fora do grid", result.Error);
    }

    [Fact]
    public void Authored_building_referencing_a_non_existent_city_index_fails()
    {
        var root = ValidRoot();
        root["Buildings"] = new JsonArray
        {
            new JsonObject { ["CityIndex"] = 7, ["BuildingTypeId"] = 1, ["X"] = 5, ["Y"] = 5 },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString(), mapWidth: 10, mapHeight: 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("CityIndex", result.Error);
    }

    [Fact]
    public void ScenarioLoaderV2_creates_the_authored_building_on_the_real_city_with_position_and_orientation()
    {
        var scenario = FullScenarioWithCityAndBuilding();

        var result = ScenarioLoaderV2.LoadWorld(scenario);

        Assert.True(result.IsSuccess);
        var world = result.Value!.World;
        Assert.Single(world.Buildings);
        var building = world.Buildings[0];
        Assert.Equal(world.Cities[0].Id, building.City);
        Assert.Equal(new CellCoord(6, 6), building.Position);
        Assert.Equal(180, building.Orientation);
    }

    // --- Portais autorados (Fase 15.1, T21) ---

    [Fact]
    public void Authored_portal_is_parsed_with_id_label_and_both_endpoints()
    {
        var root = ValidRoot();
        root["Portals"] = new JsonArray
        {
            new JsonObject
            {
                ["Id"] = "portal-north",
                ["Label"] = "Portão Norte",
                ["From"] = new JsonObject { ["Space"] = "World", ["X"] = 3, ["Y"] = 1 },
                ["To"] = new JsonObject { ["Space"] = "City", ["RefIndex"] = 0, ["X"] = 3, ["Y"] = 4 },
            },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.True(result.IsSuccess);
        var portal = Assert.Single(result.Value!.Portals);
        Assert.Equal("portal-north", portal.Id);
        Assert.Equal("Portão Norte", portal.Label);
        Assert.Equal(PortalSpaceKind.World, portal.From.Space);
        Assert.Equal(new CellCoord(3, 1), portal.From.Cell);
        Assert.Equal(PortalSpaceKind.City, portal.To.Space);
        Assert.Equal(0, portal.To.RefIndex);
        Assert.Equal(new CellCoord(3, 4), portal.To.Cell);
    }

    [Fact]
    public void A_scenario_without_a_Portals_field_still_parses_with_an_empty_portal_list()
    {
        var result = CityScenarioLoader.Load(ValidRoot().ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Portals);
    }

    [Fact]
    public void Authored_portal_referencing_a_non_existent_city_index_fails()
    {
        var root = ValidRoot();
        root["Portals"] = new JsonArray
        {
            new JsonObject
            {
                ["Id"] = "portal-north",
                ["Label"] = "Portão Norte",
                ["From"] = new JsonObject { ["Space"] = "World", ["X"] = 3, ["Y"] = 1 },
                ["To"] = new JsonObject { ["Space"] = "City", ["RefIndex"] = 7, ["X"] = 3, ["Y"] = 4 },
            },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("RefIndex", result.Error);
    }

    [Fact]
    public void Two_authored_portals_for_the_same_city_are_distinguishable_only_by_label()
    {
        var root = ValidRoot();
        root["Portals"] = new JsonArray
        {
            new JsonObject
            {
                ["Id"] = "portal-north",
                ["Label"] = "Portão Norte",
                ["From"] = new JsonObject { ["Space"] = "World", ["X"] = 3, ["Y"] = 1 },
                ["To"] = new JsonObject { ["Space"] = "City", ["RefIndex"] = 0, ["X"] = 3, ["Y"] = 4 },
            },
            new JsonObject
            {
                ["Id"] = "portal-south",
                ["Label"] = "Portão Sul",
                ["From"] = new JsonObject { ["Space"] = "World", ["X"] = 3, ["Y"] = 8 },
                ["To"] = new JsonObject { ["Space"] = "City", ["RefIndex"] = 0, ["X"] = 3, ["Y"] = 5 },
            },
        };

        var result = CityScenarioLoader.Load(root.ToJsonString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Portals.Count);
        Assert.Equal(["portal-north", "portal-south"], result.Value!.Portals.Select(p => p.Id));
    }

    [Fact]
    public void ScenarioLoaderV2_resolves_the_authored_portal_endpoint_to_the_real_city_id()
    {
        var scenario = FullScenarioWithCityAndPortal();

        var result = ScenarioLoaderV2.LoadWorld(scenario);

        Assert.True(result.IsSuccess);
        var world = result.Value!.World;
        var portal = Assert.Single(world.Portals);
        Assert.Equal(PortalSpaceKind.World, portal.From.Space);
        Assert.Equal("", portal.From.RefId);
        Assert.Equal(PortalSpaceKind.City, portal.To.Space);
        Assert.Equal(world.Cities[0].Id.ToString(), portal.To.RefId);
    }

    [Fact]
    public void ScenarioLoaderV2_loads_a_scenario_without_any_declared_portal()
    {
        var scenario = FullScenarioWithCity(cityName: "Vale de Aster");

        var result = ScenarioLoaderV2.LoadWorld(scenario);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.World.Portals);
    }

    // --- fixtures completas (Map + Population + Behavior + Economy + City) ---

    private static string FullScenarioWithCity(string? cityName) =>
        FullScenario(cityName, includeBuilding: false, includePortal: false);

    private static string FullScenarioWithCityAndBuilding() =>
        FullScenario("Vale de Aster", includeBuilding: true, includePortal: false);

    private static string FullScenarioWithCityAndPortal() =>
        FullScenario("Vale de Aster", includeBuilding: false, includePortal: true);

    private static string FullScenario(string? cityName, bool includeBuilding, bool includePortal)
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

        var cityJson = new JsonObject
        {
            ["X"] = 2,
            ["Y"] = 2,
            ["FoundedAtTick"] = 0,
            ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
        };
        if (cityName is not null) cityJson["Name"] = cityName;

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
        root["Cities"] = new JsonArray(cityJson);

        if (includeBuilding)
        {
            root["Buildings"] = new JsonArray
            {
                new JsonObject { ["CityIndex"] = 0, ["BuildingTypeId"] = 1, ["X"] = 6, ["Y"] = 6, ["Orientation"] = 180 },
            };
        }

        if (includePortal)
        {
            root["Portals"] = new JsonArray
            {
                new JsonObject
                {
                    ["Id"] = "portal-north",
                    ["Label"] = "Portão Norte",
                    ["From"] = new JsonObject { ["Space"] = "World", ["X"] = 1, ["Y"] = 1 },
                    ["To"] = new JsonObject { ["Space"] = "City", ["RefIndex"] = 0, ["X"] = 2, ["Y"] = 2 },
                },
            };
        }

        return root.ToJsonString();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
