# Decisões 2/4

### AD-011
- **Decision**: `DecisionContext` (não `WorldState` bruto) passa a ser a assinatura padrão de scoring de decisão de Agent daqui pra frente — `SelectByUtility`/`UtilityBaseOf` migram de `(WorldState world, Npc npc, ...)` para `(DecisionContext ctx, ...)`; qualquer feature futura que adicione um novo fator de decisão passa por `DecisionContextBuilder`, nunca lê `world`/`npc` direto dentro do loop de utility.
- **Reason**: Doc "Living World Cohesion" (pós-16.2) exige que decisão de Agent nunca seja onisciente (`Score(agent, decisionContext)`, nunca `Score(agent, world)`); survey de arquitetura confirmou que Memory/Belief/Relationships hoje são `PRESENTATION_ONLY` justamente por não passarem por um contexto escopado.
- **Trade-off**: Todo novo fator de decisão exige um passo a mais (expor via `DecisionContextBuilder`) em vez de ler `world` direto — mais disciplina, mas fecha por construção a classe de bug "decisão lê dado que o Agent não deveria conhecer".
- **Scope**: Fase 16.3 (Living World Cohesion) em diante — `BehaviorDecisionSystem` e qualquer sistema de decisão futuro.
- **Date**: 2026-08-25
- **Status**: active

### AD-012
- **Decision**: Powers entram em loops de decisão autônoma via um único `ActionType.UsePower` + candidato dinâmico `PowerOpportunity` (gerado por `PowerOpportunityProvider`), nunca um valor de enum por poder específico.
- **Reason**: `ActionType` é um switch fechado usado em múltiplos hot paths (`PersonalityWeighting.TraitValueOf`, `ActionCatalog`, `NpcWakeScheduler`); adicionar um valor por mechanic (27 hoje) explodiria todo esse switch por 4x. `ActionType` é categoria de ação, não "poder específico" — mesmo padrão já usado por `Buy`/`Travel`.
- **Trade-off**: O poder específico escolhido não fica visível só olhando o enum — precisa do campo volátil `Npc.PendingPowerInvocation` ao lado. Em troca, o enum fechado nunca cresce proporcionalmente ao catálogo de poderes.
- **Scope**: Fase 16.3 (Living World Cohesion) — integração Powers↔Utility; qualquer poder novo adicionado depois não ganha valor de `ActionType` próprio.
- **Date**: 2026-08-25
- **Status**: active

### AD-013
- **Decision**: O contador de `EventId` de proveniência causal (`WorldEvent.EventId`, novo) é um campo canônico próprio (`_nextHistoryEventId`/`NextHistoryEventIdAndAdvance`) — nunca reaproveita `_nextEventId`/`NextEventIdAndAdvance`, que já pertence a `ScheduledEvent`.
- **Reason**: Survey de arquitetura confirmou que `_nextEventId` já tem dono (`TickContext.ScheduleEvent`); reusar o mesmo contador pra dois conceitos diferentes (evento agendado vs. evento de história/causalidade) quebraria o significado de cada um e acoplaria dois sistemas sem necessidade.
- **Trade-off**: Mais um contador monotônico em `WorldState` (mesmo padrão já replicado várias vezes — `NpcId`/`HouseholdId`/`WorkplaceId`), nenhum custo real.
- **Scope**: Fase 16.3 (Living World Cohesion) — proveniência causal de `WorldEvent`.
- **Date**: 2026-08-25
- **Status**: active

### AD-014
- **Decision**: Regravar `tests/golden/world-hashes.json` (default seed 42/43 × 100 e 3650 ticks)
  após Phase 16.3 P1d (powers full utility) nesta branch.
- **Reason**: Hash canônico mudou de forma legítima — `ActionCatalog.MaxDurationHours` passou a
  declarar `ActionType.UsePower` (AD-040 / COH-33; enum fechado entra no snapshot) e fases P1a–P1c
  nesta branch já haviam adicionado campos canônicos (`EventId`/`CauseEventId`/`SourceSystem`,
  `Height`/`Weight`/`MuscleMass`). Não é regressão silenciosa: baseline atualizado com AD explícito
  (padrão AD-065/069).
- **Trade-off**: Mundos gravados com golden anterior não batem byte-a-byte; comportamento de
  cenário `default` sem powers ativos continua deterministicamente reproduzível sob o novo hash.
  Possessão (`ControlMechanic.TryDelegatedAction`) permanece no caminho especial intocado (COH-36).
- **Scope**: Fase 16.3 Living World Cohesion Phase 4 / T24 — `tests/golden/world-hashes.json`.
- **Date**: 2026-08-26
- **Status**: active

### AD-015
- **Decision**: Regravar `tests/baselines/action-switches.json` no closeout da Fase 16.3 cohesion.
- **Reason**: DecisionContext / Intent / PowerOpportunity (P1c–P1e) alteram escolhas de ação de
  forma legítima e determinística — mesma classe de ripple que AD-005/AD-006.
- **Trade-off**: Contagens por seed mudam; histerese continua reduzindo trocas vs braço sem
  histerese. Regravação via `ZZZ_record_action_switches_baseline`.
- **Scope**: Fase 16.3 closeout — `tests/baselines/action-switches.json`.
- **Date**: 2026-08-26
- **Status**: active

### AD-016
- **Decision**: Regravar entrada 1k de `tests/baselines/scale-sensor.json` no closeout cohesion;
  manter entrada 5k (Category=Scenario) da baseline anterior.
- **Reason**: Cohesion (body/decision/powers) deslocou `BytesPerAliveNpcPerYear` fora da faixa
  relativa de 1% no sensor de gate (1k). Não é regressão de perf absoluta — tetos de
  `PerfRules.ScaleSensorInitial` continuam válidos.
- **Trade-off**: Disco/alloc por NPC-ano sobe levemente na amostra 1k; 5k não foi re-medido neste
  closeout (custo multi-10min, fora do gate padrão).
- **Scope**: Fase 16.3 closeout — `tests/baselines/scale-sensor.json` (chave `"1000"`).
- **Date**: 2026-08-26
- **Status**: active

### AD-017
- **Decision**: Regravar `tests/golden/world-hashes.json` novamente no closeout (após AD-014/T24).
- **Reason**: WorkHardeningSystem no DefaultSystems + campos/canônicos finais da cohesion
  mudaram o hash do cenário `default` vs baseline AD-014.
- **Trade-off**: Mesmo da AD-014 — mundos com golden anterior não batem byte-a-byte.
- **Scope**: Fase 16.3 closeout — `tests/golden/world-hashes.json`.
- **Date**: 2026-08-26
- **Status**: active

### AD-018
- **Decision**: `phase-16-3-web` (demo isolada) — `LivingWorld_Frontend_Final.md` (doc consolidado, mais recente) passa a mandar sobre `LivingWorld — Frontend Experience & Design System.md` (doc anterior) nos pontos em que os dois conflitam. Primeiro conflito resolvido: NPCs nunca somem por causa do zoom (doc novo §14/§46) — revoga P1b AC1-2 da spec original ("nenhum NPC visível" no nível mundo/distrito). Nesta demo (sem simulação real rodando), a posição do NPC ganha um movimento decorativo/scripted entre pontos do fixture — trade-off explícito do usuário sobre o próprio doc novo §82 ("no fake world... nunca inventar activity/position"), aceito porque não há simulação de verdade pra derivar posição real ao longo do tempo.
- **Reason**: Usuário testou a demo ao vivo e disse que a experiência "ainda não bate com o que eu imaginei" — pediu explicitamente "poder ver os NPCs vivendo no mundo" depois de ler o doc consolidado.
- **Trade-off**: O movimento não é canônico/derivado de simulação — é uma simulação visual decorativa só pra esta demo provar a sensação de "mundo vivo" antes da integração real (`phase-16-3-world-cohesion`). Documentado explicitamente no código/README/checklist pra não ser confundido com dado real quando a integração acontecer.
- **Scope**: `.specs/features/phase-16-3-web/` (demo isolada) — não afeta `phase-16-3-world-cohesion` (backend) nem `web/` (cliente de produção).
- **Date**: 2026-08-26
- **Status**: active

### AD-019
- **Decision**: `phase-16-3-web` — trocado o bloco isométrico 2:1 (`IsoProjection`/`IsoTile`) por projeção top-down ortogonal (escala 1:1, sem shear). `BuildingInterior` já era top-down; agora exterior e interior usam a mesma lógica de projeção.
- **Reason**: usuário testou e reportou "a visão isométrica não está funcionando bem... tem que ser algo topdown". Achado incidental na mesma passada: labels do mapa (`<text>`) não tinham `fill`, herdavam preto default de SVG — ilegível sobre `--bg-world`.
- **Trade-off**: nenhum — top-down é estritamente mais simples que a projeção isométrica que substituiu, sem perda de informação espacial pro fixture atual (grid 2D raso, sem elevação real).
- **Scope**: `web-demo/src/map/{IsoProjection,IsoTileRenderer}.ts(x)`, `tokens.css`.
- **Date**: 2026-08-26
- **Status**: active
