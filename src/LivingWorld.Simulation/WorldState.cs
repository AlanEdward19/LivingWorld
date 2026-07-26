using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Estado do mundo — tudo que precisa sobreviver a um snapshot (task 7). Controles de
/// host (pausa, velocidade) ficam fora de propósito: são estado do hospedeiro, não do mundo
/// (ver <see cref="SimulationHost"/>).</summary>
public sealed class WorldState
{
    private readonly WorldRngRegistry _rng;
    private readonly EventScheduler _scheduler;
    private long _nextEventId;

    [Canonical] public WorldCalendar Calendar { get; }
    [Canonical] public WorldDate CurrentDate { get; internal set; }
    [Canonical] public long NextEventId => _nextEventId;

    /// <summary>Seed raiz do mundo. Precisa sobreviver ao snapshot: sem ela, um stream de RNG
    /// pedido pela primeira vez depois de uma rehidratação derivaria de uma raiz diferente.</summary>
    [Canonical] public ulong Seed { get; }

    [Canonical]
    public IReadOnlyList<RngStreamState> RngStreams => _rng.Snapshot();

    [Canonical]
    public IReadOnlyList<ScheduledEvent> PendingEvents => _scheduler.Snapshot();

    /// <summary>Contador do sistema de exemplo (task 11) — descartável na Fase 3. Nenhuma
    /// decisão lê este campo, por isso é volátil.</summary>
    [Volatile]
    public IReadOnlyDictionary<TickFrequency, long> ExampleTickCounts => _exampleTickCounts;

    private readonly Dictionary<TickFrequency, long> _exampleTickCounts = new()
    {
        [TickFrequency.Hourly] = 0,
        [TickFrequency.Daily] = 0,
        [TickFrequency.Monthly] = 0,
        [TickFrequency.Yearly] = 0,
    };

    public WorldState(WorldCalendar calendar, ulong seed)
    {
        Calendar = calendar;
        CurrentDate = WorldDate.Epoch(calendar);
        Seed = seed;
        _rng = new WorldRngRegistry(seed);
        _scheduler = new EventScheduler();
    }

    /// <summary>Reconstrói a partir de um snapshot (task 7/8) — rehidratação.</summary>
    public WorldState(
        WorldCalendar calendar,
        WorldDate currentDate,
        ulong seed,
        IReadOnlyList<RngStreamState> rngStreams,
        IReadOnlyList<ScheduledEvent> pendingEvents,
        long nextEventId,
        IReadOnlyDictionary<TickFrequency, long> exampleTickCounts)
    {
        Calendar = calendar;
        CurrentDate = currentDate;
        Seed = seed;
        _rng = new WorldRngRegistry(seed, rngStreams);
        _scheduler = new EventScheduler(pendingEvents);
        _nextEventId = nextEventId;
        _exampleTickCounts = new Dictionary<TickFrequency, long>(exampleTickCounts);
    }

    internal WorldRngRegistry Rng => _rng;
    internal EventScheduler Scheduler => _scheduler;

    internal long NextEventIdAndAdvance() => _nextEventId++;

    internal void IncrementExampleCount(TickFrequency frequency) => _exampleTickCounts[frequency]++;
}
