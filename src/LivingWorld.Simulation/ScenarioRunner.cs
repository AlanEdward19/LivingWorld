using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Monta o cenário "default" (vila medieval: 24h/dia, 30 dias/mês, 12 meses/ano) e
/// roda N ticks, devolvendo os dois hashes. Usado pelos testes de determinismo (mesmo
/// processo e entre processos, via LivingWorld.Workers) e pelos golden hashes.</summary>
public static class ScenarioRunner
{
    public static WorldCalendar DefaultCalendar { get; } = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    public static IReadOnlyList<ISimulationSystem> DefaultSystems() =>
    [
        new ExampleCounterSystem(TickFrequency.Hourly),
        new ExampleCounterSystem(TickFrequency.Daily),
        new ExampleCounterSystem(TickFrequency.Monthly),
        new ExampleCounterSystem(TickFrequency.Yearly),
    ];

    private static readonly GeographyCatalog DefaultCatalog = new(
        TerrainIds: new HashSet<int> { 1, 2, 3 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights DefaultCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5,
        TerrainWeight: new Dictionary<int, double> { [1] = 1.0, [2] = 1.5, [3] = 3.0 });

    public static WorldMap DefaultMap(ulong seed) =>
        MapGenerator.Generate(seed, width: 10, height: 10, regionSize: 5, DefaultCatalog, DefaultCostWeights, [])
            .Value ?? throw new InvalidOperationException("gerador default falhou — bug no gerador, não no cenário");

    public static (WorldState World, WorldClock Clock) Create(ulong seed, int maxIterationsPerTick = 1000) =>
        (new WorldState(DefaultCalendar, seed, DefaultMap(seed)), new WorldClock(DefaultSystems(), maxIterationsPerTick));

    public static (string CanonicalHash, string VolatileHash) RunAndHash(ulong seed, long ticks)
    {
        var (world, clock) = Create(seed);
        clock.Run(world, ticks);
        return (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world));
    }
}
