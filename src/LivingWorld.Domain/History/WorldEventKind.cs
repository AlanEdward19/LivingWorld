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

    /// <summary>Fundação de assentamento (Fase 15.1, LWV-04.6) — cidade nova a partir da mãe.</summary>
    SettlementFounded,

    /// <summary>Uma cidade-filha adjacente foi absorvida pela cidade-mãe (FixT18).</summary>
    CityMerged,

    /// <summary>Tentativa de uso extraordinário; inicia uma cadeia pelo invocation id.</summary>
    ExtraordinaryUseAttempted,

    /// <summary>Custo efetivamente debitado por um uso extraordinário.</summary>
    ExtraordinaryCostPaid,

    /// <summary>Efeito extraordinário aplicado a um alvo declarado.</summary>
    ExtraordinaryEffectApplied,

    /// <summary>Uso extraordinário recusado ou resolvido como falha.</summary>
    ExtraordinaryUseFailed,

    /// <summary>Uma regra autorada concedeu uma potência a um NPC.</summary>
    ExtraordinaryAcquired,

    /// <summary>Uma aquisição foi recusada porque o gatilho não casava com o cenário.</summary>
    ExtraordinaryAcquisitionFailed,

    /// <summary>Condição autorada tornou uma manifestação ativa.</summary>
    ExtraordinaryManifested,

    /// <summary>Condição autorada deixou de valer e a manifestação ficou dormente.</summary>
    ExtraordinaryDormant,

    /// <summary>Uma cultura interpretou a manifestação segundo regra própria do cenário.</summary>
    ExtraordinaryCulturalReaction,

    /// <summary>Um uso extraordinário materializou ocupação temporária no mapa.</summary>
    ExtraordinaryConstructCreated,

    /// <summary>Um construto perdeu durabilidade sem criar ou destruir recursos econômicos.</summary>
    ExtraordinaryConstructDamaged,

    /// <summary>Um construto deixou o mundo por dano ou expiração.</summary>
    ExtraordinaryConstructRemoved,

    /// <summary>Uma intervenção explícita removeu uma potência de um portador.</summary>
    ExtraordinaryRevoked,

    /// <summary>Uma intervenção explícita alterou personalidade, relação ou ação.</summary>
    AuthoringCommandApplied,

    /// <summary>Uma intervenção explícita foi recusada sem mutar o estado canônico.</summary>
    AuthoringCommandRejected,

    /// <summary>Consequência declarada de uma resolução extraordinária não plena.</summary>
    ExtraordinaryFailureApplied,

    /// <summary>Confronto NPC-vs-NPC resolvido por poder (Fase 16.1, PWR-63).</summary>
    CombatResolved,

    /// <summary>Instanciação de NPC via poder (clone/split/reincarnate, PWR-107).</summary>
    NpcInstantiated,

    /// <summary>Troca de identidade observável (possessão/body-swap, PWR-108).</summary>
    IdentityChanged,

    /// <summary>Herança de poder no nascimento (Fase 16.2, EVO-10) — payload:
    /// childId|parentAId|parentBId|outcome|descriptorIdsCsv</summary>
    PowerInherited,

    /// <summary>Agent escolheu/executou UsePower via utility AI (Fase 16.3 P1d, COH-33) —
    /// payload: npcId|powerId|mechanicToken. CauseEventId aponta ao evento de decisão.</summary>
    PowerInvoked,

    /// <summary>Planta atingiu estágio de maturidade/produção (Fase 16.4, REALISM-07) —
    /// payload: plantId.</summary>
    PlantMatured,
}
