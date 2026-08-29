# STATE

## Decisions

### AD-001
- **Decision**: A tela de "criar mundo" do cliente web vai expor o body de cenário (`ScenarioLoaderV2`) como formulário campo a campo (não um textarea de JSON cru).
- **Reason**: Usuário pediu explicitamente formulário campo a campo, mesmo sendo mais trabalho de UI — prioriza usabilidade sobre velocidade de entrega.
- **Trade-off**: Formulário precisa acompanhar manualmente qualquer campo novo que `ScenarioLoaderV2`/`MapScenarioLoader`/`PopulationScenarioLoader`/etc. passem a exigir (um editor de JSON cru não teria esse risco de drift, mas foi descartado).
- **Scope**: Feature ad-hoc "criar mundo" (ainda sem `.specs/features/` própria) — cliente web (`web/src/**`) e o novo endpoint de criação de mundo na API.
- **Date**: 2026-08-06
- **Status**: active

### AD-002
- **Decision**: Tela inicial (start menu) estilo jogo — botões centrais (Continuar/Criar mundo/Configurações) sobre fundo animado — com motivo visual deliberadamente atemporal (campo de partículas à deriva), não medieval nem preso a nenhuma época.
- **Reason**: Usuário pediu estilo "Minecraft" de menu inicial, mas corrigiu que o projeto simula qualquer período de tempo (não só medieval) — iconografia de época específica ficaria errada.
- **Trade-off**: Sem CSS/design system prévio no cliente (era HTML puro sem estilo); criado `web/src/styles/global.css` com estilos por seletor de elemento (não por classe) pra herdar em todos os componentes existentes sem reescrevê-los.
- **Scope**: UX geral do cliente web (fase 15) — tema visual, menu inicial, tela de configurações placeholder.
- **Date**: 2026-08-06
- **Status**: active

### AD-003
- **Decision**: Grid 2D real (canvas) substitui listas/botões no mapa-múndi/cidade; NPCs viram token/dot por LOD de zoom; seleção por clique abre painel lateral; editor de mapa em "criar mundo" também vira grid clicável; overlay de mapa (tecla M) em modo jogador. Token/terreno usam cor procedural determinística por id — não há pipeline de arte (pixel-art/ilustrado) no projeto.
- **Reason**: Usuário rejeitou a entrega textual do T8 original ("nada batendo com o que eu esperava") e trouxe referências de VTT ilustrado — cor procedural é o teto realista sem um pipeline de assets.
- **Trade-off**: Prédios não têm `CellCoord` no domínio — layout em anel calculado no cliente (aproximado, marcado visualmente, não é posição real). Movimento "andar até a saída" pra trocar de escopo mundo↔cidade não foi construído (exigiria sistema de movimento em escala mapa-múndi que não existe) — mantido botão/painel de drill-down.
- **Scope**: Fase 15, UX Pass 2 — ver `.specs/features/phase-15-map-visual/spec.md` (seção "UX Pass 2"), `design.md` e `tasks.md` (T10-T15), atualizados antes da implementação a pedido do usuário.
- **Date**: 2026-08-06
- **Status**: active

### AD-004
- **Decision**: UX Pass 3 — corrigido bug real (modo Jogador quebrava a conexão realtime no mapa-múndi), mapa virou tela cheia de verdade (HUD flutuante em vez de header/legenda empurrando layout), sem teto de tamanho de mapa além do limite técnico real de canvas do browser, formulário "criar mundo" virou wizard por abas com seletor de template real (backend seeda 3 períodos válidos, não inventado no cliente).
- **Reason**: Usuário rejeitou a entrega da AD-003 ("mapa é um quadrado minúsculo", "modo jogador não pega", "form ainda parece formulário") e pediu presets/templates + wizard bonito.
- **Trade-off**: Templates seedados (`DefaultPeriodSeeder.cs`) são só 3 variações de tamanho/população do mesmo cenário base — não há autoria de conteúdo temático por template (nomes de profissão, terrenos diferentes etc.), só o que já era editável no formulário. Presets de população específicos por aba (ex.: botão "vila" dentro da aba População) não foram construídos — o mecanismo de template cobre esse caso de uso.
- **Scope**: Fase 15, UX Pass 3 — mesmos arquivos de spec/design/tasks da fase 15.
- **Date**: 2026-08-06
- **Status**: active

### AD-005
- **Decision**: `RelationshipSystem.ApplyCohabitationForMembers` (convivência diária FAM-01..05) agora respeita `FamilyRules.MaxCohabitationGroupSize` — grupo até o teto forma par-a-par completo (comportamento de sempre); grupo acima do teto forma laço só com uma janela fixa de vizinhos por Id (determinístico, sem RNG), O(k×teto) em vez de O(k²). Default `int.MaxValue` (sem teto) — só `ScaleScenarioFixture` (testes de escala) declara um valor real (30).
- **Reason**: `ScenarioRunner.ScaleEconomyCatalog` permite até 8.000 trabalhadores simultâneos num workplace em população 10k — sem teto, isso é 8.000²=64M pares/dia só ali, e gerou 16,5M+ relações (5GB de RAM) em ~8 meses simulados, tornando `LongRunScaleTests.Ten_k_population_ten_years_within_perf_budget` impraticável (7h45min). Investigado e confirmado: NÃO é design intencional — o modelo par-a-par foi desenhado na fase 7 pensando em household pequeno (2-8 membros), antes do multiplicador de vagas da fase 9 existir. Decisão tomada em chat com o usuário (perguntei implicações, ele aprovou explicitamente: "Se vai deixar realista e tambem melhorar nosso tempo é totalmente valido").
- **Trade-off**: Muda resultado de simulação em grupos grandes (menos relações rastreadas) — `tests/golden/world-hashes.json` e `tests/baselines/scale-sensor.json` foram regravados deliberadamente no mesmo commit. Household/workplace normal (abaixo do teto) fica byte-idêntico a antes — só cenários de escala industrial (milhares no mesmo local) são afetados. Janela usada em grupos "teto+1 a ~2×teto" pode contar alguns pares duas vezes (documentado inline em `RelationshipSystem.cs`) — não corrigido, não há cenário real nessa faixa intermediária hoje.
- **Scope**: `.specs/features/phase-16-perf-test-suite/` — commit `17b0a2e`.
- **Date**: 2026-08-20
- **Status**: active

### AD-006
- **Decision**: `BehaviorDecisionSystem`'s ambient wander for `ActionType.Work` now requires a real `Employer` — an adult NPC with no employer (unemployed, or no vacancy) stays put ("blocked") instead of wandering aimlessly while marked `Work`.
- **Reason**: LWV-02.3 (Fase 15.1, Stage 4, T9) requires NPCs to commute to a real workplace and work only there, and forbids faking work absent real capacity. `ProductionSystem`/`SkillPracticeSystem` already gated the economic effect on `Employer` + physical presence; the ambient-wander gate was the one remaining path that faked movement for unemployed "working" adults. Confirmed with the user before implementing (AskUserQuestion) given the foreseeable blast radius on determinism.
- **Trade-off**: Since `DefaultEconomyRules.Enabled = true` and vacancies are capacity-limited, many adults are legitimately unemployed during "Work" routine hours — freezing their movement is a real, broad behavior change. It rippled into `tests/baselines/action-switches.json` (deterministic: occupancy available to other NPCs' own ambient steps in the same tick changes) — regenerated deliberately in the same commit (`6dbfb02`), same class as AD-005. Two pre-existing unit tests (`BehaviorDecisionSystemTests.cs`) that exercised Work's ambient step without an employer were updated to employ the NPC first (same intent, spec-correct precondition) rather than left asserting the now-forbidden fake-work behavior.
- **Scope**: `.specs/features/phase-15.1-stage-4-living-world/` (T9) — commit `6dbfb02`.
- **Date**: 2026-08-21
- **Status**: active

### AD-007
- **Decision**: `ConstructionSystem`'s pre-existing (Fase 8) per-city FIFO construction queue is no longer strictly "only the head advances." When the head project has completed payment but `BuildingPlacementResolver.Resolve` returns `null` for it (whole-map land scarcity), it is skipped for that tick's resource-consumption budget and its placement is retried every tick at zero cost, while the first not-yet-stuck project behind it receives that tick's resource budget instead (still exactly one project's resources consumed per city per tick — the throttle itself is unchanged, only which project qualifies as "the one").
- **Reason**: `dynamic-city-growth`'s land-scarcity handling (AC "no free cell anywhere on the map") needed a decision on what happens to an in-flight `ConstructionSystem` project caught by that condition. Two options were tried and rejected first: (a) drop the project silently — destroys its already-consumed resources with no recovery, confirmed as the original regression a Verifier pass caught; (b) leave it queued in place, retried every tick, but still strictly at the head — confirmed (by the same Verifier) to starve every other project queued behind it in the same city (measured 20+ ticks in a test). User chose skip-ahead explicitly after being shown all three options with their trade-offs.
- **Trade-off**: `City.ConstructionQueue`'s ordering guarantee is now "FIFO among non-stuck projects, with stuck projects held at their position and retried for free" rather than a pure index-0-only FIFO — any future code touching this queue needs to account for a stuck project possibly sitting anywhere in the list, not just index 0. Verified idempotent (resources charged exactly once even across multiple stuck-then-retried ticks).
- **Scope**: `.specs/features/dynamic-city-growth/` — see `spec.md` Edge Cases and `design.md` Error Handling Strategy for the full amendment text.
- **Date**: 2026-08-23
- **Status**: active

### AD-008
- **Decision**: Potência é conteúdo composicional de cenário — fonte, efeito, modo, custo, confiabilidade, falha, vulnerabilidade, manifestação e aquisição — sem enum/caso nominal por poder ou arquétipo.
- **Reason**: Permite expressar artefatos responsivos à vontade, transformações predatórias/noturnas e transformações cíclicas no mesmo motor, sem nomes ou subsistemas de franquia.
- **Trade-off**: Tags livres exigem validação semântica na borda de cada sistema-alvo; o primeiro corte valida forma/invariantes e recusa runtime ligado ainda não implementado.
- **Scope**: Fase 16 e qualquer feature futura que declare ou consuma potência extraordinária.
- **Date**: 2026-08-23
- **Status**: active

### AD-009
- **Decision**: Suíte completa de testes (com ou sem `Category=Scenario`, `scripts/test.sh` /
  `verify.sh`, gate do Verifier) é executada **somente pelo usuário**. Agentes podem rodar
  testes focados da funcionalidade em desenvolvimento (`--filter` no escopo da feature).
  Verifier analisa código/evidência estática; se precisar de comando de validação, pede ao usuário.
- **Reason**: Pedido explícito do usuário (2026-08-25) — controle do tempo/carga de máquina
  e do gate final.
- **Trade-off**: Verifier não fecha PASS sozinho sem o usuário rodar o gate; closeout depende
  de evidência de gate fornecida pelo usuário.
- **Scope**: Todo o projeto LivingWorld / fluxo tlc-spec-driven (Execute + Verifier).
- **Date**: 2026-08-25
- **Status**: active

### AD-010
- **Decision**: `combat.strike:` permanece resolução imediata single-shot (compat com poderes
  já salvos na 16.1); combate multi-round entra por token novo `combat.engage:` que cria
  `CombatEncounter` persistente em `WorldState`.
- **Reason**: Mudar o contrato de `combat.strike` quebraria comportamento observável de mundos
  existentes; `combat.engage` isola a profundidade multi-round sem regressão.
- **Trade-off**: Cenários que quiserem rounds precisam declarar `combat.engage:` explicitamente;
  `combat.strike` continua disponível para golpe único.
- **Scope**: Fase 16.4 (ex-16.3, renumerada em 2026-08-25 — nova 16.3 é "Living World Cohesion") — `CombatMechanic`, `CombatEncounterSystem`.
- **Date**: 2026-08-25
- **Status**: active

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

### AD-020
- **Decision**: `phase-16-3-web` — redesign estrutural do renderer de Settlement View: sai o SVG/React declarativo (`SemanticZoomMap` no nível "settlement", `BuildingInterior` como view separada), entra um renderer Canvas/WebGL dedicado (`PixiJS`, novo `web-demo/src/render/**`) com câmera pan/zoom, terreno/roads procedurais (layout gerado, não canônico — ver Trade-off), footprints de prédio reais, e "roof cutaway" físico (fade do telhado revelando o interior NA MESMA cena, ao focar um prédio) em vez de navegar pra uma página/view separada. World View (nível "mundo", `SemanticZoomMap`) e todo o resto do doc completo do usuário (day/night, atividades visíveis tipo sleep/work/talk, conversas, veículos, mapa mundial estilizado com terreno/rios/estradas, LOD com milhares de agents) ficam **fora desta rodada — mesma fase (`phase-16-3-web`), sem fase nova**, registrados como backlog em `spec.md`.
- **Reason**: usuário pediu um redesign profundo (doc completo, referência RimWorld) e, perguntado explicitamente, escolheu Canvas/WebGL agora (contra a recomendação de manter SVG/React, dada a escala de 11 NPCs desta demo) e escolheu Settlement View como fatia desta rodada, com o resto anotado no roadmap da mesma fase em vez de virar fase nova.
- **Trade-off**: (1) Terreno/roads são gerados por um `settlementLayout.ts` determinístico (seed = id do settlement/prédio), não dado canônico do fixture — é uma camada de apresentação/layout, nunca a simulação decidindo verdade nova (mesmo princípio do §82 do doc, aplicado à camada visual). (2) Testes de canvas não são inspecionáveis via DOM como os de SVG — lógica pura (layout, câmera, interpolação de patrulha) ganhou módulos próprios 100% testados sem Pixi; o componente Pixi em si é testado com `pixi.js` mockado (spies nas chamadas de desenho), não com asserts de pixel. (3) `BuildingInterior.tsx` (view separada, AD anterior implícito) fica obsoleta e é removida — o interior agora só existe dentro do canvas do settlement.
- **Scope**: `web-demo/src/render/**` (novo), `web-demo/src/map/patrolMath.ts` (novo, extraído de `usePatrolPosition`), `CenterStage.tsx`, remove `views/BuildingInterior.tsx` + teste. Não toca `SemanticZoomMap` no nível "world".
- **Date**: 2026-08-26
- **Status**: active

### AD-021
- **Decision**: `phase-16-3-web` — 3 correções sobre o Settlement View (AD-020) a partir de teste ao vivo do usuário: (1) navegação entre building/household/agent dentro de um settlement passa a usar `NavigationStore.replace` (novo método) em vez de `push` — são irmãos (um foco substitui o outro), não uma pilha que deve crescer; clicar no terreno vazio do mapa sempre volta pro settlement (`SettlementStage` ganhou o prop `onBackgroundClick`). (2) Causal Explorer/Timeline/Life/Feed/Threads abrem como um painel POR CIMA do mapa (`CenterStage` sempre monta a camada espacial, deriva a última rota espacial da pilha quando uma dessas 5 rotas está no topo) em vez de substituir o centro — fecha com X/backdrop/Esc, todos chamando `nav.back()`. (3) Bug real: `containerEl.setPointerCapture()` era chamado já no `pointerdown`, o que redireciona o `target` dos eventos seguintes pro elemento capturado — o listener do Pixi no `<canvas>` nunca via `pointerup`, então clique em prédio/agent/terreno não funcionava com mouse real (só em dispatch sintético direto no canvas, que não sofre essa redireção). Corrigido: captura só depois de confirmar arrasto de verdade.
- **Reason**: usuário testou ao vivo 3 vezes nesta rodada: 1ª vez reportou terreno/NPCs quebrados (bugs à parte, AD-020); 2ª vez reportou os 3 problemas de navegação/UX acima; 3ª vez (após o fix de push→replace/overlay) reportou que building ainda não abria e sugeriu investigar conflito com a feature de drag — hipótese certa, era o `setPointerCapture` cedo demais.
- **Trade-off**: nenhum real — `replace` é estritamente mais correto pro modelo "foco entre irmãos" que o doc do usuário pede; o overlay é puramente aditivo (o mapa que já existia continua existindo, só ganha um painel por cima); o fix de pointer capture não muda nenhum comportamento de pan intencional, só corrige quando a captura acontece.
- **Scope**: `web-demo/src/nav/NavigationStore.ts` (`replace`), `web-demo/src/render/SettlementStage.tsx` (`onBackgroundClick`, captura tardia), `web-demo/src/components/CenterStage.tsx` (overlay), `AgentView.tsx`/`HouseholdView.tsx`/`SettlementView.tsx`/`Inspector.tsx` (chamadas pontuais push→replace pra navegação irmã-a-irmã dentro do settlement).
- **Date**: 2026-08-26
- **Status**: active

### AD-022
- **Decision**: `phase-16-3-web` — prédios de Oakbridge (e patrolPoints dos agents que moram/trabalham neles) escalados 5x no fixture (`oakbridge.ts`) — grid antigo 0-3 virou 0-15. Settlement View ganhou zoom-to-fit no overview (`cameraState.fitZoom`, novo): a câmera inicial cabe o bounding box real dos prédios na viewport, nunca amplia além de 1x pra um settlement pequeno.
- **Reason**: usuário reportou que as casas estavam "muito coladas" pra testar — bug real por trás: `buildingFootprint` (AD-020) dá a cada prédio uma área de 2-5 tiles, mas os `gridPosition` originais (herdados do modelo antigo de 1-tile-por-prédio) ficavam a 1 tile de distância um do outro — footprints se sobrepunham de verdade (não só visualmente apertado).
- **Trade-off**: nenhum — é estritamente uma correção (positions eram artefato do modelo antigo, não dado "de verdade" de nenhum sistema). `SEMANTIC_LOCAL_CENTER` (mapa-múndi, AD-018) ajustado de 1.5→7.5 pra continuar centralizando os pontinhos de agent no settlement certo.
- **Scope**: `web-demo/src/fixture/oakbridge.ts` (gridPosition + patrolPoints, só Oakbridge — outros settlements não têm prédios/agents modelados ainda), `web-demo/src/render/cameraState.ts` (`fitZoom`), `web-demo/src/render/SettlementStage.tsx`, `web-demo/src/map/SemanticZoomMap.tsx` (`SETTLEMENT_LOCAL_CENTER`).
- **Date**: 2026-08-26
- **Status**: active

### AD-023
- **Decision**: `phase-16-3-web` — clique no terreno vazio só desfoca (`onBackgroundClick`) quando NENHUM prédio está focado. Focado num prédio, só o botão explícito "← Street" sai do foco.
- **Reason**: bug real do usuário — ao focar um prédio a câmera aproxima (`FOCUS_ZOOM`), mas o interior não preenche a viewport inteira, sobra terreno visível nas bordas da cena. Um clique um pouco impreciso perto de um NPC lá dentro (agents/mobília não cobrem o footprint inteiro) caía nesse terreno e disparava `onBackgroundClick`, voltando pra cidade inteira — parecia que "clicar no NPC fechava a visualização".
- **Trade-off**: nenhum — o "← Street" já existe e é a saída sem ambiguidade; desabilitar o clique-fora só nesse estado remove um jeito acidental de sair sem tirar nenhuma funcionalidade real (a intenção original do AD-021, "clicar fora volta pra cidade", segue valendo no nível agent/rua, onde não tem essa zona de risco).
- **Scope**: `web-demo/src/render/SettlementStage.tsx` (`terrainLayer` pointertap handler).
- **Date**: 2026-08-26
- **Status**: superseded by AD-025 (guard reverted — was a fix for a symptom, real root cause found and fixed directly).

### AD-024
- **Decision**: `phase-16-3-web` — início do redesign de sidebars/Inspector/Popup-Drawer pedido pelo usuário (novo doc "Redesign das Sidebars, Inspector, Timelines e Painéis Contextuais"). Escopo desta rodada, combinado explicitamente com o usuário via pergunta direta: fundação (`Popup`/`Drawer`, doc §19, componente novo `ContextOverlay.tsx`) + Agent Inspector redesenhado (doc §13, `AgentView.tsx`) como prova de conceito — não o doc inteiro (que cobre Explorer, 6 outros tipos de Inspector, e 2 timelines).
- **Reason**: doc é grande (45 seções); usuário pediu explicitamente pra "pensar alguns redesigns" (discussão, não implementação direta) — perguntado onde começar, confirmou a fundação+Agent Inspector antes de espalhar pro resto, mesmo padrão de scoping incremental usado nas rodadas anteriores desta fase.
- **Trade-off**: `Drawer` (tier "médio", 420-520px) foi construído junto com `Popup` mas SEM consumidor real ainda — nenhuma lista do Agent Inspector precisa desse tier. Fica pronto pra Household/Settlement/Organization na próxima rodada. "Locate"/"⋯" do header do Inspector (doc §12) ficaram de fora — "Locate" precisa de uma forma de centralizar a câmera do `SettlementStage` a partir do Inspector, "⋯" não tem ação real ainda pra abrigar.
- **Scope**: `web-demo/src/components/{ContextOverlay,InspectorPrimitives}.tsx` (novos), `web-demo/src/views/AgentView.tsx` (redesenhado), `tokens.css`. Não toca Household/Settlement/Organization/Event Inspector, Explorer, ou as timelines.
- **Date**: 2026-08-26
- **Status**: active

### AD-025
- **Decision**: `phase-16-3-web` — três correções ligadas, na ordem em que a causa raiz real foi encontrada: (1) `CenterStage.tsx` ganha `useFocusBuildingId`, que preserva o prédio focado quando a rota vira `{kind:"agent"}` SÓ SE esse agent já estava dentro do prédio já focado (memória via `useRef`, nunca cria foco novo do zero); (2) o guard do AD-023 no `terrainLayer` é revertido (clique fora volta a desfocar incondicionalmente); (3) `SettlementStage.tsx` para de deixar o Pixi (`containerEl.appendChild(app.canvas)` / `replaceChildren()`) escrever no MESMO nó DOM que o React usa pro overlay — agora são irmãos (`<div ref={containerRef}/>` exclusivo do Pixi + `<div data-testid="settlement-stage-overlay">` renderizado pelo React), com CSS (`tokens.css`) fazendo o nó do Pixi herdar o tamanho cheio do wrapper.
- **Reason**: usuário reportou 3 sintomas em sequência que pareciam contraditórios: "clico no NPC da casa, ele seleciona mas sai da casa" → causa raiz real era `nav.replace({kind:"agent"})` colapsando a rota `building`, não o terreno (AD-023 tratava só o sintoma). Fix v1 ingênuo (focar sempre que o agent tem `indoorLocation`) causou regressão reportada — "clico num NPC na rua, ele me leva pra casa dele direto" — corrigida com a memória via ref. Terceiro bug, achado só em teste AO VIVO no browser (nenhum teste jsdom pegava): mesmo com `focusBuildingId` corretamente computado (confirmado com debug globals nos dois componentes), o overlay não aparecia no DOM real — console mostrava `NotFoundError: Failed to execute 'removeChild'` não capturado, porque o `replaceChildren()` do cleanup do Pixi apagava por baixo dos pés o `<div>` do overlay que o React também considerava filho daquele mesmo nó; a reconciliação seguinte do React tentava remover um nó que já não existia, lançava, e derrubava a subtree inteira sem nenhum erro visível na UI.
- **Trade-off**: nenhum funcional — só um nó a mais no DOM (o wrapper com o testid vira puramente estrutural/CSS, o filho novo é quem o Pixi realmente possui). Um teste (`SettlementStage.test.tsx`, drag-threshold) precisou trocar de disparar eventos no wrapper pro `firstElementChild`, já que os listeners de pointer estão no nó que o Pixi realmente usa.
- **Scope**: `web-demo/src/components/CenterStage.tsx`, `web-demo/src/render/SettlementStage.tsx`, `web-demo/src/styles/tokens.css`, `web-demo/tests/{components/CenterStage,render/SettlementStage}.test.tsx`.
- **Date**: 2026-08-26
- **Status**: active

### AD-026
- **Decision**: `phase-16-3-web` — segunda leva de correções/redesign pedida pelo usuário em cima do doc de sidebars: (1) fixture ganha relacionamentos estruturados (`kind`/`familyRole`/`strength` em `fixture/types.ts`, dados reais em `fixture/oakbridge.ts` — spouse/parent/child/sibling pra Valen e Miller); (2) `FamilyTree.tsx` (novo) — árvore genealógica estilo Sims (gerações empilhadas, percorre só `familyRole`), aberta via `Drawer`; (3) `RelationshipRow` (novo primitive) — aba social com ícone por `kind` + força do vínculo, substitui `<li>texto</li>`; (4) `Popup` reancorado (`right: calc(340px + 1rem)`) — antes sobrepunha o Inspector (340px) porque `right:1rem` era relativo ao viewport inteiro; (5) `.section-link` vira `display:block` — "View life timeline" e "View Timeline" apareciam colados na mesma linha; (6) `FollowButton` some do `SettlementView` — "seguir" uma cidade inteira não fazia sentido; (7) `FollowButton` ganha significado real — `SettlementStage` trava a câmera na posição do agent seguido a cada frame do ticker (`followStore.isFollowed`), parado durante drag; (8) `BuildingView.tsx` (novo, extraído de `Inspector.tsx`) e `SettlementView.tsx` redesenhados com os primitives existentes (doc §16), removendo `<dl>`/`<ul>` cru.
- **Reason**: usuário reportou visualmente (screenshots) 6 problemas depois de usar a v1 do redesign: botões colados, popup ilegível por cima da sidebar, Follow sem efeito real, Settlement/Building Inspector "bagunçados", Follow sem sentido no settlement. Pediu explicitamente (via pergunta direta) Follow = câmera seguindo o agent, e árvore genealógica com dados de família de verdade em vez de heurística por texto livre.
- **Trade-off**: árvore genealógica só tem profundidade real pra Valen (2 gerações: pais+filhos) e Miller (casal sem filhos no fixture) — os demais agents (Rowan/Corvin/Garrick/Finn/Sela) não têm dados de família, mostram estado vazio corretamente em vez de inventar parentesco. Camera-follow é só espacial (trava x/y na posição renderizada do agent) — não auto-abre o prédio se o agent seguido entra numa casa focada em outro lugar; isso continua exigindo o clique explícito no prédio.
- **Scope**: `web-demo/src/fixture/{types,oakbridge}.ts`, `web-demo/src/views/{AgentView,SettlementView,HouseholdView,FamilyTree,BuildingView}.tsx`, `web-demo/src/components/{InspectorPrimitives,Inspector}.tsx`, `web-demo/src/render/SettlementStage.tsx`, `web-demo/src/styles/tokens.css`, testes correspondentes.
- **Date**: 2026-08-26
- **Status**: active

### AD-027
- **Decision**: `phase-16-3-web` — terceira leva, corrigindo o que sobrou do AD-026 depois de teste ao vivo: (1) Popup reancorado de novo — o `right: calc(340px+1rem)` do AD-026 resolvia a sobreposição mas não o pedido real ("mesma linha horizontal do botão, à esquerda"); agora `Popup`/`ContextOverlay` recebem `anchorRect` (via `event.currentTarget.getBoundingClientRect()` capturado no clique do `SectionLink`) e se posicionam com `top`/`right` inline exatos, sem clamp preventivo de altura (esse clamp empurrava o popup pra longe da linha mesmo quando o conteúdo era curto); (2) anel de "seguindo" (`Graphics` filho do sprite, doc pedia "highlight no NPC") — primeira tentativa usou `circle()` (mock do Pixi em teste não tinha `ellipse()`), raio grande demais cortava o corpo do sprite ("torto"); corrigido com `ellipse()` achatada nos "pés" (mock ganhou `ellipse()`); (3) `followStore` ganha `activeFollowId()`/`activate()`/`detachCamera()` — dá pra seguir vários NPCs (bookmark, lista "Followed" continua em ORDEM DE QUANDO CADA UM FOI SEGUIDO, nunca reordenada) mas a câmera só trava em UM (`activeId`, campo separado da ordem da lista — bug real: a primeira versão reusava a mesma lista pra derivar o alvo, e cada `activate()` reordenava a sidebar sozinha); arrastar o mapa de verdade (cruza `CLICK_DRAG_THRESHOLD`) chama `detachCamera()` — para de travar/esconde o anel sem des-seguir, só reata clicando o nome nome em "Followed" (`activate`) ou seguindo outro agent (`toggleFollow`); (4) `BuildingView` ganha ícone por `kind` + subtítulo "Kind · Settlement" (like Settlement Inspector) em vez de `MetricRow` pra "Kind", Occupants sobe pro topo.
- **Reason**: usuário testou ao vivo e reportou, nessa ordem: popup ainda não alinhava com o botão; anel "torto" (screenshot); "following" não destacava nem seguia de verdade (raiz: dev server com HMR preso numa versão antiga do módulo — `FOLLOW_RING_RADIUS_X` inexistente, `resolveFocusBuildingId` de uma refatoração anterior — resolvido reiniciando o processo do Vite, não só editando o código; confirmar em servidor de dev sempre que uma mudança em `SettlementStage.tsx`/`CenterStage.tsx` "não aparece" ao vivo apesar de testes passando); pediu explicitamente múltiplos NPCs seguidos com câmera só no último ativado, alternância pela lista "Followed" sem reordenar, e "desgrudar" ao arrastar sem des-seguir.
- **Trade-off**: nenhum novo — mesmo escopo do AD-026 (câmera só rastreia agents, nunca auto-entra em prédio).
- **Scope**: `web-demo/src/components/{ContextOverlay,InspectorPrimitives,Explorer}.tsx`, `web-demo/src/state/followStore.ts`, `web-demo/src/render/SettlementStage.tsx`, `web-demo/src/views/{AgentView,BuildingView}.tsx`, `web-demo/tests/setup.ts`, testes correspondentes.
- **Date**: 2026-08-26
- **Status**: active

### AD-028
- **Decision**: Regravar entrada 1k de `tests/baselines/scale-sensor.json` no closeout da
  Fase 16.4 (T21); manter entrada 5k (Category=Scenario) da baseline anterior.
- **Reason**: Drift de `BytesPerAliveNpcPerYear` (~121306 → ~123260) já existia na branch
  antes de semear fauna/flora em massa no fixture de escala (canônicos 16.4 + sistemas de
  ecologia no `DefaultSystems`). Após T21, o fixture também semeia N=pop/5 animais/plantas
  (reprodução/predação desligadas no braço de escala — custo O(n), não O(n²)). Não é
  regressão de teto absoluto: `PerfRules.ScaleSensorInitial` continua válido. Mesmo padrão
  AD-014..017.
- **Trade-off**: Disco/alloc por NPC-ano sobe levemente na amostra 1k; 5k não foi re-medido
  neste closeout (custo multi-10min, fora do gate padrão).
- **Scope**: Fase 16.4 T21 — `tests/baselines/scale-sensor.json` (chave `"1000"`).
- **Date**: 2026-08-27
- **Status**: active

### AD-029
- **Decision**: Closeout Scenario da Fase 16.4 usa horizonte de **10 anos**, não 100.
  100 anos permanece no objetivo #1 do roadmap (`LifeTable*` / Scenario de população),
  não é pré-requisito de fechamento desta fase.
- **Reason**: Usuário cancelou a rodagem de ~864k ticks (~2h+) por custo; 10 anos cobrem
  várias estações, cold-archive e variação ecológica sem duplicar o gate do objetivo #1.
- **Trade-off**: Closeout 16.4 não prova sozinho multi-geração de século; isso continua
  nos testes de população de longo prazo já existentes.
- **Scope**: `.specs/features/phase-16-4-world-realism/` + `WorldRealismCloseoutTests`.
- **Date**: 2026-08-27
- **Status**: active

### AD-030
- **Decision**: Scenario de closeout da 16.4 (`Reference_scenario_ten_years_*`) usa regras de
  espécie **hunger-only** (ReproduceProbability/PredationProbability = 0), mesmo padrão do
  `ScaleScenarioFixture`. Repro/predação continuam cobertas pelos Independent Tests unitários.
- **Reason**: Com o default (repro ~0.12–0.18 + predação), a fauna sobe ao teto
  (`MaxAliveFauna=400`) e `TryPredate` fica O(n²)/hora — 10 anos travou ~6k+ s. Usuário
  cancelou; closeout precisa provar horizonte longo sem duplicar o custo O(n²).
- **Trade-off**: O Scenario longo não exercita dinâmica populacional predatória; isso já
  está nos testes Ecology focados (T5/T6).
- **Scope**: `WorldRealismCloseoutTests` + early-out em `FaunaLifecycleSystem` quando
  probabilidades são zero.
- **Date**: 2026-08-27
- **Status**: active

### AD-031
- **Decision**: Height/Weight/MuscleMass passam a consumir combate: ofensa via
  `BodyMechanic.CombatOffenseMultiplier` (= curva de MuscleMass/`WorkCapacityMultiplier`)
  no capacity do strike/round; dano recebido via `CombatDamageTakenMultiplier`
  (Weight+Height). Equipment compatibility permanece FUTURE_DEPENDENCY.
- **Reason**: Fechar FUTURE_DEPENDENCY do audit 16.3 (COH-25) agora que combate multi-round
  existe (16.4); reusa `BodyMechanic` em vez de fórmula paralela.
- **Trade-off**: Sem equipamento ainda — só sizing corporal no Resolve/Damage.
- **Scope**: `BodyMechanic`, `CombatMechanic`, `CombatEncounterSystem`.
- **Date**: 2026-08-27
- **Status**: active

### AD-032
- **Decision**: `phase-16-3-web` — World Map redesign (novo doc "World Map Visual & Spatial Design"), Fases 1+2+4 do doc (terreno procedural/rios/estradas/footprints reais + agents como pontos vivos + continuidade de câmera ao entrar num settlement/agent). Trocado o World View de SVG declarativo (`map/SemanticZoomMap.tsx`, `IsoProjection.ts`, `usePatrolPosition.ts` — todos REMOVIDOS, sem consumidor sobrando) por `render/WorldStage.tsx`, um segundo renderer Pixi.js dedicado (mesma linguagem do `SettlementStage`, AD-020) na escala do mundo inteiro. Continuidade World↔Settlement/Agent implementada como DOIS renderers separados (decisão explícita do usuário, unificar os dois numa Pixi App/câmera só é escopo maior que fica pra outra rodada) + uma animação de ponte: `WorldStage` dá zoom na entidade clicada (câmera lerp até ~3.2x, `TRANSITION_MIN_MS`) ANTES de chamar `onSelectSettlement`/`onSelectNpc` (troca de rota), em vez de cortar direto pra `SettlementStage`. Follow/Inspector têm paridade completa com o Settlement (mesmo `followStore.activeFollowId()`/`detachCamera()`, mesmo anel de "seguindo", clique em settlement/agent abre Inspector do jeito que já abria).
- **Decisão de dado — posição absoluta**: usuário revelou que o BACKEND real usa X/Y absoluto único pro mundo inteiro (um NPC dentro de casa ocupa uma posição específica do mapa mundi, sem separação mundo/cidade/casa como o frontend tem hoje). Fixture desta demo continua hierárquica (settlement.gridPosition em unidades de mundo; building/patrol/indoorLocation em unidades LOCAIS de settlement, precisa pro `SettlementStage` desenhar) — mas `map/worldPosition.ts` (novo) é agora o ÚNICO lugar que resolve as duas escalas pra uma posição absoluta única (`agentWorldPosition(fixture, agent, now)`), a fronteira exata onde plugar uma API real que já mande X/Y absoluto sem precisar tocar em quem consome (`WorldStage` já chama só essa função, nunca lê `gridPosition`/`patrolPoints` direto).
- **Reason**: pedido explícito do usuário ("vamos implementar agora o mapa mundi"), com dois requisitos centrais: (1) mesma animação de continuidade que já existe pra entrar num prédio, aplicada a entrar numa cidade a partir do mapa mundi; (2) toda funcionalidade de NPC (seguir, inspecionar) precisa funcionar igual no mapa mundi. Confirmado via pergunta direta: dois renderers + animação de ponte (não unificação total, mais rápido de entregar) e Fases 1+2+4 do doc (não clima/estações/histórico, doc já diz "só depois").
- **Trade-off**: continuidade não é uma câmera matematicamente contínua de verdade (dois renderers/coordenadas diferentes) — é uma animação de zoom convincente seguida de uma troca de componente, não um único frame ininterrupto. Terreno/rio/florestas são procedurais determinísticos (mesma técnica de `settlementLayout.ts`), não dado canônico de nenhum sistema. `agentWorldPosition` usa a posição do PRÉDIO (não o offset exato do cômodo) pra agents indoors — no LOD do mapa mundi essa diferença é imperceptível, não duplica a transform de interior do `SettlementStage`.
- **Scope**: `web-demo/src/render/{WorldStage,worldLayout}.tsx`, `web-demo/src/map/worldPosition.ts` (novos); `web-demo/src/components/CenterStage.tsx` (troca de renderer na rota "world"); `web-demo/src/map/isoPalette.ts` (remove `TERRAIN_PALETTE` morto, `SETTLEMENT_PALETTE` agora reaproveitada); REMOVIDOS: `web-demo/src/map/{SemanticZoomMap,IsoProjection,usePatrolPosition}.tsx/ts` + testes correspondentes; testes novos/atualizados em `web-demo/tests/{render,map,components}/`.
- **Date**: 2026-08-27
- **Status**: active

### AD-033
- **Decision**: `phase-16-3-web` — três correções no World Map, achadas ao vivo em cima do AD-032: (1) agents ganham LOD de verdade (doc §29-32/§50) — ponto discreto de longe, sprite real (mesma textura do `SettlementStage`) a partir de `SPRITE_REVEAL_ZOOM` (2.2x), decidido uma vez por frame no ticker; (2) clicar um agent no mapa mundi vira seleção INSTANTÂNEA (sem zoom de transição) — só settlements continuam com a animação de "entrar"; `CenterStage` ganha `useSpatialScope` (mesma família de fix do `useFocusBuildingId`, memória via `useRef`) pra decidir se o mapa espacial continua no World ou pula pro Settlement do agent — só pula se a gente JÁ estava dentro de algum settlement quando selecionou, preservando o deep-link direto `/agent/:id` (mostra o settlement, comportamento antigo intacto); (3) `components/MapHoverCard.tsx` (novo) — card de hover LOD (menos informação que a sidebar cheia) ancorado perto do cursor (`event.clientX/Y` + offset), sem bloquear clique (`pointer-events:none`); ligado em settlement/agent do `WorldStage` E building/agent do `SettlementStage` (mesmo componente reusado nos dois renderers).
- **Reason**: usuário reportou 3 bugs testando o AD-032 ao vivo: agents deveriam ter LOD igual o doc pede (círculo de longe, sprite de perto — "idem pra carros/carruagens", mas a demo não tem fixture de veículo nenhum, então não aplicável ainda); clicar um NPC estava "abrindo a cidade" dele em vez de só selecionar (bug real de rota, mesma classe do bug do AD-025); pediu hover-popup estilo LOD pra cidade, e explicitamente "o mesmo comportamento para casas e NPCs" — estendido pro Settlement View também, não só o mapa mundi.
- **Trade-off**: hover no mouse real (pointerover/pointerout no Pixi) foi verificado só por teste (mock do Pixi + wiring), não pixel-a-pixel ao vivo num mouse de verdade — reaproveita a MESMA infra de eventos (`eventMode:"static"`) já validada ao vivo pro clique, risco residual considerado baixo. Sem veículos no fixture desta demo — o pedido "idem para carros, carruagens" fica sem o que renderizar até existir esse tipo de entidade no core.
- **Scope**: `web-demo/src/render/{WorldStage,SettlementStage}.tsx`, `web-demo/src/components/{CenterStage,MapHoverCard}.tsx` (novo), `web-demo/src/styles/tokens.css`, `web-demo/tests/{render,components}/`.
- **Date**: 2026-08-27
- **Status**: active

## Handoff

- **Feature**: Body→combat sizing (AD-031) — **done** on `feat/body-combat-sizing`
- **Base**: `main` (16.4 via PR #8)
- **What**: MuscleMass → combat offense capacity; Height/Weight → damage taken; equipment still FUTURE
- **Tests**: BodyMechanicTests + CombatMechanic/Encounter scoped — 26 passed
- **Prior**: Fase 16.4 World Realism CLOSED

---

### Histórico — pausa paralelismo / gate hygiene (pré-16.3 Execute)


- **Execução paralela PAUSADA (2026-08-25 19:46)**: STOP.json ativo em todos worktrees.
  Locks liberados. Ver `.specs/parallel-execution/STATUS.md` + progress files antes de retomar.
  Worktrees: `LivingWorld` (16.2), `LivingWorld-16-3` (spec renumerada p/ 16.4, worktree/branch mantêm nome "16-3" antigo), `LivingWorld-trilha-c`.
- **Gate hygiene round-2 (2026-08-25, uncommitted)**: sampling+horizonte 1 mês/24h;
  scale gate só 1k; golden gate 100 ticks; Utility/Economy hash 1 mês; API sem
  `DisableParallelization` na collection (8 mutadores com fixture própria). Longos →
  `Category=Scenario`. Checkpoint anterior: `d0942bd`.
- **Gate hygiene (2026-08-25)**: conservação/invariantes longos no gate → **1 mês**
  (`30*24`h) com amostra diária; 1yr/10yr/100yr → `Category=Scenario`. Natality-hash
  sensor só em Scenario (≤90d não diverge no seed 42). Filtro focado: 20 passed ~3s.
- **API fixture hygiene (2026-08-25)**: `LivingWorldApiFactory` + collection de leitura
  (serial entre si; sem DisableParallelization de assembly). Mutadores: fixture própria.
- **Temperature omit / PERF-12 (2026-08-25)**: `MapCellJsonConverter` omite `Temperature`;
  golden + scale-sensor regravados em `d0942bd`. **PERF-12 implementado**: cache de
  fragmentos JSON canônicos por NPC + propriedades estáticas; `TouchCanonical` nos mutadores
  de `Npc`; `IncrementalHasher.MatchesCanonical` verde (1 ano).
- **Fase 16.1 Re-verify 1/3 (2026-08-25)**: AD-009 — só o usuário roda suíte completa.
  PWR-40/41 deferred. 16.2 blocked. WIP 16.1 em `d0942bd`; round-2 gate ainda uncommitted.
- **Round-4 post-ship fixes (2026-08-23) — wall-marker regression + migration hysteresis**: user
  reported cities rendering as tiny circular markers instead of walled footprints, right after the
  round-3 fixes above landed. Two real root causes, one a direct regression from round-3's own fix,
  one a pre-existing latent instability in a much older system that round-3 simply made reachable
  for the first time:
  1. **Regression from `433a219` (FixT13)**: `CityBoundsResolver.ClampSideAgainstOtherCities`'s
     shrink loop floored the candidate side at `1` (1x1) instead of the existing `MinSize` (3) that
     every other sizing path in the file already respects (`SideFor`). A 1x1/2x2 result flips the
     frontend's own size check (`renderer.ts`/`hitTest.ts`: `size.w > 1 || size.h > 1`) from "draw
     walled footprint" to "draw as a small marker" — exactly the reported circles. Fix: floor
     changed to `MinSize`; if even `MinSize` can't reach the required gap (two cities founded
     pathologically close), the gap is allowed to fall short rather than ever violating the map's
     own minimum-viable-city-size invariant (same "decline over degenerate result" philosophy as
     AD-007/`BuildingPlacementResolver`, adapted since this resolver has no `null` to return).
     Commit `015cabf` — `fix(cities): never shrink a city below its minimum viable size when
     avoiding a neighbor`.
  2. **Pre-existing latent instability, exposed (not introduced) by `dynamic-city-growth`**:
     `MigrationSystem` (Fase 8, T12 — predates this feature entirely) scored households against
     every city daily using live-recomputed `EmploymentLevel`/`FoodLevel` and relocated on a strict
     `score > bestScore`, no margin. Two cities close together is now a real, supported state
     (round-3's own fixes), and that made the daily re-scoring self-reinforcingly oscillate:
     relocating a household shifts the very population/food counts that feed tomorrow's score for
     the city it left. Fix: candidate must now beat the current score by `HysteresisMargin` (15%
     relative — `score > bestScore * 1.15`); relative because `ScoreOf`'s scale is weight-dependent
     (`CityRules` weights aren't normalized to sum to 1), so a fixed additive margin would be
     meaningless at one scale and overpowering at another. No cooldown/timer added — the margin
     alone stops the oscillation (proven by a 5-tick stabilization test) and every other rule in
     the file is already timer-free. Commit `703ffaa` — `fix(cities): add migration hysteresis so
     households don't flip-flop between close cities`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 236
    passed, 0 failed (232 baseline + 1 new test for fix 1 + 3 new tests for fix 2).
  - Both discrimination-checked: temporarily reverting each fix's core change made its own new
    test(s) fail (mutants killed) before the real fix was restored and committed.
  - Detail in `.specs/features/dynamic-city-growth/tasks.md` (FixT15, FixT16).

- **Round-3 post-ship fixes (2026-08-23) — population-box cross-city clamp + household poaching**:
  user reported cities' walls touching again AND population "jumping" between two adjacent
  cities, both after FixT8-FixT12 already landed. Two more real root causes, both in
  `dynamic-city-growth`'s own code, both committed:
  1. `CityBoundsResolver.Resolve`'s `otherCityBoundsToAvoid` clamp (FixT8) only ever applied to
     the MERGED overflow boxes — the base population-only `populationBox` was returned unclamped
     whenever there were no owned boxes to merge. Since households don't have real building
     positions yet, this population box is what's actually rendered/dominant on the map, so two
     cities could grow into each other purely from population increase. Fix: new private
     `ClampSideAgainstOtherCities` helper shrinks `populationBox` the same way `ClampOrigin`
     already shrinks it for the map edge. Two existing tests whose assertions assumed the
     population box was immune to cross-city clamping were rewritten against the production code
     path's own (now correctly shrunk) bounds — same precedent as FixT8's own test rewrite.
     Commit `433a219` — `fix(cities): clamp the population-derived bounds box against neighboring
     cities too`.
  2. `SpatialSettlementFoundingSystem.HandleEvent`'s household-reassignment loop had no check that
     a household belonged to the founding cluster's own mother city — it swept up ANY household
     whose head stood inside `clusterBounds`, including households already properly settled in a
     NEIGHBORING city. Combined with fix 1 above (cities can now legitimately sit closer
     together), this repeatedly poached already-settled households back and forth every monthly
     re-scan. Fix: `if (household.City != motherCityId) continue;` guard before the existing
     geometric check. Commit `499d6d1` — `fix(cities): stop spatial founding from poaching
     households that already belong elsewhere`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 232
    passed, 0 failed (230 baseline + 2 new tests).
  - Detail in `.specs/features/dynamic-city-growth/tasks.md` (FixT13, FixT14).

- **Ghost-town fixes (2026-08-23)** — usuário relatou que cidades recém-fundadas continuam
  virando cidade-fantasma (NPCs "vão trabalhar" numa cidade vizinha e voltam). Duas causas raiz
  reais, sem relação uma com a outra, ambas commitadas:
  1. `EmploymentSystem.Tick`/`VacancyIndex` contratavam o primeiro workplace com vaga livre NO
     MUNDO INTEIRO, sem checar `Workplace`/`Npc.City` — causa DIRETA do relato ("vai trabalhar,
     volta"). Fix: `Workplace` ganhou `CityId City` (setado por `ConstructionSystem` na
     construção), `VacancyIndex.FirstWorkplaceWithVacancy` ganhou overload escopado por cidade,
     `EmploymentSystem` usa o overload. Commit `27e9fcc` —
     `fix(economy): scope hiring to the NPC's own city`.
  2. `SpatialSettlementFoundingSystem.HandleEvent`'s reassignment de household pra cidade nova
     testava `household.Location` — campo gravado uma única vez na criação e nunca atualizado,
     então quase nunca refletia onde a família de fato morava no momento da fundação. Fix: troca
     o critério pra `Npc.CurrentLocation` do chefe do household (mesmo padrão já usado 2 linhas
     acima, pro limiar de concentração). `Household.cs` intocado (mutação compartilhada com o
     trabalho de relocation em progresso, não commitado). Commit `aeeb29a` —
     `fix(cities): reassign households by current NPC location, not stale household coordinates`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&(FullyQualifiedName~Cities|FullyQualifiedName~Economy)"`
    — 296 passed, 2 failed (`ScarcityPriceCausalTests`/`FamineCausalChainTests`, pré-existentes e
    já documentados abaixo, confirmados falhando idênticos antes destes 2 fixes).
  - Detalhe completo do Fix 2 em `.specs/features/dynamic-city-growth/tasks.md` (FixT12). Fix 1
    não pertence a nenhuma feature commitada nesta pasta (é `EmploymentSystem`, Fase 5).

- **RETOMADO (2026-08-23) — os 2 fixes que estavam em background quando a sessão pausou foram
  revisados e commitados**: ver detalhe abaixo. Ainda pendente: 1 feature nova
  (`real-household-workplace-buildings`) com Design aprovado mas Tasks/Execute ainda não iniciada.

### Fixes commitados nesta retomada (eram 2 agentes background mortos antes do commit final)

Contexto original: usuário testou ao vivo os fixes anteriores de "seguir NPC entre escopos"
(`497a09d`, `379a665`) e de "cidade colada" (`a6584ad`, `822ba4a`) e **ambos os bugs originais
ainda ocorriam**, cada um por uma causa raiz DIFERENTE. Dois agentes de fix foram disparados em
background, cada um confirmou a causa raiz + testes passando, mas foram interrompidos (`TaskStop`)
um passo antes do commit final — as edições ficaram no working tree, não commitadas.

1. **Fix A — seleção/highlight de NPC seguido some quando ele cruza para um escopo onde está
   "pooled" (ainda não materializado) — commit `e024465`**: `MapView.refreshEntities` tratava
   "sem marcador desenhável no novo escopo" como "não existe mais" e limpava a seleção — um NPC
   pooled legitimamente não tem `NpcVisual`/marcador (`PoolNpcIds` reportado separado), então
   cruzar pra um escopo onde ele está pooled disparava limpeza incorreta. Fix: antes de limpar,
   `MapView` agora consulta a mesma `NpcInspection` que o `NpcInspector` já usa pra distinguir
   pooled de genuinamente ausente (`POOLED_LOD`), e só limpa quando a inspeção confirma que não é
   nenhum dos dois. `simulationStore.refreshNpcInspection` também cacheia `null` sincronamente
   quando não há fonte de inspeção configurada, pra `npcInspectionOf` dar veredito imediato em vez
   de ficar `undefined` pra sempre. Revisão confirmou que o diff era limpo e auto-contido nos 3
   arquivos que o agente tocou (`web/src/components/MapView.tsx`,
   `web/src/state/simulationStore.ts`, `web/tests/MapView.test.tsx`); `web/src/data/contracts.ts`
   tinha o `POOLED_LOD` export entrelaçado com ~10 hunks não relacionados (Stage-4 pré-existente,
   `rest`/`food`/`ProcessVisual`/etc.) — isolado via patch manual (`git apply --cached
   --unidiff-zero` num hunk de 4 linhas) em vez de `git add` do arquivo inteiro. `selectionStore.ts`
   não tinha edição nenhuma (a hipótese original de tocar esse arquivo não se confirmou no diff
   real do agente). `npx vitest run`: 409 passed, 0 failed.
2. **Fix B — cidade nova ainda funda colada numa existente — commit `077ed50`**: o sistema NOVO
   (`SpatialSettlementFoundingSystem`, `a6584ad`/`822ba4a`) só cobre overflow de prédios; households
   ainda não têm `Building` real, então o crescimento passa quase sempre pelo sistema ANTIGO,
   `SettlementFoundingSystem` + `FoundingSitePicker`, que nunca teve checagem de distância mínima
   (só evitava a MESMA célula exata). Fix: `FoundingSitePicker.Pick` agora rejeita qualquer
   candidato a menos de `AbsorptionRingCells` de qualquer OUTRA cidade existente (reusa
   `OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity`), devolve `null` (falha honesta)
   se nenhuma célula do mapa respeita essa distância. `FoundingSitePicker.cs` e
   `FoundingSitePickerTests.cs` eram inteiramente novos/untracked (sem risco de entrelaçamento) —
   commitados como estavam. `NpcEndpointsTests.cs`/`NpcInspectionDtoCoverageTests.cs` (modificados
   no `git status`) confirmados como trabalho Stage-4 pré-existente NÃO relacionado a este fix —
   deixados de fora do commit. Gate estreito
   (`--filter "Category!=Scenario&FullyQualifiedName~Cities"`): 226 passed, 0 failed. Gate completo
   (`bash scripts/test.sh`, backend+frontend): backend 1527 passed / 6 failed, frontend 409 passed /
   0 failed. Das 6 falhas backend: 2 já documentadas/aceitas nesta mesma seção antes desta rodada
   (`ScarcityPriceCausalTests`/`FamineCausalChainTests`, re-tuning de limiares pendente); as outras
   4 (`ProductionCompositionTests.Production_living_system_order_is_explicit_and_stable` + 3
   `GoldenHashesTests`) são causadas por OUTRA camada de trabalho Stage-4 já não-commitada
   (`SpatialSettlementFoundingSystem`/`RelocationArrivalSystem` já registrados no `Program.cs`
   pré-existente-dirty) — confirmado não relacionado a este fix (que não toca registro de sistema
   nenhum) antes de commitar. Ver `.specs/features/dynamic-city-growth/tasks.md` (FixT10) pro
   detalhe completo.
- **Observação adicional do usuário (ainda não endereçada, é o gap `real-household-workplace-buildings` mesmo)**: cidade cresceu 3x3→4x4 mas os NPCs que construíram casas fora da cidade NÃO
  foram incorporados nesse crescimento — confirmado pela investigação: `OwnedBuildingFootprintBoxesWithOwners` só olha `world.Buildings`, e household/NPC "morando fora" hoje é só uma
  coordenada sem `Building` nenhum atrás. Não tem conserto possível no código de crescimento até
  `real-household-workplace-buildings` (spec+design já aprovados, Tasks já escrito, Execute NÃO
  iniciado — ver seção própria abaixo) dar a eles um `Building` real.

### Feature parada, pronta pra Execute quando o usuário disser — `real-household-workplace-buildings`

- **Onde**: `.specs/features/real-household-workplace-buildings/` — `spec.md` e `design.md`
  aprovados pelo usuário, `tasks.md` escrito (5 tasks, 3 fases: T1/T3/T4 paralelas, T2 depende de
  T1, T5 gate final). **Execute NÃO começou** — usuário pediu pra pausar isso e focar nos bugs
  reportados ao vivo (seção "EM ANDAMENTO AGORA" acima) antes de retomar.
- **O que resolve**: households e workplaces nunca tiveram um `Building` real (só uma coordenada
  bare) — cidade no mapa não mostra casa/local de trabalho nenhum. Sem backfill (só mundos novos,
  por pedido explícito do usuário), 1 casa por household sem compartilhamento, posição resolvida
  UMA vez na criação e escrita igual no `Building` e no household/workplace (nunca duas fontes que
  podem dessincronizar).
- **Next step**: quando o usuário disser pra retomar, dispatch Phase 1 (T1 confirma/cria tipo de
  prédio "casa" no catálogo, T3 workplaces do cenário default, T4 workplaces autorados +
  reordenação cidade-antes-de-workplace — os 3 em paralelo), depois Phase 2 (T2, households), depois
  Phase 3 (T5, gate completo) — mesmo padrão de dispatch por fase já usado em `dynamic-city-growth`.

---

- **Feature**: `dynamic-city-growth` — `.specs/features/dynamic-city-growth/` (spec.md, design.md, tasks.md, validation.md) — **fechada e validada** (PASS, round 4), mas com 1 causa raiz nova encontrada em teste ao vivo pós-fechamento (Fix B acima) que a extrapola pro sistema de fundação ANTIGO. Sessão iniciada a partir de um bugfix de visual (fase-15.1-stage-4) onde o usuário pediu, além do bugfix, uma feature nova: cidade sem espaço livre constrói fora dos bounds (overflow), bounds crescem pra absorver overflow próximo, cluster longe o bastante e com população real de verdade (não só prédios) funda cidade nova — reusando a MESMA fórmula/limiar de `SettlementFoundingSystem`, nunca um limiar de contagem de prédios (correção explícita do usuário: "1 casa não monta 1 cidade, 1 pessoa não monta uma cidade, uma sociedade sim").
- **Post-ship fix (2026-08-23, achado em produção pelo usuário)**: cidade nova fundada ("UrVal") apareceu com os muros literalmente colados/sobrepostos aos de uma cidade já existente. Causa raiz: `CityBoundsResolver.Resolve` crescia os bounds de uma cidade só a partir dos próprios prédios de overflow, sem NUNCA checar contra os bounds de nenhuma outra cidade — duas cidades fundadas a uma distância segura podiam crescer uma em direção à outra, tick após tick, até se tocar/sobrepor. Causa compõe: `SpatialSettlementFoundingSystem.HandleEvent` reverificava o limiar de concentração no disparo do evento, mas nunca reverificava a distância de absorção (só checada uma vez, no agendamento) — se outra cidade crescesse durante a espera de `OrganizationTicks`, a fundação seguia mesmo assim. Fix 1 (`a6584ad`): `CityBoundsResolver.Resolve`/`SpatialBoundsResolver.ResolveCity` ganharam um `otherCityBoundsToAvoid` opcional — qualquer prédio de overflow que empurrasse os bounds pra dentro de `AbsorptionRingCells` de outra cidade simplesmente não é absorvido; `CityOccupancy.ResolveGrownBounds` alimenta essa lista com o crescimento PRÓPRIO (não cross-clamped) de cada outra cidade, um único nível não-recursivo pra não reintroduzir o blocker O(2^N) já corrigido nesta mesma feature. Fix 2 (`822ba4a`): `SpatialSettlementFoundingSystem.HandleEvent` agora reverifica a distância de absorção no disparo (`OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity`, exposto como `internal`), dropando a fundação silenciosamente se alguma cidade cresceu pra dentro do alcance durante a espera. Um teste existente (`ResolveGrownBounds_absorbs_an_overflow_building_only_into_its_own_city_even_when_another_city_is_geometrically_closer`) tinha uma premissa matematicamente incompatível com o novo invariante (prédio geometricamente mais perto de uma cidade estrangeira do que o próprio anel de absorção) — renomeado/reescrito pra `..._never_absorbs_an_overflow_building_into_a_city_that_is_not_its_owner` com a asserção correta. Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` em 223/223 (220 antes + 3 testes novos), 0 falhas.
- **Fixes/commits desta feature** (T1-T8 + 3 rondas de fix pós-Verifier + AD-007): `16b3ea8 dcdd4ec 77cb124 76940f6 7400416 25bb02c 2fb2895 f15acf3` (T1-T7), depois `596824f 2133401` (round-1 Verifier: blocker O(2^N) na resolução de ocupação + major do clamp de mapa/land-scarcity), depois `9a517bf 7fcfb61 3fe4c18 42e4305 e9524d1 142dd08` (round-2 Verifier: 6 gaps minor, incluindo um bug real — projeto de construção land-scarce sendo descartado sem retry), depois `f2219bc e48b15a` (AD-007: fila de construção passou a pular-adiante um projeto land-scarce em vez de bloquear os outros atrás dele, decisão de produto tomada em chat depois de 3 opções apresentadas). Ver `validation.md` pro histórico completo das 4 rodadas do Verifier.
- **In-progress**: nenhum — feature fechada, `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` em 220/220, sensor de mutação 0 sobreviventes na rodada final.
- **Achado incidental corrigido no caminho** (fora do escopo desta feature, mas bloqueava o gate dela): `CropSystem.cs` (trabalho Stage-4 não commitado, pré-existente na working tree) não replantava no mesmo tick da colheita, cortando a produção de comida ~pela metade e colapsando a população em `PopulationBaselineTests`/`ScaleScenarioFixtureTests`. Corrigido (uncommitted, pertence à sessão de Stage-4 do usuário, não a esta feature) — `PopulationBaselineTests` voltou a passar depois do fix. `FamineCausalChainTests`/`ScarcityPriceCausalTests` ainda precisam de re-tuning dos limiares de choque pro novo modelo de ciclo de plantio (não tocado, decisão do usuário).
- **Uncommitted files**: a working tree já tinha um volume grande de trabalho Stage-4 não commitado ANTES desta sessão começar (relocation/crops/founding-site-picker/etc. — `RelocationArrivalSystem.cs`, `FoundingSitePicker.cs`, `ConstructionDemandSystem.cs` entre outros, mais o fix do `CropSystem.cs` acima) — nenhum deles foi tocado/commitado por esta sessão alem do fix pontual do crop replant; o usuário disse que vai commitar esse trabalho separadamente. `HEAD` (`e48b15a`) sozinho NÃO compila sem essa camada não commitada (T5 bundlou um hunk de `MigrationSystem.cs` que depende de `Household.BeginRelocation`/`RelocationArrivalSystem.cs` ainda não commitados — aceito explicitamente pelo usuário, ver `validation.md`).
- **Next step**: usuário decide — (a) commitar o resto do trabalho Stage-4 solto na working tree (relocation/crops/founding-site-picker), (b) retomar Fase 15.1 Stage 4 oficial em T10 (Demand-driven construction — ver `.specs/features/phase-15.1-stage-4-living-world/tasks.md`, ainda não tocado nesta sessão, T1-T9 done), ou (c) re-tunar os testes causais de fome/escassez pro novo modelo de crops.
- **Blockers**: nenhum técnico.
- **Branch**: main.

---

### Histórico anterior (fase 15.1 stage 4, pré-dynamic-city-growth) — mantido como referência

- **Feature**: Fase 15.1, Stage 4 (Living World Integration) — `.specs/features/phase-15.1-stage-4-living-world/tasks.md`, **Phase 2 em andamento**: T1-T9 done, T10 next.
- **Phase / Task**: T9 (comuta de propósito pro trabalho, LWV-02.3/LWV-06) implementado via `tlc-spec-driven` Execute — commit `6dbfb02`. Fix: `BehaviorDecisionSystem`'s ambient wander pra `Work` agora exige `Employer` real (sem ele, NPC fica bloqueado em vez de fingir trabalhar) — ver AD-006 acima pro raciocínio completo e o trade-off de determinismo. `tests/LivingWorld.Tests/Stage4/PurposefulCommuteTests.cs` novo (6 testes); `tests/baselines/action-switches.json` regravado deliberadamente no mesmo commit.
- **In-progress**: nenhum — T9 commitado, gate (`PurposefulCommuteTests` + safety sweep de `Behavior|Economy|Population|Stage4`) verde.
- **Next step**: T10 (Demand-driven construction — cidade sem capacidade de trabalho enfileira construção real, não fabrica trabalho). Verifier de feature só dispara depois da última task do stage (T17) — não antes.
- **Blockers**: nenhum.
- **Uncommitted files**: nenhum (só `resultado.txt`, pré-existente, não relacionado a nenhuma sessão de trabalho).
- **Branch**: main.

---

### Histórico anterior (fase 16, pré-stage-4) — mantido como referência

- **Feature**: Fase 16 (perf da suíte de testes) **fechada e validada** — `.specs/features/phase-16-perf-test-suite/validation.md`, PASS. Suite completa (sem filtro, todos os `Category=Scenario` inclusos): **8h03m19s → 36m7s** (13.4×). Teste-alvo isolado (`Ten_k_population_ten_years_within_perf_budget`): **7h45m12s → 24m30s** (19×). Meta de <1h batida.
- **Fixes commitados** (`e275931`..`5609944`): (1) `BehaviorDecisionSystem` memoiza população de cidade 1x/tick; (2) `EventScheduler.Schedule` trocou Add+Sort completo por inserção via busca binária (alimentava um índice que nunca era lido — achado real, ~95% do custo); (3) `RelationshipSystem` removeu um `.OrderBy` inútil no loop de decay diário; (4) `FamilyRules.MaxCohabitationGroupSize` (AD-005) — teto no par-a-par O(k²) de cohabitation, decisão de produto aprovada pelo usuário em chat (muda resultado só em grupos grandes/escala industrial; golden hashes e baseline de scale-sensor regravados deliberadamente).
- **2 falhas remanescentes na suite completa** (`FamilyPairedScenarioTests.Vitality_cv_...`, `LongRunScaleTests.Storage_cost_per_alive_npc_stable_across_horizons`) são as MESMAS falhas pré-existentes já presentes no baseline original (T1) — confirmadas idênticas, fora de escopo desta fase, não são regressão.
- **Achado incidental, não investigado a fundo** (mesma classe do "mass die-off" já registrado em fases anteriores nesta seção): `ScaleScenarioFixture.CreateWorld(seed:42, pop:10_000)` crashou de 7.342 pra 447 vivos em ~4,5 anos simulados durante os diagnósticos desta fase — população cai muito rápido mesmo no cenário de escala calibrado (`ScaleEconomyCatalog`/`ScaleFamilyRules`). Não é objetivo desta fase (perf, não balanceamento), mas é sinal de que o problema de balanceamento já suspeitado antes (fome/economia em cenários grandes) continua real.
- **Next step**: backlog antigo ainda aberto: decisão sobre "Continuar"/save (slot único vs. slots múltiplos) e balanceamento de fome/economia em população grande — nenhum dos dois foi tocado desde então.

### Histórico anterior (fase 15.1 bugfix, pré-fase-16) — mantido como referência

- **Feature**: Fase 15.1 (VTT frontend redesign) fechada e validada (`.specs/features/phase-15.1-vtt-frontend-redesign/validation.md`, PASS). Sessão atual: bugfix pós-fechamento no fluxo de "criar mundo"/simulação ao vivo, relatado pelo usuário em 2 rodadas. **Fase ainda NÃO fechada de novo pelo usuário** — ele mesmo vai testar e rodar o gate de cenário antes disso.
- **Phase / Task**: Fora do fluxo `tlc-spec-driven` normal — bugfix direto, sem tasks.md próprio. Investigação com API+web reais rodando (curl direto + `preview_start`/`Claude_Browser`), não só leitura de código — 2 dos 3 root causes desta sessão só apareceram testando ao vivo.

- **Rodada 1 (bugs 1-2, commit `3aac17e`) — completa e confirmada ao vivo**:
  1. Cancelar criação não voltava ao menu, "abria um mundo" — `App.tsx`: `cancelCreatingWorld`/`hasEnteredWorldRef`.
  2. Nome digitado nunca chegava na API (`createWorld()` não enviava `name`) → sempre "Name é obrigatório"; "Começar" também passou a exigir nome preenchido.

- **Rodada 2 (bugs 3-4-5, commit `2776a2b`) — completa, testada por filtro estreito, NÃO confirmada ao vivo pelo usuário ainda**:
  3. Header mostrava "Criar mundo" já dentro de um mundo rodando — `App.tsx`: agora só existe a partir do `StartMenu`; dentro do jogo só "Cancelar" (durante criação) ou nada.
  4. **População/assentamentos nunca apareciam** (nem NPCs, nem cidade real) — `PopulationSeeder` sempre criava `Npc`/`Household` com `City = default`, invisíveis em toda projeção. Afetava 100% dos mundos criados (branco ou template), bug pré-existente à fase 15.1. Fix: `ScenarioLoaderV2.LoadWorld` funda/reaproveita uma cidade real em `population.Village` antes de semear, `CityId` propagado até `Npc`/`Household`.
     - **Regressão pega na própria verificação**: `PairIntoHouseholds` recebeu o `city` mas não repassava pro `new Household(...)` — `Npc.City` certo, `Household.City` ficava default. Corrigido no mesmo commit.
  5. Cidade renderizada maior que o mapa — `CityBoundsResolver` usava tamanho fixo 34×24 (herdado de um placeholder de grid local antigo).

- **Rodada 3 (bugs achados agora, POR COMMITAR) — 3 itens do usuário nesta mensagem**:
  - **5b — cidade AINDA maior que o mapa, confirmado ao vivo via curl** (`GET /visual/subscribe?scope=World` num mundo 20×20/pop 150 mostrou `bounds: {width:25,height:25}` — meu fix da rodada 2 escalava só por população, sem considerar o mapa: `sqrt(150)*2≈24.5`. **Fix**: `CityBoundsResolver.Resolve` ganhou `mapWidth`/`mapHeight`, multiplicador caiu de ×2 pra ×1, e o lado nunca excede `min(mapWidth, mapHeight)` — chamada em `GlobalProjector.cs` passa `world.Map.Width/Height`. Teste novo: `City_bounds_never_exceed_the_smaller_map_dimension...` reproduz exatamente o caso "Cidade média" (20×20, pop 150) que estourava.
  - **NPCs amontoados e parados, não se movem** — 2 causas, uma real bug crítico:
    - **Causa raiz confirmada**: `run.cmd` nunca setava `TICK_LOOP_ENABLED=true` — sem essa env var, `TickLoopService` nunca roda como `IHostedService` (só é ativado assim em `Program.cs:95`, desabilitado por padrão pra nenhum teste ganhar tick sozinho). Resultado: **o relógio da simulação NUNCA avança no app real, mesmo clicando Play** — só nos testes que chamam `RunOneCycle()` direto. **Fix**: `run.cmd` agora seta a env var na janela da API. Usuário precisa reabrir via `run.cmd` (ou setar a env var manualmente) pra ver efeito — a API antiga continua sem tick loop até reiniciar.
    - **Causa secundária, não é bug**: todo NPC seedado nasce na MESMA célula (`VillageX/VillageY`, um único ponto) — é assim que `PopulationSeeder`/`PopulationGenerator` sempre funcionaram, não é regressão desta sessão. Ficam "amontoados" até o tick loop rodar de verdade e sistemas de comportamento os moverem. Com o fix acima, isso deve resolver sozinho ao longo do tempo simulado — **não mexi na lógica de dispersão inicial**, só destravei o motor.
  - **"Continuar" sem indicar save real** — usuário pediu pra desabilitar já que "hoje não tem [salvar]". **Investigado, NÃO alterado**: hoje EXISTE persistência de fato — `Program.cs:40` (`worldRunner.LoadLatest()`) carrega o último snapshot salvo no SQLite ao subir a API, e `WorldCreateEndpoints` salva a cada `/worlds/create`. Ou seja "Continuar" já reconecta a um mundo persistido de verdade entre reinícios da API — não é um botão morto. **Decisão pendente do usuário**: ele quis dizer "não tem save manual/slots explícitos" (aí a leitura async é diferente — vale considerar UI melhor, ex. mostrar "última vez salvo" ou nome do mundo persistido) ou realmente achava que não persistia nada? Não desabilitei o botão porque a premissa ("hoje não tem") parece factualmente incorreta — perguntar antes de remover uma funcionalidade que já funciona.

- **Arquivos alterados nesta sessão, POR COMMITAR** (`git status` no momento da pausa):
  - `run.cmd` (TICK_LOOP_ENABLED=true)
  - `src/LivingWorld.Api/Visual/GlobalProjector.cs` (passa map width/height pro resolver)
  - `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs` (cap por mapa)
  - `src/LivingWorld.Domain/Cities/SpatialBoundsResolver.cs` (idem, assinatura)
  - `tests/LivingWorld.Tests/Cities/BuildingFootprintAndPlacementTests.cs` (teste novo do cap por mapa)
  - `tests/LivingWorld.Tests/Geography/SpatialAddressAndScaleTests.cs` (assinatura nova)
  - `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs` (assinatura nova)

- **Verificação feita nesta rodada**:
  - `dotnet build LivingWorld.sln` — verde.
  - `dotnet test --filter "ScenarioLoaderV2Tests|BuildingFootprintAndPlacement|SpatialAddressAndScale|GlobalProjectorTests|WorldCreateEndpointsTests|PopulationGeneratorTests|CityAndBuildingAuthoringTests"` — **82 passed, 0 failed**, ~2s (filtro estreito, por instrução explícita do usuário: **checar a tag antes de rodar testes de API, não rodar o que não precisa** — nenhum filtro amplo/sem tag foi rodado nesta rodada).
  - **NÃO rodado**: web (`vitest`/`tsc`) — nenhum arquivo `web/` mudou nesta rodada 3. `Category=Scenario` — fica com o usuário, conforme pedido dele.
  - **NÃO reconfirmado ao vivo no browser** — usuário disse que quer testar ele mesmo depois que eu terminar.

- **Rodada 4 (commit `f9ceee0`) — usuário reabriu via `run.cmd` atualizado e testou de novo, 3 achados novos**:
  1. **"Continuar" abriu um mundo que não era o que ele tinha gerado por último** — investigado ao vivo (curl direto na API do usuário, processo já rodando, não tocado). Causa raiz: `PersistentWorldRunner`/`SqliteWorldRepository` mantêm **um único slot persistido** (`BranchId.Root`, sempre tick 0 — `SaveSnapshotWithEvents` faz upsert nessa mesma chave a cada `/worlds/create`). "Continuar" == `worldRunner.LoadLatest()` no boot da API == literalmente "o que quer que tenha sido o ÚLTIMO create nesse processo, de qualquer sessão". **O mundo "aleatório" que ele viu era resíduo de UM DOS MEUS PRÓPRIOS testes ao vivo desta investigação** (mesmo seed/preset determinístico, por isso os IDs de cidade batiam com testes anteriores meus) — não um bug de "Continuar" escolher errado, é o design de slot único fazendo exatamente o que faz. **Não é bug, é a mesma limitação de design que ele já tinha levantado antes** ("só faz sentido Continuar se der pra salvar") — CONFIRMADO na prática agora: qualquer create (inclusive um meu, de teste) pisa no que "Continuar" mostra pra ele. Decisão de produto pendente, ver Next step.
  2. **1 assentamento colocado, 2 cidades no mundo rodando** — confirmado ao vivo. Causa raiz REAL (diferente do que eu achava na rodada 2): `defaultScenarioForm().cities` sempre injetava uma cidade fantasma fixa em (2,2), sem nenhuma relação com o assentamento clicado no mapa (`form.settlements`, decorativo) nem com `villageX/Y` (onde a população nasce de fato). O fix da rodada 2 (fundar cidade real onde a população nasce) então sempre criava uma SEGUNDA cidade ao lado da fantasma vazia. **Fix**: `cities: []` por padrão (igual aos 3 templates reais do `DefaultPeriodSeeder`, que nunca tiveram esse problema) — `web/src/scenarioDefaults.ts`. **Confirmado ao vivo, mundo novo criado só com o padrão (sem clicar nada extra)**: exatamente 1 cidade, em (5,5) (= village), população real 36, bounds 6×6 num mapa 20×20 — sem estourar.
  3. **NPCs amontoados/parados** — parcialmente explicado, achado preocupante novo: no mundo de teste acima, a população (36) **foi a zero entre o tick ~150 e ~325** (poucos dias simulados) — confirmado via 3 leituras sucessivas de `/visual/subscribe`, `externalNpcs` também zerou, nenhum NPC responde em `/npcs/{id}`. Não investigado a fundo (fora do escopo dos 3 bugs originais, achado incidental) — hipótese mais provável: fome/economia (preset em branco não configura nenhuma produção de comida — `Workplaces: []`, `HungerDecayPerHour: 2` — população pode estar morrendo de fome em ~2-4 dias simulados antes de ter chance de se dispersar). **Se for isso, o "não se movem" original também se explica**: não é que travaram, é que morreram antes de ter tempo de andar. **NÃO CORRIGIDO** — precisa decisão: é esperado (preset em branco == sobrevivência difícil de propósito) ou é bug de balanceamento a corrigir (dar comida inicial/produção mínima por padrão)?
  4. **Pergunta sobre cores do terreno** (azul/roxo/musgo/dourado) — respondida, não é bug: `colorById.ts`/T37 geram cor procedural determinística por id de terreno (ângulo áureo em HSL) — **não há semântica de bioma real ainda** ("grama"/"deserto"/"água" não existem como conceito, só ids numéricos coloridos de forma estável). Azul == água/rio (isso sim é modelado, camada `Rivers`); as demais cores (roxo/musgo/dourado) são só terrenos-id diferentes sem nome nem arte própria — decisão de produto já registrada em fases anteriores (sem pipeline de arte/paleta temática ainda).

- **Verificação da rodada 4**: `npx vitest run` — 267 passed (só `scenarioDefaults.ts` mudou, frontend puro, sem necessidade de rebuild/teste backend). Confirmado ao vivo via browser + curl direto na API do usuário (processo dele, só leitura — nunca reiniciado/matado por mim nesta rodada).

- **Processos auxiliares**: API do usuário (porta 5289) e o dev server dele (porta 5173) foram só CONSULTADOS (curl/fetch), nunca reiniciados ou mortos nesta rodada 4. Minha própria tentativa de subir uma segunda API na 5289 falhou por porta ocupada (esperado) e não deixou processo pra trás.

- **In-progress**: nenhum — rodada 4 commitada. Item novo (morte em massa da população) fica registrado como achado, não corrigido.

- **Next step**:
  1. **Usuário vai testar ao vivo de novo** (fix de "2 cidades" só precisa reload do Vite, sem restart de API) e depois **rodar `bash scripts/test.sh --filter Category=Scenario`** ele mesmo.
  2. **Decisão pendente sobre "Continuar"/save**: confirmado que é slot único de verdade — perguntar se o usuário quer (a) manter como está agora que ele entende a causa, (b) UI indicando qual mundo será continuado (nome/data), ou (c) save slots múltiplos de verdade (feature nova, endpoint novo).
  3. **Decisão pendente sobre morte em massa de população** (achado na rodada 4, não investigado a fundo): balancear economia/fome do preset em branco, ou é esperado?
  4. Rodar a suíte ampla (`Population|Cities|...`) inteira pelo menos uma vez antes do próximo fechamento de fase — fora de sessão interativa, achado real: ao menos um teste no padrão "Population" (não `Category=Scenario`) leva 12+ minutos sozinho.

- **Blockers**: nenhum técnico. 2 decisões de produto pendentes (Continuar/save, balanceamento de fome) antes de considerar a fase realmente fechada de novo.
- **Uncommitted files**: nenhum — tudo commitado (`f9ceee0`).
- **Branch**: main
