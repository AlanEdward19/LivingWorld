using System.Text.Json.Nodes;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>Fase 13, T10 (PERIOD-14..16): braço controle (sem viés) vs. tratamento (
/// <c>Dynamics.ProfessionBiases</c> favorecendo uma profissão), mesma seed par a par, através de
/// 20 seeds — confirma que o viés desloca a fração inicial de população naquela profissão na
/// direção declarada na maioria esmagadora das seeds (roadmap: ≥18/20). Só a semeadura inicial
/// (0 ticks) — o viés já atua no sorteio de profissão de <c>PopulationSeeder</c>, não precisa
/// rodar horizonte pra provar direção (ver <see cref="PeriodEvolutionHorizonBaselineTests"/> pro
/// horizonte curto).</summary>
public class PeriodCausalTests
{
    private const int BiasedProfessionId = 2;
    private const double BiasedWeight = 5.0;
    private const int SeedCount = 20;
    private const int RequiredSeedsWithExpectedDirection = 18;

    private static JsonObject BasePeriod()
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();
        root["ProfessionIds"] = new JsonArray(1, 2);

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

    private static double BiasedProfessionFraction(JsonObject period, ulong seed)
    {
        var root = period.DeepClone().AsObject();
        root["Seed"] = seed;

        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var world = result.Value.World;

        int biased = world.Npcs.Count(n => n.Profession.Id == BiasedProfessionId);
        return (double)biased / world.Npcs.Count;
    }

    [Fact]
    public void Declared_profession_bias_shifts_the_initial_population_toward_the_favored_profession_in_most_seeds()
    {
        var control = BasePeriod();

        var treatment = BasePeriod();
        treatment["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = BiasedProfessionId, ["Weight"] = BiasedWeight }),
        };

        int seedsWithExpectedDirection = 0;
        for (ulong seed = 1; seed <= SeedCount; seed++)
        {
            double controlFraction = BiasedProfessionFraction(control, seed);
            double treatmentFraction = BiasedProfessionFraction(treatment, seed);
            if (treatmentFraction > controlFraction)
                seedsWithExpectedDirection++;
        }

        Assert.True(
            seedsWithExpectedDirection >= RequiredSeedsWithExpectedDirection,
            $"viés na direção esperada em só {seedsWithExpectedDirection}/{SeedCount} seeds (esperado >= {RequiredSeedsWithExpectedDirection})");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
