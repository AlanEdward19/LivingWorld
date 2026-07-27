using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Monta o cenário "default" (vila medieval: 24h/dia, 30 dias/mês, 12 meses/ano, 100
/// NPCs iniciais) e roda N ticks, devolvendo os dois hashes. Usado pelos testes de determinismo
/// (mesmo processo e entre processos, via LivingWorld.Workers) e pelos golden hashes.</summary>
public static class ScenarioRunner
{
    public const int DefaultInitialPopulation = 100;

    /// <summary>Teto de bytes/NPC/ano do cenário default (task 13) — mesmo valor declarado em
    /// scenarios/default.json (AD-027: o "default" do gate continua hardcoded aqui).</summary>
    public const long DefaultMaxBytesPerNpcPerYear = 4000;

    public static WorldCalendar DefaultCalendar { get; } = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    /// <summary>Ordem (Fase 4, task 15): decaimento de necessidade roda antes da decisão de
    /// ação no mesmo tick — senão <see cref="BehaviorDecisionSystem"/> decidiria com o dado de
    /// necessidade do tick anterior. Ambos entram depois de Mortalidade/Natalidade (NPC recém
    /// nascido/morto neste tick já participa da conta certa: morto não decide, nascido decai a
    /// partir do próprio nascimento).</summary>
    public static IReadOnlyList<ISimulationSystem> DefaultSystems() =>
    [
        new ExampleCounterSystem(TickFrequency.Hourly),
        new ExampleCounterSystem(TickFrequency.Daily),
        new ExampleCounterSystem(TickFrequency.Monthly),
        new ExampleCounterSystem(TickFrequency.Yearly),
        new MortalitySystem(),
        new NatalitySystem(),
        new NeedsDecaySystem(),
        new BehaviorDecisionSystem(),
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

    public static (WorldState World, WorldClock Clock) Create(
        ulong seed, int maxIterationsPerTick = 1000, int initialPopulation = DefaultInitialPopulation)
    {
        var world = new WorldState(
            DefaultCalendar, seed, DefaultMap(seed), DefaultPopulationCatalog, DefaultPopulationRules,
            DefaultNeedsRules, DefaultActionCatalog, DefaultLifeStageRules);
        if (initialPopulation > 0)
            PopulationSeeder.SeedInitial(world, initialPopulation, DefaultCulture, DefaultVillageLocation);

        return (world, new WorldClock(DefaultSystems(), maxIterationsPerTick));
    }

    public static (string CanonicalHash, string VolatileHash) RunAndHash(ulong seed, long ticks)
    {
        var (world, clock) = Create(seed);
        clock.Run(world, ticks);
        return (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world));
    }
}
