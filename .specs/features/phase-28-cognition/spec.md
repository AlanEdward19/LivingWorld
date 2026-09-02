# Fase 28 — Cognição e LOD observacional — Specification

## Problem Statement

Hoje o utility AI (`behavior.md`, Fase 4) decide sozinho: calcula a nota vencedora e some — nada
fica gravado sobre necessidade dominante, traço aplicado ou opção descartada, e não existe forma
de ver isso pelo NPC no cliente web. Ao mesmo tempo, todo NPC detalhado dentro de uma cidade
observada paga o custo cosmético completo (posição exata, micro-ação, rastro) mesmo dentro de um
prédio que nenhuma fonte está olhando — custo que compete direto com o teto de 10k NPCs/100 anos
da Fase 9. Esta fase resolve os dois problemas com o mesmo princípio: só pagar o custo fino
(rastro auditável, posição exata) onde alguma fonte de fato olha — **nunca** a vida do NPC em si
(envelhecimento, mortalidade, relações, emprego), que continua rodando via Fase 9 igual, observado
ou não. "Mundo vivo" não é opcional nem condicionado a câmera.

## Goals

- [ ] Toda decisão relevante do utility AI grava um rastro estruturado (necessidade dominante,
      traço aplicado, memória consultada, opção descartada) — consultável por API.
- [ ] Painel web "ver o cérebro": selecionar NPC detalhado expõe o rastro em dados e em visual,
      sem recalcular nada — fecha o objetivo técnico #5 do `ROADMAP.md`.
- [ ] LOD por três escopos observacionais (mundo/cidade/interior) reduz o custo da camada
      cosmética (posição exata, micro-ação, rastro) de NPC fora de qualquer enquadramento,
      medido pelo sensor da Fase 9 — sem jamais atrasar, pular ou aproximar um evento de vida.
- [ ] Compressão de estado frio (log de eventos, snapshot histórico, interning de string) reduz
      bytes/NPC/ano contra a linha de base medida na Fase 9.

## Out of Scope

| Feature | Razão |
| --- | --- |
| Traço/emoção nova como input de decisão | Fase 21+ (Realismo humano) — esta fase instrumenta o motor existente, não adiciona eixo novo |
| Fatores biológicos indiretos (metabolismo, fadiga, atividade física, nutrição) como input do motor | Mencionado pelo usuário e explicitamente adiado — registrado como ideia futura em `STATE.md`, não especificado aqui |
| Qualquer efeito extraordinário (Potência/Divindade/Tempo/Cosmos) como entrada de decisão | Fases 16–20 — trilha independente por decisão de `ROADMAP.md` |
| Jogador incarnado como fonte concreta de observação | Fase 25 — esta fase garante que o mecanismo de fontes já suporte mais de uma; implementar o jogador como fonte é da 25, não desta |
| Histórico completo do rastro para todo NPC por padrão | Substituído pelo modelo de marcação (watchlist) — ver Assumptions |
| Novo modelo de edifício/cômodo | Reusa o que a Fase 8 e as specs `city-house-layout`/`real-household-workplace-buildings` já entregam |

---

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-30)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Prioridade dos três eixos | **Cognição é P1** (fecha o objetivo #5 sozinha); **LOD observacional e Compressão são P2** | Usuário confirmou explicitamente — painel "ver o cérebro" é a fatia vertical demonstrável; custo é importante mas não bloqueia o P1 fechar |
| Sandbox de decisão isolado | **P3**, depois do painel | Usuário confirmou explicitamente — reusa o motor do painel; não faz sentido construir antes do rastro básico existir no mundo real |
| Retenção do rastro por NPC | **Padrão: janela curta (ring buffer)**. **NPC marcado ("watchlist") ganha histórico completo comprimido a partir do momento da marcação** — não retroativo | Usuário confirmou explicitamente: quer histórico completo, mas de forma barata; marcar por NPC em vez de por padrão evita estourar o teto de bytes/NPC da Fase 9 para os milhares de NPCs que ninguém está pesquisando |
| Tamanho da janela curta padrão | 50 decisões por NPC (default de cenário, R3 como a Fase 9) | Sem preferência expressa do usuário; valor arbitrado como ponto de partida barato — cenário pode declarar outro |
| Tolerância de equivalência no recompute ao entrar em escopo | **Nenhuma — recompute é exato, não aproximado.** Decaimento usa a fórmula fechada já determinística da Fase 9 (task 5); decisão dependente de RNG usa stream sob demanda da Fase 9 (task 8), reproduzível byte-a-byte pela mesma seed | Consequência direta do desenho já existente da Fase 9 — não é uma decisão nova desta fase, é reconhecer que "aproximação com tolerância" não se aplica aqui |
| Granularidade de interação social em LOD "cidade" (dois NPCs não observados interagindo) | Um evento agregado por mudança de macro-estado (ex.: "família jantou junta"), não por hora | Sem preferência expressa do usuário; segue o mesmo princípio de custo O(decisões) da Fase 9 task 4, não O(NPCs × hora) |
| Escopo do LOD observacional: vida do NPC ou só camada cosmética | **Só camada cosmética** (posição exata, micro-ação, rastro). Eventos de vida (envelhecimento, mortalidade, nascimento, casamento, separação, emprego/demissão) rodam via evento agendado da Fase 9 (task 4), sempre, observado ou não — LOD desta fase nunca os toca | Usuário corrigiu explicitamente: "mundo vivo" é o princípio central do projeto; capar/aproximar vida por falta de observação quebraria isso. Fase 9 já resolve o custo desses eventos como O(decisões), independente de câmera, desde antes desta fase existir |
| Fonte de observação é única ou plural | **Plural desde o início**: lugar em detalhe = união dos escopos de toda fonte ativa. Hoje só a câmera do cliente (Fase 15) existe como fonte; a Fase 25 acrescenta cada jogador incarnado como fonte adicional, sem redesenhar o mecanismo | Usuário corrigiu explicitamente — Fase 25 não "substitui" o observador da Fase 15, os dois coexistem depois dela. Número máximo de lugares simultâneos em detalhe sob custo é aberto (não bloqueia esta spec, ver Edge Cases) |

**Open questions**: nenhuma — todas resolvidas ou registradas acima.

---

## User Stories

### P1: Rastro de decisão gravável ⭐ MVP

**User Story**: Como quem quer entender por que um NPC fez o que fez, quero que toda decisão
relevante do utility AI grave necessidade dominante, traço aplicado, memória consultada e opção
descartada, para que exista dado real por trás de qualquer painel ou pesquisa futura.

**Why P1**: Sem o rastro, não existe o que exibir — é a fundação de tudo que a fase promete.

**Acceptance Criteria**:

1. WHEN o utility AI resolve uma decisão para um NPC no escopo observado (cidade/interior, ver
   LOD-01) THEN o motor SHALL gravar um registro estruturado com necessidade dominante, traço
   aplicado (nome + peso), memória consultada (se houve) e a opção vencedora com sua nota.
2. WHEN a decisão descarta pelo menos uma opção candidata THEN o registro SHALL incluir a opção
   descartada de maior nota e o motivo (nota inferior, indisponibilidade, etc.) — nunca só a
   vencedora.
3. WHEN o NPC não está em escopo observado (agregado ou aproximado) THEN o motor SHALL **não**
   gravar rastro algum para aquela decisão — zero custo adicional fora de observação.
4. WHEN a mesma seed e o mesmo histórico de estímulo rodam duas vezes THEN o rastro gravado
   SHALL ser byte-idêntico entre as execuções.
5. WHEN o rastro de um NPC excede a janela de retenção corrente (curta ou watchlist) THEN o
   registro mais antigo SHALL ser descartado em ordem FIFO, nunca por amostragem aleatória.

**Independent Test**: NPC observado toma 10 decisões seguidas num cenário controlado (fome alta
vs. dinheiro baixo) — API retorna rastro com necessidade dominante e opção descartada coerentes
com `behavior.md`; NPC fora de observação no mesmo tick não produz nenhum registro.

---

### P1: Painel "ver o cérebro" (web)

**User Story**: Como quem está olhando um NPC detalhado no cliente web, quero clicar nele e ver
o motor de decisão — em dados e em visual — para entender o que o levou a agir assim.

**Why P1**: É o objetivo técnico #5 do `ROADMAP.md` — a entrega demonstrável da fase.

**Acceptance Criteria**:

1. WHEN o operador seleciona um NPC detalhado no cliente web THEN a interface SHALL exibir o
   rastro corrente em tabela/timeline (dados), consumindo a API do rastro sem recalcular decisão
   alguma no cliente.
2. WHEN o operador abre a visão visual do mesmo NPC THEN a interface SHALL renderizar o fluxo
   estímulo → ponderação → decisão da última decisão registrada, navegável para decisões
   anteriores dentro da janela retida.
3. WHEN o NPC selecionado não tem nenhum rastro gravado (fora de escopo até o momento da seleção)
   THEN o painel SHALL exibir estado vazio explícito ("sem rastro — fora de observação"), nunca
   dado inventado ou extrapolado.
4. WHEN dois operadores consultam o mesmo NPC no mesmo tick THEN ambos SHALL ver exatamente o
   mesmo rastro — leitura idempotente, sem efeito colateral de consulta.

**Independent Test**: selecionar um NPC recém-observado (sem rastro), depois um NPC com 10
decisões gravadas — painel distingue os dois estados corretamente, e dados batem com o retornado
pela API do rastro.

---

### P1: Retenção por marcação (watchlist)

**User Story**: Como quem quer pesquisa aprofundada em alguns NPCs específicos sem pagar o custo
para todos, quero marcar um NPC para histórico completo comprimido a partir da marcação, mantendo
janela curta como padrão para o resto da população.

**Why P1**: É a decisão de retenção confirmada — sem ela, o painel ou estoura o teto de bytes/NPC
da Fase 9 (histórico completo pra todos) ou não serve pra pesquisa aprofundada (só janela curta).

**Acceptance Criteria**:

1. WHEN nenhum NPC é marcado THEN o motor SHALL manter, para todo NPC com rastro, apenas as
   últimas N decisões (N = janela curta do cenário, default 50) — FIFO.
2. WHEN um NPC é marcado para watchlist THEN a partir do tick da marcação o motor SHALL reter
   **todas** as decisões seguintes, comprimidas (Fase 28 cluster de compressão), sem limite de
   janela — decisões anteriores à marcação **não** são retroativamente recuperadas.
3. WHEN um NPC é removido da watchlist THEN o motor SHALL preservar o histórico já acumulado
   (não descarta o que já gravou) e voltar a aplicar a janela curta dali em diante.
4. WHEN o sensor de escala da Fase 9 roda com M NPCs marcados THEN o custo adicional de bytes
   SHALL ser proporcional a M × decisões-desde-a-marca, nunca à população total.

**Independent Test**: marcar 1 NPC de uma população de 100 — após 5 anos simulados, o marcado tem
histórico integral comprimido desde a marca; os outros 99 têm no máximo 50 decisões retidas.

---

### P2: LOD por três escopos observacionais

**User Story**: Como quem quer sustentar milhares de NPCs, quero que um NPC dentro de um prédio
não observado custe menos que um NPC observado, sem perder consistência quando o jogador entra.

**Why P2**: É o que efetivamente baixa o custo — não bloqueia o painel (P1) fechar, mas é
necessário pro teto de 10k NPCs da Fase 9 valer com o rastro ligado.

**Acceptance Criteria**:

1. WHEN nenhuma fonte de observação enquadra uma cidade/região THEN ela SHALL permanecer na
   resolução agregada de `simulation-lod.md` — comportamento já existente, esta fase não o
   altera.
2. WHEN pelo menos uma fonte de observação enquadra uma cidade THEN NPCs dentro de prédios
   **não** enquadrados por nenhuma fonte SHALL ter só a camada cosmética rebaixada (posição
   aproximada em vez de pathing passo a passo, micro-ação e rastro desligados) — ver tabela
   em `observational-lod.md`. Os eventos de vida do NPC (envelhecimento, mortalidade,
   nascimento, casamento, separação, emprego/demissão) continuam ocorrendo normalmente pelo
   motor de evento da Fase 9 (task 4): LOD desta fase **nunca** os atrasa, pula ou aproxima.
3. WHEN pelo menos uma fonte enquadra um prédio específico THEN todo NPC dentro dele SHALL
   ganhar a camada cosmética completa (posição exata, micro-ação, gravação de rastro — story
   anterior) — os eventos de vida já rodavam iguais, isso não muda com a promoção.
4. WHEN mais de uma fonte de observação está ativa ao mesmo tempo (hoje: múltiplos clientes
   web; depois da Fase 25, também jogadores incarnados) THEN o conjunto de lugares em detalhe
   SHALL ser a união dos escopos de cada fonte — nenhuma fonte rebaixa um lugar que outra fonte
   está observando.
5. WHEN o mesmo NPC transiciona entre os três escopos no mesmo dia simulado THEN o motor SHALL
   nunca manter dois níveis ativos ao mesmo tempo para ele — promoção e rebaixamento são
   mutuamente exclusivos por tick.

**Independent Test**: cidade com 50 NPCs, uma fonte focando um prédio de 5 e outra fonte
focando um prédio diferente de 3 — todos os 50 continuam envelhecendo/trabalhando/decidindo
igual; sensor de custo (P2 seguinte) mostra só a camada cosmética diferindo: os 42 fora de
ambos os prédios com posição aproximada, os 8 dentro (5+3) com posição exata e rastro.

---

### P2: Recompute exato da camada cosmética ao entrar em escopo

**User Story**: Como quem entra num prédio depois de horas fora, quero ver a posição e o
estado exatos que o NPC teria se sua camada cosmética nunca tivesse saído de detalhe pleno —
sem "pulo" perceptível. A vida do NPC (eventos da Fase 9) já rodou igual o tempo todo; aqui só
a aparência/posição estava aproximada.

**Why P2**: É a garantia de correção do LOD acima — sem ela, LOD é só um bug de consistência
disfarçado de otimização.

**Acceptance Criteria**:

1. WHEN um NPC com camada cosmética aproximada é promovido a detalhe pleno (uma fonte passa a
   enquadrá-lo) THEN o motor SHALL recalcular sua posição pela fórmula fechada da Fase 9
   (task 5) a partir da última posição conhecida e da rota — resultado exato, não aproximado.
2. WHEN a micro-ação pendente de um NPC promovido depende de RNG THEN o motor SHALL derivar o
   stream sob demanda da mesma seed raiz (Fase 9 task 8) — sequência idêntica à que teria sido
   consumida se a camada cosmética tivesse rodado em detalhe pleno o tempo todo.
3. WHEN o mesmo cenário roda duas vezes — um braço com o NPC observado o tempo todo, outro com
   a camada cosmética aproximada por um intervalo e depois promovida — THEN a posição e o
   estado cosmético final dos dois braços SHALL ser byte-idênticos no tick de comparação; os
   eventos de vida do NPC (Fase 9 task 4) já eram idênticos nos dois braços desde o início.

**Independent Test**: NPC com camada cosmética aproximada por 6 horas simuladas, depois
promovido — posição recomputada bate exatamente com um NPC de controle que rodou em detalhe
pleno todo o intervalo, mesma seed; os eventos de vida de ambos (idade, necessidades) já eram
idênticos durante todo o intervalo, promovido ou não.

---

### P2: Sensor de custo por escopo

**User Story**: Como quem precisa provar o ganho, quero que o gate meça o custo de NPC observado
vs. aproximado separadamente, para que a fase não feche com ganho alegado e não medido.

**Why P2**: Consistente com a disciplina de "ganho medido, não alegado" já usada na Fase 9.

**Acceptance Criteria**:

1. WHEN o sensor de escala roda THEN ele SHALL reportar µs/NPC-tick separadamente para
   observado (SEMPRE-TICK) e aproximado (LAZY-RECOMPUTE), não uma média única.
2. WHEN o custo do NPC aproximado excede a fração declarada no cenário do custo do NPC
   observado THEN o sensor SHALL reprovar o gate.
3. WHEN o rastro de decisão (P1) está ligado THEN o sensor SHALL confirmar que NPCs fora de
   escopo observado não contribuem para o custo de gravação de rastro (story 1, AC 3).

**Independent Test**: cenário com metade da população observada, metade aproximada — sensor
reporta os dois custos e reprova se a fração ultrapassar o teto declarado.

---

### P2: Compressão de estado frio

**User Story**: Como quem opera o mundo por 100 anos, quero que log de eventos, snapshot
histórico e strings repetidas ocupem menos disco/RAM do que hoje, sem perder round-trip exato.

**Why P2**: Reduz o teto que a Fase 9 mede — necessário pro histórico completo da watchlist
(story 3) não estourar sozinho, mas não bloqueia o painel (P1) fechar.

**Acceptance Criteria**:

1. WHEN um evento sai da janela quente para o arquivo frio (Fase 9 task 7) THEN o motor SHALL
   codificá-lo com delta/dicionário em vez de texto/JSON puro.
2. WHEN um snapshot sai da janela recente THEN o motor SHALL armazená-lo como referência + diff
   do snapshot anterior, não como cópia integral.
3. WHEN duas ou mais entidades compartilham o mesmo valor de string (profissão, tag de evento,
   nome de traço) THEN o motor SHALL armazenar uma única cópia interned e referências, nunca
   strings duplicadas por entidade.
4. WHEN um log/snapshot comprimido é descomprimido THEN o resultado SHALL ser byte-idêntico ao
   estado original — round-trip exato, testável.
5. WHEN a Fase 9 mede bytes/NPC/ano pós-compactação THEN o valor SHALL ficar dentro do teto já
   declarado naquela fase (ou mais apertado, se o cenário desta fase declarar um novo).

**Independent Test**: 10 anos simulados com e sem compressão ligada, mesma seed — bytes em disco
caem, round-trip (comprimir → descomprimir → mundo) produz hash idêntico ao não comprimido.

---

### P3: Sandbox de decisão isolado

**User Story**: Como quem quer testar hipóteses de personalidade/estímulo sem afetar o mundo,
quero um ambiente separado que rode o mesmo motor de decisão com entrada sintética.

**Why P3**: Ferramenta de pesquisa/autoria — valiosa, mas depende do rastro (P1) já existir e não
bloqueia nenhum objetivo do `ROADMAP.md`.

**Acceptance Criteria**:

1. WHEN um operador injeta estímulo/traço sintético no sandbox THEN o motor SHALL produzir uma
   decisão usando exatamente o mesmo pipeline da story 1, sem tocar tick, RNG de mundo ou estado
   de NPC nenhum.
2. WHEN o sandbox roda em paralelo ao mundo principal THEN nenhuma escrita do sandbox SHALL ser
   observável no estado do mundo — isolamento testável (mundo antes/depois do uso do sandbox é
   idêntico).
3. WHEN o mesmo estímulo sintético é injetado duas vezes THEN a decisão resultante SHALL ser
   idêntica — determinismo do sandbox, mesma garantia do mundo principal.

**Independent Test**: rodar o sandbox com 5 combinações de traço/estímulo sintético — mundo
principal permanece com hash inalterado antes/depois; mesma combinação repetida produz mesma
decisão.

## Edge Cases

- WHEN um NPC é marcado para watchlist e imediatamente desmarcado no mesmo tick THEN o motor
  SHALL registrar zero decisões adicionais além da já gravada nesse tick — marcação não retroage
  nem preserva estado "quase marcado".
- WHEN um evento (incêndio, ataque) ocorre numa cidade com o jogador dentro de um prédio
  específico THEN apenas o evento em si SHALL promover os NPCs diretamente envolvidos a
  SEMPRE-TICK — a cidade inteira **não** sobe de escopo automaticamente (registrado como
  assumption; granularidade fina de "evento que afeta LOD" fica para Design).
- WHEN dois NPCs interagem e um está em LAZY-RECOMPUTE enquanto o outro está SEMPRE-TICK THEN o
  motor SHALL resolver a interação promovendo o par ao nível mais alto pela duração da interação,
  nunca simulando metade da interação em cada nível.
- WHEN a janela de retenção curta (50) é atingida no mesmo tick em que uma decisão nova é gravada
  THEN o motor SHALL descartar a mais antiga antes de gravar a nova — nunca ultrapassar N.
- WHEN um NPC watchlisted morre THEN seu histórico completo comprimido SHALL seguir para o
  arquivo frio (Fase 9 task 7) como qualquer outro estado de morto, não é descartado por ter sido
  watchlisted.
- WHEN um evento de vida (morte, nascimento, casamento, separação, emprego/demissão) ocorre para
  um NPC com camada cosmética aproximada (fora de qualquer fonte) THEN o evento SHALL ocorrer no
  tick correto de qualquer forma — só a posição/aparência resultante fica aproximada até uma
  fonte observar; o evento em si nunca espera por observação.
- WHEN o número de lugares em detalhe simultâneos (união de todas as fontes ativas) cresce a
  ponto de o sensor de custo (P2 seguinte) reprovar o teto do cenário THEN o comportamento de
  degradação (recusar nova fonte, rebaixar o lugar menos recente, etc.) **não é definido nesta
  spec** — registrado como decisão em aberto pra Design, sem bloquear o fechamento desta fase
  porque hoje só existe uma fonte (Fase 15) e o cenário não atinge esse teto sozinho.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| COG-01 | P1: Rastro — grava necessidade/traço/memória/descarte em escopo observado | Done |
| COG-02 | P1: Rastro — zero gravação fora de escopo observado | Done |
| COG-03 | P1: Rastro — determinismo por seed | Done |
| COG-04 | P1: Rastro — descarte FIFO ao exceder janela | Done |
| COG-10 | P1: Painel — dados via API, sem recálculo no cliente | Done |
| COG-11 | P1: Painel — visual navegável dentro da janela retida | Done |
| COG-12 | P1: Painel — estado vazio explícito, nunca inventado | Done |
| COG-13 | P1: Painel — leitura idempotente | Done |
| COG-20 | P1: Watchlist — janela curta default para não marcados | Done |
| COG-21 | P1: Watchlist — histórico completo comprimido a partir da marca, não retroativo | Done |
| COG-22 | P1: Watchlist — desmarcar preserva acumulado | Done |
| COG-23 | P1: Watchlist — custo proporcional a marcados, não à população total | Done |
| LOD-01 | P2: Escopos — mundo inalterado (comportamento já existente) | Done |
| LOD-02 | P2: Escopos — cidade rebaixa prédio não enquadrado a LAZY | Done |
| LOD-03 | P2: Escopos — interior enquadrado roda SEMPRE-TICK com rastro | Done |
| LOD-04 | P2: Escopos — lugar em detalhe é união dos escopos de toda fonte ativa | Done |
| LOD-05 | P2: Escopos — nunca dois níveis ativos simultâneos | Done |
| LOD-10 | P2: Recompute — fórmula fechada exata na promoção | Done |
| LOD-11 | P2: Recompute — RNG sob demanda reproduz sequência idêntica | Done |
| LOD-12 | P2: Recompute — braços LAZY e SEMPRE-TICK convergem byte-idêntico | Done |
| LOD-20 | P2: Sensor — custo observado vs. aproximado reportado separadamente | Done |
| LOD-21 | P2: Sensor — reprova acima da fração declarada | Done |
| LOD-22 | P2: Sensor — confirma zero custo de rastro fora de escopo | Done |
| CMP-01 | P2: Compressão — log frio delta/dicionário | Done |
| CMP-02 | P2: Compressão — snapshot histórico como diff | Done |
| CMP-03 | P2: Compressão — interning de string compartilhada | Done |
| CMP-04 | P2: Compressão — round-trip byte-idêntico | Done |
| CMP-05 | P2: Compressão — bytes/NPC/ano dentro do teto da Fase 9 | Done |
| SBX-01 | P3: Sandbox — mesmo motor, sem tocar mundo | Done |
| SBX-02 | P3: Sandbox — isolamento de escrita testável | Done |
| SBX-03 | P3: Sandbox — determinismo do sandbox | Done |

**Coverage**: 30 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Painel "ver o cérebro" exibe rastro real de um NPC observado, dados e visual consistentes
      entre si — objetivo técnico #5 do `ROADMAP.md` demonstrável.
- [ ] NPC fora de escopo observado tem custo de rastro exatamente zero (medido, não alegado).
- [ ] NPC watchlisted acumula histórico completo comprimido; população geral permanece em janela
      curta — custo total proporcional a marcados, não à população.
- [ ] Sensor de escala (Fase 9) reporta observado vs. aproximado separadamente e reprova fora do
      teto declarado pelo cenário.
- [ ] Recompute ao entrar em escopo produz estado byte-idêntico ao de simulação contínua na
      mesma seed — zero "pulo" perceptível.
- [ ] Nenhum evento de vida (morte, nascimento, casamento, separação, emprego/demissão) atrasa,
      pula ou se aproxima por falta de observação — taxa/tempo idêntico com e sem fonte olhando,
      10/10 seeds. Este é o critério que mais importa: "mundo vivo" nunca é condicional a câmera.
- [ ] Compressão de log/snapshot/string reduz bytes/NPC/ano contra a linha de base da Fase 9 sem
      perder round-trip exato.
- [ ] Sandbox de decisão roda sem alterar hash do mundo principal antes/depois do uso.
- [ ] `bash scripts/verify.sh` em 0 com todas as suítes desta fase.
