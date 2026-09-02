namespace LivingWorld.Infrastructure.Records;

/// <summary>Linha append-only da tabela de fatos (Fase 10, HIST-02) — imutável; corrigir
/// história é evento compensatório novo, nunca <c>UPDATE</c>.</summary>
public sealed class FactLogRecord
{
    public long BranchId { get; set; }
    public long FactId { get; set; }
    public long Tick { get; set; }
    public required string Kind { get; set; }
    public required string Participants { get; set; }
    public string? LocationCityId { get; set; }
    public double Significance { get; set; }
    public required string Payload { get; set; }
}
