namespace LivingWorld.Infrastructure;

/// <summary>Linha de persistência de um template de período (Fase 13, T4): payload JSON
/// canônico versionado (design.md). Chave composta (<see cref="PeriodId"/>, <see cref="Version"/>)
/// — mesmo padrão de chave composta de <see cref="WorldSnapshotRecord"/> — nunca sobrescreve uma
/// versão já registrada (PERIOD-07..10: conflito de versão é rejeitado, nunca silenciosamente
/// substituído).</summary>
public sealed class PeriodTemplateRecord
{
    public required string PeriodId { get; set; }
    public int Version { get; set; }
    public required string PayloadJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required string Source { get; set; }
}
