using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Infrastructure.Repositories;

/// <summary>Persistência de snapshot + event log. <see cref="BranchId"/> é parâmetro
/// explícito de todo método — nunca implícito nem ambiente. Um teste de arquitetura
/// (LivingWorld.Tests) reprova qualquer método público desta interface sem esse parâmetro.</summary>
public interface IWorldRepository
{
    /// <summary>Salva o snapshot e os eventos de história acumulados desde o último, numa única
    /// transação (rules/database-entities.md: "escrita em lote, dentro de uma transação por tick
    /// persistido"). Com <paramref name="binary"/> o snapshot vai no bloco posicional (e <paramref name="json"/>
    /// pode ficar vazio); <paramref name="baseTick"/> marca o bloco como diff daquela base.</summary>
    void SaveSnapshotWithEvents(
        BranchId branch, long tick, string json, string canonicalHash, string volatileHash,
        IReadOnlyList<WorldEvent> events, byte[]? binary = null, long? baseTick = null);

    WorldSnapshotRecord? LoadLatestSnapshot(BranchId branch);

    WorldSnapshotRecord? LoadSnapshotAt(BranchId branch, long tick);

    IReadOnlyList<EventLogRecord> LoadEvents(BranchId branch);
}
