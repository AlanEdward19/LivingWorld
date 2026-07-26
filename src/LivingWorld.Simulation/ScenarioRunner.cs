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

    public static (WorldState World, WorldClock Clock) Create(ulong seed, int maxIterationsPerTick = 1000) =>
        (new WorldState(DefaultCalendar, seed), new WorldClock(DefaultSystems(), maxIterationsPerTick));

    public static (string CanonicalHash, string VolatileHash) RunAndHash(ulong seed, long ticks)
    {
        var (world, clock) = Create(seed);
        clock.Run(world, ticks);
        return (WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world));
    }
}
