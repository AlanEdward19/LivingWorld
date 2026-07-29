# Fase 10 (História degradável) Specification

## Problem Statement

O motor hoje registra história como log — fiel, crescente, e caro (ADR-0006 previa
"retenção genérica" e ela não fecha: 1M de habitantes por 2.000 anos passa de 58 TB, ver
ADR-0007). Pior: um log fiel e comprimido produz um resumo sem graça, não uma história. A
Fase 10 substitui isso pelo modelo do ADR-0007: o motor guarda um **esqueleto imutável do
fato** (a verdade, só o motor vê) e o mundo guarda **relatos degradados** — tradição, livro,
crônica, canção, monumento — cada um com fidelidade e proveniência próprias. Morta a última
testemunha, o fato deixa de ser consultável como registro fiel e passa a existir só como
relato. O NPC decide sobre a **crença**, nunca sobre o fato.

## Goals

- [ ] Custo de armazenamento de história fica independente do tempo decorrido — cânone
      limitado por comunidade, não por ano de mundo (a propriedade central do ADR-0007).
- [ ] Toda distorção de relato é determinística e reproduzível — mesma seed produz o mesmo
      relato byte-idêntico em dois processos; a LLM nunca participa da distorção (ela não
      existe ainda nesta fase — mas o contrato já a impede de participar quando chegar na
      Fase 11).
- [ ] Nenhum caminho de jogo alcança a verdade do motor — só a crença, e a separação é
      provada por enumeração exaustiva dos handlers da API de jogo, não por convenção.
- [ ] Dinastias e linhagens são sempre deriváveis do esqueleto do fato, sem tabela paralela
      e sem `UPDATE` no passado — correção é sempre evento compensatório anexado.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Guerra entre cidades, tratados, política externa | Apontado para a Fase 10 pela Fase 8 (AD-067), mas o roadmap da própria Fase 10 marca o item como "sem tasks/critérios próprios ainda" — precisa de levantamento de design dedicado antes de virar task. Não inventado aqui. |
| Prosa narrativa, jornais, biografias geradas | Fase 12 — esta fase produz **dado estruturado** (fato, relato, crença), nunca texto livre. |
| LLM lendo o passado para narrar | Fase 11 em diante. Esta fase não impede o consumo futuro — a distorção fica pronta como dado para a LLM só narrar, nunca fabricar, quando a Fase 11 chegar. |
| Formação de facções/religiões/reformas políticas a partir de relatos divergentes | Society/política (Fase 23+) — esta fase só garante que a crença divergente *existe e é consultável*, não que ela produz comportamento político novo. |
| Interface visual de linha do tempo/genealogia | Fase 15 (cliente) — esta fase só garante que a consulta existe e é indexada, não a apresentação. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Fórmula exata dos 8 operadores de distorção | Cada operador é uma transformação determinística e parametrizada sobre o payload do relato (ex.: inflação de magnitude multiplica um campo numérico por um fator derivado do RNG seedado; troca de atribuição substitui `AttributedToNpcId` por outro participante do fato elegível). Probabilidade de aplicação por operador é dado de cenário (`HistoryRules`), não constante em C# (R3). | Mantém o espírito "o motor distorce, a LLM narra" sem exigir um motor de NLP nesta fase — distorção opera sobre campos estruturados do relato, não sobre texto. | n |
| Fórmula exata de peso de despejo do cânone | `peso = importância × transmissibilidade(meio) × recência(hops desde a criação)`, todos os três fatores declarados por cenário. | Seguindo literalmente a redação do roadmap ("importância × transmissibilidade × recência") — só falta o cenário declarar os pesos relativos, que é parâmetro, não invenção de mecanismo. | n |
| Valor default de N (tamanho do cânone por comunidade) | Nenhum — `HistoryRules.CanonSizePerCommunity` é campo obrigatório do cenário (sem `N` implícito). Cenários de teste declaram um `N` pequeno (dezenas) para tornar despejo observável em ticks curtos. | R3 proíbe número mágico; o próprio ADR-0007 já trata N como parâmetro de cenário. | n |
| Taxa de decaimento de fidelidade por meio de transmissão | Tabela de parâmetros por `TransmissionMediumType` (`DistortionRatePerHop`, `ReachTicks`, `DeathCondition`) declarada em `HistoryRules`, com a ordem relativa fixada pelo domínio (`docs/domain/historical-memory.md`: tradição oral > canção > livro > monumento em taxa de distorção por salto; livro > monumento em alcance). Valores absolutos ficam em `tests/baselines/*.json` (20 seeds), nunca literais no critério (R3). | Domínio já dá a ordem relativa; só os números absolutos são ambíguos, e esses vão para baseline como qualquer outro teto/taxa do projeto. | n |
| Limiar de significância que separa "sobrevive no esqueleto" de "colapsa" | `HistoryRules.SkeletonSignificanceThreshold`, campo de cenário (double, mesma escala 0–1 da significância calculada). | Já é o comportamento pedido pela task 2/critério de verificação — só precisa de nome de campo, não de valor. | n |
| `X%` mínimo de colapso abaixo do limiar (critério de verificação) | Vem de `tests/baselines/*.json` medido em 20 seeds do cenário de teste declarado, nunca hardcoded (R3, mesmo padrão de PERF-01 na Fase 9). | Redação do próprio roadmap já pede isso ("`X` vindo do cenário"). | n |
| `k` (multiplicador de linhas lidas vs tamanho do resultado, prova de índice) | Baseline medido, não literal — mesmo padrão do `k` seria arbitrário sem medir o pior caso real do índice implementado. | R3 + o próprio critério de verificação já pede baseline, não `k` cravado no texto. | n |
| Mecanismo de redescoberta de livro perdido | Evento **declarado**: um `BookRediscoveryEvent` agendado no `EventScheduler` (nunca dado por sorteio implícito no tick) — o cenário ou uma regra explícita decide quando um livro perdido é redescoberto, com RNG seedado pelo par (bookId, tentativa). | O roadmap já exige "evento declarado, não um acaso" — a interpretação literal é "existe um evento no scheduler", não "existe uma varredura por tick que sorteia redescoberta". | n |
| Granularidade de "comunidade" para o cânone limitado | `City` (já existe como agregado de comunidade desde a Fase 8, `CityId`). Cultura (`CultureId`, catálogo simples) é um agrupamento *derivado* de várias cidades — mito cultural é consultado filtrando o cânone de todas as cidades da mesma cultura, sem cânone próprio duplicado por cultura. | Evita criar uma segunda estrutura de cânone (ladder: reusa `City` já existente em vez de um novo agregado "Comunidade"); `CultureId` já existe como agrupador barato. | n |
| Relação entre `Relato` (novo) e `Household`/tradição familiar | Tradição oral familiar é um `TransmissionMedium` cujo escopo de alcance (`ReachScope`) é o `HouseholdId`/linhagem, não a cidade inteira — não cria um cânone por família separado do cânone por comunidade; a linha familiar é só um filtro sobre o mesmo cânone. | Mesmo raciocínio do item anterior — um mecanismo de cânone, filtrado por escopo, não N mecanismos. | n |
| Como um NPC materializado sob demanda (LOD, AD-068) resolve consulta de Crença | Resolve direto para o cânone da comunidade/cultura — nenhum estado de crença por-NPC é criado para NPC ainda não materializado; ao materializar, o NPC "herda" a crença coletiva vigente no momento da materialização, sem retrofit de crença passada. | Conforma ao padrão de LOD agregado da Fase 8 (AD-068): não pagar estado individual para quem nunca foi individual. Registrado explicitamente porque a task pediu a checagem. | n |
| Representação interna do Relato (aggregate vs derivado por replay) | Deixado para o Design (exploração de 2–3 abordagens é obrigatória para Large/Complex) — não decidido no spec. | Decisão arquitetural, não requisito de produto. | n |

**Open questions:** none — todas resolvidas ou registradas acima.

---

## User Stories

### P1: Fato imutável e sua conversão em relato ⭐ MVP

**User Story**: Como motor de simulação, quero guardar um esqueleto imutável de cada fato
significativo e convertê-lo em relato quando a última testemunha morre, para que a história
seja consultável sem crescer sem limite.

**Why P1**: É o núcleo do ADR-0007 — sem esqueleto imutável e sem o gatilho de conversão,
nenhum outro mecanismo desta fase (distorção, cânone, livros, consultas) tem o que consumir.

**Acceptance Criteria**:

1. WHEN um evento de significância ≥ `HistoryRules.SkeletonSignificanceThreshold` ocorre THEN
   o sistema SHALL gravar um `Fact` imutável no esqueleto com campos mínimos: quem
   (participantes por `NpcId`), o quê (`FactKind`), onde (`CellCoord`/`CityId`), quando
   (`Tick`), e a significância calculada.
2. WHEN o esqueleto do fato é gravado THEN o sistema SHALL rejeitar qualquer tentativa de
   `UPDATE` ou `DELETE` direto na tabela de fatos no armazenamento — ambas as operações
   falham.
3. WHEN um fato tem ao menos uma testemunha viva (participante direto ou observador
   registrado) THEN o sistema SHALL permitir consultar o fato com fidelidade alta
   (já enviesada pela testemunha, ver HIST-03), sem convertê-lo em relato.
4. WHEN a última testemunha de um fato morre THEN o sistema SHALL agendar, no
   `EventScheduler` já existente (nunca varredura por tick), a conversão do fato em um
   `Relato` inicial (hop 0) para cada comunidade com testemunha vinculada.
5. WHEN um evento com significância abaixo do limiar ocorre THEN o sistema SHALL colapsá-lo
   (não gravar esqueleto individual) — colapso é omissão na escrita, nunca deleção de uma
   linha já gravada.

**Independent Test**: Rodar um cenário com um NPC cuja morte é a última testemunha de um
fato registrado; assertar que o `Relato` hop-0 aparece agendado no tick da morte, com o
`Fact` original permanecendo legível e byte-idêntico no esqueleto.

---

### P1: Distorção determinística por salto de transmissão ⭐ MVP

**User Story**: Como motor de simulação, quero aplicar operadores de distorção
determinísticos a cada salto de transmissão de um relato, para que a história se degrade de
forma reproduzível sem que uma LLM precise participar.

**Why P1**: É a garantia central de determinismo do ADR-0007 — sem isso, "mesma seed → mesmo
mundo" quebra assim que a história divergir da verdade.

**Acceptance Criteria**:

1. WHEN um relato passa por um salto de transmissão (hop `n` → `n+1`) THEN o sistema SHALL
   aplicar um subconjunto dos 8 operadores determinísticos (troca de atribuição, inflação de
   magnitude, compressão temporal, perda de causa, moralização, anacronismo, omissão
   conveniente, fusão de personagens) escolhido e parametrizado por RNG derivado de
   `(RelatoId, hop)` via `WorldRngRegistry`.
2. WHEN o mesmo cenário roda duas vezes com a mesma seed, em dois processos separados THEN o
   sistema SHALL produzir o relato distorcido byte-idêntico em ambos.
3. WHEN a distorção de um relato é executada THEN o sistema SHALL falhar o teste se qualquer
   provider de LLM (real ou fake, injetado por teste) for invocado durante a distorção — a
   LLM não participa da geração do relato nesta fase (nem em nenhuma futura, por contrato).
4. WHEN a distância `d` (fato↔relato) é medida ao longo dos saltos de uma cadeia de
   transmissão THEN o sistema SHALL garantir `d(hop n+1) >= d(hop n)` para toda cadeia — a
   distância nunca decresce sem uma causa declarada.
5. WHEN `d` cai num salto THEN o sistema SHALL exigir que esse salto tenha sido precedido por
   um evento de redescoberta declarado (HIST-08/AC4) — queda sem redescoberta é uma falha de
   invariante, nunca um comportamento válido.

**Independent Test**: Encadear um relato por 5 saltos de transmissão com seed fixa; rodar em
dois processos e comparar o relato final byte a byte; assertar `d` não decrescente nos 5
saltos; instrumentar um fake LLM provider que lança exceção se chamado e rodar a cadeia
inteira sem disparo.

---

### P1: Meios de transmissão e cânone limitado por comunidade ⭐ MVP

**User Story**: Como motor de simulação, quero que cada meio de transmissão tenha fidelidade
e alcance próprios, e que cada comunidade mantenha no máximo N relatos vivos, para que o
custo de armazenar história não cresça com o tempo do mundo.

**Why P1**: É a propriedade que resolve o problema de escala do ADR-0007 (58 TB → ~6 GB) —
sem cânone limitado, a Fase 10 reintroduz o mesmo problema que a Fase 9 acabou de fechar
para NPCs.

**Acceptance Criteria**:

1. WHEN um relato é criado ou transmitido por um meio (memória viva, tradição oral familiar,
   livro/crônica, monumento/inscrição, canção/ditado) THEN o sistema SHALL aplicar a taxa de
   distorção por salto, o alcance e a condição de morte declarados para aquele meio em
   `HistoryRules`.
2. WHEN uma comunidade (`City`) já possui `HistoryRules.CanonSizePerCommunity` relatos vivos
   e um relato novo entra no cânone THEN o sistema SHALL despejar o relato de menor peso
   (`importância × transmissibilidade × recência`) para abrir espaço — nunca ultrapassar o
   teto.
3. WHEN o cenário roda por 50, 100 e 200 anos de mundo THEN o sistema SHALL manter o total
   de relatos vivos por comunidade no teto declarado nos três horizontes, sem tendência de
   crescimento entre eles.
4. WHEN o custo em bytes por relato retido é medido em 10 anos de mundo THEN o sistema SHALL
   ficar dentro do orçamento registrado em `tests/baselines/*.json` (20 seeds) — nunca um
   valor chutado no critério, nunca uma espera de 100 anos para medir.

**Independent Test**: Rodar o mesmo cenário de teste a 50, 100 e 200 anos e comparar a
contagem de relatos vivos por comunidade nos três pontos — deve ficar estável no teto, não
crescente.

---

### P1: Livros como objetos do mundo ⭐ MVP

**User Story**: Como motor de simulação, quero que livros sejam objetos do mundo que podem
ser copiados com erro, perdidos e redescobertos por evento declarado, para que registros
escritos tenham vida própria além do relato que carregam.

**Why P1**: O roadmap declara isso como task própria (8) e como um dos comportamentos que o
ADR-0007 promete desbloquear ("o estudioso que acha um livro antigo e contradiz o
consenso").

**Acceptance Criteria**:

1. WHEN um livro é copiado THEN o sistema SHALL gerar uma nova instância de `Book` com
   `CopyOfBookId` apontando para o original e aplicar erro de copista (mesmo mecanismo de
   distorção determinística por hop, meio = Livro).
2. WHEN um livro é perdido (queimado, destruído, extraviado por evento declarado) THEN o
   sistema SHALL marcar `Book.Lost = true` no tick do evento, sem apagar a linha.
3. WHEN um livro perdido é redescoberto THEN o sistema SHALL exigir um `BookRediscoveryEvent`
   agendado explicitamente (nunca uma checagem por tick que sorteia redescoberta
   implicitamente) e SHALL permitir que o conteúdo redescoberto contradiga o cânone vigente
   da comunidade.

**Independent Test**: Perder um livro, avançar o mundo sem nenhum evento de redescoberta
agendado, assertar que ele continua perdido; agendar um `BookRediscoveryEvent` e assertar que
o livro volta a ser consultável no tick agendado.

---

### P1: Duas consultas separadas — Verdade e Crença ⭐ MVP

**User Story**: Como autor/depurador do motor, quero uma consulta de Verdade isolada da
consulta de Crença que o jogo usa, para que nenhum NPC ou sistema de jogo jamais acesse o
fato bruto.

**Why P1**: É a fronteira de segurança explícita do ADR-0007 — "verdade histórica vazar para
o mundo do jogo" já está listado em `STATE.md` como risco mitigado por este mecanismo
(tabela de riscos, linha "Verdade histórica vazar").

**Acceptance Criteria**:

1. WHEN qualquer consulta de motor/debug/ferramenta de autor precisa do fato THEN o sistema
   SHALL expor essa necessidade só pela consulta `Verdade`, implementada num único handler
   dedicado, nunca reaproveitado por um handler de jogo.
2. WHEN um NPC, família ou cultura precisa decidir com base no passado THEN o sistema SHALL
   expor essa necessidade só pela consulta `Crença`, que resolve para o relato vigente
   daquele NPC/família/cultura, nunca para o fato.
3. WHEN todos os handlers da API/CLI de jogo são enumerados por reflexão THEN o sistema
   SHALL garantir que nenhum resolve para a consulta de `Verdade` — a enumeração falha se
   algum handler ficar sem cobertura (nenhum handler novo escapa da checagem por omissão).
4. WHEN a checagem do item 3 é desligada por flag de teste (par de mutação) THEN o sistema
   SHALL fazer o próprio critério de verificação falhar — se não falhar, a checagem não mede
   nada.
5. WHEN duas comunidades diferentes consultam Crença sobre o mesmo fato THEN o sistema SHALL
   permitir que as duas versões divirjam — e o cenário de teste SHALL conter ao menos um caso
   onde Verdade e Crença de fato divergem (se nunca divergem, a distorção não está ligada).

**Independent Test**: Enumerar todos os tipos de handler de `LivingWorld.Api`/
`LivingWorld.Workers` por reflexão/`NetArchTest`, assertar zero referência ao tipo da
consulta de Verdade; desligar a checagem por flag e assertar que o teste de arquitetura
passa a falhar (par de mutação obrigatório por `rules/eval-criteria.md`).

---

### P2: Índices de consulta por ano, entidade e tipo

**User Story**: Como consumidor da linha do tempo (API, CLI, LLM futura), quero consultar
fatos e relatos por ano, entidade ou tipo sem que o motor varra a base inteira, para que a
consulta de história escale com o tamanho do resultado, não com o tamanho do mundo.

**Why P2**: Necessário para que a história seja de fato consultável em produção, mas não
bloqueia o núcleo de fato→relato→cânone (P1) — pode ser adicionado sobre o esqueleto e o
cânone já existentes.

**Acceptance Criteria**:

1. WHEN uma consulta de linha do tempo é feita por ano, por entidade ou por tipo THEN o
   sistema SHALL resolver via índice, sem varrer a base.
2. WHEN o número de linhas lidas por uma consulta é contado (contador de I/O ou plano de
   query) THEN o sistema SHALL manter essa contagem ≤ `k × tamanho do resultado`, com `k`
   medido em baseline (nunca literal no critério).

**Independent Test**: Popular o esqueleto e o cânone com um volume grande de fatos/relatos;
consultar por ano/entidade/tipo e contar linhas lidas via interceptor de comando (mesmo
padrão de `CountingCommandInterceptor` já usado na Fase 3/9); assertar que a contagem não
cresce linearmente com o total de fatos gravados.

---

### P2: Dinastias e linhagens derivadas, correção por evento compensatório

**User Story**: Como consumidor da história, quero que dinastias e linhagens sejam sempre
deriváveis do esqueleto do fato, e que qualquer correção do passado seja um evento novo, para
que a história nunca seja reescrita silenciosamente.

**Why P2**: Depende do esqueleto do fato (P1) já existir com nascimento/morte/parentesco
gravados — é uma consulta derivada sobre P1, não um mecanismo de gravação novo.

**Acceptance Criteria**:

1. WHEN uma linhagem é reconstruída a partir do esqueleto THEN o sistema SHALL chegar a um
   fundador sem buraco e sem ciclo, e toda morte no esqueleto SHALL ter um nascimento do
   mesmo `NpcId` em tick anterior.
2. WHEN um NPC morre THEN o sistema SHALL garantir zero eventos do esqueleto registrados para
   aquele `NpcId` em tick posterior à morte.
3. WHEN o passado precisa ser corrigido (ex.: paternidade incorreta descoberta depois) THEN o
   sistema SHALL exigir um evento compensatório — a linha original permanece legível e
   marcada, nunca reescrita ou apagada.
4. WHEN um evento compensatório é consultado THEN o sistema SHALL retornar tanto o evento
   compensatório quanto a linha original ainda legível, com marcação explícita de qual é qual.

**Independent Test**: Construir uma linhagem de 4 gerações no esqueleto; reconstruir por
consulta e assertar chegada ao fundador sem buraco/ciclo; emitir uma correção de paternidade
como evento compensatório e assertar que a consulta expõe as duas linhas, marcadas.

---

### P3: Reidratação e replay produzem o mesmo `Hash(world)`

**User Story**: Como operador do motor, quero que reidratar um snapshot e reaplicar o log a
partir dele reproduza o mesmo `Hash(world)`, para que a história degradável não introduza uma
fonte nova de não-determinismo na persistência.

**Why P3**: É uma checagem de regressão sobre o invariante já existente (ADR-0006/Fase 1) —
importante fechar, mas não é o comportamento novo desta fase.

**Acceptance Criteria**:

1. WHEN um snapshot é reidratado e o log é reaplicado a partir dele THEN o sistema SHALL
   reproduzir o mesmo `Hash(world)` que a execução contínua produziria no mesmo tick.

**Independent Test**: Rodar um cenário até o tick T, tirar snapshot, reidratar, reaplicar o
log até T, comparar `Hash(world)` com o hash da execução contínua até T.

---

## Edge Cases

- WHEN um fato não tem nenhuma testemunha viva desde o início (ex.: evento agregado/populacional sem NPC nomeado) THEN o sistema SHALL converter direto para relato hop-0 sem passar por janela de memória viva.
- WHEN duas testemunhas do mesmo fato morrem no mesmo tick THEN o sistema SHALL agendar a conversão determinìsticamente (ordem de `NpcId`, nunca ordem de iteração de coleção — `rules/simulation-determinism.md`).
- WHEN um relato despejado do cânone ainda é referenciado por um livro existente THEN o sistema SHALL preservar o livro (o despejo remove do cânone vivo da comunidade, não deleta o objeto-livro que já existe no mundo).
- WHEN um cenário declara `CanonSizePerCommunity = 0` THEN o sistema SHALL rejeitar na validação de `HistoryRules.Create` (mesmo padrão de `Result<T>.Fail` de `EconomyRules`/`PerfRules`).
- WHEN a consulta de Crença é feita para um NPC ainda não materializado (pool agregado, AD-068) THEN o sistema SHALL resolver para o cânone da comunidade/cultura sem criar estado de crença individual (ver Assumptions).
- WHEN um `UPDATE`/`DELETE` é tentado diretamente contra a tabela de fatos por qualquer caminho (inclusive fora do repositório declarado) THEN o sistema SHALL rejeitar ambos no armazenamento (constraint de banco, não só código de aplicação).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| HIST-01 | P1: Fato imutável e sua conversão em relato | Design | Pending |
| HIST-02 | P1: Fato imutável e sua conversão em relato | Design | Pending |
| HIST-03 | P1: Fato imutável e sua conversão em relato | Design | Pending |
| HIST-04 | P1: Fato imutável e sua conversão em relato | Design | Pending |
| HIST-05 | P1: Distorção determinística por salto de transmissão | Design | Pending |
| HIST-06 | P1: Distorção determinística por salto de transmissão | Design | Pending |
| HIST-07 | P1: Distorção determinística por salto de transmissão | Design | Pending |
| HIST-08 | P1: Meios de transmissão e cânone limitado por comunidade | Design | Pending |
| HIST-09 | P1: Meios de transmissão e cânone limitado por comunidade | Design | Pending |
| HIST-10 | P1: Meios de transmissão e cânone limitado por comunidade | Design | Pending |
| HIST-11 | P1: Meios de transmissão e cânone limitado por comunidade | Design | Pending |
| HIST-12 | P1: Livros como objetos do mundo | Design | Pending |
| HIST-13 | P1: Livros como objetos do mundo | Design | Pending |
| HIST-14 | P1: Livros como objetos do mundo | Design | Pending |
| HIST-15 | P1: Duas consultas separadas — Verdade e Crença | Design | Pending |
| HIST-16 | P1: Duas consultas separadas — Verdade e Crença | Design | Pending |
| HIST-17 | P1: Duas consultas separadas — Verdade e Crença | Design | Pending |
| HIST-18 | P1: Duas consultas separadas — Verdade e Crença | Design | Pending |
| HIST-19 | P1: Duas consultas separadas — Verdade e Crença | Design | Pending |
| HIST-20 | P2: Índices de consulta por ano, entidade e tipo | Design | Pending |
| HIST-21 | P2: Índices de consulta por ano, entidade e tipo | Design | Pending |
| HIST-22 | P2: Dinastias e linhagens derivadas, correção por evento compensatório | Design | Pending |
| HIST-23 | P2: Dinastias e linhagens derivadas, correção por evento compensatório | Design | Pending |
| HIST-24 | P2: Dinastias e linhagens derivadas, correção por evento compensatório | Design | Pending |
| HIST-25 | P2: Dinastias e linhagens derivadas, correção por evento compensatório | Design | Pending |
| HIST-26 | P3: Reidratação e replay produzem o mesmo `Hash(world)` | Design | Pending |

**ID format:** `HIST-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 26 total, 26 mapeados para user stories acima, 0 sem mapeamento ⚠️ nenhum

---

## Success Criteria

How we know the feature is successful:

- [ ] O cânone não cresce com o tempo: 50, 100 e 200 anos de mundo têm o mesmo teto de
      relatos vivos por comunidade, sem tendência de crescimento (HIST-10).
- [ ] Orçamento por relato medido em 10 anos fica dentro do baseline de 20 seeds — nenhum
      valor chutado (HIST-11).
- [ ] Colapso é seletivo: 100% dos eventos ≥ limiar sobrevivem íntegros; ≥ X% (do cenário)
      dos abaixo do limiar são colapsados (HIST-02/HIST-05).
- [ ] Consulta de linha do tempo é indexada, provado por contagem de linhas lidas, não por
      milissegundos (HIST-20/HIST-21).
- [ ] `UPDATE`/`DELETE` diretos na tabela de fatos falham ambos, testado por tentativa real
      (HIST-02).
- [ ] Distorção é determinística: mesma seed → relato byte-idêntico em dois processos; fake
      LLM provider falha o teste se chamado durante a distorção (HIST-06/HIST-07).
- [ ] Distância relato↔fato não decrescente ao longo dos saltos; nenhuma queda sem evento de
      redescoberta declarado (HIST-07).
- [ ] Verdade e crença divergem em ao menos um caso do cenário de teste (HIST-19).
- [ ] Nenhum caminho de jogo alcança a consulta de Verdade — enumeração por reflexão sem
      exceção, e o par de mutação (desligar a checagem) faz o critério falhar (HIST-17/HIST-18).
- [ ] Crenças incompatíveis coexistem entre duas comunidades sobre o mesmo fato, cada NPC
      decidindo de forma coerente com a própria crença (HIST-19).
- [ ] Toda morte no esqueleto tem nascimento do mesmo `NpcId` em tick anterior; zero eventos
      após a morte; linhagem reconstruída chega a um fundador sem buraco e sem ciclo
      (HIST-22/HIST-23).
- [ ] Evento compensatório aparece na consulta com a linha original ainda legível e marcada
      (HIST-24).
- [ ] Reidratar um snapshot e reaplicar o log a partir dele reproduz o mesmo `Hash(world)`
      (HIST-26).
