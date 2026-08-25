# Fase 18 — Linhas temporais — Specification

## Problem Statement

Viagem no tempo hoje não existe no motor. Esta fase introduz o único modelo compatível com os
invariantes já fixados (determinismo, log append-only, hash canônico): viajar não reescreve — cria
uma linha nova a partir de um snapshot, copy-on-write, com seed derivada. A linha-mãe segue
existindo, intocada e indiferente. `BranchId` já está no esquema desde a Fase 3 (ADR-0009); esta
fase o usa pela primeira vez de verdade.

## Goals

- [ ] Um salto temporal é um evento anexado ao log da linha de origem — nenhum `UPDATE`, nenhuma
      reescrita, carregando tick alvo, intervenção pretendida e resultado da rolagem, inclusive
      falha.
- [ ] O branch resultante referencia o snapshot da mãe no tick de divergência e grava só o que
      diverge (copy-on-write) — custo proporcional à divergência, nunca ao tamanho do mundo.
- [ ] `seed_B = H(seed_A, tick_divergência, id_intervenção)` — função pura, estável entre
      processos e versões, sem teto de profundidade (branch de branch usa a mesma função
      encadeada).
- [ ] A rolagem do salto usa o primitivo único (ADR-0011, perfil `Dramático`), consumindo uma
      stream de RNG própria da linha-mãe (`"timeline-jump"`), com dificuldade calculada 100% pelo
      modelo de inércia histórica da Fase 10 — nenhuma fórmula nova de dificuldade.
- [ ] Os 5 níveis de resultado do `Resolver` (`CriticalFailure`/`Failure`/`PartialSuccess`/
      `Success`/`CriticalSuccess`) mapeiam para os modos de falha do domínio sem primitivo de
      decisão adicional: `CriticalFailure` = branch natimorto, `Failure` = nada abre, sucessos
      parciais/totais têm consequência declarada (chegada no tick errado, chegada danificada,
      máquina consumida, viajante preso sem retorno).
- [ ] Um branch persiste enquanto tiver âncora (habitante, viajante, artefato, consequência
      pendente); sem âncora, é coletado — e a coleta é ela própria um evento no log (entra no
      hash canônico), determinística e ordenada, nunca varredura oportunista.
- [ ] Teto de branches vivos declarado no cenário; a árvore de branches é consultável
      (somente leitura, API/CLI) com origem, tick de divergência, intervenção, âncoras e estado.
- [ ] Um viajante que chega num branch é sempre materializado como `Npc` completo (Fase 8),
      sujeito à mesma simulação LOD normal da Fase 9 dali em diante — nenhum caso especial de
      "meio-materializado" no motor de branch.

## Out of Scope

| Item | Razão |
| --- | --- |
| Voltar à linha de origem, catch-up de branch dormente, relógio por branch | Fase 20 (Trânsito interdimensional). |
| Fusão de branches | Não existe, por decisão (ADR-0008) — perda é permanente. |
| Contato cósmico, degrau alienígena | Fase 19 (Cosmos). |
| A potência que habilita o salto em si (custo/rolagem/eixos do `PowerDescriptor` do viajante) | Fase 16 — esta fase consome um resultado de invocação já resolvido, não define a mecânica de poder. |
| Culto que venera uma linha perdida | Fase 17 (Divindade) — reusa este mecanismo depois de pronto. |
| Prosa sobre o viajante e sobre a linha abandonada | Fase 12 (Narrativa). |
| Teto de profundidade de ramificação | Decisão explícita do usuário — sem teto; a linhagem de seed encadeada já aguenta. |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Profundidade de ramificação | **Sem teto** — branch de branch usa a mesma `seed_B = H(seed_A, ...)` encadeada; árvore/coleta/debug tratam profundidade como só mais um campo consultável | Usuário confirmou explicitamente (Recommended) |
| Coleta de branch | **Evento no log da mãe** — entra no hash canônico, testado com a mesma disciplina de determinismo do resto do motor | Usuário confirmou explicitamente — consistente com ADR-0006 ("tudo é evento") |
| Stream de RNG da rolagem do salto | **Stream própria da linha-mãe** (`world.Rng.Stream("timeline-jump")`) — nunca deriva de `NpcId` do viajante/alvo | Usuário confirmou explicitamente — mantém A determinística e isolada, RNG de sistema separado de RNG de entidade |
| Falha vs. falha crítica do salto | **Distintos, mapeados direto nos 5 níveis do `Resolver`** (`CriticalFailure`=natimorto, `Failure`=nada abre) — zero primitivo de decisão novo | Usuário confirmou explicitamente — reusa ADR-0011 sem extensão |
| Materialização do viajante | **Sempre `Npc` completo desde a chegada** — pode virar agregado depois via LOD normal da Fase 9, sem caso especial no motor de branch | Usuário confirmou explicitamente (Recommended) — evita decidir "o que sobra da identidade" em estado parcial |

**Todas as 5 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: Salto como evento anexado, mãe intocada

**User Story**: Como quem quer viagem no tempo sem quebrar determinismo/log/hash, quero que um
salto seja só mais um evento anexado ao log da linha de origem — nunca uma reescrita — de forma
que a linha-mãe permaneça byte-idêntica para sempre, independente do que aconteça em qualquer
branch dela.

**Why P1**: É o critério que sustenta a fase inteira — sem ele, nenhuma outra história importa.

**Acceptance Criteria**:

1. WHEN um salto temporal é solicitado THEN o motor SHALL anexar ao log da linha de origem um
   evento carregando tick alvo, intervenção pretendida e resultado da rolagem — nunca emitir um
   `UPDATE`/reescrita em qualquer registro existente.
2. WHEN o hash canônico da linha-mãe é capturado no tick de divergência THEN nenhuma ação
   subsequente no branch resultante (ticks, mortes, guerra, coleta do próprio branch) SHALL
   alterá-lo — assert a cada tick em 10 anos simulados no gate; uma única divergência reprova a
   fase inteira.
3. WHEN uma escrita retroativa real é tentada no log da linha-mãe (fora do fluxo de evento
   anexado) THEN o motor SHALL retornar `Failure` e `Hash(mãe)` SHALL permanecer inalterado.
4. WHEN a proteção contra escrita retroativa é desligada por flag de teste (par de mutação) THEN
   o critério do AC anterior SHALL falhar — provando que o teste de fato detecta a proteção, não
   é tautologia.

**Independent Test**: capturar hash de A no tick 500; simular 10 anos em B (branch de A) com
mortes, guerra e eventual coleta de sub-branches; hash de A permanece idêntico ao capturado.

---

### P1: Branch por copy-on-write com seed derivada

**User Story**: Como quem quer que branches sejam baratos, quero que um branch referencie o
snapshot da mãe e grave só a divergência, com seed derivada deterministicamente da seed da mãe,
do tick de divergência e da intervenção — sem teto de profundidade.

**Why P1**: É o mecanismo central que torna "mil branches" viável sem explodir armazenamento.

**Acceptance Criteria**:

1. WHEN um branch é criado a partir do tick T de uma linha de origem THEN o motor SHALL
   reidratar o estado a partir do snapshot de T (mesma mecânica de replay determinístico da
   Fase 1/3) e persistir apenas os campos/entidades que divergem dali em diante.
2. WHEN a seed do branch é calculada THEN ela SHALL ser exatamente
   `H(seed_origem, tick_divergência, id_intervenção)` — função pura, sem dependência de estado
   mutável, estável entre processos e versões do motor.
3. WHEN um branch é criado a partir de outro branch (não a linha raiz) THEN a mesma função de
   derivação SHALL se aplicar recursivamente (`seed_neto = H(seed_filho, ...)`) sem limite de
   profundidade e sem tratamento especial no motor.
4. WHEN a mesma origem, mesmo tick e mesma intervenção são usados em dois processos separados
   THEN o hash canônico do branch resultante SHALL ser idêntico entre os dois processos —
   repetir no mesmo processo não é suficiente prova.
5. WHEN o número de mutações é fixado pelo cenário e a população da linha-mãe varia pelos
   tamanhos declarados THEN o armazenamento do branch SHALL permanecer dentro do baseline de 20
   seeds em `tests/baselines/` — custo que acompanha o tamanho do mundo (em vez da divergência)
   reprova.

**Independent Test**: criar branch B a partir do tick 500 de A; medir armazenamento de B com
população de A em 3 tamanhos diferentes (mesmas mutações fixas) — armazenamento de B não varia
com o tamanho de A.

---

### P1: Rolagem contra a inércia histórica, sem fórmula nova

**User Story**: Como quem quer que o passado resista a mudança proporcionalmente à sua
importância, quero que a rolagem do salto use o primitivo único de resolução (ADR-0011) com
dificuldade 100% derivada do modelo de história degradável já existente (significância,
testemunhas, densidade de registro escrito, grau causal) — nenhuma fórmula de dificuldade nova.

**Why P1**: Preserva "matar um camponês anônimo é barato; impedir a fundação de Valen é quase
impossível" como propriedade emergente, não roteirizada.

**Acceptance Criteria**:

1. WHEN a dificuldade de um salto é calculada THEN o motor SHALL usar exatamente
   `f(significância, nº testemunhas, densidade de registro escrito, grau causal)` já calculado
   pelo modelo da Fase 10 — nenhum termo adicional introduzido por esta fase.
2. WHEN a rolagem do salto é executada THEN ela SHALL consumir `world.Rng.Stream("timeline-jump")`
   sobre a linha-mãe (ADR-0011, perfil `Dramático`) — nunca uma stream derivada de `NpcId` do
   viajante ou do alvo.
3. WHEN quatro pares base/tratamento na mesma seed elevam, cada um, **um único** fator
   (significância, testemunhas, registro escrito, grau causal) THEN a taxa de sucesso do salto no
   braço tratado SHALL ser menor que no braço base em 10/10 seeds — variar dois fatores juntos
   não é uma prova válida do teste.

**Independent Test**: par base/tratamento variando só "nº de testemunhas" do alvo — taxa de
sucesso do braço tratado (mais testemunhas) menor que o base em 10 seeds distintas.

---

### P1: Modos de falha mapeados nos 5 níveis do Resolver

**User Story**: Como quem desenha o resultado de um salto, quero que os 5 níveis de resultado já
existentes do `Resolver` (ADR-0011) cubram todos os modos de falha do domínio sem inventar um
segundo primitivo de decisão — sucesso parcial é resultado de primeira classe.

**Why P1**: Reuso disciplinado — evita duplicar a lógica de resolução que ADR-0011 já centraliza.

**Acceptance Criteria**:

1. WHEN a rolagem do salto resulta em `CriticalFailure` THEN o motor SHALL abrir um branch
   natimorto (existe no log, mas nunca ganha estado habitável/viajante vivo) — nunca "nenhum
   branch".
2. WHEN a rolagem resulta em `Failure` THEN o motor SHALL não abrir nenhum branch — evento de
   falha é anexado ao log da mãe (AC da história de "salto como evento anexado"), mas nenhum
   `BranchId` novo é criado.
3. WHEN a rolagem resulta em `PartialSuccess` THEN o motor SHALL abrir o branch com uma
   consequência declarada no cenário (chegada no tick errado, chegada danificada, máquina
   consumida no processo, viajante preso sem retorno) — nunca um branch "limpo" nesse nível.
4. WHEN a rolagem resulta em `Success` ou `CriticalSuccess` THEN o motor SHALL abrir o branch sem
   consequência negativa declarada (o viajante chega íntegro) — `CriticalSuccess` pode conceder
   um benefício adicional declarado no cenário, opcional.
5. WHEN qualquer um dos 5 níveis ocorre THEN o resultado exato (nível + consequência, se houver)
   SHALL ser reproduzível para a mesma seed/origem/tick/intervenção entre execuções.

**Independent Test**: forçar (via seed controlada) cada um dos 5 níveis em cenário de teste —
cada um produz o comportamento declarado acima, sem exceção.

---

### P1: Âncora e coleta como evento determinístico

**User Story**: Como quem quer que branches não acumulem sem limite, quero que um branch sem
nenhuma âncora (habitante, viajante, artefato, consequência pendente) seja coletado, e que a
coleta seja ela própria um evento anexado ao log — determinística, ordenada, sujeita ao mesmo
teste de determinismo de qualquer outro evento.

**Why P1**: É o mecanismo que torna o custo de "mil branches" estável, e a decisão do usuário
(coleta = evento) exige que ela participe do hash canônico como qualquer outra mutação.

**Acceptance Criteria**:

1. WHEN um branch perde sua última âncora THEN o motor SHALL coletá-lo em no máximo `K` ticks
   (`K` declarado no cenário) — assert a cada tick em 10 anos simulados no gate.
2. WHEN a coleta ocorre THEN o motor SHALL anexar um evento de coleta ao log (da mãe ou da
   própria linha coletada, conforme Design decidir a topologia) — o evento entra no hash
   canônico e é reproduzível entre execuções, mesma seed.
3. WHEN o teto de branches vivos declarado no cenário é atingido THEN a coleta de branches sem
   âncora SHALL ser priorizada de forma determinística e ordenada (nunca varredura oportunista/
   não-determinística) até o total voltar ao teto ou abaixo dele.
4. WHEN o total de branches vivos é medido ao longo de 50, 100 e 200 anos simulados (nightly)
   THEN a regressão linear do total contra o tempo SHALL não ter inclinação positiva — cenário
   com âncoras controladas não deve crescer sem limite.

**Independent Test**: branch sem âncora coletado em ≤ K ticks; branch com uma consequência
pendente sobrevive além de K; teste nightly de 50-200 anos confirma platô do total de branches
vivos.

---

### P1: Árvore de branches consultável, somente leitura

**User Story**: Como quem opera/debuga o mundo, quero consultar a árvore de branches (origem,
tick de divergência, intervenção, âncoras, estado) por API/CLI, sem instrumentar o motor por
dentro pra saber o que existe.

**Why P1**: Torna coleta e custo mensuráveis de fora — pré-requisito pros próprios critérios de
verificação desta fase (teto de branches vivos, custo por branch).

**Acceptance Criteria**:

1. WHEN a árvore de branches é consultada THEN a resposta SHALL incluir, por branch: `BranchId`,
   origem (`BranchId` da mãe ou raiz), tick de divergência, intervenção que o originou, âncoras
   ativas, e estado (`Alive`/`Collected`/`Stillborn`).
2. WHEN um branch é filho de outro branch (profundidade > 1) THEN a árvore SHALL expor a cadeia
   completa até a raiz — nenhuma limitação de profundidade na consulta.
3. WHEN a consulta é feita via CLI e via API THEN ambas SHALL retornar dados consistentes (mesmo
   modelo subjacente, sem drift entre os dois canais).
4. WHEN a árvore é consultada THEN nenhuma escrita SHALL ocorrer como efeito colateral — consulta
   é somente leitura, mesma disciplina de inspeção já usada na Fase 8.

**Independent Test**: árvore com 3 gerações de branch (raiz → filho → neto) consultada via CLI e
API retorna a mesma cadeia completa nos dois canais, sem mutar estado.

---

### P1: Viajante sempre materializado como Npc completo

**User Story**: Como quem quer consistência com o resto do motor, quero que o viajante que chega
num branch novo seja sempre um `Npc` completo desde a chegada — sujeito à mesma simulação LOD
normal da Fase 9 dali em diante — sem estado especial de "meio-materializado".

**Why P1**: Decisão explícita do usuário — evita um segundo modelo de identidade paralelo ao que
a Fase 8/9 já resolvem.

**Acceptance Criteria**:

1. WHEN um salto bem-sucedido (Success/CriticalSuccess/PartialSuccess) abre um branch THEN o
   viajante SHALL ser materializado como `Npc` completo no branch, com identidade, atributos e
   conhecimento preservados do momento do salto (sujeitos à consequência declarada, se
   `PartialSuccess`).
2. WHEN o branch onde o viajante está não tem observador ativo (LOD baixo) THEN o viajante SHALL
   ser simulado pela mesma mecânica de agregação da Fase 9 usada para qualquer NPC — nenhum
   tratamento especial por ser viajante.
3. WHEN o viajante retorna a atenção plena (observado novamente) THEN seu estado SHALL ser
   consistente com a simulação agregada que ocorreu enquanto não observado — mesma garantia já
   dada pela Fase 9 para NPCs comuns.

**Independent Test**: viajante materializado num branch sem observação por 20 anos simulados
(LOD agregado) — ao ser observado de novo, estado é coerente (idade avançou, eventos agregados
aplicados), sem exceção lançada por caminho "viajante" não coberto.

## Edge Cases

- WHEN um salto é solicitado para um tick no futuro da linha corrente (ainda não simulado) THEN
  o motor SHALL rejeitar com falha declarada — branch só pode divergir de um tick já simulado
  (snapshot existente).
- WHEN dois saltos concorrentes divergem do mesmo tick T da mesma linha THEN cada um SHALL
  produzir um `BranchId` distinto — nenhuma colisão de identidade entre branches irmãos.
- WHEN um branch natimorto (`CriticalFailure`) nunca ganha âncora THEN ele SHALL ser elegível
  para coleta imediata (mesma regra de "sem âncora, coletado em ≤ K ticks"), nunca persistir
  indefinidamente só por ter sido criado.
- WHEN o teto de branches vivos do cenário é atingido e um novo salto bem-sucedido ocorreria
  THEN o Design SHALL declarar o comportamento (recusar o salto, ou coletar um candidato
  elegível antes de admitir o novo) — resolvido na fase de Design, não deixado implícito.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| TML-01 | P1: Salto — evento anexado, nunca UPDATE | Pending |
| TML-02 | P1: Salto — hash da mãe intocado em 10 anos de atividade em B | Pending |
| TML-03 | P1: Salto — escrita retroativa real retorna Failure, hash intocado | Pending |
| TML-04 | P1: Salto — par de mutação: desligar proteção derruba o critério | Pending |
| TML-10 | P1: Branch — copy-on-write a partir do snapshot de T | Pending |
| TML-11 | P1: Branch — seed derivada `H(seed, tick, intervenção)` | Pending |
| TML-12 | P1: Branch — derivação recursiva sem teto de profundidade | Pending |
| TML-13 | P1: Branch — hash idêntico entre 2 processos separados | Pending |
| TML-14 | P1: Branch — armazenamento proporcional à divergência, não ao mundo | Pending |
| TML-20 | P1: Rolagem — dificuldade só usa o modelo já existente da Fase 10 | Pending |
| TML-21 | P1: Rolagem — stream `"timeline-jump"` da linha-mãe | Pending |
| TML-22 | P1: Rolagem — 4 pares fator-a-fator, 10/10 seeds cada | Pending |
| TML-30 | P1: Falha — CriticalFailure = branch natimorto | Pending |
| TML-31 | P1: Falha — Failure = nenhum branch aberto | Pending |
| TML-32 | P1: Falha — PartialSuccess sempre com consequência declarada | Pending |
| TML-33 | P1: Falha — Success/CriticalSuccess sem consequência negativa | Pending |
| TML-34 | P1: Falha — resultado reproduzível pela mesma seed | Pending |
| TML-40 | P1: Coleta — sem âncora, coletado em ≤ K ticks | Pending |
| TML-41 | P1: Coleta — coleta é evento no log, entra no hash | Pending |
| TML-42 | P1: Coleta — priorização determinística e ordenada no teto | Pending |
| TML-43 | P1: Coleta — regressão de branches vivos sem inclinação positiva (nightly) | Pending |
| TML-50 | P1: Árvore — consulta expõe origem/tick/intervenção/âncoras/estado | Pending |
| TML-51 | P1: Árvore — cadeia completa até a raiz, sem limite de profundidade | Pending |
| TML-52 | P1: Árvore — CLI e API consistentes | Pending |
| TML-53 | P1: Árvore — consulta somente leitura, sem efeito colateral | Pending |
| TML-60 | P1: Viajante — materializado como Npc completo desde a chegada | Pending |
| TML-61 | P1: Viajante — sujeito à mesma agregação LOD da Fase 9 | Pending |
| TML-62 | P1: Viajante — estado coerente ao retomar observação | Pending |

**Coverage**: 28 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Mãe fica byte-idêntica: hash de A intocado por 10 anos de atividade em B, assert a cada
      tick no gate; 100 anos em nightly.
- [ ] `UPDATE` retroativo real na mãe é transição rejeitada, com par de mutação provando que o
      teste detecta a proteção.
- [ ] Ramificação reprodutível entre 2 processos separados (mesma origem/tick/intervenção → hash
      idêntico).
- [ ] Custo do branch proporcional à divergência: dentro do baseline de 20 seeds mesmo variando
      o tamanho da população da mãe.
- [ ] Sem âncora, sem branch: coleta em ≤ K ticks, teto de branches vivos respeitado, sem
      inclinação positiva em 50/100/200 anos nightly.
- [ ] Inércia resiste fator a fator: 4 pares base/tratamento, 10/10 seeds cada, direção correta.
- [ ] Ramificação entrou na conta: desligar o subsistema muda o hash canônico em 10 anos.
- [ ] `dotnet test` completo sem regressão nas suítes `History*`/`Extraordinary*`/`Population*`.
