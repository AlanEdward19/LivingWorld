using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation;
using LivingWorld.Tests.Baselines;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T10 (PERIOD-14..16): "baseline de horizonte mínimo" — mesmo braço de
/// tratamento de <see cref="PeriodCausalTests"/> (viés declarado pra profissão 2), rodado por um
/// horizonte curto (30 dias) em vez de só a semeadura inicial. Grava/reprova a fração de
/// população naquela profissão por seed em <c>tests/baselines/period-evolution-horizon.json</c>
/// — mesmo padrão de <see cref="LivingWorld.Tests.Population.PopulationBaselineTests"/>, prova
/// que o viés continua valendo (nascimentos/materialização também sorteiam por
/// <see cref="LivingWorld.Domain.PopulationCatalog.RollProfession"/>) conforme o mundo evolui,
/// não só no instante zero.</summary>
public class PeriodEvolutionHorizonBaselineTests
{
    private const int BiasedProfessionId = 2;
    private const double BiasedWeight = 5.0;
    private const long ShortHorizonTicks = 30 * 24; // 30 dias
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");

    private static JsonObject TreatmentPeriod(ulong seed)
    {
        string defaultJson = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var root = JsonNode.Parse(defaultJson)!.AsObject();
        root["Seed"] = seed;
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

        root["Dynamics"] = new JsonObject
        {
            ["ProfessionBiases"] = new JsonArray(new JsonObject { ["ProfessionId"] = BiasedProfessionId, ["Weight"] = BiasedWeight }),
        };

        return root;
    }

    private static double BiasedProfessionFractionAfterHorizon(ulong seed)
    {
        var result = ScenarioLoaderV2.LoadWorld(TreatmentPeriod(seed).ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        for (long tick = 0; tick < ShortHorizonTicks; tick++)
            clock.Tick(world);

        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        return alive.Count == 0 ? 0.0 : (double)alive.Count(n => n.Profession.Id == BiasedProfessionId) / alive.Count;
    }

    [Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]
    public void ZZZ_record_baseline()
    {
        var fractions = Enumerable.Range(1, 20).ToDictionary(seed => seed, seed => BiasedProfessionFractionAfterHorizon((ulong)seed));
        BaselineFixture.Record(BaselinesDir, "period-evolution-horizon", fractions);
    }

    [Fact]
    public void Biased_profession_fraction_after_a_short_horizon_matches_the_recorded_baseline_within_tolerance()
    {
        var path = Path.Combine(BaselinesDir, "period-evolution-horizon.json");
        var recorded = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path))!;

        foreach (var (seedText, expectedFraction) in recorded)
        {
            int seed = int.Parse(seedText);
            double actual = BiasedProfessionFractionAfterHorizon((ulong)seed);
            Assert.InRange(actual, expectedFraction - 0.1, expectedFraction + 0.1);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
