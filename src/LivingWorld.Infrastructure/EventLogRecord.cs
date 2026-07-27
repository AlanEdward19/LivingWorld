namespace LivingWorld.Infrastructure;

/// <summary>Linha do event log Tier A (ADR-0006, task 8): imutável — corrigir a história é
/// escrever outro evento, nunca <c>UPDATE</c> (rules/database-entities.md). <see cref="Id"/> é
/// o mesmo id monotônico do <c>ScheduledEvent</c>/log de origem, único por branch.</summary>
public sealed class EventLogRecord
{
    public long BranchId { get; set; }
    public long Tick { get; set; }

    /// <summary>Desempate determinístico entre eventos do mesmo tick (posição de emissão),
    /// atribuído pelo repositório — nunca autoincremento do banco (ADR-0002: nada exclusivo do
    /// SQLite entra no esquema).</summary>
    public int Sequence { get; set; }

    public required string Kind { get; set; }
    public required string Payload { get; set; }
}
