using System.Text.Json.Nodes;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T13 (Goal #2 da fase; edge case "remoção de profissão em uso"): as 4 regras
/// de transformação (Emerge/Merge/Split/Disappear) realmente mudam
/// <see cref="LivingWorld.Domain.PopulationCatalog.ProfessionIds"/> em runtime e reatribuem quem
/// ficou com a profissão removida — nunca deixam um NPC com um id fora do catálogo.</summary>
public class PeriodEvolutionSystemTests
{
    private static JsonObject BasePeriod(int[] professionIds, int initialPopulation = 20)
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();
        root["ProfessionIds"] = new JsonArray(professionIds.Select(i => (JsonNode)i).ToArray());
        root["InitialPopulation"] = initialPopulation;
        // RoutineSlots referencia ProfessionId 1/2 fixos do default.json — sem efeito aqui, o
        // motor só usa Stage/Action quando ProfessionId não bate (RoutineSlots[].ProfessionId
        // null é o fallback), então profissões fora de {1,2} continuam funcionando.

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
    public void Emerge_adds_the_target_profession_to_the_catalog_once_the_trigger_tick_is_reached()
    {
        var root = BasePeriod([1, 2]);
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Emerge",
                ["TargetProfessionIds"] = new JsonArray(5),
                ["TriggerTick"] = 5,
            }),
        };

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        Assert.DoesNotContain(5, world.PopulationCatalog.ProfessionIds);

        for (int tick = 0; tick < 10; tick++)
            clock.Tick(world);

        Assert.Contains(5, world.PopulationCatalog.ProfessionIds);
    }

    [Fact]
    public void Disappear_removes_the_source_profession_and_reassigns_every_holder_to_None()
    {
        var root = BasePeriod([1, 2], initialPopulation: 30);
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Disappear",
                ["SourceProfessionIds"] = new JsonArray(1),
                ["TriggerTick"] = 0,
            }),
        };

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        for (int tick = 0; tick < 3; tick++)
            clock.Tick(world);

        Assert.DoesNotContain(1, world.PopulationCatalog.ProfessionIds);
        Assert.DoesNotContain(world.Npcs, n => n.IsAlive && n.Profession.Id == 1);
    }

    [Fact]
    public void Merge_removes_the_sources_adds_the_target_and_reassigns_holders_to_it()
    {
        var root = BasePeriod([1, 2, 3], initialPopulation: 30);
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Merge",
                ["SourceProfessionIds"] = new JsonArray(1, 2),
                ["TargetProfessionIds"] = new JsonArray(3),
                ["TriggerTick"] = 0,
            }),
        };

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        for (int tick = 0; tick < 3; tick++)
            clock.Tick(world);

        Assert.Equal([3], world.PopulationCatalog.ProfessionIds.OrderBy(id => id));
        Assert.DoesNotContain(world.Npcs, n => n.IsAlive && (n.Profession.Id == 1 || n.Profession.Id == 2));
    }

    [Fact]
    public void Split_removes_the_source_adds_both_targets_and_redistributes_holders_between_them()
    {
        // Só a profissão 3 existe no catálogo inicial — todo mundo nasce com ela.
        var root = BasePeriod([3], initialPopulation: 30);
        root["Dynamics"] = new JsonObject
        {
            ["TransformationRules"] = new JsonArray(new JsonObject
            {
                ["Kind"] = "Split",
                ["SourceProfessionIds"] = new JsonArray(3),
                ["TargetProfessionIds"] = new JsonArray(1, 2),
                ["TriggerTick"] = 0,
            }),
        };

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        for (int tick = 0; tick < 3; tick++)
            clock.Tick(world);

        Assert.Equal([1, 2], world.PopulationCatalog.ProfessionIds.OrderBy(id => id));
        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        Assert.DoesNotContain(alive, n => n.Profession.Id == 3);
        Assert.Contains(alive, n => n.Profession.Id == 1);
        Assert.Contains(alive, n => n.Profession.Id == 2);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
