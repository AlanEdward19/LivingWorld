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

        // Post-ship fix (map-auto-resize removal): default.json's authored 10x10 relied on the
        // now-removed InitialMapForPopulation silently growing it to fit real household/workplace
        // footprints. The authored map is used exactly as declared now, so this fixture declares
        // enough room itself instead of depending on that removed behavior -- unrelated to what
        // each test actually exercises (population split, workplace placement, dynamics).
        root["Width"] = 40;
        root["Height"] = 40;

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

        Assert.Equal(20, world.Npcs.Count);
        Assert.True(world.EconomyRules.Enabled);
        Assert.True(world.CityRules.Enabled);
        Assert.Single(world.Workplaces);
        // Bugfix real (usuário, 2026-08-13): a vila inicial (VillageX/Y = 5,5 em default.json)
        // não coincide com a cidade autorada (X/Y = 2,2, população 0) — antes desta correção a
        // população inicial nunca era vinculada a nenhuma cidade (Npc.City ficava default),
        // então sumia de toda projeção. Agora uma segunda cidade é fundada na própria vila.
        Assert.Equal(2, world.Cities.Count);
        var homeCity = Assert.Single(world.Cities, c => c.Location == new CellCoord(5, 5));
        var otherCity = Assert.Single(world.Cities, c => c.Location == new CellCoord(2, 2));

        // Bugfix real (usuário, 2026-08-14): com 2+ cidades autoradas, só a vila inicial ganhava
        // população — a outra nascia sempre com 0 moradores. Agora InitialPopulation se
        // distribui por todas (resto pra vila inicial), nenhuma cidade autorada fica vazia à toa.
        Assert.All(world.Npcs, npc => Assert.True(npc.City == homeCity.Id || npc.City == otherCity.Id));
        Assert.Contains(world.Npcs, npc => npc.City == homeCity.Id);
        Assert.Contains(world.Npcs, npc => npc.City == otherCity.Id);
        Assert.All(world.Households, household => Assert.True(household.City == homeCity.Id || household.City == otherCity.Id));
    }

    [Fact]
    public void Authored_map_dimensions_are_never_resized_regardless_of_population()
    {
        var root = FullValidRoot();
        root["Width"] = 10;
        root["Height"] = 10;
        // Old formula ((population+2)*30 area) would have resized a 10x10 map for ANY population
        // above ~1 -- a modest population that still fits comfortably (no land-scarce decline)
        // is enough to prove the authored 10x10 is kept exactly as declared.
        root["InitialPopulation"] = 5;

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;
        Assert.Equal(10, world.Map.Width);
        Assert.Equal(10, world.Map.Height);
    }

    [Fact]
    public void Small_authored_map_with_population_too_large_to_fit_does_not_crash()
    {
        var root = FullValidRoot();
        root["Width"] = 10;
        root["Height"] = 10;
        root["InitialPopulation"] = 300;

        // Land scarcity on a 10x10 map for 300 NPCs is expected -- the requirement is that it
        // never throws (BuildingPlacementResolver/OverflowPlacer/PopulationSeeder decline
        // gracefully, never an unhandled exception). LoadWorld surfaces that decline as a Result
        // failure rather than crashing the process; a success would still carry the authored
        // 10x10 map, never resized.
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        if (result.IsSuccess)
        {
            var (world, _) = result.Value;
            Assert.Equal(10, world.Map.Width);
            Assert.Equal(10, world.Map.Height);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        }
    }

    [Fact]
    public void Identical_scenario_produces_byte_identical_map_terrain_for_the_same_seed()
    {
        string json = FullValidRoot().ToJsonString();

        var first = ScenarioLoaderV2.LoadWorld(json);
        var second = ScenarioLoaderV2.LoadWorld(json);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(MapSpatialHash.Compute(first.Value.World.Map), MapSpatialHash.Compute(second.Value.World.Map));
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

    [Fact]
    public void Initial_population_splits_across_every_authored_city_none_stays_empty()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 21;
        root["Cities"] = new JsonArray(
            new JsonObject
            {
                ["X"] = 2,
                ["Y"] = 2,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
            },
            new JsonObject
            {
                ["X"] = 8,
                ["Y"] = 8,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
            });

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;

        // 3 cidades (2 autoradas + a vila fundada em 5,5, que não coincide com nenhuma) — 21
        // dividido em 3 dá 7 exatos, então essa combinação não exercita o resto; ver o teste
        // Happy_path acima pra caso com resto.
        Assert.Equal(3, world.Cities.Count);
        foreach (var city in world.Cities)
            Assert.Contains(world.Npcs, npc => npc.City == city.Id);
        Assert.Equal(21, world.Npcs.Count);
    }

    [Fact]
    public void Explicit_initial_population_per_city_is_respected_remainder_split_among_the_rest()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 21;
        root["Cities"] = new JsonArray(
            new JsonObject
            {
                ["X"] = 2,
                ["Y"] = 2,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
                ["InitialPopulation"] = 15,
            },
            new JsonObject
            {
                ["X"] = 8,
                ["Y"] = 8,
                ["FoundedAtTick"] = 0,
                ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
            });

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;

        // (2,2) fixa 15 moradores; o resto (21-15=6) divide igual entre (8,8) e a vila fundada em
        // (5,5) — 3 cada, sem mexer na fórmula de footprint (CityBoundsResolver continua
        // derivando o tamanho da população atual, T44b não trava crescimento nenhum).
        Assert.Equal(3, world.Cities.Count);
        Assert.Equal(21, world.Npcs.Count);
        var explicitCity = Assert.Single(world.Cities, c => c.Location == new CellCoord(2, 2));
        Assert.Equal(15, world.Npcs.Count(npc => npc.City == explicitCity.Id));
        foreach (var city in world.Cities.Where(c => c.Id != explicitCity.Id))
            Assert.Equal(3, world.Npcs.Count(npc => npc.City == city.Id));
    }

    [Fact]
    public void Authored_workplace_is_placed_only_after_its_city_exists()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 0;

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;
        var workplace = Assert.Single(world.Workplaces);
        var building = Assert.Single(world.Buildings);

        Assert.Contains(world.Cities, city => city.Id == workplace.City && city.Id == building.City);
    }

    [Fact]
    public void Authored_workplace_free_location_is_preserved_by_its_building()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 0;

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;
        var workplace = Assert.Single(world.Workplaces);
        var building = Assert.Single(world.Buildings);

        Assert.Equal((new CellCoord(1, 1), (CellCoord?)new CellCoord(1, 1)),
            (workplace.Location, building.Position));
    }

    [Fact]
    public void Authored_workplace_collision_resolves_to_non_overlapping_buildings()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 0;
        root["Workplaces"] = new JsonArray(WorkplaceAt(1, 1), WorkplaceAt(1, 1));

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;
        var buildings = world.Buildings.OrderBy(building => building.Id.Value).ToArray();
        var firstCells = AbsoluteFootprint(buildings[0]).ToHashSet();

        Assert.DoesNotContain(AbsoluteFootprint(buildings[1]), firstCells.Contains);
    }

    [Fact]
    public void Authored_workplace_is_owned_by_nearest_city_by_chebyshev_distance()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 0;
        root["Cities"] = new JsonArray(CityAt(0, 0), CityAt(8, 8));
        root["Workplaces"] = new JsonArray(WorkplaceAt(7, 7));

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;
        var nearest = Assert.Single(world.Cities, city => city.Location == new CellCoord(8, 8));

        Assert.Equal(nearest.Id, Assert.Single(world.Workplaces).City);
    }

    [Fact]
    public void Authored_workplace_without_any_city_keeps_legacy_location_without_crashing()
    {
        var root = FullValidRoot();
        root["InitialPopulation"] = 0;
        root["Cities"] = new JsonArray();
        root["Workplaces"] = new JsonArray(WorkplaceAt(3, 4));

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, _) = result.Value;

        Assert.Equal((new CellCoord(3, 4), 0),
            (Assert.Single(world.Workplaces).Location, world.Buildings.Count));
    }

    private static JsonObject WorkplaceAt(int x, int y) => new()
    {
        ["LocationTypeId"] = 1,
        ["X"] = x,
        ["Y"] = y,
        ["MaxVacancies"] = 3,
        ["Treasury"] = 0,
        ["Stock"] = new JsonObject(),
        ["Prices"] = new JsonObject(),
    };

    private static JsonObject CityAt(int x, int y) => new()
    {
        ["X"] = x,
        ["Y"] = y,
        ["FoundedAtTick"] = 0,
        ["AggregatePool"] = new JsonObject { ["Count"] = 0, ["WealthSum"] = 0, ["HealthSum"] = 0 },
    };

    private static IEnumerable<CellCoord> AbsoluteFootprint(Building building)
    {
        var origin = building.Position!.Value;
        return BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId)
            .Select(cell => new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
