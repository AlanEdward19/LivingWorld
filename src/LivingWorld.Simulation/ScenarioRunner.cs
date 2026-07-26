using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Monta o cenário "default" (vila medieval: 24h/dia, 30 dias/mês, 12 meses/ano, 100
/// NPCs iniciais) e roda N ticks, devolvendo os dois hashes. Usado pelos testes de determinismo
/// (mesmo processo e entre processos, via LivingWorld.Workers) e pelos golden hashes.</summary>
public static class ScenarioRunner
{
    public const int DefaultInitialPopulation = 100;

    public static WorldCalendar DefaultCalendar { get; } = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    public static IReadOnlyList<ISimulationSystem> DefaultSystems() =>
    [
        new ExampleCounterSystem(TickFrequency.Hourly),
        new ExampleCounterSystem(TickFrequency.Daily),
        new ExampleCounterSystem(TickFrequency.Monthly),
        new ExampleCounterSystem(TickFrequency.Yearly),
        new MortalitySystem(),
        new NatalitySystem(),
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

    public static readonly PopulationCatalog DefaultPopulationCatalog = new(
        CultureIds: new HashSet<int> { 1 }, ProfessionIds: new HashSet<int>(), LocationTypeIds: new HashSet<int>());

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

    public static (WorldState World, WorldClock Clock) Create(
        ulong seed, int maxIterationsPerTick = 1000, int initialPopulation = DefaultInitialPopulation)
    {
        var world = new WorldState(DefaultCalendar, seed, DefaultMap(seed), DefaultPopulationCatalog, DefaultPopulationRules);
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
