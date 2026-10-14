# Decisões 1/4


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
