# Fase 20 — Trânsito interdimensional e catch-up — Specification

## Problem Statement

A Fase 18 dá a um viajante o poder de sair da linha-mãe pra um branch novo, mas nunca de voltar.
Esta fase fecha o ciclo: branch é dimensão, e quem desenvolve trânsito volta pra qualquer linha —
inclusive a de origem, que precisa ter "existido" enquanto ninguém olhava. A resposta (ADR-0012)
é congelar branches dormentes e simular sob demanda: como o mundo é função pura de
`(seed, estado, ticks)`, simular tarde produz exatamente o mesmo resultado que simular na hora.
Preguiça aqui não é aproximação — é o mesmo mundo, mais barato.

## Goals

- [ ] Cada branch guarda `simuladoAté` — único ponto de verdade sobre até onde a linha existe;
      avançado só por catch-up concluído (parcial incluso, ver decisão de orçamento).
- [ ] Catch-up sob demanda: `T <= simuladoAté` é caminho sem trabalho — zero ticks executados,
      hash inalterado.
- [ ] Relógio próprio por branch — "agora" é por linha, nenhuma consulta assume relógio global.
- [ ] `LOD(branch, tick)` é função pura do registro de presença append-only — resolução é
      definição do mundo, nunca otimização; degradação é definitiva (re-simular em fidelidade
      maior é transição rejeitada).
- [ ] Cache do catch-up é append-only — nunca refeito, nunca sobrescrito, mesma disciplina do log
      (ADR-0006).
- [ ] Pré-aquecimento em background de branches ancorados, fora do caminho crítico do tick, sem
      efeito sobre o resultado — só sobre quando fica pronto (bit-idêntico ao catch-up sob
      demanda).
- [ ] Orçamento de catch-up declarado no cenário; estourar o orçamento é `PartialSuccess`
      explícito — `simuladoAté` avança até onde deu, cache append-only preserva o progresso,
      próxima chamada continua dali.
- [ ] Trânsito é invocação de potência (Fase 16): custo cobrado no uso, rolagem pelo primitivo
      único (ADR-0011, perfil `Dramático`), modos de falha com consequência.
- [ ] Chegada em linha onde o viajante já tem uma contraparte independente: identidades distintas
      com laço explícito — nunca fusão, nunca substituição silenciosa. Retorno à própria linha de
      origem (de onde ele saiu) nunca produz duplicata, porque ele estava ausente dela, não
      vivendo nela em paralelo.
- [ ] Viajante conta como âncora da linha de origem mesmo ausente — a linha nunca é coletada
      (Fase 18) enquanto ele existir em qualquer lugar com referência de origem nela.

## Out of Scope

| Item | Razão |
| --- | --- |
| Criação de branch, âncora, coleta, inércia histórica do salto | Fase 18 — esta fase consome `BranchId`/âncora/coleta já definidos, nunca reintroduz. |
| Fusão de linhas | Não existe, por decisão (ADR-0008). |
| Contato e escala cósmica | Fase 19. |
| A potência genérica que o trânsito instancia (eixos/custo/rolagem do `PowerDescriptor`) | Fase 16 — aqui só consome um resultado de invocação já resolvido. |
| Prosa sobre o retorno | Fase 12 (Narrativa). |
| Gate de custo de trânsito | Explicitamente sem gate (ADR-0010) — balanceamento é regra de cenário. |
| Chegada em linha onde o viajante já existe por ter sido ELE MESMO quem ficou (não uma contraparte independente) | Esse caso é simplesmente "ele voltou pra própria linha" — coberto pelo catch-up normal, nunca gera duplicata (ver decisão de identidade). |

## Assumptions & Decisions (confirmadas com o usuário — 2026-08-25)

| Decisão | Escolha confirmada | Racional |
| --- | --- | --- |
| Identidade duplicada | **Identidades distintas com laço explícito, MAS só quando existe uma contraparte que viveu independentemente na linha de destino** (ex.: viajar pro passado de uma linha onde ele já existia). Retornar à própria linha de origem (a que ele deixou ao partir) NUNCA gera duplicata — ele estava ausente dela, catch-up simplesmente a traz até `T`, sem ninguém ocupando o lugar dele nesse meio-tempo | Usuário especificou exatamente esta distinção, com o exemplo: viajar no tempo em A gera branch B; passar 10 anos em B e voltar a A dentro da janela de ausência não duplica (ele "sumiu" de A ao partir); viajar para o PASSADO de uma linha onde já existia gera as duas identidades com laço |
| Orçamento estourado | **`PartialSuccess` — `simuladoAté` avança até onde deu**, cache append-only preserva o progresso, próxima chamada continua dali (nunca refaz trabalho pago) | Usuário confirmou explicitamente (Recommended) — consistente com "preguiça não é aproximação, é o mesmo resultado mais barato" |
| Âncora do viajante ausente | **Conta como âncora mesmo ausente** — enquanto o viajante existir (em qualquer linha) com referência de origem em A, A tem âncora; "não há pra onde voltar" nunca acontece por coleta automática | Usuário confirmou explicitamente (Recommended) — consistente com âncora = "consequência pendente" já listada na Fase 18 |

**Todas as 3 decisões estão confirmadas** — nenhuma pendência bloqueando Design.

## User Stories

### P1: `simuladoAté` como único ponto de verdade, catch-up sem trabalho redundante

**User Story**: Como quem quer branches baratos, quero que cada linha guarde exatamente até onde
foi calculada, e que voltar a um tick já coberto não execute nenhum tick — zero retrabalho
disfarçado de cache.

**Why P1**: É a fundação de custo de toda a fase — sem ela, "preguiçoso" é só um nome.

**Acceptance Criteria**:

1. WHEN um branch é consultado por `simuladoAté` THEN o valor retornado SHALL ser exatamente o
   tick até onde o catch-up (completo ou parcial concluído) já avançou aquela linha — nunca um
   valor estimado ou aproximado.
2. WHEN um catch-up é solicitado para `T <= simuladoAté` THEN o motor SHALL executar zero ticks
   (contagem instrumentada == 0) e o hash canônico SHALL permanecer inalterado — um único tick
   executado reprova.
3. WHEN um catch-up avança `simuladoAté` (completo ou parcial) THEN o avanço SHALL ser
   persistido de forma append-only — nunca sobrescrito, nunca refeito numa chamada
   subsequente.

**Independent Test**: branch com `simuladoAté=1000`; catch-up solicitado até tick 500 executa 0
ticks; catch-up solicitado até tick 1500 avança normalmente e `simuladoAté` passa a refletir o
novo valor.

---

### P1: Preguiçoso == eager, dado o mesmo registro de presença

**User Story**: Como quem sustenta o ADR-0012 inteiro, quero que simular um branch direto até T e
simular em dois lances (até T/2, depois até T) produzam hash canônico idêntico — em processos
separados — porque isso é o que torna congelar branches dormentes seguro.

**Why P1**: É o critério que sustenta a fase inteira — falhando isto, "congelar branch dormente"
cai junto.

**Acceptance Criteria**:

1. WHEN o mesmo registro de presença é fixado pelo cenário THEN simular um branch direto até T e
   simular em dois lances (até T/2, depois até T) SHALL produzir hash canônico byte-idêntico —
   comparado em **dois processos separados** (repetir no mesmo processo não é prova válida).
2. WHEN o registro de presença varia entre os dois cenários comparados THEN a comparação NÃO
   SHALL ser usada como prova de `preguiçoso == eager` — a cláusula "dado o mesmo registro de
   presença" é condição necessária do teste, nunca implícita.

**Independent Test**: `test-catchup` pareado — branch simulado eager até T vs. em 2 lances,
mesmo registro de presença fixado — hash idêntico em 2 processos.

---

### P1: Relógio próprio por branch, LOD como definição do mundo

**User Story**: Como quem quer linhas genuinamente independentes, quero que cada branch tenha
"agora" próprio (nenhuma consulta assume relógio global), e que a resolução em que um tick foi
simulado seja definitiva — nunca uma escolha de otimização revertível.

**Why P1**: Formaliza "o detalhe que ninguém viu nunca existiu" (ADR-0007/ADR-0012) — sem isso,
degradação vira bug em vez de verdade da linha.

**Acceptance Criteria**:

1. WHEN qualquer consulta temporal é feita a um branch THEN ela SHALL resolver "agora" a partir
   do relógio daquele `BranchId` específico — nenhum caminho de código assume um relógio global
   compartilhado entre linhas.
2. WHEN um intervalo de um branch já foi simulado numa resolução `L` THEN uma tentativa de
   re-simular esse mesmo intervalo em fidelidade maior SHALL retornar `Failure` (transição
   rejeitada) e deixar o mundo byte-idêntico depois da tentativa.
3. WHEN `LOD(branch, tick)` é consultado THEN o valor SHALL ser função pura do registro de
   presença append-only até aquele tick — nunca um parâmetro escolhido pelo chamador do
   catch-up.

**Independent Test**: intervalo simulado em resolução agregada; tentativa de forçar
re-simulação em detalhe máximo retorna `Failure`, hash inalterado antes/depois da tentativa.

---

### P1: Pré-aquecimento bit-idêntico ao catch-up sob demanda

**User Story**: Como quem quer branches ancorados prontos sem travar o jogador, quero que
pré-aquecimento em background produza exatamente o mesmo resultado que catch-up sob demanda
produziria — a diferença é só quando o resultado fica disponível, nunca o que ele é.

**Why P1**: Garante que otimizar performance (pré-aquecer) nunca inventa fidelidade.

**Acceptance Criteria**:

1. WHEN um branch ancorado é pré-aquecido em background THEN o processo SHALL ficar fora do
   caminho crítico do tick corrente — nenhuma consulta síncrona espera pelo pré-aquecimento.
2. WHEN o mesmo branch é pré-aquecido em background E alcançado sob demanda (em cenários
   pareados idênticos) THEN os dois caminhos SHALL produzir hash canônico idêntico — qualquer
   divergência prova fidelidade inventada pelo pré-aquecimento.

**Independent Test**: par de cenários idênticos — um com pré-aquecimento em background
concluído antes da visita, outro sem pré-aquecimento (catch-up puro sob demanda) — hash final
idêntico nos dois.

---

### P1: Orçamento de catch-up com resultado explícito

**User Story**: Como quem quer catch-up longo controlável, quero que o motor respeite um teto de
trabalho por chamada declarado no cenário, reporte progresso, e que estourar o orçamento seja um
resultado explícito (`PartialSuccess`) — nunca travamento silencioso nem descarte de trabalho já
pago.

**Why P1**: Sem orçamento explícito, a primeira visita a um branch muito antigo pode travar o
jogador indefinidamente.

**Acceptance Criteria**:

1. WHEN um catch-up excede o teto de trabalho declarado no cenário THEN o motor SHALL retornar
   `PartialSuccess`, com `simuladoAté` avançado até onde o orçamento permitiu — nunca `Failure`
   descartando o progresso feito.
2. WHEN o mesmo branch recebe uma nova chamada de catch-up após um `PartialSuccess` anterior
   THEN o trabalho SHALL continuar exatamente de onde parou (a partir do `simuladoAté`
   persistido) — nenhum tick já computado é refeito.
3. WHEN um catch-up longo está em andamento THEN o motor SHALL expor progresso consultável
   (ticks processados / ticks totais estimados) — nunca uma chamada opaca sem visibilidade.
4. WHEN o custo de um catch-up é medido com `N` anos de atraso fixo pelo cenário THEN ticks
   executados e tempo de parede SHALL ficar dentro do baseline de 20 seeds em
   `tests/baselines/`, independente do `simuladoAté` inicial variar pelos valores declarados —
   custo que acompanha a idade total do branch (em vez do intervalo de atraso) reprova.

**Independent Test**: catch-up de N anos com orçamento insuficiente pra completar de uma vez
retorna `PartialSuccess`; segunda chamada completa o restante sem refazer o já feito; custo
medido não escala com a idade total do branch, só com N.

---

### P1: Trânsito como invocação de potência, com modos de falha

**User Story**: Como quem desenha trânsito interdimensional, quero que ele seja uma invocação de
potência comum (Fase 16) — custo no uso, rolagem pelo primitivo único, modos de falha com
consequência real — nunca um botão garantido.

**Why P1**: Preserva "trânsito não é botão" e reusa 100% do motor de potência já existente.

**Acceptance Criteria**:

1. WHEN um trânsito é invocado THEN o motor SHALL usar o mesmo pipeline `Prepare`/
   `PrepareEffects`/resolução da Fase 16, com `Reliability="ResolutionCheck"` e perfil
   `Dramático` (ADR-0011) — nenhuma rolagem paralela criada por esta fase.
2. WHEN a rolagem do trânsito falha (`CriticalFailure`/`Failure`) THEN o motor SHALL aplicar uma
   consequência declarada no cenário (chegada na linha errada, chegada fora do tick pretendido,
   meio de trânsito consumido) — nunca um no-op silencioso.
3. WHEN a rolagem resulta em sucesso (`Success`/`CriticalSuccess`/`PartialSuccess`) THEN o
   viajante SHALL chegar na linha/tick pretendidos (ou com a variação declarada pra
   `PartialSuccess`), sujeito ao catch-up daquela linha até o tick de chegada.

**Independent Test**: N trânsitos invocados em seed controlada — distribuição de resultados bate
com o perfil `Dramático`; cada modo de falha produz a consequência declarada correspondente.

---

### P1: Identidade duplicada só com contraparte independente, laço explícito

**User Story**: Como quem quer coerência narrativa sem duplicar população por acidente, quero que
uma chegada só produza duas identidades quando existe de fato uma contraparte que viveu
independentemente na linha de destino (ex.: viajar pro passado onde ele já existia) — nunca
quando ele só está voltando pra própria linha de origem, de onde estava ausente.

**Why P1**: Decisão explícita do usuário, com exemplo concreto — evita o erro de duplicar todo
retorno à linha-mãe.

**Acceptance Criteria**:

1. WHEN um viajante retorna à linha da qual ele originalmente partiu (sua própria origem, onde
   ele estava ausente, não vivendo em paralelo) THEN o motor SHALL apenas reintegrar o `Npc`
   existente (mesmo `NpcId`) após o catch-up daquela linha até o tick de retorno — nenhuma
   segunda identidade é criada.
2. WHEN um viajante chega numa linha onde uma contraparte dele existe por ter vivido
   independentemente ali (ex.: viajou pro passado de uma linha onde ele já nasceu e cresceu por
   conta própria) THEN o motor SHALL criar um `NpcId` distinto pro recém-chegado, com um campo
   de laço explícito referenciando a contraparte pré-existente — nunca fusão, nunca substituição
   silenciosa, nunca recusa automática da chegada.
3. WHEN a distinção entre os dois casos (retorno à origem vs. chegada com contraparte
   independente) é avaliada THEN o motor SHALL decidir com base na linhagem de `BranchId`
   (retorno = mesmo `BranchId` de onde ele partiu; contraparte = `BranchId` diferente com
   histórico próprio contendo o `NpcId` original ativo) — nunca uma heurística ambígua.
4. WHEN o teste de conservação de população é aplicado a qualquer um dos dois casos THEN o total
   SHALL bater exatamente com o esperado por caso: retorno não soma população (é o mesmo `Npc`);
   contraparte soma exatamente 1 `Npc` novo com laço.

**Independent Test**: cenário A — viajante sai de A pra B, volta a A dentro da janela: A tem
exatamente 1 `Npc` (o mesmo, catch-up aplicado). Cenário B — viajante viaja pro passado de uma
linha onde ele já existe: linha destino tem 2 `Npc`s distintos, um com laço apontando pro outro.

---

### P1: Viajante é âncora da linha de origem mesmo ausente

**User Story**: Como quem quer que "voltar pra casa" seja sempre possível, quero que o viajante
conte como âncora da sua linha de origem mesmo estando fora dela — a linha nunca é coletada
(Fase 18) enquanto ele existir em qualquer lugar com referência de origem nela.

**Why P1**: Decisão explícita do usuário — fecha o edge case "e se a casa não existir mais
quando eu voltar".

**Acceptance Criteria**:

1. WHEN um viajante parte de uma linha de origem THEN essa linha SHALL manter uma
   `BranchAnchor` do tipo `Traveler` referenciando o viajante ausente (mesmo mecanismo da Fase
   18, `AnchorKind.Traveler`) — a âncora persiste independente de onde o viajante esteja
   fisicamente simulado.
2. WHEN o viajante deixa de existir (morte permanente, sem possibilidade de retorno declarada no
   cenário) THEN a âncora correspondente SHALL ser removida, tornando a linha de origem
   elegível pra coleta normal (Fase 18) se não tiver outra âncora.
3. WHEN a linha de origem de um viajante ausente é avaliada por `BranchCollectionSystem` (Fase
   18) THEN ela SHALL nunca ser coletada enquanto a âncora do viajante existir — mesmo com zero
   outras âncoras.

**Independent Test**: viajante parte de A pra B; A permanece sem nenhuma outra âncora por N anos
simulados (N > `CollectionGraceTicks` do cenário) — A não é coletada; viajante morre
permanentemente em B — A se torna elegível pra coleta na próxima avaliação sem outra âncora.

## Edge Cases

- WHEN um trânsito é invocado sem nenhum `simuladoAté` prévio na linha de destino (primeira
  visita) THEN o catch-up SHALL partir do snapshot de criação daquela linha (0 ou o tick de
  divergência, conforme Fase 18) — nunca falhar por "histórico ausente".
- WHEN dois viajantes distintos chegam na mesma linha no mesmo tick (concorrência) THEN cada um
  SHALL ser processado com identidade própria, sem colisão — mesma disciplina de ordenação
  determinística já usada em outras fases.
- WHEN o registro de presença append-only é consultado para um branch nunca observado por
  ninguém THEN `LOD(branch, tick)` SHALL retornar a resolução mínima definida no cenário — nunca
  erro por ausência de registro.
- WHEN um viajante com laço explícito (contraparte independente) morre THEN a contraparte
  original SHALL continuar existindo normalmente — o laço nunca implica destino compartilhado.

## Requirement Traceability

| ID | Story | Status |
| --- | --- | --- |
| ITD-01 | P1: simuladoAté — ponto de verdade exato, nunca estimado | Pending |
| ITD-02 | P1: simuladoAté — T ≤ simuladoAté executa 0 ticks | Pending |
| ITD-03 | P1: simuladoAté — avanço persistido append-only | Pending |
| ITD-10 | P1: Preguiçoso==eager — hash idêntico eager vs. 2 lances, 2 processos | Pending |
| ITD-11 | P1: Preguiçoso==eager — cláusula "mesmo registro de presença" é obrigatória no teste | Pending |
| ITD-20 | P1: Relógio — consulta resolve "agora" por BranchId, nunca global | Pending |
| ITD-21 | P1: Relógio — re-simular em fidelidade maior é transição rejeitada | Pending |
| ITD-22 | P1: Relógio — LOD(branch,tick) é função pura do registro de presença | Pending |
| ITD-30 | P1: Pré-aquecimento — fora do caminho crítico do tick | Pending |
| ITD-31 | P1: Pré-aquecimento — bit-idêntico ao catch-up sob demanda | Pending |
| ITD-40 | P1: Orçamento — estouro retorna PartialSuccess, simuladoAté avança até onde deu | Pending |
| ITD-41 | P1: Orçamento — chamada seguinte continua sem refazer trabalho | Pending |
| ITD-42 | P1: Orçamento — progresso consultável durante catch-up longo | Pending |
| ITD-43 | P1: Orçamento — custo acompanha o intervalo, não a idade do branch | Pending |
| ITD-50 | P1: Trânsito — usa pipeline Prepare/PrepareEffects/Resolver da Fase 16, sem rolagem paralela | Pending |
| ITD-51 | P1: Trânsito — falha aplica consequência declarada, nunca no-op | Pending |
| ITD-52 | P1: Trânsito — sucesso chega na linha/tick pretendidos, sujeito a catch-up | Pending |
| ITD-60 | P1: Identidade — retorno à origem reintegra o mesmo NpcId, sem duplicata | Pending |
| ITD-61 | P1: Identidade — contraparte independente gera NpcId novo com laço explícito | Pending |
| ITD-62 | P1: Identidade — distinção decidida por linhagem de BranchId, nunca heurística ambígua | Pending |
| ITD-63 | P1: Identidade — conservação de população correta em ambos os casos | Pending |
| ITD-70 | P1: Âncora — viajante ausente mantém BranchAnchor do tipo Traveler na origem | Pending |
| ITD-71 | P1: Âncora — morte permanente remove a âncora | Pending |
| ITD-72 | P1: Âncora — linha de origem nunca coletada enquanto a âncora do viajante existir | Pending |

**Coverage**: 23 total. Todas as decisões de Assumptions confirmadas — sem bloqueio pra Design.

## Success Criteria

- [ ] Preguiçoso == eager dado o mesmo registro de presença: hash idêntico entre simulação direta
      e em 2 lances, comparado em 2 processos.
- [ ] Resolução é definitiva: re-simular intervalo já simulado em fidelidade maior é `Failure`,
      mundo byte-idêntico depois.
- [ ] Pré-aquecimento bit-idêntico ao catch-up sob demanda.
- [ ] `T <= simuladoAté` não executa tick nenhum (contagem instrumentada == 0).
- [ ] Catch-up custa o intervalo, não a idade do branch — dentro do baseline de 20 seeds.
- [ ] Nenhuma consulta mistura linhas: enumeração por reflexão de toda superfície de consulta
      temporal, com par de mutação provando que o filtro de `BranchId` é testado de verdade.
- [ ] A volta encontra a casa envelhecida na medida certa: `simuladoAté(A) == T+D` exato, log
      contínuo sem buraco/duplicata, hash bate com braço eager equivalente.
- [ ] Trânsito entrou na conta: desligar o subsistema muda o hash canônico em 10 anos.
- [ ] `dotnet test` completo sem regressão nas suítes `History*`/`Extraordinary*`/`Cities*`.
