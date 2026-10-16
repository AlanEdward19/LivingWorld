using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Infrastructure;

/// <summary>Orquestra tick + persistência: a simulação roda inteiramente em memória e
/// o repositório só é chamado nas fronteiras de snapshot — nunca dentro do laço de tick.</summary>
public sealed class PersistentWorldRunner(
    IWorldRepository repository, BranchId branch, long snapshotIntervalTicks, ColdTierPersistence? coldTier = null)
{
    private const int DiffsPerBase = 12;
    private readonly object _repositoryGate = new();
    private SnapshotBase? _base;
    private int _diffsSinceBase;

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
            var events = EventLogCompactor.Compact(sink.DrainAll(), world.Calendar);
            long tick = world.CurrentDate.TotalHours;
            byte[] block;
            long? baseTick = null;
            // base nova: mundo novo ou diferente, tick zero, ou 12 diffs depois da última; senão, diff sobre a base
            if (tick == 0 || _base is null || !_base.Fits(world) || _diffsSinceBase >= DiffsPerBase)
            {
                block = PositionalSnapshot.EncodeFull(world, out var fresh);
                _diffsSinceBase = 0;
                _base = fresh;
            }
            else
            {
                block = PositionalSnapshot.EncodeDelta(world, _base);
                baseTick = _base.Tick;
                _diffsSinceBase++;
            }

            try
            {
                repository.SaveSnapshotWithEvents(
                    branch, tick, string.Empty, WorldSnapshot.CanonicalHash(world), WorldSnapshot.VolatileHash(world),
                    events, block, baseTick);
            }
            catch
            {
                _base = null; // a base pode não ter chegado ao banco: a próxima gravação recomeça completa
                throw;
            }

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
            var world = Rehydrate(record);
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
            var world = Rehydrate(record);
            if (coldTier is not null)
                world.ColdArchive.RestoreNpcSummaries(coldTier.Load(world.Calendar));
            return world;
        }
    }

    /// <summary>Linha antiga (só JSON) pelo leitor JSON; linha nova pelo bloco binário, com a base lida do banco quando
    /// é um diff. A cadeia continua da base reconstruída; JSON antigo recomeça com uma base completa.</summary>
    private WorldState Rehydrate(WorldSnapshotRecord record)
    {
        _diffsSinceBase = 0;
        if (record.Binary is null)
        {
            _base = null;
            return WorldSnapshot.Deserialize(record.Json);
        }

        var world = PositionalSnapshot.Decode(
            record.Binary, record.Tick, record.BaseTick,
            baseTick => repository.LoadSnapshotAt(branch, baseTick)?.Binary, out var restoredBase);

        // o bloco reconstruído tem de dar o hash canônico gravado com ele: base sobrescrita ou corrompida não passa calada
        if (WorldSnapshot.CanonicalHash(world) != record.CanonicalHash)
            throw new InvalidDataException($"snapshot do tick {record.Tick} não reconstrói o hash canônico gravado");

        // base nova na próxima gravação quando o mundo veio de um diff: a cadeia não cresce sem fim a cada reabertura
        _base = record.BaseTick is null ? restoredBase : null;
        return world;
    }
}
