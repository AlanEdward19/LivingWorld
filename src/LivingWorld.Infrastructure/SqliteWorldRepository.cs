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
        context.Snapshots.Add(new WorldSnapshotRecord
        {
            BranchId = branch.Value,
            Tick = tick,
            Json = json,
            CanonicalHash = canonicalHash,
            VolatileHash = volatileHash,
        });

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
                Kind = evt.Kind.ToString(),
                Payload = evt.Payload,
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

    public IReadOnlyList<EventLogRecord> LoadEvents(BranchId branch) =>
        context.EventLog
            .Where(l => l.BranchId == branch.Value)
            .OrderBy(l => l.Tick)
            .ThenBy(l => l.Sequence)
            .ToList();
}
