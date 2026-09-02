using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Infrastructure;

/// <summary>Implementação EF Core de <see cref="IWorldRepository"/> (task 8/9). Nenhum recurso
/// exclusivo de SQLite entra no mapeamento (ADR-0002) — trocar de provider é trocar a
/// connection string do <see cref="WorldDbContext"/>.</summary>
public sealed class SqliteWorldRepository(WorldDbContext context) : IWorldRepository
{
    public void SaveSnapshotWithEvents(
        BranchId branch, long tick, string json, string canonicalHash, string volatileHash,
        IReadOnlyList<WorldEvent> events)
    {
        // Tick zero representa a criação de um novo mundo no slot único. Snapshots mais altos
        // pertencem ao mundo anterior e, se sobreviverem, LoadLatest() os escolherá no próximo
        // boot em vez do mundo recém-criado.
        if (tick == 0)
        {
            context.Snapshots.RemoveRange(
                context.Snapshots.Where(s => s.BranchId == branch.Value && s.Tick > 0));
            context.EventLog.RemoveRange(
                context.EventLog.Where(e => e.BranchId == branch.Value));
        }

        // Upsert por (BranchId, Tick): `worldRepository` é um único DbContext de vida longa
        // (Program.cs), então re-salvar o mesmo tick — ex.: tick 0 de um mundo novo criado via
        // POST /worlds/create, mesma chave do snapshot inicial de bootstrap — colide na
        // identity map do EF antes mesmo de chegar no banco se sempre inserirmos. `Find` olha o
        // set rastreado em memória e o banco, então cobre os dois casos.
        var existing = context.Snapshots.Find(branch.Value, tick);
        if (existing is not null)
        {
            existing.Json = json;
            existing.CanonicalHash = canonicalHash;
            existing.VolatileHash = volatileHash;
        }
        else
        {
            context.Snapshots.Add(new WorldSnapshotRecord
            {
                BranchId = branch.Value,
                Tick = tick,
                Json = json,
                CanonicalHash = canonicalHash,
                VolatileHash = volatileHash,
            });
        }

        var kindPool = new StringInternPool();
        EventLogKindEncoding.SeedPool(
            kindPool,
            context.EventLog
                .Where(l => l.BranchId == branch.Value)
                .OrderBy(l => l.Tick)
                .ThenBy(l => l.Sequence)
                .Select(l => l.Kind));

        var sequenceByTick = new Dictionary<long, int>();
        foreach (var evt in events)
        {
            int sequence = sequenceByTick.GetValueOrDefault(evt.Tick);
            sequenceByTick[evt.Tick] = sequence + 1;

            context.EventLog.Add(new EventLogRecord
            {
                BranchId = branch.Value,
                Tick = evt.Tick,
                Sequence = sequence,
                Kind = EventLogKindEncoding.Encode(evt.Kind.ToString(), kindPool),
                Payload = evt.Payload,
                EventId = evt.EventId,
                CauseEventId = evt.CauseEventId,
                SourceSystem = evt.SourceSystem,
            });
        }

        // Uma chamada a SaveChanges = uma transação (rules/database-entities.md).
        context.SaveChanges();
    }

    public WorldSnapshotRecord? LoadLatestSnapshot(BranchId branch) =>
        context.Snapshots
            .Where(s => s.BranchId == branch.Value)
            .OrderByDescending(s => s.Tick)
            .FirstOrDefault();

    public WorldSnapshotRecord? LoadSnapshotAt(BranchId branch, long tick) =>
        context.Snapshots.SingleOrDefault(s => s.BranchId == branch.Value && s.Tick == tick);

    public IReadOnlyList<EventLogRecord> LoadEvents(BranchId branch)
    {
        var kindPool = new StringInternPool();
        var rows = context.EventLog
            .Where(l => l.BranchId == branch.Value)
            .OrderBy(l => l.Tick)
            .ThenBy(l => l.Sequence)
            .ToList();

        var decoded = new List<EventLogRecord>(rows.Count);
        foreach (var row in rows)
        {
            decoded.Add(new EventLogRecord
            {
                BranchId = row.BranchId,
                Tick = row.Tick,
                Sequence = row.Sequence,
                Kind = EventLogKindEncoding.Decode(row.Kind, kindPool),
                Payload = row.Payload,
                EventId = row.EventId,
                CauseEventId = row.CauseEventId,
                SourceSystem = row.SourceSystem,
            });
        }

        return decoded;
    }
}
