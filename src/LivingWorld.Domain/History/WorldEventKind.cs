namespace LivingWorld.Domain.History;

/// <summary>O que aconteceu no esqueleto imutável do log Tier A. Vive em Domain para
/// que <see cref="Fact"/> não dependa de Simulation.</summary>
public enum WorldEventKind
{
    Birth,
    Death,

    /// <summary>Morte por fome sustentada — kind próprio, não <see cref="Death"/> com
    /// causa no payload.</summary>
    Starvation,

    Hired,
    Fired,
    WageUnpaid,
    ResourceLost,
    Minted,
    Destroyed,

    /// <summary>Casamento — payload <c>spouseAId|spouseBId</c>.</summary>
    Marriage,

    /// <summary>Cortejo iniciado — payload <c>npcAId|npcBId</c>.</summary>
    CourtshipStarted,

    CourtshipRejected,

    /// <summary>Cortejo concluído — emitido antes de <see cref="Marriage"/>.</summary>
    CourtshipSucceeded,

    MaternalDeath,

    StillBirth,

    /// <summary>Fato registrado no esqueleto — meta-evento quando o
    /// <see cref="Fact"/> entra na coleção canônica.</summary>
    FactRecorded,

    /// <summary>Fato convertido em relato hop-0 — payload
    /// <c>reportId|factId|communityId</c>.</summary>
    ReportConverted,

    /// <summary>Livro perdido — payload <c>bookId|tick</c>.</summary>
    BookLost,

    /// <summary>Livro perdido redescoberto por evento agendado — payload
    /// <c>bookId|tick</c>.</summary>
    BookRediscovered,

    /// <summary>Correção compensatória do esqueleto — payload
    /// <c>correctsFactId|correctedPayload|reason</c>.</summary>
    CompensatingCorrection,

    /// <summary>Fundação de assentamento — cidade nova a partir da mãe.</summary>
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

    /// <summary>Confronto NPC-vs-NPC resolvido por poder ou encontro
    /// multi-round encerrado.</summary>
    CombatResolved,

    /// <summary>Encontro multi-round iniciado via <c>combat.engage:</c>
    /// — payload: encounterId|attackerId|defenderId.</summary>
    CombatEncounterStarted,

    /// <summary>Um round de encontro processado — payload:
    /// encounterId|round|attackerHp|defenderHp.</summary>
    CombatRound,

    /// <summary>Instanciação de NPC via poder (clone/split/reincarnate).</summary>
    NpcInstantiated,

    /// <summary>Troca de identidade observável (possessão/body-swap).</summary>
    IdentityChanged,

    /// <summary>Herança de poder no nascimento — payload:
    /// childId|parentAId|parentBId|outcome|descriptorIdsCsv</summary>
    PowerInherited,

    /// <summary>Agent escolheu/executou UsePower via utility AI —
    /// payload: npcId|powerId|mechanicToken. CauseEventId aponta ao evento de decisão.</summary>
    PowerInvoked,

    /// <summary>Planta atingiu estágio de maturidade/produção —
    /// payload: plantId.</summary>
    PlantMatured,

    /// <summary>Hospedeiro retomou controle de possessão —
    /// payload: <c>hostId|possessorId|possession-resisted</c>.</summary>
    PossessionResisted,

    /// <summary>Ajuda mútua: alguém deu dinheiro a uma casa em necessidade — payload
    /// <c>ajudanteId|receptorId|valor</c>.</summary>
    AidGiven,

    /// <summary>Morte por sede sustentada — payload: npcId (mesmo molde de <see cref="Starvation"/>).</summary>
    Dehydration,

    /// <summary>Morte por privação de sono sustentada — payload: npcId.</summary>
    Exhaustion,

    /// <summary>Primeiro contato de compra entre comprador e quem atende a oficina — payload:
    /// <c>compradorId|atendenteId|oficinaId|recursoId|valor</c>.</summary>
    Trade,

    /// <summary>Espólio de um NPC morto repartido entre os herdeiros — payload:
    /// <c>mortoId|herdeiros(ids por vírgula, vazio = tesouro da cidade)|moeda|unidadesDeEstoque|oficinasTransferidas</c>.</summary>
    EstateSettled,

    /// <summary>Morte por doença — payload: <c>npcId|doençaId</c>.</summary>
    Disease,

    /// <summary>Um surto começou numa cidade — payload: <c>doençaId|pacienteZeroId|cidadeId</c>.</summary>
    OutbreakStarted,

    /// <summary>Furto descoberto (pego em flagrante ou denunciado) — payload: <c>ladraoId|vitimaId|moeda|1 se levou, 0 se falhou</c>.</summary>
    TheftDiscovered,

    /// <summary>Um jogador assumiu um NPC — payload: <c>npcId|jogadorId</c>.</summary>
    PlayerIncarnated,

    /// <summary>O jogador saiu e o NPC desapareceu — payload: <c>npcId|jogadorId|cidadeId|vinculosProximos</c>.</summary>
    PlayerDisappeared,

    /// <summary>Um vínculo próximo reencontrou o desaparecido — payload: <c>npcId|buscadorId|dias</c>.</summary>
    DisappearedFound,

    /// <summary>A procura acabou sem achar e quem ficou segue em frente — payload: <c>npcId|vinculosProximos|dias</c>.</summary>
    SearchGaveUp,

    /// <summary>Aluguel de hospedagem não pago no mês — payload: <c>domicilioId|predioId|valorDevido|mesesDeAtraso</c>.</summary>
    RentUnpaid,

    /// <summary>Domicílio despejado por atraso de aluguel — payload: <c>domicilioId|predioId</c>.</summary>
    Evicted,

    /// <summary>Uma cultura concluiu um protótipo — payload: <c>cidadeId|culturaId|vetor|vetorDaTecnologiaDeQueÉVariação (vazio se nova)</c>.</summary>
    TechnologyDiscovered,

    /// <summary>Uma cidade passou a usar uma tecnologia da sua cultura — payload: <c>cidadeId|culturaId|vetor</c>.</summary>
    TechnologyAdopted,
}
