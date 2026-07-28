namespace LivingWorld.Simulation;

/// <summary>Esqueleto imutável do log Tier A (ADR-0006/rules/database-entities.md): só o que
/// vira história consultável. Fase 3 cobre nascimento e morte; guerra/fundação/invenção chegam
/// com as fases que as introduzem.</summary>
public enum WorldEventKind
{
    Birth,
    Death,

    /// <summary>Morte por fome sustentada (Fase 4, task 10/NEEDS-03) — valor de enum próprio em
    /// vez de reusar <see cref="Death"/> com causa no payload: mesmo padrão de "kind carrega o
    /// que aconteceu, payload carrega quem" já usado por Birth/Death.</summary>
    Starvation,

    // Fase 5 (Economia) — ver ECON-02/18/19/22/26 em .specs/features/phase-05-economy/spec.md.
    Hired,
    Fired,
    WageUnpaid,
    ResourceLost,
    Minted,
    Destroyed,

    /// <summary>Casamento (Fase 7, T15) — payload <c>spouseAId|spouseBId</c>.</summary>
    Marriage,

    /// <summary>Cortejo iniciado (Fase 7, T16) — payload <c>npcAId|npcBId</c> (IDs ordenados).</summary>
    CourtshipStarted,

    /// <summary>Cortejo rejeitado (Fase 7, T16) — payload
    /// <c>motivo|seekerId|candidateId</c> (<see cref="CourtshipRejectionReason"/>).</summary>
    CourtshipRejected,

    /// <summary>Cortejo concluído (Fase 7, T16, FAM-11) — payload <c>npcAId|npcBId</c>, emitido
    /// antes de <see cref="Marriage"/>.</summary>
    CourtshipSucceeded,

    /// <summary>Morte materna no parto (Fase 7, T17) — payload <c>motherId</c>.</summary>
    MaternalDeath,

    /// <summary>Nascimento morto / sem filho vivo (Fase 7, T17) — payload
    /// <c>motherId|fatherId</c>.</summary>
    StillBirth,
}

/// <summary>Um evento de história, carimbado com o tick em que ocorreu. Sem <c>BranchId</c> —
/// quem persiste (Infrastructure) atribui o branch explicitamente, nunca implícito
/// (ADR-0009).</summary>
public sealed record WorldEvent(long Tick, WorldEventKind Kind, string Payload);

/// <summary>Recebe eventos de história emitidos durante o tick. Nulo por padrão — sistemas não
/// exigem log para rodar; só quem persiste (Fase 3, task 10) fornece um.</summary>
public interface IWorldEventSink
{
    void Record(WorldEvent evt);
}
