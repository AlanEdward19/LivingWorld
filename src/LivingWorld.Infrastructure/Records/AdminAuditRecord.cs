namespace LivingWorld.Infrastructure.Records;

/// <summary>Linha da auditoria de admin (ADR-0015): imutável, fora do hash canônico, mesmo append-only do log de fatos
/// (gatilhos de SQLite rejeitam <c>UPDATE</c> e <c>DELETE</c>). <see cref="Sequence"/> é a ordem de gravação por branch,
/// atribuída pelo repositório — nunca autoincremento do banco.</summary>
public sealed class AdminAuditRecord
{
    public long BranchId { get; set; }
    public long Sequence { get; set; }
    public long Tick { get; set; }
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Target { get; set; }
    public required string Detail { get; set; }
}
