using System.Text.Json.Nodes;
using LivingWorld.Infrastructure;

namespace LivingWorld.Api;

/// <summary>UX pass 3 (feedback do usuário: "permitir usar algum dos templates que temos" no
/// wizard de criar mundo) — o repositório de períodos (`IPeriodTemplateRepository`) começa
/// vazio em todo processo novo (sqlite `:memory:`), então "os templates que temos" não existiam
/// de fato até este seeder rodar. Semeia 3 variações do mesmo cenário válido (mesmo shape testado
/// em `ScenarioLoaderV2Tests.FullValidRoot`), só variando tamanho de mapa/população — idempotente
/// (não sobrescreve se o operador já registrou algo com esses ids via <c>POST /periods</c>).</summary>
public static class DefaultPeriodSeeder
{
    private sealed record Preset(string Id, string Name, int Width, int Height, ulong Seed, int InitialPopulation);

    private static readonly Preset[] Presets =
    [
        new("vila-pequena", "Vila pequena", Width: 10, Height: 10, Seed: 1, InitialPopulation: 40),
        new("cidade-media", "Cidade média", Width: 20, Height: 20, Seed: 2, InitialPopulation: 150),
        new("grande-metropole", "Grande metrópole", Width: 32, Height: 32, Seed: 3, InitialPopulation: 400),
    ];

    public static void SeedIfEmpty(IPeriodTemplateRepository repository)
    {
        foreach (var preset in Presets)
        {
            if (repository.FindLatestVersion(preset.Id) is not null) continue;

            repository.Save(new PeriodTemplateRecord
            {
                PeriodId = preset.Id,
                Version = 1,
                PayloadJson = BuildPayload(preset),
                CreatedAtUtc = DateTime.UtcNow,
                Source = preset.Name,
            });
        }
    }

    private static string BuildPayload(Preset preset)
    {
        var root = new JsonObject
        {
            ["Width"] = preset.Width,
            ["Height"] = preset.Height,
            ["Seed"] = preset.Seed,
            ["RegionSize"] = 5,
            ["TerrainIds"] = new JsonArray(1, 2, 3),
            ["BiomeIds"] = new JsonArray(1),
            ["ResourceIds"] = new JsonArray(),
            ["CostWeights"] = new JsonObject
            {
                ["Base"] = 1.0,
                ["AltitudeWeight"] = 0.5,
                ["TerrainWeight"] = new JsonObject { ["1"] = 1.0, ["2"] = 1.5, ["3"] = 3.0 },
            },
            ["Settlements"] = new JsonArray(new JsonObject
            {
                ["Name"] = "vila",
                ["X"] = preset.Width / 2,
                ["Y"] = preset.Height / 2,
            }),

            ["InitialPopulation"] = preset.InitialPopulation,
            ["Culture"] = 1,
            ["VillageX"] = preset.Width / 2,
            ["VillageY"] = preset.Height / 2,
            ["CultureIds"] = new JsonArray(1),
            ["ProfessionIds"] = new JsonArray(1, 2),
            ["LocationTypeIds"] = new JsonArray(),
            ["MaxLongevityYears"] = 90,
            ["LifeTableBrackets"] = new JsonArray(
                new JsonObject { ["MinAgeYears"] = 0, ["MaxAgeYears"] = 1, ["BaseAnnualMortality"] = 0.08 },
                new JsonObject { ["MinAgeYears"] = 2, ["MaxAgeYears"] = 14, ["BaseAnnualMortality"] = 0.01 },
                new JsonObject { ["MinAgeYears"] = 15, ["MaxAgeYears"] = 39, ["BaseAnnualMortality"] = 0.004 },
                new JsonObject { ["MinAgeYears"] = 40, ["MaxAgeYears"] = 59, ["BaseAnnualMortality"] = 0.01 },
                new JsonObject { ["MinAgeYears"] = 60, ["MaxAgeYears"] = 79, ["BaseAnnualMortality"] = 0.04 },
                new JsonObject { ["MinAgeYears"] = 80, ["MaxAgeYears"] = 89, ["BaseAnnualMortality"] = 0.15 }),
            ["FertilityMinAge"] = 16,
            ["FertilityMaxAge"] = 45,
            ["AnnualConceptionChance"] = 0.25,
            ["GestationDays"] = 270,
            ["MaxBytesPerNpcPerYear"] = 4000,

            ["HungerDecayPerHour"] = 2.0,
            ["ThirstDecayPerHour"] = 3.0,
            ["SleepDecayPerHour"] = 1.5,
            ["SocialDecayPerHour"] = 1.0,
            ["UrgencyThreshold"] = 70,
            ["MaxActionSelectionSteps"] = 10,
            ["HysteresisEnabled"] = true,
            ["ContinuityBonus"] = 5.0,
            ["HomelessSleepEfficiency"] = 0.5,
            ["MaxDurationHours"] = new JsonObject
            {
                ["Eat"] = 2,
                ["Sleep"] = 8,
                ["Work"] = 8,
                ["Socialize"] = 3,
                ["Travel"] = 4,
                ["Idle"] = 2,
                ["Buy"] = 2,
                ["UsePower"] = 1,
            },
            ["RoutineSlots"] = new JsonArray(
                new JsonObject { ["ProfessionId"] = 1, ["Stage"] = "Adult", ["HourStart"] = 6, ["HourEnd"] = 14, ["Action"] = "Work" },
                new JsonObject { ["ProfessionId"] = 2, ["Stage"] = "Adult", ["HourStart"] = 7, ["HourEnd"] = 15, ["Action"] = "Work" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Adult", ["HourStart"] = 18, ["HourEnd"] = 20, ["Action"] = "Socialize" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Adult", ["HourStart"] = 22, ["HourEnd"] = 23, ["Action"] = "Sleep" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Adult", ["HourStart"] = 0, ["HourEnd"] = 5, ["Action"] = "Sleep" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Child", ["HourStart"] = 20, ["HourEnd"] = 23, ["Action"] = "Sleep" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Child", ["HourStart"] = 0, ["HourEnd"] = 6, ["Action"] = "Sleep" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Elder", ["HourStart"] = 21, ["HourEnd"] = 23, ["Action"] = "Sleep" },
                new JsonObject { ["ProfessionId"] = null, ["Stage"] = "Elder", ["HourStart"] = 0, ["HourEnd"] = 6, ["Action"] = "Sleep" }),
            ["DefaultAction"] = "Idle",

            // Estes presets não autoram cadeia produtiva, estoque ou preços. Mantê-los com a
            // economia ligada fazia a população morrer de fome durante a simples observação.
            ["EconomyEnabled"] = false,
            ["FoodResourceId"] = 1,
            ["WaterResourceId"] = 2,
            ["PriceSensitivity"] = 0.1,
            ["CapacityByResourceLocation"] = new JsonObject(),
            ["SpoilagePerDayByResource"] = new JsonObject(),
            ["WageByProfession"] = new JsonObject(),
            ["PriceFloor"] = new JsonObject(),
            ["PriceCeiling"] = new JsonObject(),
            ["DemandBaselinePerNpc"] = new JsonObject(),
            ["Recipes"] = new JsonObject(),
            ["MarketLocationTypeIds"] = new JsonArray(),
            ["LocationTypeByProfession"] = new JsonObject(),
            ["Workplaces"] = new JsonArray(),

            ["CitiesEnabled"] = true,
            ["FoodShortageThreshold"] = 0.1,
            ["HousingShortageThreshold"] = 0.1,
            ["SecurityShortageThreshold"] = 0.1,
            ["EmigrationRatePerDeficitUnit"] = 0.1,
            ["MigrationEmploymentWeight"] = 0.1,
            ["MigrationFoodWeight"] = 0.1,
            ["MigrationSecurityWeight"] = 0.1,
            ["MigrationFamilyTiesWeight"] = 0.1,
            ["FoundingConcentrationThreshold"] = 0.1,
            ["FoundingResourceThreshold"] = 0.1,
            ["FoundingRouteThreshold"] = 0.1,
            ["FoundingDefensibilityThreshold"] = 0.1,
            ["FoundingLeadershipThreshold"] = 0.1,
            ["OrganizationTicks"] = 1,
            ["MaterializationIdleTicksBeforeEligible"] = 1,
            ["BuildingRecipes"] = new JsonObject(),
            ["Cities"] = new JsonArray(),
        };

        return root.ToJsonString();
    }
}
