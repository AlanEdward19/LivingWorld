namespace LivingWorld.Infrastructure;

/// <summary>Linha de persistência do snapshot (ADR-0006, task 8): serialização completa do
/// mundo (<c>WorldSnapshot.Serialize</c>) num ponto de retomada. Chave composta
/// (<see cref="BranchId"/>, <see cref="Tick"/>) desde a primeira migração (ADR-0009) — não vaza
/// para a borda, é mapeamento puro de Infrastructure (rules/database-entities.md).</summary>
public sealed class WorldSnapshotRecord
{
    public long BranchId { get; set; }
    public long Tick { get; set; }
    public required string Json { get; set; }
    public required string CanonicalHash { get; set; }
    public required string VolatileHash { get; set; }
}
