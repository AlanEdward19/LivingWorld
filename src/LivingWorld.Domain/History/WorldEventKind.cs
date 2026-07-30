namespace LivingWorld.Domain;

/// <summary>O que aconteceu no esqueleto imutável do log Tier A (ADR-0006). Vive em Domain para
/// que <see cref="Fact"/> não dependa de Simulation.</summary>
public enum WorldEventKind
{
    Birth,
    Death,

    /// <summary>Morte por fome sustentada (Fase 4) — kind próprio, não <see cref="Death"/> com
    /// causa no payload.</summary>
    Starvation,

    // Fase 5 (Economia)
    Hired,
    Fired,
    WageUnpaid,
    ResourceLost,
    Minted,
    Destroyed,

    /// <summary>Casamento (Fase 7) — payload <c>spouseAId|spouseBId</c>.</summary>
    Marriage,

    /// <summary>Cortejo iniciado (Fase 7) — payload <c>npcAId|npcBId</c>.</summary>
    CourtshipStarted,

    /// <summary>Cortejo rejeitado (Fase 7).</summary>
    CourtshipRejected,

    /// <summary>Cortejo concluído (Fase 7) — emitido antes de <see cref="Marriage"/>.</summary>
    CourtshipSucceeded,

    /// <summary>Morte materna no parto (Fase 7).</summary>
    MaternalDeath,

    /// <summary>Nascimento morto (Fase 7).</summary>
    StillBirth,

    /// <summary>Fato registrado no esqueleto (Fase 10, HIST-01) — meta-evento quando o
    /// <see cref="Fact"/> entra na coleção canônica.</summary>
    FactRecorded,

    /// <summary>Fato convertido em relato hop-0 (Fase 10, HIST-03) — payload
    /// <c>reportId|factId|communityId</c>.</summary>
    ReportConverted,

    /// <summary>Livro perdido (Fase 10, HIST-09) — payload <c>bookId|tick</c>.</summary>
    BookLost,

    /// <summary>Livro perdido redescoberto por evento agendado (Fase 10, HIST-09) — payload
    /// <c>bookId|tick</c>.</summary>
    BookRediscovered,

    /// <summary>Correção compensatória do esqueleto (Fase 10, HIST-24) — payload
    /// <c>correctsFactId|correctedPayload|reason</c>.</summary>
    CompensatingCorrection,
}
