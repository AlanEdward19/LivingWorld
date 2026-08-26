# Fase 16.3-web — World Explorer UX Demo Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-16-3-web/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Gerado por amostragem de `web/` (projeto irmão, mesma stack) — confirmar antes de Execute. `web-demo/` não existe ainda; convenção herdada de `web/package.json`/`web/vite.config.ts`/`web/tests/**` (vitest + jsdom + @testing-library/react, testes em `tests/` espelhando `src/`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Lógica pura (`IsoProjection`, `NavigationStore`, `SearchIndex`, fixture integrity) | Unit | Todo branch/caso de borda (canto do grid, pilha vazia, busca sem resultado, referência quebrada) | `web-demo/tests/**/*.test.ts` | `npm --prefix web-demo test` |
| Componentes React (Views, `IsoTileRenderer`, `SemanticZoomMap`, `WhyPanel`, `CausalExplorer`, `NpcToken`) | Component (`@testing-library/react`) | Happy path de render + toda interação que a AC do spec descreve (clique navega, toggle funciona) | `web-demo/tests/**/*.test.tsx` | `npm --prefix web-demo test` |
| Fixture (`fixture/oakbridge.ts`) | Integrity (unit) | Todo `id` referenciado (`householdId`, `settlementId`, `causeEventId`, `affectedAgentIds` etc.) existe no fixture | `web-demo/tests/fixture/integrity.test.ts` | `npm --prefix web-demo test` |
| Config/build (`vite.config.ts`, `tsconfig.json`, `package.json`) | none | build gate only | — | `npm --prefix web-demo run build` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Unit (lógica pura) | Yes | Sem estado compartilhado, fixture importado read-only | Vitest default (paralelo por arquivo) |
| Component (`@testing-library/react`) | Yes | `render()` isolado por teste, jsdom por worker | Padrão já usado em `web/tests/*.test.tsx` |
| Integrity | Yes | Só leitura do fixture | N/A — sem I/O |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Depois de cada task com testes unit/component | `npm --prefix web-demo test -- <pattern>` (vitest filtra por nome de arquivo/describe) |
| Full | Depois de cada fase | `npm --prefix web-demo test` |
| Build | Fechamento da demo | `npm --prefix web-demo run build` |

---

## Execution Plan

### Phase 1: Foundation — scaffold, fixture, token de NPC (Sequential)

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Mapa isométrico (Sequential)

```
T6 → T7 → T8 → T9 → T10
```

### Phase 3: Navegação (Sequential)

```
T11 → T12
```

### Phase 4: P1/P1b — fluxo vertical MVP (Sequential com 1 ramo paralelo)

```
T13 → T14 → T15 → T16 → T17 ┬→ T18 [P]
                             └→ T19 [P]
T18, T19 → T20
```

### Phase 5: P2 — Timeline/Life/Follow/Feed (Sequential com paralelo)

```
T21 → T22 ┬→ T23 [P]
          └→ T24 [P]
T23, T24 → T25
```

### Phase 6: P3 — Threads/Debug Mode/Search (Sequential)

```
T26 → T27 → T28
```

### Phase 7: Fechamento (Sequential)

```
T29 → T30 → T31
```

---

## Task Breakdown

### T1: Scaffold `web-demo/` — Vite + React + TS + Vitest

**What**: Novo projeto em `web-demo/`, `package.json`/`vite.config.ts`/`tsconfig.json` espelhando `web/` (mesmas versões de React/Vite/TS/Vitest/jsdom/@testing-library), sem dependência de `web/`; `npm --prefix web-demo install` roda limpo.
**Where**: `web-demo/package.json`, `web-demo/vite.config.ts`, `web-demo/tsconfig.json`, `web-demo/tests/setup.ts`, `web-demo/index.html`, `web-demo/src/main.tsx`
**Depends on**: None
**Reuses**: padrão de `web/package.json`/`web/vite.config.ts`/`web/tests/setup.ts`
**Requirement**: (suporte a todos)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] `npm --prefix web-demo run dev` sobe uma página em branco sem erro
- [x] `npm --prefix web-demo run build` passa
- [x] `npm --prefix web-demo test` roda (mesmo sem teste ainda, comando funciona)
- [x] Nenhum import de `web/src/**`

**Tests**: none
**Gate**: build
**Commit**: `chore(web-demo): scaffold new isolated Vite+React+TS+Vitest project`

---

### T2: `fixture/oakbridge.ts` + tipos `WorldFixture`

**What**: Dado estático completo (Oakbridge, Mira Valen, household Valen, relações Rowan/Corvin, cadeia causal de grão, Story Thread "The Oakbridge Food Crisis") conforme design.md § Data Models.
**Where**: `web-demo/src/fixture/oakbridge.ts`, `web-demo/src/fixture/types.ts`
**Depends on**: T1
**Reuses**: nenhum
**Requirement**: suporte a todas

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Fixture cobre todos os elementos citados no doc#108-130 (settlement pulse, household, agent, why factors, cadeia causal, story thread, timeline de eventos, life milestones)
- [x] Tipos exportados batem 1:1 com design.md § Data Models
- [x] Gate check passa: `npm --prefix web-demo run build` (type-check)

**Tests**: none
**Gate**: build
**Commit**: `feat(web-demo): add Oakbridge world fixture matching doc example`

---

### T3: Teste de integridade do fixture

**What**: Todo `id` referenciado em qualquer campo do fixture (`householdId`, `settlementId`, `causeEventId`, `affectedAgentIds`, `affectedHouseholdIds`, `withAgentId`, `eventIds`/`householdIds`/`agentIds` em `storyThreads`) existe na lista correspondente.
**Where**: `web-demo/tests/fixture/integrity.test.ts`
**Depends on**: T2
**Reuses**: nenhum
**Requirement**: suporte (Risk do design.md — referência quebrada)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Teste falha deliberadamente se um id for quebrado (prova removendo uma referência temporariamente, confirma falha, restaura)
- [x] Gate check passa: `npm --prefix web-demo test -- integrity`

**Tests**: unit
**Gate**: quick
**Commit**: `test(web-demo): verify referential integrity of the fixture`

---

### T4: Portar `npc/appearance.ts`

**What**: Cópia literal de `web/src/npcAppearance.ts` pra `web-demo/src/npc/appearance.ts`, zero alteração de lógica.
**Where**: `web-demo/src/npc/appearance.ts`
**Depends on**: T1
**Reuses**: `web/src/npcAppearance.ts` (copiado, não importado)
**Requirement**: suporte (Goal "reusar token de NPC")

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] `appearanceForNpc(id)` produz o mesmo resultado que o original pro mesmo id (teste comparando saída pra 5+ ids fixos)
- [x] Gate check passa: `npm --prefix web-demo test -- appearance`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(web-demo): port NPC appearance phenotype generator from web/`

---

### T5: Portar `npc/NpcToken.tsx`

**What**: Cópia literal de `web/src/components/NpcTokenSvg.tsx` pra `web-demo/src/npc/NpcToken.tsx`.
**Where**: `web-demo/src/npc/NpcToken.tsx`
**Depends on**: T4
**Reuses**: `web/src/components/NpcTokenSvg.tsx` (copiado)
**Requirement**: suporte

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] `<NpcToken id="mira-valen" />` renderiza sem erro (teste de render)
- [x] Gate check passa: `npm --prefix web-demo test -- NpcToken`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): port NpcToken SVG component from web/`

---

### T6: `map/IsoProjection.ts`

**What**: `toScreen`/`toGrid` — projeção isométrica 2:1 pura, com inverso testado (round-trip).
**Where**: `web-demo/src/map/IsoProjection.ts`
**Depends on**: T1
**Reuses**: nenhum
**Requirement**: WEBX-15 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] `toGrid(toScreen(x, y, ...))` retorna `(x, y)` original pra vários pontos, incluindo cantos do grid
- [x] Casos de borda de altura de bloco sobreposta cobertos (Risk do design.md)
- [x] Gate check passa: `npm --prefix web-demo test -- IsoProjection`
- [x] Test count: 8+

**Tests**: unit
**Gate**: quick
**Commit**: `feat(web-demo): add isometric 2:1 grid-to-screen projection math`

---

### T7: `map/isoPalette.ts`

**What**: Paleta nova (3 faces por bloco: top/left/right) por `BuildingKind`, terreno, settlement — neutra/atmosférica, não rústica.
**Where**: `web-demo/src/map/isoPalette.ts`
**Depends on**: T1
**Reuses**: nenhum (redesenho confirmado)
**Requirement**: WEBX-15

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Toda `BuildingKind` do fixture (`residence`/`agriculture`/`forge`/`generic`) tem paleta definida
- [x] Gate check passa: `npm --prefix web-demo test -- isoPalette`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(web-demo): add new neutral isometric building/tile palette`

---

### T8: `map/IsoTileRenderer.tsx`

**What**: `<IsoTile>` — desenha um bloco isométrico (3 faces `<polygon>` SVG) com `onClick`.
**Where**: `web-demo/src/map/IsoTileRenderer.tsx`
**Depends on**: T6, T7
**Reuses**: `IsoProjection`, `isoPalette`
**Requirement**: WEBX-15

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Renderiza 3 faces com cores da paleta correta pro `kind` passado
- [x] `onClick` dispara com o `gridX`/`gridY` corretos (teste de clique via `@testing-library/react`)
- [x] Gate check passa: `npm --prefix web-demo test -- IsoTile`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): render isometric building blocks as SVG polygons`

---

### T9: `map/SemanticZoomMap.tsx` — nível "mundo"

**What**: Componente de mapa, zoom inicial "mundo" — mostra Oakbridge + assentamentos vizinhos mínimos (rótulo só), sem prédios/NPCs.
**Where**: `web-demo/src/map/SemanticZoomMap.tsx`
**Depends on**: T8
**Reuses**: `IsoTile`, `IsoProjection`
**Requirement**: WEBX-11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Nível "mundo" não renderiza nenhum `IsoTile` de prédio nem `NpcToken` (spec P1b AC1)
- [x] Clicar num assentamento chama `onSelectSettlement` (teste)
- [x] Gate check passa: `npm --prefix web-demo test -- SemanticZoomMap`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add world-level semantic zoom map`

---

### T10: `SemanticZoomMap` — níveis "distrito" e "agente"

**What**: Extend com os outros 2 níveis: "distrito" mostra prédios (sem NPC), "agente" mostra `NpcToken`s posicionados e clicáveis.
**Where**: `web-demo/src/map/SemanticZoomMap.tsx` (extend)
**Depends on**: T9, T5
**Reuses**: `IsoTile`, `NpcToken`
**Requirement**: WEBX-12, WEBX-13, WEBX-14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Nível "distrito" mostra prédios do settlement selecionado, sem NPC (spec AC2)
- [x] Nível "agente" mostra NPCs clicáveis, `onSelectNpc` disparado corretamente (spec AC3)
- [x] Densidade de informação muda visivelmente entre os 3 níveis (teste conta elementos renderizados por nível)
- [x] Gate check passa: `npm --prefix web-demo test -- SemanticZoomMap`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add district and agent semantic zoom levels`

---

### T11: `nav/NavigationStore.ts`

**What**: Pilha de breadcrumb + `Route` union type + `push`/`back`/`current`/`breadcrumb`, `useSyncExternalStore`-compatible (mesmo idioma de `web/src/state/*Store.ts`).
**Where**: `web-demo/src/nav/NavigationStore.ts`
**Depends on**: T1
**Reuses**: idioma de store de `web/src/state/simulationStore.ts`/`viewStore.ts` (implementação própria)
**Requirement**: WEBX-08 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] `push`/`back` mantêm pilha consistente (teste com sequência de 5+ pushes e backs)
- [x] `breadcrumb()` retorna a pilha completa na ordem certa
- [x] Gate check passa: `npm --prefix web-demo test -- NavigationStore`
- [x] Test count: 6+

**Tests**: unit
**Gate**: quick
**Commit**: `feat(web-demo): add NavigationStore breadcrumb stack`

---

### T12: Sincronização de URL (deep-link)

**What**: `NavigationStore` sincroniza com `history.pushState`/`popstate` — URL reflete o topo da pilha; navegação direta pra URL restaura o estado correto; id inexistente redireciona pra World View (Edge Case da spec).
**Where**: `web-demo/src/nav/NavigationStore.ts` (extend)
**Depends on**: T11, T2
**Reuses**: `history` API nativa
**Requirement**: (Edge Case: deep-link)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Navegar `push` atualiza a URL
- [x] Simular `popstate` (botão voltar do browser) sincroniza a pilha interna sem duplicar entrada
- [x] URL com id inexistente no fixture redireciona pra World View
- [x] Gate check passa: `npm --prefix web-demo test -- NavigationStore`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(web-demo): sync NavigationStore with browser history for deep-linking`

---

### T13: `views/WorldView.tsx`

**What**: Tela raiz — mapa (nível mundo) + resumo do mundo (doc#107) derivado do fixture.
**Where**: `web-demo/src/views/WorldView.tsx`
**Depends on**: T9, T11
**Reuses**: `SemanticZoomMap`, `NavigationStore`
**Requirement**: WEBX-01

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Renderiza resumo do mundo do fixture
- [x] Clicar em Oakbridge chama `NavigationStore.push({kind:"settlement", id:...})`
- [x] Gate check passa: `npm --prefix web-demo test -- WorldView`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add WorldView entry screen`

---

### T14: `views/SettlementView.tsx`

**What**: Settlement Pulse (população, food/employment/migration/construction, eventos recentes) + mapa nível distrito.
**Where**: `web-demo/src/views/SettlementView.tsx`
**Depends on**: T13, T10
**Reuses**: `SemanticZoomMap`
**Requirement**: WEBX-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Valores exatos do fixture exibidos (comparação snapshot com doc#125)
- [x] Clicar no household Valen navega pra `HouseholdView`
- [x] Gate check passa: `npm --prefix web-demo test -- SettlementView`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add SettlementView with settlement pulse`

---

### T15: `views/HouseholdView.tsx`

**What**: Membros, árvore familiar simples, estoque, eventos recentes do household Valen.
**Where**: `web-demo/src/views/HouseholdView.tsx`
**Depends on**: T14
**Reuses**: `NpcToken`
**Requirement**: WEBX-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Mostra Mira/Tomas/Eli/Nora conforme doc#124
- [x] Clicar em Mira navega pra `AgentView`
- [x] Gate check passa: `npm --prefix web-demo test -- HouseholdView`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add HouseholdView with family tree and stock`

---

### T16: `views/AgentView.tsx` + `views/WhyPanel.tsx`

**What**: Identidade/profissão/localização/intent/condição/corpo/household/relações/eventos recentes de Mira + botão/painel Why? (doc#109/#113/#114).
**Where**: `web-demo/src/views/AgentView.tsx`, `web-demo/src/views/WhyPanel.tsx`
**Depends on**: T15
**Reuses**: `NpcToken`
**Requirement**: WEBX-04, WEBX-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Card de Mira bate com o exemplo do doc#113 (nome/idade/profissão/intent/condição/build/household/relações/recent life)
- [x] `WhyPanel` mostra os `whyFactors` do fixture em linguagem humana (doc#114)
- [x] Pelo menos 1 fator é clicável
- [x] Gate check passa: `npm --prefix web-demo test -- AgentView|WhyPanel`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add AgentView with Why panel`

---

### T17: `views/CausalExplorer.tsx`

**What**: `WHY? → causa` + `CONSEQUENCES → árvore ramificada` (doc#117-118), sistemas envolvidos listados.
**Where**: `web-demo/src/views/CausalExplorer.tsx`
**Depends on**: T16
**Reuses**: cadeia `causeEventId` do fixture
**Requirement**: WEBX-06

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Clicar num fator do Why abre o Causal Explorer no evento certo
- [x] Árvore de consequências bate com doc#117-118 (Valen household reduced purchases → Mira became VeryHungry → Mira left work early; Baker reduced production; Migration pressure increased)
- [x] Lista de sistemas envolvidos bate com doc#118
- [x] Evento sem `causeEventId` mostra "sem causa anterior conhecida" (Edge Case)
- [x] Gate check passa: `npm --prefix web-demo test -- CausalExplorer`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add CausalExplorer with why/consequences tree`

---

### T18: Wiring mapa → navegação (clique em qualquer nível de zoom) [P]

**What**: Clicar em NPC/settlement diretamente no mapa (qualquer nível) dispara a mesma navegação que clicar na lista (spec P1b AC4).
**Where**: `web-demo/src/views/WorldView.tsx`, `SettlementView.tsx` (extend, conecta `SemanticZoomMap` callbacks ao `NavigationStore`)
**Depends on**: T17
**Reuses**: `NavigationStore`
**Requirement**: WEBX-14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Clicar em Mira no mapa nível "agente" abre o mesmo `AgentView` que clicar na lista (teste compara resultado dos dois caminhos)
- [x] Gate check passa: `npm --prefix web-demo test -- WorldView|SettlementView`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): wire map clicks to the same navigation as list clicks`

---

### T19: Breadcrumb visual + botão voltar [P]

**What**: Componente de breadcrumb visível em toda tela, lendo `NavigationStore.breadcrumb()`; botão voltar preserva estado (spec P1 AC8).
**Where**: `web-demo/src/components/Breadcrumb.tsx` (novo)
**Depends on**: T17
**Reuses**: `NavigationStore`
**Requirement**: WEBX-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Breadcrumb mostra a pilha correta em cada ponto do fluxo P1 AC1-7
- [x] Botão voltar retorna à tela anterior com estado preservado (não reseta pro World View)
- [x] Gate check passa: `npm --prefix web-demo test -- Breadcrumb`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add persistent breadcrumb with working back navigation`

---

### T20: Teste end-to-end do fluxo vertical P1 completo

**What**: Teste único que percorre `World → Settlement → Household → Agent → Why → CausalExplorer → Timeline`-stub clique-a-clique (spec P1 Independent Test), confirmando nenhum passo quebra.
**Where**: `web-demo/tests/flow/verticalSlice.test.tsx` (novo)
**Depends on**: T18, T19
**Reuses**: todas as views de P1
**Requirement**: WEBX-01..08 (fechamento)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Teste percorre os 7 passos do fluxo sem falhar
- [x] Gate check passa: `npm --prefix web-demo test`

**Tests**: component (integration-style)
**Gate**: full
**Commit**: `test(web-demo): verify complete vertical slice navigation flow`

---

### T21: `views/Timeline.tsx`

**What**: Eventos do fixture em ordem cronológica, filtráveis por escopo (World/Settlement/Household/Agent/tipo — doc#121).
**Where**: `web-demo/src/views/Timeline.tsx`
**Depends on**: T20
**Reuses**: `events` do fixture
**Requirement**: WEBX-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Filtro por household Valen mostra só os eventos relevantes (spec P2 Independent Test)
- [x] Clicar num evento do Causal Explorer navega pra esse ponto na Timeline preservando breadcrumb (spec P1 AC7)
- [x] Gate check passa: `npm --prefix web-demo test -- Timeline`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add filterable chronological Timeline`

---

### T22: `state/followStore.ts` + `views/LifeView.tsx`

**What**: `followStore` (toggle em memória, nunca altera fixture); Life View de Mira com marcos do fixture (doc#122).
**Where**: `web-demo/src/state/followStore.ts`, `web-demo/src/views/LifeView.tsx`
**Depends on**: T21
**Reuses**: `lifeMilestones` do fixture
**Requirement**: WEBX-22, WEBX-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Life View mostra os marcos do doc#122 (nascimento, mudança, aprendizado, casamento, filhos, master baker, morte do pai, atual)
- [x] `toggleFollow` marca/desmarca (toggle, não duplica — Edge Case da spec)
- [x] Destaque de follow persiste ao navegar pra outra tela e voltar
- [x] Gate check passa: `npm --prefix web-demo test -- followStore|LifeView`

**Tests**: unit + component
**Gate**: quick
**Commit**: `feat(web-demo): add follow toggle and LifeView milestones`

---

### T23: `components/FollowButton.tsx` — integrado nas views existentes [P]

**What**: Botão Follow reusável, plugado em AgentView/HouseholdView/SettlementView.
**Where**: `web-demo/src/components/FollowButton.tsx`, extend views existentes
**Depends on**: T22
**Reuses**: `followStore`
**Requirement**: WEBX-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Botão aparece nas 3 views, estado visual reflete `followStore`
- [x] Gate check passa: `npm --prefix web-demo test -- FollowButton`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add reusable FollowButton to Agent/Household/Settlement views`

---

### T24: `views/WorldFeed.tsx` [P]

**What**: Lista cronológica agrupada de eventos com timestamp, priorizada por relevância (doc#129-130).
**Where**: `web-demo/src/views/WorldFeed.tsx`
**Depends on**: T21
**Reuses**: `events` do fixture
**Requirement**: WEBX-24

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Feed mostra eventos do fixture agrupados/ordenados
- [x] Gate check passa: `npm --prefix web-demo test -- WorldFeed`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add World Feed chronological event list`

---

### T25: Integração P2 no fluxo de navegação

**What**: Timeline/Life View/World Feed acessíveis a partir de qualquer ponto do fluxo P1 (entradas de navegação adicionadas nas views existentes, conforme design § Architecture).
**Where**: views existentes (extend)
**Depends on**: T23, T24
**Reuses**: `NavigationStore`
**Requirement**: WEBX-21..24 (fechamento)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Timeline acessível de World/Settlement/Household/Agent (spec P2 AC1)
- [x] Gate check passa: `npm --prefix web-demo test`

**Tests**: component
**Gate**: full
**Commit**: `feat(web-demo): wire Timeline/Life/Feed entry points into P1 flow`

---

### T26: `views/StoryThreads.tsx`

**What**: Card "The Oakbridge Food Crisis" (18 events · 4 households · 11 Agents · 6 systems, doc#126) clicável, abre Causal Explorer nesse thread.
**Where**: `web-demo/src/views/StoryThreads.tsx`
**Depends on**: T25
**Reuses**: `storyThreads` do fixture, `CausalExplorer`
**Requirement**: WEBX-31

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Card mostra os números exatos do fixture
- [x] Clicar abre o Causal Explorer no thread certo
- [x] Gate check passa: `npm --prefix web-demo test -- StoryThreads`

**Tests**: component
**Gate**: quick
**Commit**: `feat(web-demo): add Story Threads with Oakbridge Food Crisis card`

---

### T27: `state/modeStore.ts` — Experience ↔ Debug

**What**: Toggle de modo; `AgentView`/`WhyPanel`/`CausalExplorer` trocam linguagem/detalhe exibido sem trocar navegação atual (doc#116).
**Where**: `web-demo/src/state/modeStore.ts`, extend views afetadas
**Depends on**: T26
**Reuses**: idioma de store (`followStore`)
**Requirement**: WEBX-32

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Alternar pra Debug Mode mostra campos técnicos (`WakeReason`/tick/etc do fixture) sem perder a seleção atual (spec P3 Independent Test)
- [x] Gate check passa: `npm --prefix web-demo test -- modeStore`

**Tests**: unit + component
**Gate**: quick
**Commit**: `feat(web-demo): add Experience/Debug mode toggle`

---

### T28: `search/SearchIndex.ts` + busca global na UI

**What**: Busca client-side agrupada por People/Places/Households/Events/Threads (doc#138); campo de busca na UI conectado.
**Where**: `web-demo/src/search/SearchIndex.ts`, `web-demo/src/components/SearchBar.tsx` (novo)
**Depends on**: T27
**Reuses**: `fixture`
**Requirement**: WEBX-33

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [x] Busca "Mira" retorna Mira em People
- [x] Busca sem match retorna estado vazio explícito por categoria (Edge Case)
- [x] Resultado clicável navega pra entidade certa
- [x] Gate check passa: `npm --prefix web-demo test -- SearchIndex|SearchBar`

**Tests**: unit + component
**Gate**: quick
**Commit**: `feat(web-demo): add global search grouped by entity type`

---

### T29: Aplicar o checklist de experiência (doc#147)

**What**: Rodar as perguntas do checklist do doc contra a demo rodando; documentar respostas.
**Where**: `docs/ui/living-world-experience-checklist.md` (novo, se ainda não existir — reusa se `phase-16-3-world-cohesion` já criou)
**Depends on**: T20, T25, T28
**Reuses**: perguntas literais do doc#147
**Requirement**: (Success Criteria da spec)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Todas as perguntas do doc#147 respondidas com evidência (screenshot ou descrição do que se vê)
- [ ] Perguntas centrais ("consigo entender por quê?", "parece um mundo ou um dashboard?") respondidas "sim" — se não, vira gap documentado, não fechamento forçado

**Tests**: none
**Gate**: build
**Commit**: `docs(web-demo): apply living-world-experience-checklist to the demo`

---

### T30: README + comparação visual com `web/` atual

**What**: `web-demo/README.md` explicando propósito (demo isolada, sem integração), como rodar, o que foi redesenhado vs. portado; screenshot lado a lado provando que só o token de NPC é reconhecível como igual (spec P1b Independent Test).
**Where**: `web-demo/README.md`
**Depends on**: T29
**Reuses**: nenhum
**Requirement**: (Success Criteria)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] README explica escopo/isolamento/como rodar
- [ ] Comparação visual documentada (token de NPC igual; prédio/tile/cidade visivelmente diferente)

**Tests**: none
**Gate**: build
**Commit**: `docs(web-demo): add README with scope and visual comparison`

---

### T31: Fechamento — gate completo

**What**: Roda a suíte inteira + build; confirma nenhuma regressão; atualiza `.specs/features/phase-16-3-web/spec.md` traceability pra `Verified`.
**Where**: `.specs/features/phase-16-3-web/spec.md`
**Depends on**: T30
**Reuses**: nenhum
**Requirement**: fechamento de todos os WEBX-*

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `npm --prefix web-demo test` verde
- [ ] `npm --prefix web-demo run build` verde
- [ ] Traceability table atualizada

**Tests**: none
**Gate**: full
**Commit**: `docs(web-demo): close phase 16.3-web demo`

---

## Parallel Execution Map

```
Phase 1 (Sequential): T1 → T2 → T3 → T4 → T5

Phase 2 (Sequential): T6 → T7 → T8 → T9 → T10

Phase 3 (Sequential): T11 → T12

Phase 4 (mostly Sequential): T13 → T14 → T15 → T16 → T17, then:
  ├── T18 [P]
  └── T19 [P]
  T18, T19 → T20

Phase 5 (mostly Sequential): T21 → T22, then:
  ├── T23 [P]
  └── T24 [P]
  T23, T24 → T25

Phase 6 (Sequential): T26 → T27 → T28

Phase 7 (Sequential): T29 → T30 → T31
```

**Parallelism constraint:** T18/T19 tocam arquivos diferentes (`WorldView`/`SettlementView` vs. `Breadcrumb.tsx` novo). T23/T24 tocam arquivos diferentes (`FollowButton`+views existentes vs. `WorldFeed.tsx` novo).

**7 fases** → ofertar sub-agent por fase (sequencial) ao entrar em Execute.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1-T5 | 1 componente/config por task | ✅ Granular |
| T6-T12 | 1 componente/módulo por task | ✅ Granular |
| T13-T20 | 1 view (ou wiring focado) por task | ✅ Granular |
| T21-T25 | 1 componente/store por task | ✅ Granular |
| T26-T28 | 1 componente/store por task | ✅ Granular |
| T29-T31 | 1 entregável de fechamento por task | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | Phase 1 início | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T2 | T2→T3 | ✅ Match |
| T4 | T1 | segue T3 na sequência (dependência real é T1) | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T1 | Phase 2 início | ✅ Match |
| T7 | T1 | segue T6 na sequência (dependência real é T1) | ✅ Match |
| T8 | T6, T7 | T6,T7→T8 | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T9, T5 | T9→T10 (T5 é dependência cruzada de fase, documentada no corpo) | ✅ Match |
| T11 | T1 | Phase 3 início | ✅ Match |
| T12 | T11, T2 | T11→T12 (T2 é dependência cruzada de fase) | ✅ Match |
| T13 | T9, T11 | Phase 4 início | ✅ Match |
| T14 | T13, T10 | T13→T14 | ✅ Match |
| T15 | T14 | T14→T15 | ✅ Match |
| T16 | T15 | T15→T16 | ✅ Match |
| T17 | T16 | T16→T17 | ✅ Match |
| T18 | T17 | T17→T18 [P] | ✅ Match |
| T19 | T17 | T17→T19 [P] | ✅ Match |
| T20 | T18, T19 | T18,T19→T20 | ✅ Match |
| T21 | T20 | Phase 5 início | ✅ Match |
| T22 | T21 | T21→T22 | ✅ Match |
| T23 | T22 | T22→T23 [P] | ✅ Match |
| T24 | T21 | T21→T24 [P] (dependência real é T21, não T22 — roda em paralelo com T23) | ✅ Match |
| T25 | T23, T24 | T23,T24→T25 | ✅ Match |
| T26 | T25 | Phase 6 início | ✅ Match |
| T27 | T26 | T26→T27 | ✅ Match |
| T28 | T27 | T27→T28 | ✅ Match |
| T29 | T20, T25, T28 | Phase 7 início | ✅ Match |
| T30 | T29 | T29→T30 | ✅ Match |
| T31 | T30 | T30→T31 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Config | none | none | ✅ OK |
| T2 | Fixture data | none (matrix: dado puro, integridade coberta por T3) | none | ✅ OK |
| T3 | Fixture integrity test | Unit | unit | ✅ OK |
| T4 | Lógica pura (`appearance.ts`) | Unit | unit | ✅ OK |
| T5 | Componente (`NpcToken`) | Component | component | ✅ OK |
| T6 | Lógica pura (`IsoProjection`) | Unit | unit | ✅ OK |
| T7 | Lógica pura (`isoPalette`) | Unit | unit | ✅ OK |
| T8-T10 | Componentes (mapa) | Component | component | ✅ OK |
| T11-T12 | Lógica pura (`NavigationStore`) | Unit | unit | ✅ OK |
| T13-T19 | Componentes (views) | Component | component | ✅ OK |
| T20 | Teste de integração | Component (integration-style) | component | ✅ OK |
| T21 | Componente (`Timeline`) | Component | component | ✅ OK |
| T22 | Lógica pura + componente | Unit + Component | unit + component | ✅ OK |
| T23-T24 | Componentes | Component | component | ✅ OK |
| T25 | Wiring (componentes existentes) | Component | component | ✅ OK |
| T26 | Componente | Component | component | ✅ OK |
| T27 | Lógica pura + componente | Unit + Component | unit + component | ✅ OK |
| T28 | Lógica pura + componente | Unit + Component | unit + component | ✅ OK |
| T29-T31 | Docs/fechamento | none | none | ✅ OK |

Nenhuma violação.

---

## Tips

- **[P] = Order-free** dentro da mesma fase
- **7 fases** → ofertar sub-agent por fase ao entrar em Execute
- **Token de NPC é o único port literal** (T4/T5) — todo o resto do visual é código novo
- **Fixture (T2) é a dependência mais crítica** — qualquer erro de referência ali se propaga pra todas as views; T3 protege isso desde cedo
- **Isométrico é a parte de maior risco técnico** (T6/T8) — projeção testada exaustivamente antes de qualquer view depender dela
