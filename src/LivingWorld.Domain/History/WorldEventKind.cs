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

    /// <summary>Um marco de desenvolvimento regrediu por doença — payload: <c>npcId|eixo|antes|depois|doençaId</c>.</summary>
    MilestoneRegressed,

    /// <summary>Uma criança sem adulto no domicílio foi acolhida — payload: <c>criancaId|acolhedorId|domicilioId|vinculo</c> (parente, afeto ou comunidade).</summary>
    OrphanTakenIn,

    /// <summary>Uma família autorada chegou ao mundo em andamento (#46 fatia 4) — payload: <c>domicilioId|cidadeId|npcIds (separados por vírgula)</c>.</summary>
    AuthoredImmigration,

    /// <summary>Um local de trabalho autorado foi fundado no mundo em andamento — payload: <c>localId|cidadeId|tipo|x|y</c>.</summary>
    AuthoredWorkplaceFounded,

    /// <summary>Uma união acabou em vida (AD-136) — payload: <c>quemSaiuId|quemFicouId|modo|domicilioNovoId</c> (modo: separacao,
    /// abandono, anulacao, divorcio).</summary>
    UnionEnded,

    /// <summary>Um casado começou um caso (AD-136) — payload: <c>infielId|amanteId</c>.</summary>
    AffairStarted,

    /// <summary>O traído soube do caso (AD-136) — payload: <c>traidoId|infielId|amanteId|desfecho</c> (perdao, separacao, violencia).</summary>
    AffairDiscovered,

    /// <summary>Um jovem escolheu o ofício (AD-136) — payload: <c>npcId|profissao|profissaoDaCasa|pressionado(0/1)|pressao</c>.</summary>
    VocationChosen,

    /// <summary>Um protótipo funcionou (AD-141) — payload: <c>npcId|designId|origem|designPai|segundoPai</c> (origem: 0 inventou,
    /// 1 aprendeu, 2 copiou, 3 engenharia reversa).</summary>
    DesignPrototyped,

    /// <summary>Um protótipo ou uma construção falhou (AD-141) — payload: <c>npcId|modo|construção(0/1)</c> (modo: 1 inválido,
    /// 2 afundou, 3 virou, 4 quebrou, 5 não andou).</summary>
    PrototypeFailed,

    /// <summary>Alguém aprendeu um design com quem sabia (AD-141) — payload: <c>aprendizId|mestreId|designId|origem|fidelidade‰</c>.</summary>
    DesignLearned,

    /// <summary>Morte por acidente (queda, afogamento, fogo, coice), nomeada na sobra da tábua (AD-137) — payload: <c>npcId</c>.</summary>
    Accident,

    /// <summary>Morte por velhice, nomeada na sobra da tábua (AD-137) — payload: <c>npcId</c>.</summary>
    OldAge,

    /// <summary>Um surto importado ficou sem caso (AD-137) — payload: <c>doençaId|cidadeId|tickDoInício</c>.</summary>
    OutbreakEnded,

    /// <summary>A gravidez de uma mulher sem união apareceu (AD-139) — payload: <c>maeId|paiId|desfecho</c> (casamento: o pai
    /// foi levado a casar; abandono: ele não casou; sem_pai: ele morreu).</summary>
    UnwedPregnancy,

    /// <summary>Alguém comprou no mercado uma peça feita por outro (AD-143) — payload: <c>compradorId|vendedorId|designId|preço</c>.</summary>
    DesignSold,

    /// <summary>Um adulto sem trabalho nem vaga foi embora (AD-145) — payload: <c>npcId|destino</c> (destino: id da cidade do
    /// mundo para onde foi, ou <c>fora</c> quando saiu do mundo simulado).</summary>
    Emigrated,

    /// <summary>Lucro retirado do caixa acima da reserva de giro (AD-144) — payload: <c>oficinaId|donoId|valor</c>, ou
    /// <c>oficinaId|city-cidadeId|valor</c> quando a oficina não tem dono e o excedente vai ao tesouro da cidade.</summary>
    ProfitDrawn,

    /// <summary>Um NPC abriu o próprio negócio (AD-152) — payload: <c>oficinaId|fundadorId|origem|capital|doCasal|herdado|sócios</c>;
    /// origem é <c>poupanca</c>, <c>heranca</c> ou <c>sociedade</c>; sócios é <c>id:valor,...</c> ou <c>-</c>; um 8º campo <c>reaberta</c>
    /// marca o dono que reabriu a oficina parada dele.</summary>
    VentureFounded,

    /// <summary>Um furto aconteceu, visto ou não (AD-150) — payload: <c>autorId|vitimaId|alvo|motivo|bem|quantidade|valor|desfecho|testemunhas|casoId</c>
    /// (alvo: pessoa, casa, banca, caixa; motivo: fome, miseria, oportunidade; desfecho: levou, falhou; vítima -1 = da cidade).</summary>
    TheftCommitted,

    /// <summary>Uma agressão (AD-150) — payload: <c>agressorId|vitimaId|motivo|dano|desfecho|testemunhas|casoId</c> (motivo: ofensa,
    /// ciume, traicao, heranca, denuncia, vinganca, flagrante; desfecho: ferido, morto).</summary>
    Assault,

    /// <summary>Morte por agressão (AD-150), pelo mesmo caminho das outras mortes — payload: <c>npcId</c>.</summary>
    Homicide,

    /// <summary>Alguém denunciou (AD-150) — payload: <c>denuncianteId|acusadoId|casoId|crime|culpadoDeVerdade(1/0)</c>.</summary>
    CrimeReported,

    /// <summary>Julgamento (AD-150) — payload: <c>acusadoId|vitimaId|casoId|crime|veredito|punicao|valor|culpadoDeVerdade(1/0)</c>
    /// (veredito: culpado, inocentado; punição: multa, castigo, banimento, forca, nenhuma).</summary>
    Verdict,

    /// <summary>Morte na forca por sentença (AD-150) — payload: <c>npcId</c>.</summary>
    Executed,

    /// <summary>Saiu do mundo pela justiça (AD-150) — payload: <c>npcId|casoId|crime|modo</c> (modo: sentenca, fuga).</summary>
    Banished,

    /// <summary>Herdeiros brigaram pela partilha (AD-150) — payload: <c>herdeiroId|outroHerdeiroId|mortoId|valor</c>.</summary>
    InheritanceDisputed,

    /// <summary>Agressão dentro de casa (AD-154) — payload: <c>agressorId|vitimaId|vinculo|motivo|dano|desfecho|testemunhas|casoId</c>
    /// (vínculo: conjuge, filho, pai, outro; motivo: temperamento, fome, miseria, desemprego, ciume; desfecho: ferido, morto).</summary>
    DomesticViolence,

    /// <summary>Sequestro (AD-154) — payload: <c>autorId|vitimaId|motivo|categoria|resgate|testemunhas|casoId</c> (motivo: resgate,
    /// vinganca, posse; categoria: adulto, crianca; resgate: moeda pedida, 0 sem pedido).</summary>
    Kidnapped,

    /// <summary>Fim do cativeiro (AD-154) — payload: <c>vitimaId|autorId|desfecho|dias|valorPago|casoId</c> (desfecho: resgate, soltura,
    /// fuga, achada, morte, autor_saiu).</summary>
    KidnappingEnded,

    /// <summary>Violência sexual (AD-154), evento abstrato do motor, sem detalhe do ato — payload:
    /// <c>autorId|vitimaId|categoria|testemunhas|casoId|concepcao</c> (categoria: adulto, crianca; concepção: 1/0).</summary>
    SexualAssault,

    /// <summary>Empréstimo concedido em moeda que existe (AD-155) — payload:
    /// <c>emprestimoId|credor|tipoCredor|tomadorId|finalidade|principal|devido|parcela|prazoMeses|oficinaGarantia</c> (credor: <c>npc-ID</c> ou
    /// <c>city-ID</c>; tipo: parente, conhecido, rico, cidade; finalidade: abertura, giro, reabertura; oficina 0 = sem garantia).</summary>
    LoanGranted,

    /// <summary>Empréstimo quitado (AD-155) — payload: <c>emprestimoId|tomadorId|credor|pagoNoTotal</c>.</summary>
    LoanRepaid,

    /// <summary>Calote (AD-155) — payload: <c>emprestimoId|tomadorId|credor|saldoPerdido|penhorado|garantia</c> (garantia: oficina,
    /// penhora).</summary>
    LoanDefaulted,

    /// <summary>Oficina fechada por falência (AD-155) — payload:
    /// <c>oficinaId|donoId|caixaEVenda|aosCredores|dividaQueFica|empregadosLiberados|ficaCom</c> (ficaCom: <c>npc-ID</c>, <c>city-ID</c>).</summary>
    WorkplaceBankrupt,
}
