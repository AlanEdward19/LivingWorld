using System.Text.Json.Nodes;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Infrastructure.Repositories;

namespace LivingWorld.Api.Periods;

/// <summary>UX pass 3 (feedback do usuário: "permitir usar algum dos templates que temos" no
/// wizard de criar mundo) — o repositório de períodos (`IPeriodTemplateRepository`) começa
/// vazio em todo processo novo (sqlite `:memory:`), então "os templates que temos" não existiam
/// de fato até este seeder rodar. Semeia 3 variações do mesmo cenário válido (mesmo shape testado
/// em `ScenarioLoaderV2Tests.FullValidRoot`), só variando tamanho de mapa/população — idempotente
/// (não sobrescreve se o operador já registrou algo com esses ids via <c>POST /periods</c>).
/// AD-175: cada preset declara o bloco <c>Presentation</c> (nome, época, descrição e tecnologia de partida do cartão do
/// menu). Um preset gravado por este seeder antes disso (mesma fonte, sem o bloco) ganha uma versão nova com o payload
/// gravado intacto mais o bloco — nada que o motor lê muda, e o cadastro de um operador (outra fonte) nunca é tocado.</summary>
public static class DefaultPeriodSeeder
{
    private sealed record Preset(string Id, string Name, int Width, int Height, ulong Seed, int InitialPopulation, string CardName, string Description);

    /// <summary>Os três presets são a mesma época em três tamanhos: o cartão diz isso, sem prometer outra época.</summary>
    private const string Era = "Aldeias agrícolas, antes da indústria";
    private const string Technology = "Ferramentas de mão e fogo. O resto os moradores inventam com o tempo.";

    private static readonly Preset[] Presets =
    [
        new("vila-pequena", "Vila pequena", Width: 10, Height: 10, Seed: 1, InitialPopulation: 40, "Vila pequena",
            "Poucas famílias num vale tranquilo. Dá para conhecer cada pessoa pelo nome."),
        new("cidade-media", "Cidade média", Width: 20, Height: 20, Seed: 2, InitialPopulation: 150, "Vila",
            "Lavradores e artesãos lado a lado: vizinhos, ofícios e conversa."),
        new("grande-metropole", "Grande metrópole", Width: 32, Height: 32, Seed: 3, InitialPopulation: 400, "Cidade",
            "Centenas de vidas ao mesmo tempo, para quem gosta de observar multidões."),
    ];

    /// <summary>Ids dos períodos do menu, na ordem do cartão.</summary>
    public static IReadOnlyList<string> PresetIds { get; } = Presets.Select(p => p.Id).ToList();

    /// <summary>AD-179: o payload que o seeder grava para o período, sem banco (testes de cenário e diagnóstico).</summary>
    public static string PayloadFor(string periodId) =>
        BuildPayload(Presets.FirstOrDefault(p => p.Id == periodId) ?? throw new ArgumentException($"período desconhecido: {periodId}", nameof(periodId)));

    public static void SeedIfEmpty(IPeriodTemplateRepository repository)
    {
        foreach (var preset in Presets)
        {
            var latest = repository.FindLatestVersion(preset.Id);
            if (latest is null)
            {
                Save(repository, preset, 1, BuildPayload(preset));
                continue;
            }
            if (latest.Source != preset.Name) continue; // cadastro do operador: nunca tocado
            var stored = JsonNode.Parse(latest.PayloadJson)?.AsObject();
            if (stored is null) continue;
            // AD-179: o preset gravado como fixture (economia desligada) vira o mundo completo, numa versão nova
            if (stored["EconomyEnabled"] is JsonValue economy && economy.TryGetValue<bool>(out bool on) && !on)
            {
                Save(repository, preset, latest.Version + 1, BuildPayload(preset));
                continue;
            }
            if (stored.ContainsKey("Presentation")) continue;
            stored["Presentation"] = PresentationOf(preset);
            Save(repository, preset, latest.Version + 1, stored.ToJsonString());
        }
    }

    private static void Save(IPeriodTemplateRepository repository, Preset preset, int version, string payload) =>
        repository.Save(new PeriodTemplateRecord
        {
            PeriodId = preset.Id,
            Version = version,
            PayloadJson = payload,
            CreatedAtUtc = DateTime.UtcNow,
            Source = preset.Name,
        });

    private static JsonObject PresentationOf(Preset preset) => new()
    {
        ["Name"] = preset.CardName,
        ["Era"] = Era,
        ["Description"] = preset.Description,
        ["Technology"] = Technology,
    };

    /// <summary>AD-179: lado do mapa pela população, a mesma regra da vila de código (<c>InitialMapSideForPopulation</c>: lote de
    /// cada casa e oficina com folga). 10×10 para 40, 20×20 para 150 e 32×32 para 400 apertavam a cidade num quarteirão.</summary>
    private static int SideOf(Preset preset) =>
        Math.Max(Math.Max(preset.Width, preset.Height), LivingWorld.Simulation.Scenarios.ScenarioRunner.InitialMapSideForPopulation(preset.InitialPopulation));

    /// <summary>AD-179: uma fazenda e uma forja por centena de moradores, com as vagas e o caixa das da vila de código; o lote sai
    /// do traçado (<c>WorkplacePlacer</c>). AD-185: a forja (ofício) no miolo; com mais de uma fazenda, cada uma na borda da
    /// cidade, em direções diferentes (a lavoura fica fora do miolo, perto de quem a trabalha); antes todas no centro, e a casa ficava
    /// a 2,1–2,5 h do trabalho na metrópole.</summary>
    private static JsonArray WorkplacesOf(Preset preset)
    {
        int c = SideOf(preset) / 2;
        int sets = Math.Max(1, (preset.InitialPopulation + 99) / 100);
        int ring = sets == 1 ? 0 : SideOf(preset) / 5; // a borda da parte construída (a cidade inicial ocupa ~40% do lado)
        (int Dx, int Dy)[] directions = [(1, 1), (-1, -1), (1, -1), (-1, 1), (1, 0), (-1, 0), (0, 1), (0, -1)];
        var list = new JsonArray();
        for (int i = 0; i < sets; i++)
        {
            var (dx, dy) = directions[i % directions.Length];
            int r = dx != 0 && dy != 0 ? ring * 7 / 10 : ring; // na diagonal, o mesmo raio
            list.Add(new JsonObject
            {
                ["LocationTypeId"] = 1,
                ["X"] = c + dx * r,
                ["Y"] = c + dy * r,
                ["MaxVacancies"] = 160,
                ["Treasury"] = 500_000,
                ["Stock"] = new JsonObject(),
                ["Prices"] = new JsonObject { ["1"] = 5, ["2"] = 5 },
            });
            list.Add(new JsonObject
            {
                ["LocationTypeId"] = 2,
                ["X"] = c,
                ["Y"] = c,
                ["MaxVacancies"] = 40,
                ["Treasury"] = 500_000,
                ["Stock"] = new JsonObject(),
                ["Prices"] = new JsonObject { ["4"] = LivingWorld.Simulation.Scenarios.ScenarioRunner.ForgeToolPrice },
            });
        }
        return list;
    }

    private static string BuildPayload(Preset preset)
    {
        var root = new JsonObject
        {
            // AD-175: o cartão do menu (só apresentação; nenhum sistema lê)
            ["Presentation"] = PresentationOf(preset),
            ["Width"] = SideOf(preset),
            ["Height"] = SideOf(preset),
            ["Seed"] = preset.Seed,
            ["RegionSize"] = 5,
            ["TerrainIds"] = new JsonArray(1, 2, 3),
            ["BiomeIds"] = new JsonArray(1),
            ["ResourceIds"] = new JsonArray(),
            ["CostWeights"] = new JsonObject
            {
                // AD-179: a escala da vila de código (célula de ~30 min a pé); 1,0 fazia cada célula valer horas
                ["Base"] = 0.05,
                ["AltitudeWeight"] = 0.05,
                ["TerrainWeight"] = new JsonObject { ["1"] = 1.0, ["2"] = 1.5, ["3"] = 3.0 },
            },
            ["Settlements"] = new JsonArray(new JsonObject
            {
                ["Name"] = "vila",
                ["X"] = SideOf(preset) / 2,
                ["Y"] = SideOf(preset) / 2,
            }),

            ["InitialPopulation"] = preset.InitialPopulation,
            ["Culture"] = 1,
            ["VillageX"] = SideOf(preset) / 2,
            ["VillageY"] = SideOf(preset) / 2,
            ["CultureIds"] = new JsonArray(1),
            ["ProfessionIds"] = new JsonArray(1, 2),
            ["LocationTypeIds"] = new JsonArray(),
            // Nomes são só rótulo para o cliente (GET /periods/{id}/catalog). Peso 1.0 = o mesmo
            // sorteio uniforme de antes (hash do mundo não muda). Habilidades 0 e 7 são as que
            // ScenarioRunner liga às profissões 1 e 2.
            ["Dynamics"] = new JsonObject
            {
                ["ProfessionBiases"] = new JsonArray(
                    new JsonObject { ["ProfessionId"] = 1, ["Weight"] = 1.0, ["Name"] = "Lavrador" },
                    new JsonObject { ["ProfessionId"] = 2, ["Weight"] = 1.0, ["Name"] = "Artesão" }),
                ["SkillBiases"] = new JsonArray(
                    new JsonObject { ["SkillId"] = 0, ["Weight"] = 1.0, ["Name"] = "Agricultura" },
                    new JsonObject { ["SkillId"] = 7, ["Weight"] = 1.0, ["Name"] = "Artesanato" }),
            },
            // Aparência herdada dos pais (#46 fatia 2, ADR-0032): os mundos criados pelo menu já nascem com genes de aparência.
            ["Appearance"] = new JsonObject { ["Heritability"] = 0.6 },
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

            // AD-179: os períodos do menu são mundos completos, com a economia da vila de código (ScenarioRunner): fazenda (trigo e
            // poço) e forja, salário, mercado, ferramenta, presença no trabalho, empreendimento e crédito. Antes eram fixture (economia
            // desligada, sem trabalho, mercado nem água da natureza): ninguém saía de casa e a metrópole morria de sede em 2 meses.
            ["EconomyEnabled"] = true,
            ["FoodResourceId"] = 1,
            ["WaterResourceId"] = 2,
            ["PriceSensitivity"] = 0.2,
            ["WageTaxRate"] = 0.05,
            ["SurplusDrawShare"] = 1.0,
            ["PresenceNeedsWork"] = true,
            ["Venture"] = new JsonObject { ["LocationTypeIds"] = new JsonArray(2), ["EquipmentResourceId"] = 4, ["EquipmentUnits"] = 40 },
            ["Credit"] = new JsonObject { ["Enabled"] = true },
            ["CapacityByResourceLocation"] = new JsonObject { ["1,1"] = 1000, ["2,1"] = 1000, ["4,2"] = 1000 },
            ["SpoilagePerDayByResource"] = new JsonObject { ["1"] = 0.00045 },
            ["WageByProfession"] = new JsonObject { ["1"] = 90, ["2"] = 110 },
            ["PriceFloor"] = new JsonObject { ["1"] = 1, ["2"] = 1, ["4"] = 1 },
            ["PriceCeiling"] = new JsonObject { ["1"] = 20, ["2"] = 15, ["4"] = 80 },
            ["DemandBaselinePerNpc"] = new JsonObject { ["1"] = 0.5, ["2"] = 0.3 },
            ["Recipes"] = new JsonObject
            {
                ["1"] = new JsonObject
                {
                    ["Inputs"] = new JsonObject(),
                    ["Outputs"] = new JsonObject { ["1"] = 10, ["2"] = 8 },
                    ["RequiresCellResource"] = null,
                    ["MaxWorkersPerCycle"] = 80,
                    ["Tool"] = new JsonObject { ["ResourceId"] = 4, ["KitPerWorker"] = 3.0, ["WearPerWorkerYear"] = 2.0, ["UntooledOutput"] = 0.6 },
                },
                ["2"] = new JsonObject
                {
                    ["Inputs"] = new JsonObject(),
                    ["Outputs"] = new JsonObject { ["4"] = 5 },
                    ["RequiresCellResource"] = null,
                    ["MaxWorkersPerCycle"] = 40,
                },
            },
            ["MarketLocationTypeIds"] = new JsonArray(1, 2),
            ["LocationTypeByProfession"] = new JsonObject { ["1"] = 1, ["2"] = 2 },
            ["CraftLocationTypeIds"] = new JsonArray(2),
            ["Workplaces"] = WorkplacesOf(preset),
            // AD-179: tecnologia e descoberta ligadas (mobilidade, calçar): a vila do menu pode chegar à rua calçada
            ["Emergence"] = new JsonObject { ["Enabled"] = true },

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

        // Mobília como dado de cenário (#47 fatia 1, ADR-0034): o interior sai do catálogo do mundo, não do assets.json do cliente.
        LivingWorld.Simulation.Objects.DefaultObjects.AddTo(root);
        return root.ToJsonString();
    }
}
