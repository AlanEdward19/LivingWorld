namespace LivingWorld.Infrastructure.Records;

/// <summary>Linha de persistência do snapshot: serialização completa do
/// mundo (<c>WorldSnapshot.Serialize</c>) num ponto de retomada. Chave composta
/// (<see cref="BranchId"/>, <see cref="Tick"/>) desde a primeira migração — não vaza
/// para a borda, é mapeamento puro de Infrastructure (rules/database-entities.md).</summary>
public sealed class WorldSnapshotRecord
{
    public long BranchId { get; set; }
    public long Tick { get; set; }
    public required string Json { get; set; }
    public required string CanonicalHash { get; set; }
    public required string VolatileHash { get; set; }

    /// <summary>Bloco binário posicional (<c>PositionalSnapshot</c>); nulo nas linhas antigas, que só têm
    /// <see cref="Json"/>. Nas linhas novas <see cref="Json"/> fica vazio.</summary>
    public byte[]? Binary { get; set; }

    /// <summary>Tick da linha-base completa sobre a qual este bloco é um diff; nulo quando o bloco é completo.</summary>
    public long? BaseTick { get; set; }
}
