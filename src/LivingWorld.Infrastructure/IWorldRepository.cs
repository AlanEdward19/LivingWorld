using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Infrastructure;

/// <summary>Persistência de snapshot + event log (ADR-0006). <see cref="BranchId"/> é parâmetro
/// explícito de todo método (ADR-0009) — nunca implícito nem ambiente. Um teste de arquitetura
/// (LivingWorld.Tests) reprova qualquer método público desta interface sem esse parâmetro.</summary>
public interface IWorldRepository
{
    /// <summary>Salva o snapshot e os eventos de história acumulados desde o último, numa única
    /// transação (rules/database-entities.md: "escrita em lote, dentro de uma transação por tick
    /// persistido").</summary>
    void SaveSnapshotWithEvents(
        BranchId branch, long tick, string json, string canonicalHash, string volatileHash,
        IReadOnlyList<WorldEvent> events);

    WorldSnapshotRecord? LoadLatestSnapshot(BranchId branch);

    WorldSnapshotRecord? LoadSnapshotAt(BranchId branch, long tick);

    IReadOnlyList<EventLogRecord> LoadEvents(BranchId branch);
}
