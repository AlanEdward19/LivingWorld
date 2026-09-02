using LivingWorld.Domain;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure;

/// <summary>Orquestra tick + persistência (task 10): a simulação roda inteiramente em memória e
/// o repositório só é chamado nas fronteiras de snapshot — nunca dentro do laço de tick (task
/// 11, ADR-0006).</summary>
public sealed class PersistentWorldRunner(IWorldRepository repository, BranchId branch, long snapshotIntervalTicks)
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

    /// <summary>Salva o estado atual e drena o buffer de eventos — uma transação (task 8/10).</summary>
    public void Snapshot(WorldState world, BufferingWorldEventSink sink)
    {
        lock (_repositoryGate)
        {
            var events = sink.DrainAll();
            repository.SaveSnapshotWithEvents(
                branch, world.CurrentDate.TotalHours,
                WorldSnapshot.Serialize(world), WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world),
                events);
        }
    }

    /// <summary>Reidrata o mundo do snapshot mais recente do branch, ou <c>null</c> se nunca
    /// houve um salvo (mundo novo).</summary>
    public WorldState? LoadLatest()
    {
        lock (_repositoryGate)
        {
            var record = repository.LoadLatestSnapshot(branch);
            return record is null ? null : WorldSnapshot.Deserialize(record.Json);
        }
    }

    /// <summary>Reidrata o mundo de um tick específico já salvo (idempotência de replay, task 10).</summary>
    public WorldState? LoadAt(long tick)
    {
        lock (_repositoryGate)
        {
            var record = repository.LoadSnapshotAt(branch, tick);
            return record is null ? null : WorldSnapshot.Deserialize(record.Json);
        }
    }
}
