using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure;

/// <summary>Orquestra tick + persistência: a simulação roda inteiramente em memória e
/// o repositório só é chamado nas fronteiras de snapshot — nunca dentro do laço de tick.</summary>
public sealed class PersistentWorldRunner(
    IWorldRepository repository, BranchId branch, long snapshotIntervalTicks, ColdTierPersistence? coldTier = null)
{
    private readonly object _repositoryGate = new();
    /// <summary>Roda <paramref name="ticks"/> ticks, salvando a cada <c>snapshotIntervalTicks</c>.
    /// <paramref name="clock"/> precisa ter sido construído com <paramref name="sink"/> para os
    /// eventos de história chegarem ao buffer certo.</summary>
    public void Run(WorldState world, WorldClock clock, BufferingWorldEventSink sink, long ticks)
    {
        for (long i = 0; i < ticks; i++)
        {
            clock.Tick(world);
            if (world.CurrentDate.TotalHours % snapshotIntervalTicks == 0)
                Snapshot(world, sink);
        }
    }

    /// <summary>Salva o estado atual e drena o buffer de eventos — uma transação. O arquivo frio
    /// (<see cref="ColdTierArchive"/>) é salvo na mesma fronteira quando <paramref name="coldTier"/>
    /// foi configurado; sem ele (ex.: banco <c>:memory:</c>), o arquivo frio continua só em
    /// memória, mesmo comportamento de antes desta mudança.</summary>
    public void Snapshot(WorldState world, BufferingWorldEventSink sink)
    {
        lock (_repositoryGate)
        {
            var events = sink.DrainAll();
            repository.SaveSnapshotWithEvents(
                branch, world.CurrentDate.TotalHours,
                WorldSnapshot.Serialize(world), WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world),
                events);
            coldTier?.Save(world.ColdArchive.AllNpcSummaries, world.Calendar);
        }
    }

    /// <summary>Reidrata o mundo do snapshot mais recente do branch, ou <c>null</c> se nunca
    /// houve um salvo (mundo novo). Reidrata também o arquivo frio quando <paramref name="coldTier"/>
    /// foi configurado.</summary>
    public WorldState? LoadLatest()
    {
        lock (_repositoryGate)
        {
            var record = repository.LoadLatestSnapshot(branch);
            if (record is null) return null;
            var world = WorldSnapshot.Deserialize(record.Json);
            if (coldTier is not null)
                world.ColdArchive.RestoreNpcSummaries(coldTier.Load(world.Calendar));
            return world;
        }
    }

    /// <summary>Reidrata o mundo de um tick específico já salvo (idempotência de replay). Reidrata
    /// também o arquivo frio quando <paramref name="coldTier"/> foi configurado.</summary>
    public WorldState? LoadAt(long tick)
    {
        lock (_repositoryGate)
        {
            var record = repository.LoadSnapshotAt(branch, tick);
            if (record is null) return null;
            var world = WorldSnapshot.Deserialize(record.Json);
            if (coldTier is not null)
                world.ColdArchive.RestoreNpcSummaries(coldTier.Load(world.Calendar));
            return world;
        }
    }
}
