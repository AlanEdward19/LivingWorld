# Fase 16.4 — World Realism (Ecologia Autônoma + Aprofundamento de Mecânicas Ocas)

## Problem Statement

A Fase 16.1 adicionou fauna, flora e temperatura, mas só como **stubs reativos a poder**:
animal é posição+espécie+vivo (sem reprodução/fome/predação), planta é só um estágio de
crescimento (sem ciclo de vida próprio), temperatura é um valor por célula que só um poder
altera — nada disso existe ou muda se nenhum poder tocar. A 16.1 também fechou 5 mecânicas
de maior risco (`npc.clone`/`npc.split-on-death`/`npc.reincarnate`, `control.possess`,
`bond.share`/`bond.oath`, `foresight.preview`) com o mínimo exigido pelo AC declarado na
época, deixando lacunas que um revisor apontou depois: clone/split/reincarnate não herdam
skill/relações reais, foresight só faz preview sem informar decisão nenhuma, combate resolve
em golpe único sem estado. O objetivo #1 do roadmap é "100 NPCs numa vila por 100 anos
**coerente**" — um mundo onde fauna não come, flora não cresce e clima não muda sozinho não
é coerente, é cenário de fundo pintado. Esta fase fecha essa lacuna: fauna/flora/clima viram
sistemas simulados por si sós, e as 5 mecânicas ocas ganham profundidade real.

## Goals

- [ ] Fauna é uma população simulada com fome, reprodução e predação — muda tick a tick sem
      nenhum poder ativo no mundo.
- [ ] Flora tem ciclo de vida (brota → cresce → produz/reproduz → morre) dirigido pela
      temperatura e estação local, não só por um multiplicador de poder.
- [ ] Temperatura varia por estação e bioma automaticamente — poder é modificador sobre uma
      base que já se move sozinha, não a única fonte de variação.
- [ ] Combate resolve em múltiplos rounds com estado (dano acumulado, esquiva/bloqueio,
      possibilidade de fuga ou morte no meio do combate), não mais golpe único determinado
      num tick.
- [ ] `npc.clone`/`npc.split-on-death`/`npc.reincarnate` herdam skill e vínculos reais do
      NPC de origem (não só personalidade), consistente com a regra de herança já usada em
      `NatalitySystem`.
- [ ] `foresight.preview` informa a decisão real do portador (o preview pode ser consultado
      pela utility AI antes de agir), não é mais só um relatório que ninguém lê.
- [ ] Toda simulação nova (fauna/flora/clima) roda dentro do teto de custo por NPC-tick já
      fixado na Fase 9 — animais e plantas contam para o orçamento como entidades leves, não
      como NPCs completos.

## Out of Scope

| Item | Razão |
| --- | --- |
| Novo namespace de token de poder (mecânica nova de efeito/custo) | Escopo fechado da Fase 16.1 — esta fase só aprofunda mecânicas e sistemas que já existem, nunca adiciona categoria de efeito nova. |
| Mistura genética de poderes / estágios de evolução de poder | Já é feature própria em andamento (`phase-16-2-power-evolution`, outro agente) — não duplicar. |
| Epidemiologia completa de doença (vetores, contágio populacional, cura) | A 16.1 já resolveu o vínculo mínimo de transmissão (`fauna.infect-vector`); esta fase não expande doença, só fauna/flora/clima/combate/instanciação/possessão/foresight. |
| IA/personalidade de fauna (animal com utility AI própria, rotina, memória) | Fora do problema — animal precisa de fome/reprodução/predação determinística, não precisa de decisão deliberativa como NPC. |
| Vínculo/pacto (`bond.share`/`bond.oath`) ganhar mecânica nova além do já fechado na 16.1 | Revisor citou "possessão/vínculo" como categoria a olhar, mas o AC de vínculo da 16.1 (desfazer ao morrer, aplicar consequência) já está completo — só `control.possess` (identidade/controle prolongado) precisa de profundidade adicional nesta fase. |
| UI do criador (web) | Explicitamente fora — o usuário pediu que só o web seja tratado em paralelo, fora desta spec; esta spec é backend-only. |
| Balanceamento de população fauna/flora (extinção nunca acontece, crescimento nunca satura) | Fora do problema declarado — só precisa ser determinístico e coerente no horizonte de closeout (10 anos, AD-029); 100 anos permanece no objetivo #1 do roadmap, não nesta fase. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Fauna/flora/clima viram sistema autônomo, não só hook de poder | Confirmado pelo usuário | "Sistema real e autônomo" — opção recomendada escolhida explicitamente | y |
| Escopo da spec inclui combate/instanciação/foresight/possessão junto com fauna/flora/clima | Confirmado pelo usuário | "Tudo junto nesta spec" — opção recomendada escolhida explicitamente | y |
| Profundidade mínima de combate | Múltiplos rounds com estado (dano acumulado, esquiva/bloqueio, fuga/morte no meio) | "Troca de golpes com estado" — opção recomendada escolhida explicitamente | y |
| Teto de custo por NPC-tick (Fase 9) se aplica a fauna/flora | Animal/planta são entidades leves com orçamento próprio, menor que o de um NPC completo (sem personalidade/skill/família) | Risco já registrado no `STATE.md`/`ROADMAP.md`: "custo por NPC-tick" é o teto que toda fase seguinte gasta — fauna/flora em massa (potencialmente mais numerosos que NPCs) não pode furar esse teto | n — assumido, precisa validação de sensor de escala na Fase Design |
| `control.possess` ganha profundidade, `bond.*` não | O AC de vínculo (16.1) já cobre desfazer-ao-morrer e consequência automática — nada ficou "oco" ali; só possessão contínua carece de mais estado (ex.: retorno de controle, resistência do hospedeiro) | Leitura do spec.md da 16.1 (linhas 839-898): vínculo tem 3 AC completos e testados; possessão tem só 1 AC básico | n — assumido, a confirmar no Design se o usuário achar que vínculo precisa de mais |
| Fauna não ganha utility AI própria | Fome/reprodução/predação são regras determinísticas simples (limiar + probabilidade por seed), não um sistema de decisão como NPC | Fora do problema declarado pelo usuário (fauna/flora/clima citados, não "IA de animal") — evita inflar escopo pra uma feature de "vida selvagem completa" não pedida | n — assumido, out-of-scope explícito acima |
| Ordem de implementação dentro da fase | Fauna → Flora → Temperatura → Combate → Instanciação (clone/split/reincarnate) → Foresight → Possessão | Segue a mesma disciplina já usada na 16.1 ("combate por último", risco crescente por último) — aqui fauna/flora/clima são a base que dá sentido às mecânicas de poder que os consomem, então vêm primeiro | n — assumido, ordem é decisão de execução, ajustável no Tasks |

**Open questions:** nenhuma — todas resolvidas ou registradas acima.

### Implicit-Requirement Dimensions (sweep — Large/Complex)

| Dimensão | Cobertura |
| --- | --- |
| Input validation & bounds | REALISM-01/06/13: parâmetros de reprodução/crescimento/dano são clamped a faixas declaradas por bioma/espécie no cenário — nunca aceitam valor fora do domínio (ex.: temperatura negativa absoluta, taxa de reprodução negativa). |
| Failure / partial-failure states | REALISM-19: se fauna/flora ultrapassar o teto de custo por NPC-tick, o sistema SHALL degradar (decaimento preguiçoso, mesmo padrão da Fase 9), nunca travar o tick nem abortar a simulação. |
| Idempotency / retry / duplicate handling | N/A nesta fase — não há chamada externa nem retry; toda transição é determinística por tick único (mesmo modelo de `Fact`/`WorldState` já usado em todo o motor). |
| Auth boundaries & rate limits | N/A — sistema é puramente de simulação interna, sem superfície de API nova nesta fase (API já existe pra poderes; esta fase não adiciona endpoint). |
| Concurrency / ordering | REALISM-20: ordem de processamento fauna→flora→temperatura→combate→instanciação dentro do tick é determinística e fixa (mesma garantia de dois-processos já usada no motor de tempo). |
| Data lifecycle / expiry | REALISM-21: animal/planta morta segue o mesmo arquivo frio de mortos já usado por NPC (Fase 9) — não pode crescer sem teto o estado histórico de fauna/flora. |
| Observability | REALISM-22: cada transição relevante (nascimento/morte de animal, estágio de flora, round de combate, clone/split/reincarnate, entrada/saída de possessão) gera um `Fact` no log causal já existente — nada novo em telemetria, reusa o mecanismo. |
| External-dependency failure | N/A — nenhuma dependência externa nova (sem LLM, sem rede) nesta fase. |
| State-transition integrity | REALISM-23: transições de fauna (viva→morta), flora (estágio N→N+1), combate (ativo→resolvido/fuga/morte), instanciação (viva→clonada/dividida/reencarnada), possessão (livre→possuído→livre) são guardadas — nenhuma transição inválida (ex.: animal morto reproduzindo, combate resolvendo duas vezes) passa despercebida pelo sweep de integridade referencial já existente (Fase 3). |

---

## User Stories

### P1: Fauna como população simulada ⭐ MVP

**User Story**: Como quem projeta o mundo, quero que animais existam como população viva —
com fome, reprodução e predação — pra que "mestre dos animais"/"comunicação animal"/
"portador da peste" (poderes já declarados na 16.1) tenham algo real pra controlar, e pra
que o mundo pareça habitado mesmo sem nenhum poder ativo.

**Why P1**: É o exemplo mais citado pelo revisor ("fauna/flora — entidades mínimas") e o
pré-requisito de sentido pros 3 poderes que hoje mexem num stub sem comportamento.

**Acceptance Criteria**:

1. WHEN o mundo processa um tick com fauna habilitada THEN cada animal vivo SHALL consumir
   um valor de fome determinado por espécie e decair sua reserva de energia. (REALISM-01)
2. WHEN a energia de um animal chega a zero THEN o animal SHALL morrer e gerar `Fact` de
   morte (mesmo padrão causal já usado por `NpcDeath`). (REALISM-02)
3. WHEN dois animais da mesma espécie estão dentro do raio de reprodução E ambos têm energia
   acima do limiar declarado por espécie THEN, por probabilidade determinística por seed,
   um novo animal SHALL nascer próximo a eles. (REALISM-03)
4. WHEN um animal predador está no raio de um animal presa (par espécie declarado no
   cenário) THEN, por probabilidade determinística, o predador SHALL consumir a presa
   (presa morre, predador ganha energia). (REALISM-04)
5. WHEN `fauna.dominate`/`fauna.infect-vector` (poderes já existentes na 16.1) atuam sobre
   um animal THEN o comportamento de fome/reprodução/predação SHALL continuar rodando por
   baixo — o poder modula, não substitui, a simulação de base. (REALISM-05)
6. WHEN `Extraordinary.Enabled == false` THEN fauna SHALL continuar simulando fome/
   reprodução/predação normalmente — esses não são efeitos de poder, são sistema de base.
   (REALISM-06)

**Independent Test**: mundo com N animais de 2 espécies (uma presa, uma predadora), 0 poderes
ativos, rodado por T ticks — população varia (nasce, morre, é predada) sem nenhuma invocação
de poder no log.

---

### P1: Flora com ciclo de vida próprio ⭐ MVP

**User Story**: Como quem projeta o mundo, quero que plantas brotem, cresçam, produzam/
reproduzam e morram sozinhas, dirigidas por temperatura e estação, pra que agricultura e
poderes de flora atuem sobre algo vivo, não um contador estático.

**Why P1**: Par direto de Fauna, mesmo problema apontado pelo revisor, e pré-requisito pra
Temperatura ter efeito visível em algo além do log.

**Acceptance Criteria**:

1. WHEN o mundo processa um tick THEN cada planta SHALL avançar seu estágio de vida
   (broto → crescimento → produção → senescência → morte) numa taxa determinada por
   temperatura local e estação, sem depender de nenhum poder ativo. (REALISM-07)
2. WHEN a temperatura local está fora da faixa de tolerância declarada pra espécie de planta
   THEN a taxa de crescimento SHALL cair (podendo chegar a zero ou reverter estágio em frio/
   calor extremo), nunca avançar normalmente. (REALISM-08)
3. WHEN uma planta atinge o estágio de produção THEN ela SHALL gerar recurso consumível pelo
   `CropSystem`/economia (mesma integração de consumo já existente, sem duplicar estoque).
   (REALISM-09)
4. WHEN uma planta em produção está dentro do raio de reprodução de espaço livre compatível
   THEN, por probabilidade determinística, uma nova planta jovem SHALL brotar. (REALISM-10)
5. WHEN `flora.growth-rate` (poder já existente) está ativo THEN ele SHALL multiplicar a taxa
   base calculada por temperatura/estação — nunca substituir o cálculo de base por um valor
   fixo. (REALISM-11)

**Independent Test**: área sem nenhum poder ativo, rodada por T ticks em 2 estações
diferentes (uma dentro, uma fora da faixa de tolerância da espécie) — taxa de avanço de
estágio medida difere nitidamente entre as duas estações.

---

### P1: Temperatura variando por estação e bioma ⭐ MVP

**User Story**: Como quem projeta o mundo, quero que a temperatura de cada célula mude
sozinha ao longo do ano conforme bioma, pra que fauna/flora/cultivo tenham um sinal real de
clima — hoje temperatura só existe se um poder a criar.

**Why P1**: Pré-requisito direto de Fauna e Flora (ambos dependem de temperatura local pra
ter comportamento real, não só um valor congelado).

**Acceptance Criteria**:

1. WHEN o calendário avança de estação THEN a temperatura base de cada célula SHALL se
   ajustar conforme a curva sazonal declarada pro bioma daquela célula (ex.: bioma frio tem
   amplitude e mínima diferentes de bioma quente). (REALISM-12)
2. WHEN nenhum poder de clima está ativo sobre uma célula THEN sua temperatura SHALL seguir
   só a curva sazonal de bioma — nunca ficar travada num valor único o ano inteiro (regressão
   do comportamento atual, que só muda por poder). (REALISM-13)
3. WHEN um poder declara `environment.temperature:<região>:<delta>:<duração>` (já existente)
   THEN o delta SHALL somar sobre o valor sazonal calculado, não sobre um valor base fixo.
   (REALISM-14)
4. WHEN `CropSystem`/fauna/flora consultam temperatura da célula THEN o valor lido SHALL ser
   o resultado combinado (sazonal + delta de poder ativo, se houver). (REALISM-15)

**Independent Test**: célula sem nenhum poder ativo, observada em 2 estações opostas do
mesmo ano — valor de temperatura lido difere; célula com poder ativo soma delta sobre o
valor sazonal do momento, confirmado comparando com/sem poder na mesma estação.

---

### P2: Combate com estado e múltiplos rounds

**User Story**: Como quem projeta violência/dano (Fase 16.1), quero que um combate resolva
em múltiplos rounds com dano acumulado, chance de esquiva/bloqueio e possibilidade de fuga ou
morte no meio do processo, não mais um golpe único que decide tudo num tick.

**Why P2**: Depende do dano/vida já existentes na 16.1; profundidade, não pré-requisito de
Fauna/Flora/Temperatura — pode vir depois da base ecológica.

**Acceptance Criteria**:

1. WHEN um combate é iniciado entre dois NPCs (ou NPC e animal, reusando a mesma resolução)
   THEN o motor SHALL criar um estado de combate ativo que persiste entre ticks até ser
   resolvido — não mais aplicar dano final num único cálculo. (REALISM-16)
2. WHEN um round de combate é processado THEN cada lado SHALL ter chance determinística
   (por seed) de acertar, esquivar ou bloquear, com dano acumulando sobre a vida já reduzida
   do round anterior. (REALISM-17)
3. WHEN a vida de um participante chega a zero durante qualquer round THEN o combate SHALL
   ser resolvido imediatamente como morte — não espera rounds restantes. (REALISM-18)
4. WHEN um participante está abaixo de um limiar de vida declarado por cenário THEN ele
   SHALL ter chance determinística de tentar fugir, encerrando o combate sem morte se a fuga
   for bem-sucedida. (REALISM-24)
5. WHEN `Extraordinary.Enabled == false` THEN combate SHALL continuar resolvendo por rounds
   normalmente — o sistema de rounds é base de simulação, não efeito de poder. (REALISM-25)

**Independent Test**: 2 NPCs com vida/dano declarados entram em combate — log de `Fact`
mostra múltiplos rounds distintos antes da resolução final (morte ou fuga), nunca um único
evento de "combate resolvido" no mesmo tick do início.

---

### P2: Clone/split/reincarnate herdam skill e vínculos reais

**User Story**: Como quem cria "clone"/"divisão"/"reencarnação", quero que o NPC resultante
herde skill e relações reais do original (não só personalidade), pra que a mecânica seja
coerente com a regra de herança já usada em nascimento normal.

**Why P2**: Aprofunda mecânica já fechada na 16.1 (P2, `Done`) que o revisor considerou oca —
não é MVP novo, é reforço.

**Acceptance Criteria**:

1. WHEN `npc.clone` instancia um novo NPC THEN o clone SHALL herdar o nível de skill atual
   do original em cada categoria (não só personalidade, que já era herdada). (REALISM-26)
2. WHEN `npc.split-on-death` gera N novos NPCs THEN cada um SHALL herdar uma fração
   proporcional de skill do original (não skill completa duplicada em todos), consistente
   com "divisão" em vez de "clonagem completa". (REALISM-27)
3. WHEN `npc.reincarnate` influencia o próximo nascimento THEN o recém-nascido SHALL herdar
   uma fração declarada de skill do falecido, pelo mesmo peso de mistura genética (`w_gene`,
   já parâmetro auditável da Fase 7) usado para atributos — não herança de skill 1:1.
   (REALISM-28)
4. WHEN qualquer uma dessas 3 mecânicas ocorre THEN vínculos sociais diretos do original
   (família, empregador, relações de confiança) SHALL ser copiados/transferidos conforme a
   mecânica declarar (clone = cópia independente; split = cada novo NPC mantém os vínculos
   originais; reincarnate = vínculos não sobrevivem, é um NPC novo) — nunca deixados vazios
   por omissão. (REALISM-29)

**Independent Test**: NPC de origem com skill nível N e F vínculos sociais ativa `npc.clone`
— clone nasce com skill nível N (não zero) e F vínculos copiados, confirmado consultando o
snapshot do clone no mesmo tick.

---

### P2: Foresight informa a decisão real

**User Story**: Como quem cria "premonição"/"visão do futuro", quero que o preview gerado
por `foresight.preview` seja consultável pela utility AI do portador antes de agir, não
apenas um relatório que ninguém lê.

**Why P2**: Aprofunda mecânica já fechada (P2, `Done`) apontada como oca pelo revisor —
"só preview" sem efeito em decisão nenhuma.

**Acceptance Criteria**:

1. WHEN `foresight.preview:<evento>` roda a simulação especulativa (comportamento já
   existente) THEN o resultado SHALL ficar disponível como entrada de contexto pra utility
   AI do portador no mesmo tick, não só registrado em log. (REALISM-30)
2. WHEN a utility AI do portador avalia uma ação que tem um preview de foresight disponível
   pra ela THEN a pontuação daquela ação SHALL ser ajustada pelo resultado do preview
   (ex.: ação que o preview indica desfecho ruim tem utility reduzida). (REALISM-31)
3. WHEN nenhum `foresight.preview` foi rodado no tick THEN a decisão da utility AI SHALL
   funcionar exatamente como antes (sem regressão pra quem não tem o poder). (REALISM-32)

**Independent Test**: portador com `foresight.preview` ativo sobre uma ação que a simulação
especulativa indica desfecho ruim — a utility AI real do portador evita essa ação com
frequência maior do que um NPC idêntico sem foresight, no mesmo cenário/seed.

---

### P3: Possessão contínua com resistência e retorno de controle

**User Story**: Como quem cria "possessão"/"troca de corpo", quero que o hospedeiro tenha
chance de resistir e retomar controle, não uma possessão que só termina quando o possuidor
decide ou o poder acaba.

**Why P3**: Aprofunda mecânica já fechada (P2, `Done`) citada pelo revisor — nice-to-have de
profundidade, não bloqueia nenhum outro item desta fase.

**Acceptance Criteria**:

1. WHEN `control.possess` está ativo THEN o hospedeiro SHALL ter, a cada tick, chance
   determinística (por seed, modulada por atributo de vontade/resistência do hospedeiro) de
   interromper a possessão e retomar controle. (REALISM-33)
2. WHEN o hospedeiro retoma controle por resistência THEN um `Fact` SHALL registrar o evento
   com o NPC possuidor identificado (mantém atribuição causal, já garantida na 16.1).
   (REALISM-34)

**Independent Test**: hospedeiro com atributo de resistência alto sob `control.possess`
recupera controle numa fração maior de execuções do que um hospedeiro com resistência baixa,
no mesmo seed/cenário.

---

## Edge Cases

- WHEN uma espécie de animal não tem par predador/presa declarado no cenário THEN ela SHALL
  reproduzir e morrer só por fome, sem predação (nenhum erro por ausência de par).
- WHEN todos os animais de uma espécie morrem THEN a espécie SHALL ficar extinta nesse mundo
  sem religar sozinha (sem respawn espontâneo — coerente com "sem RNG espontâneo" já regra
  do motor).
- WHEN uma planta nunca entra na faixa de tolerância de temperatura durante toda sua vida
  THEN ela SHALL morrer sem nunca produzir (não trava em "crescimento" eterno).
- WHEN um combate ultrapassa um teto declarado de rounds (anti-loop-infinito, mesmo padrão
  de teto de iterações por tick já usado no motor de tempo) THEN o combate SHALL ser
  forçado a resolver (empate por exaustão ou fuga automática), nunca travar o tick.
- WHEN `npc.split-on-death` produziria mais NPCs do que o teto de população viva declarado
  pro cenário THEN o motor SHALL limitar a N NPCs (mesmo mecanismo de teto já usado em
  reprodução normal), sem estourar custo de memória (risco já mapeado no `STATE.md`).
- WHEN dois efeitos de temperatura de poder se sobrepõem na mesma célula THEN os deltas
  SHALL somar (mesma regra de composição já usada por outros modificadores ambientais),
  sobre o valor sazonal do momento.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| REALISM-01 | P1: Fauna | Design | Pending |
| REALISM-02 | P1: Fauna | Design | Pending |
| REALISM-03 | P1: Fauna | Design | Pending |
| REALISM-04 | P1: Fauna | Design | Pending |
| REALISM-05 | P1: Fauna | Design | Pending |
| REALISM-06 | P1: Fauna | Design | Pending |
| REALISM-07 | P1: Flora | Design | Pending |
| REALISM-08 | P1: Flora | Design | Pending |
| REALISM-09 | P1: Flora | Design | Pending |
| REALISM-10 | P1: Flora | Design | Pending |
| REALISM-11 | P1: Flora | Design | Pending |
| REALISM-12 | P1: Temperatura | Design | Pending |
| REALISM-13 | P1: Temperatura | Design | Pending |
| REALISM-14 | P1: Temperatura | Design | Pending |
| REALISM-15 | P1: Temperatura | Design | Pending |
| REALISM-16 | P2: Combate | Design | Pending |
| REALISM-17 | P2: Combate | Design | Pending |
| REALISM-18 | P2: Combate | Design | Pending |
| REALISM-24 | P2: Combate | Design | Pending |
| REALISM-25 | P2: Combate | Design | Pending |
| REALISM-26 | P2: Instanciação | Design | Pending |
| REALISM-27 | P2: Instanciação | Design | Pending |
| REALISM-28 | P2: Instanciação | Design | Pending |
| REALISM-29 | P2: Instanciação | Design | Pending |
| REALISM-30 | P2: Foresight | Design | Pending |
| REALISM-31 | P2: Foresight | Design | Pending |
| REALISM-32 | P2: Foresight | Design | Pending |
| REALISM-33 | P3: Possessão | Design | Pending |
| REALISM-34 | P3: Possessão | Design | Pending |
| REALISM-19 | Dimensão: Failure states | Design | Pending |
| REALISM-20 | Dimensão: Concurrency/ordering | Design | Pending |
| REALISM-21 | Dimensão: Data lifecycle | Design | Pending |
| REALISM-22 | Dimensão: Observability | Design | Pending |
| REALISM-23 | Dimensão: State-transition integrity | Design | Pending |

**Coverage:** 34 total, 0 mapped to tasks (Tasks phase ainda não rodou), 0 unmapped ⚠️
(todos endereçados por uma story ou dimensão — nenhum item solto).

---

## Success Criteria

- [x] Mundo com fauna/flora/temperatura habilitadas e **0 poderes ativos**, rodado por **10
      anos** no cenário de referência (AD-029; 100 anos fica no objetivo #1 / LifeTable),
      termina sem travar e com população de fauna/estágios de flora variando de forma
      auditável no log — não estática.
- [x] `bash scripts/verify.sh` permanece verde (0 falhas novas) com fauna/flora/clima/combate
      por round/instanciação-com-herança/foresight-informando-decisão/possessão-com-resistência
      todos cobertos por teste.
- [x] Sensor de custo por NPC-tick (Fase 9) confirma que fauna/flora em massa não fura o
      teto já fixado — nenhuma regressão de performance no cenário de referência.
- [x] Nenhuma das 5 mecânicas antes "ocas" (clone/split/reincarnate, foresight, possessão)
      continua com o gap específico citado pelo revisor.
