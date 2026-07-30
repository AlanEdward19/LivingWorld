using LivingWorld.Domain;

using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Population;

namespace LivingWorld.Simulation;

/// <summary>Monta o cenário "default" (vila medieval: 24h/dia, 30 dias/mês, 12 meses/ano, 100
/// NPCs iniciais) e roda N ticks, devolvendo os dois hashes. Usado pelos testes de determinismo
/// (mesmo processo e entre processos, via LivingWorld.Workers) e pelos golden hashes.</summary>
public static class ScenarioRunner
{
    public const int DefaultInitialPopulation = 100;

    /// <summary>Teto de bytes/NPC/ano do cenário default (task 13) — delega a
    /// <see cref="DefaultPerfRules"/> (Fase 9, PERF-03).</summary>
    public static long DefaultMaxBytesPerNpcPerYear => DefaultPerfRules.MaxBytesPerAliveNpcPerYear;

    public static readonly PerfRules DefaultPerfRules = PerfRules.Default;

    public static WorldCalendar DefaultCalendar { get; } = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    /// <summary>Ordem (Fase 4, task 15): decaimento de necessidade roda antes da decisão de
    /// ação no mesmo tick — senão <see cref="BehaviorDecisionSystem"/> decidiria com o dado de
    /// necessidade do tick anterior. Ambos entram depois de Mortalidade/Natalidade (NPC recém
    /// nascido/morto neste tick já participa da conta certa: morto não decide, nascido decai a
    /// partir do próprio nascimento). Fase 5 (T20): os 4 sistemas de economia entram depois de
    /// <see cref="BehaviorDecisionSystem"/>, nessa ordem — quem contratou hoje já pode produzir
    /// no mesmo dia (Employment antes de Production); preço reage ao estoque já atualizado
    /// (Production antes de MarketPricing); salário é o último evento do mês sobre o Treasury
    /// que a produção/venda alimentou o mês inteiro. Fase 6 (T12): <see
    /// cref="SkillPracticeSystem"/> e <see cref="SkillTeachingSystem"/> entram entre Employment e
    /// Production — quem contratou hoje já pratica hoje, e a produção do mesmo dia já lê a
    /// habilidade atualizada (mesmo raciocínio de Employment-antes-de-Production). Fase 7 (T19):
    /// <see cref="RelationshipSystem"/> entra depois de <see cref="EmploymentSystem"/> (convivência
    /// em workplace/household alimenta relações antes da prática de habilidade); <see
    /// cref="CourtshipSystem"/> entra antes de <see cref="NatalitySystem"/> (casal formado no
    /// cortejo é quem a natalidade consome via <c>Npc.Spouse</c>).</summary>
    public static IReadOnlyList<ISimulationSystem> DefaultSystems() =>
    [
        new ExampleCounterSystem(TickFrequency.Hourly),
        new ExampleCounterSystem(TickFrequency.Daily),
        new ExampleCounterSystem(TickFrequency.Monthly),
        new ExampleCounterSystem(TickFrequency.Yearly),
        new MortalitySystem(),
        new FactToReportConversionScheduler(),
        new ColdArchiveSystem(),
        new CourtshipSystem(),
        new NatalitySystem(),
        new NeedsDecaySystem(),
        new BehaviorDecisionSystem(DefaultSkillsRules),
        new EmploymentSystem(),
        new RelationshipSystem(),
        new SkillPracticeSystem(DefaultSkillsRules),
        new SkillTeachingSystem(DefaultSkillsRules, DefaultLifeStageRules),
        new ProductionSystem(DefaultSkillsRules),
        new MarketPricingSystem(),
        new WagePaymentSystem(),
    ];

    private static readonly GeographyCatalog DefaultCatalog = new(
        TerrainIds: new HashSet<int> { 1, 2, 3 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights DefaultCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5,
        TerrainWeight: new Dictionary<int, double> { [1] = 1.0, [2] = 1.5, [3] = 3.0 });

    public static WorldMap DefaultMap(ulong seed) =>
        MapGenerator.Generate(seed, width: 10, height: 10, regionSize: 5, DefaultCatalog, DefaultCostWeights, [])
            .Value ?? throw new InvalidOperationException("gerador default falhou — bug no gerador, não no cenário");

    public static readonly CultureId DefaultCulture = new(1);

    public static readonly CellCoord DefaultVillageLocation = new(5, 5);

    // Profissão 1 = lavrador, 2 = ferreiro (nome só neste comentário — o motor só vê o id,
    // AD-023/AD-025). Sorteadas por peso uniforme na criação do NPC (Fase 4, task 7).
    public static readonly PopulationCatalog DefaultPopulationCatalog = new(
        CultureIds: new HashSet<int> { 1 }, ProfessionIds: new HashSet<int> { 1, 2 }, LocationTypeIds: new HashSet<int>());

    // Mortalidade infantil alta (task 5) e longevidade máxima explícita: quem sobrevive à
    // infância tem risco baixo até a velhice, onde sobe de novo.
    private static readonly LifeTable DefaultLifeTable = LifeTable.Create(
        maxLongevityYears: 90,
        brackets:
        [
            new LifeTableBracket(0, 1, 0.08),
            new LifeTableBracket(2, 14, 0.01),
            new LifeTableBracket(15, 39, 0.004),
            new LifeTableBracket(40, 59, 0.01),
            new LifeTableBracket(60, 79, 0.04),
            new LifeTableBracket(80, 89, 0.15),
        ]).Value ?? throw new InvalidOperationException("life table default inválida — bug no cenário, não no gerador");

    public static readonly PopulationRules DefaultPopulationRules = PopulationRules.Create(
        DefaultLifeTable, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 0.25, gestationDays: 270)
        .Value ?? throw new InvalidOperationException("population rules default inválida — bug no cenário, não no gerador");

    // Conteúdo real do cenário medieval (Fase 4, task 15): decaimento por hora tal que sede
    // (33h) e sono (67h) esgotam antes de fome (50h) sem comer/dormir — pressão de sobrevivência
    // mensurável em dias, não em minutos.
    public static readonly NeedsRules DefaultNeedsRules = NeedsRules.Create(
        hungerDecayPerHour: 2.0, thirstDecayPerHour: 3.0, sleepDecayPerHour: 1.5, socialDecayPerHour: 1.0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: true,
        continuityBonus: 5.0, homelessSleepEfficiency: 0.5)
        .Value ?? throw new InvalidOperationException("needs rules default inválida — bug no cenário, não no gerador");

    // Criança até 14 (mesmo corte de fertilidade mínima não se aplica ainda), adulto até 64,
    // idoso dali em diante — coerente com DefaultLifeTable (mortalidade sobe de novo aos 60).
    public static readonly LifeStageRules DefaultLifeStageRules = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64)
        .Value ?? throw new InvalidOperationException("life stage rules default inválida — bug no cenário, não no gerador");

    // Fase 6 (T12): teto único (task 1 do roadmap) compartilhado pelas 13 habilidades; taxas-base
    // por fonte — Tutoring/Parental maiores que Observation (mestre dedicado ensina melhor que
    // colega de trabalho, mesmo raciocínio de SkillTeachingSystem.GainFromTutoring); lavrador
    // (profissão 1) pratica Agriculture, ferreiro (profissão 2) pratica Craft, mesmo par de
    // DefaultEconomyCatalog.LocationTypeByProfession.
    public static readonly SkillsRules DefaultSkillsRules = SkillsRules.Create(
        cap: 100,
        baseRateBySource: new Dictionary<SkillGainSource, double>
        {
            [SkillGainSource.Practice] = 0.3,
            [SkillGainSource.DeliberateTraining] = 0.4,
            [SkillGainSource.School] = 0.2,
            [SkillGainSource.Parental] = 0.15,
            [SkillGainSource.Observation] = 0.05,
            [SkillGainSource.Tutoring] = 0.25,
        },
        skillByProfession: new Dictionary<int, SkillType> { [1] = SkillType.Agriculture, [2] = SkillType.Craft })
        .Value ?? throw new InvalidOperationException("skills rules default inválida — bug no cenário, não no gerador");

    public static readonly FamilyRules DefaultFamilyRules = FamilyRules.Create(
        relationshipDeltas: BuildDefaultRelationshipDeltas(),
        decayPerDay: 0.25,
        contactLossThresholdDays: 30,
        neutralAxisValue: 50,
        attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
        courtshipThreshold: 0.55,
        courtshipDurationDays: 90,
        marriageInitialStock: new Dictionary<int, long> { [1] = 50, [2] = 50 },
        conceptionHealthFloor: 30,
        conceptionRelationshipFloor: 20,
        conceptionResourceFloor: new Dictionary<int, long> { [1] = 5, [2] = 5 },
        maternalDeathRisk: 0.01,
        infantDeathRisk: 0.03,
        vitalityMotherWeight: 0.5,
        vitalityFatherWeight: 0.5,
        vitalityMutationStdDev: 5,
        vitalityMortalityWeight: 0.4,
        upbringingWealthWeight: 0.6,
        environmentalWealthChannelEnabled: true,
        neutralDriftEnabled: false,
        vitalityMortalitySelectionEnabled: true).Value
        ?? throw new InvalidOperationException("family rules default inválida — bug no cenário, não no gerador");

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> BuildDefaultRelationshipDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = 1.5;
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Affection)] = 1.0;
        return deltas;
    }

    // Rotina real do cenário medieval (Fase 4, task 15): lavrador/ferreiro trabalham de dia em
    // turnos distintos, adulto sem profissão específica (sentinela ProfessionType.None cai no
    // slot "any") segue o mesmo turno via slot any; todo mundo dorme à noite por estágio de
    // vida (janela sem wraparound — ActionCatalog.RoutineOf não entende hora que cruza meia-noite,
    // por isso duas janelas por estágio); fora essas janelas, Idle (DefaultAction).
    public static readonly ActionCatalog DefaultActionCatalog = ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 8,
            [ActionType.Work] = 8,
            [ActionType.Socialize] = 3,
            [ActionType.Travel] = 4,
            [ActionType.Idle] = 2,
            [ActionType.Buy] = 2,
        },
        routineSlots:
        [
            new RoutineSlot(ProfessionId: 1, LifeStage.Adult, HourStart: 6, HourEnd: 14, ActionType.Work),
            new RoutineSlot(ProfessionId: 2, LifeStage.Adult, HourStart: 7, HourEnd: 15, ActionType.Work),
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 8, HourEnd: 16, ActionType.Work),
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 18, HourEnd: 20, ActionType.Socialize),
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 22, HourEnd: 23, ActionType.Sleep),
            new RoutineSlot(ProfessionId: null, LifeStage.Adult, HourStart: 0, HourEnd: 5, ActionType.Sleep),
            new RoutineSlot(ProfessionId: null, LifeStage.Child, HourStart: 20, HourEnd: 23, ActionType.Sleep),
            new RoutineSlot(ProfessionId: null, LifeStage.Child, HourStart: 0, HourEnd: 6, ActionType.Sleep),
            new RoutineSlot(ProfessionId: null, LifeStage.Elder, HourStart: 21, HourEnd: 23, ActionType.Sleep),
            new RoutineSlot(ProfessionId: null, LifeStage.Elder, HourStart: 0, HourEnd: 6, ActionType.Sleep),
        ],
        defaultAction: ActionType.Idle)
        .Value ?? throw new InvalidOperationException("action catalog default inválido — bug no cenário, não no gerador");

    // Conteúdo real do cenário medieval (Fase 5, T20): trigo (1) e água (2) são o alimento/água
    // que Eat consome (ECON-16/17); ferro (4) é o produto da ferraria. Fazenda e ferraria são
    // sua própria loja (AD-043 — um Workplace só, papel de produção+mercado decidido por
    // MarketLocationTypeIds, não uma classe Market separada) — profissão 1 (lavrador) trabalha
    // na fazenda, profissão 2 (ferreiro) na ferraria, mesmo par já usado pela rotina (task 15).
    // Sem recurso de célula exigido: DefaultCatalog (Geografia, Fase 2) não declara recurso
    // natural nenhum (ResourceIds vazio) — exigir um aqui travaria toda produção em silêncio.
    public static readonly EconomyRules DefaultEconomyRules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int ResourceId, int LocationTypeId), long>
        {
            [(1, 1)] = 1000, // trigo na fazenda
            [(2, 1)] = 1000, // água na fazenda (poço da propriedade)
            [(4, 2)] = 1000, // ferro na ferraria
        },
        spoilagePerDayByResource: new Dictionary<int, double> { [1] = 0.01 }, // só trigo estraga
                                                                              // Salário mensal precisa cobrir consumo diário (Eat gasta ~1 trigo + 1 água por vez,
                                                                              // aproximadamente 1x/dia por NEEDS-01) a preço de mercado — ~60/mês de custo a preço 1-2,
                                                                              // wage bem acima disso dá folga real, não só sobrevivência no fio da navalha.
        wageByProfession: new Dictionary<int, long> { [1] = 90, [2] = 110 },
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1, [4] = 1 },
        priceCeiling: new Dictionary<int, long> { [1] = 20, [2] = 15, [4] = 80 },
        priceSensitivity: 0.2,
        demandBaselinePerNpc: new Dictionary<int, double> { [1] = 0.5, [2] = 0.3 })
        .Value ?? throw new InvalidOperationException("economy rules default inválida — bug no cenário, não no gerador");

    public static readonly EconomyCatalog DefaultEconomyCatalog = new(
        Recipes: new Dictionary<int, ProductionRecipe>
        {
            // Fazenda produz trigo e água (poço da propriedade) — sem isso, sede nunca teria
            // fonte nenhuma no cenário default e a vila se extinguiria por sede em qualquer
            // horizonte longo, violando o objetivo #1 (100 anos coerente).
            // MaxWorkersPerCycle acompanha MaxVacancies (SeedDefaultWorkplaces) — teto de vaga
            // menor que o teto de produção deixaria gente contratada sem contar pra produção
            // real, um gargalo artificial que a vila inteira sentiria (visto na prática: 100
            // NPCs, só 15 vagas, população inteira morrendo de fome com celeiro cheio e sem
            // vender nada).
            [1] = ProductionRecipe.Create(
                inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [1] = 10, [2] = 8 },
                requiresCellResource: null, maxWorkersPerCycle: 80)
                .Value ?? throw new InvalidOperationException("recipe fazenda inválida"),
            [2] = ProductionRecipe.Create(
                inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [4] = 5 },
                requiresCellResource: null, maxWorkersPerCycle: 40)
                .Value ?? throw new InvalidOperationException("recipe ferraria inválida"),
        },
        MarketLocationTypeIds: [1, 2],
        LocationTypeByProfession: new Dictionary<int, int> { [1] = 1, [2] = 2 });

    /// <summary>Receitas com tetos de trabalhadores altos para cenário de escala (PERF-01).</summary>
    public static EconomyCatalog ScaleEconomyCatalog(int workerCapMultiplier)
    {
        int mult = Math.Max(1, workerCapMultiplier);
        return new EconomyCatalog(
            Recipes: new Dictionary<int, ProductionRecipe>
            {
                [1] = ProductionRecipe.Create(
                    inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [1] = 10, [2] = 8 },
                    requiresCellResource: null, maxWorkersPerCycle: 80 * mult)
                    .Value ?? throw new InvalidOperationException("recipe fazenda inválida"),
                [2] = ProductionRecipe.Create(
                    inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [4] = 5 },
                    requiresCellResource: null, maxWorkersPerCycle: 40 * mult)
                    .Value ?? throw new InvalidOperationException("recipe ferraria inválida"),
            },
            MarketLocationTypeIds: DefaultEconomyCatalog.MarketLocationTypeIds,
            LocationTypeByProfession: DefaultEconomyCatalog.LocationTypeByProfession);
    }

    /// <summary>Fazenda e ferraria iniciais, sem empregado (contratados pelo <see
    /// cref="EmploymentSystem"/> no primeiro Daily), sem estoque (produzido pelo <see
    /// cref="ProductionSystem"/>). Preço inicial 5 (não 1, o piso): <see
    /// cref="MarketPricingSystem"/> é multiplicativo — arredondado pra inteiro, preço 1 nunca sai
    /// do lugar de verdade (fator 0.8 ou 1.2 sobre 1 arredonda de volta pra 1), escondendo
    /// qualquer sinal de escassez/fartura (achado escrevendo o teste causal de T25).</summary>
    private static void SeedDefaultWorkplaces(WorldState world, int vacancyMultiplier = 1)
    {
        int mult = Math.Max(1, vacancyMultiplier);
        // Treasury inicial grande (capital de giro do dono, estado inicial declarado — não
        // cunhagem, ECON-26/27 continua íntegro): folha de ~36 empregados a 90-110/mês esgotaria
        // um treasury pequeno bem antes da receita de venda (compras em lote esporádicas)
        // acompanhar, gerando WageUnpaid em cascata e, por tabela, fome real generalizada.
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), DefaultVillageLocation, maxVacancies: 80 * mult,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: new Money(500_000),
            prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5, [new ResourceType(2)] = 5 }));
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(2), DefaultVillageLocation, maxVacancies: 40 * mult,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: new Money(500_000),
            prices: new Dictionary<ResourceType, long> { [new ResourceType(4)] = 5 }));
    }

    /// <summary>Trigo/água de despensa + moeda no bolso pra cada NPC/Household inicial —
    /// ponte pro primeiro salário (<see cref="WagePaymentSystem"/> é Monthly): sem isso, todo
    /// mundo bate fome/sede zero e morre de fome (NEEDS-03) bem antes do primeiro emprego
    /// (<see cref="EmploymentSystem"/>, Daily) gerar produção e renda de verdade — o cenário
    /// default se extinguiria em semanas, não sobreviveria os 100 anos do objetivo #1.
    /// Quantidade cobre só o mês de bootstrap; da­í em diante o ciclo emprego→produção→salário→
    /// compra é quem sustenta.</summary>
    private static void SeedInitialEconomyBuffer(WorldState world)
    {
        foreach (var household in world.Households)
        {
            household.Deposit(new ResourceType(1), 50); // trigo
            household.Deposit(new ResourceType(2), 50); // água
        }

        foreach (var npc in world.Npcs)
            npc.CreditWallet(new Money(50));
    }

    /// <summary><paramref name="economyRules"/> permite ao harness de teste base/tratamento
    /// (T25/ECON-28) variar só um parâmetro (ex.: capacidade de um recurso) sem duplicar o resto
    /// da montagem do cenário — default é <see cref="DefaultEconomyRules"/>, ninguém fora de
    /// teste precisa informar.</summary>
    public static (WorldState World, WorldClock Clock) Create(
        ulong seed, int maxIterationsPerTick = 1000, int initialPopulation = DefaultInitialPopulation,
        EconomyRules? economyRules = null, FamilyRules? familyRules = null, PerfRules? perfRules = null,
        PopulationRules? populationRules = null, int workplaceVacancyMultiplier = 1,
        EconomyCatalog? economyCatalog = null, HistoryRules? historyRules = null)
    {
        var rules = economyRules ?? DefaultEconomyRules;
        var family = familyRules ?? DefaultFamilyRules;
        var perf = perfRules ?? DefaultPerfRules;
        var population = populationRules ?? DefaultPopulationRules;
        var catalog = economyCatalog ?? DefaultEconomyCatalog;
        var history = historyRules ?? HistoryRules.Disabled;
        var world = new WorldState(
            DefaultCalendar, seed, DefaultMap(seed), DefaultPopulationCatalog, population,
            DefaultNeedsRules, DefaultActionCatalog, DefaultLifeStageRules,
            economyRules: rules, economyCatalog: catalog, familyRules: family, perfRules: perf,
            historyRules: history);
        if (initialPopulation > 0)
        {
            PopulationSeeder.SeedInitial(world, initialPopulation, DefaultCulture, DefaultVillageLocation);
            SeedInitialEconomyBuffer(world);
        }
        SeedDefaultWorkplaces(world, workplaceVacancyMultiplier);

        return (world, new WorldClock(DefaultSystems(), maxIterationsPerTick));
    }

    public static (string CanonicalHash, string VolatileHash) RunAndHash(ulong seed, long ticks)
    {
        var (world, clock) = Create(seed);
        clock.Run(world, ticks);
        return (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world));
    }
}
