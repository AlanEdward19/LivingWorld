using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Performance;

/// <summary>Cenário de escala com demografia estável (PERF-01) — calibrado para não colapsar
/// como o default (que cai a ~13% dos iniciais em pop 1k/5k). Ajustes vs default (AD-047):
/// mortalidade adulta mais baixa, salários e produção maiores, riscos perinatais menores.</summary>
public static class ScaleScenarioFixture
{
    public const int PopulationSmall = 1_000;
    public const int PopulationLarge = 5_000;

    /// <summary>População viva mínima aceitável após 1 ano-sim = 20% do inicial (tasks T-A2).</summary>
    public static int MinimumAliveAfterOneYear(int initialPopulation) =>
        (int)(initialPopulation * 0.2);

    private static readonly LifeTable ScaleLifeTable = LifeTable.Create(
        maxLongevityYears: 90,
        brackets:
        [
            new LifeTableBracket(0, 1, 0.03),
            new LifeTableBracket(2, 14, 0.005),
            new LifeTableBracket(15, 39, 0.002),
            new LifeTableBracket(40, 59, 0.006),
            new LifeTableBracket(60, 79, 0.025),
            new LifeTableBracket(80, 89, 0.10),
        ]).Value ?? throw new InvalidOperationException("scale life table inválida");

    public static readonly PopulationRules ScalePopulationRules = PopulationRules.Create(
        ScaleLifeTable, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 0.22, gestationDays: 270)
        .Value ?? throw new InvalidOperationException("scale population rules inválida");

    public static readonly EconomyRules ScaleEconomyRules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>
        {
            [(1, 1)] = 50_000,
            [(2, 1)] = 50_000,
            [(4, 2)] = 50_000,
        },
        spoilagePerDayByResource: new Dictionary<int, double> { [1] = 0.005 },
        wageByProfession: new Dictionary<int, long> { [1] = 150, [2] = 180 },
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1, [4] = 1 },
        priceCeiling: new Dictionary<int, long> { [1] = 25, [2] = 20, [4] = 100 },
        priceSensitivity: 0.15,
        demandBaselinePerNpc: new Dictionary<int, double> { [1] = 0.45, [2] = 0.25 })
        .Value ?? throw new InvalidOperationException("scale economy rules inválida");

    public static readonly FamilyRules ScaleFamilyRules = FamilyRules.Create(
        relationshipDeltas: ScenarioRunner.DefaultFamilyRules.RelationshipDeltas,
        decayPerDay: 0.25,
        contactLossThresholdDays: 30,
        neutralAxisValue: 50,
        attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
        courtshipThreshold: 0.55,
        courtshipDurationDays: 90,
        marriageInitialStock: new Dictionary<int, long> { [1] = 80, [2] = 80 },
        conceptionHealthFloor: 25,
        conceptionRelationshipFloor: 15,
        conceptionResourceFloor: new Dictionary<int, long> { [1] = 3, [2] = 3 },
        maternalDeathRisk: 0.005,
        infantDeathRisk: 0.01,
        vitalityMotherWeight: 0.5,
        vitalityFatherWeight: 0.5,
        vitalityMutationStdDev: 5,
        vitalityMortalityWeight: 0.35,
        upbringingWealthWeight: 0.6,
        environmentalWealthChannelEnabled: true,
        neutralDriftEnabled: false,
        vitalityMortalitySelectionEnabled: true,
        // Sem teto, um workplace de escala (ScaleEconomyCatalog permite milhares de trabalhadores
        // simultâneos) faz RelationshipSystem formar par-a-par O(k²) — 8.000² = 64M pares/dia num
        // único workplace, achado real que tornava LongRunScaleTests impraticável (baseline-
        // timings.md, fase 16 T5). 30 é "círculo social" plausível (mais que qualquer household,
        // bem menos que centenas de colegas) — não afeta nenhum household/workplace pequeno
        // (default de outros cenários nem declara este campo, cai no int.MaxValue sem teto).
        maxCohabitationGroupSize: 30).Value
        ?? throw new InvalidOperationException("scale family rules inválida");

    public static (WorldState World, WorldClock Clock) CreateWorld(ulong seed, int initialPopulation)
    {
        int vacancyMult = Math.Max(1, initialPopulation / 100);
        var scenario = ScenarioRunner.Create(
            seed,
            initialPopulation: initialPopulation,
            economyRules: ScaleEconomyRules,
            familyRules: ScaleFamilyRules,
            populationRules: ScalePopulationRules,
            perfRules: PerfRules.ScaleSensorInitial,
            workplaceVacancyMultiplier: vacancyMult,
            economyCatalog: ScenarioRunner.ScaleEconomyCatalog(vacancyMult));

        // PERF-01 mede custo e estabilidade demográfica, não a capacidade de um único mercado
        // central alimentar uma metrópole. Depois que cada household ganhou residência física,
        // os moradores mais distantes passaram a consumir o buffer default antes de completar
        // o primeiro deslocamento. Uma reserva anual mantém o fixture causalmente estável sem
        // alterar a economia dos mundos reais nem esconder fome nos respectivos testes.
        const long annualReservePerMember = 400;
        foreach (var household in scenario.World.Households)
        {
            long reserve = annualReservePerMember * household.Members.Count;
            household.Deposit(new ResourceType(1), reserve);
            household.Deposit(new ResourceType(2), reserve);
        }

        return scenario;
    }
}
