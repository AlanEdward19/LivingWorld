namespace LivingWorld.Infrastructure.Records;

/// <summary>Linha do event log Tier A (ADR-0006, task 8): imutável — corrigir a história é
/// escrever outro evento, nunca <c>UPDATE</c> (rules/database-entities.md). <see cref="Id"/> é
/// o mesmo id monotônico do <c>ScheduledEvent</c>/log de origem, único por branch.
/// <see cref="EventId"/>/<see cref="CauseEventId"/>/<see cref="SourceSystem"/> são nullable
/// aditivos (COH-04) — leitores antigos ignoram as colunas novas.</summary>
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

    /// <summary>Id monotônico de proveniência causal (<c>WorldEvent.EventId</c>) — nullable
    /// para linhas pré-COH-04.</summary>
    public long? EventId { get; set; }

    public long? CauseEventId { get; set; }

    public string? SourceSystem { get; set; }
}
