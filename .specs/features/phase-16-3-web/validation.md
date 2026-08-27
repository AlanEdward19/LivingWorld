# Phase 16.3-web Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/phase-16-3-web/spec.md`
**Diff range**: worktree fast-forwarded to `9531d07` (tip of `feat/phase-16-2-power-evolution` at start of this verification; the intended range `531a577^..HEAD` does not exist as given — see Process Note)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Process Note (read first)

The assigned diff range `531a577^..HEAD` does not resolve in the worktree this Verifier was
started in — that worktree's HEAD (`4c0919b`) was several commits **behind** the feature. The
`web-demo/` commits (38 commits, `7c48fab`..`9531d07`, prefixed `feat/docs/test(web-demo)`) exist
only on the main checkout's branch `feat/phase-16-2-power-evolution` (tip `9531d07`). Since this
Verifier's worktree was already isolated and its tree was clean, it was fast-forwarded
(`git reset --hard 9531d07`) to bring the feature in — this does not touch the orchestrator's
real tree. `design.md`, referenced ~15 times by `tasks.md`, **does not exist anywhere in git
history** for this feature (`git log --all -- .specs/features/phase-16-3-web/design.md` returns
nothing) — every "per design.md §..." citation in tasks.md/README/checklist is unverifiable
against an actual document. This is reported as a gap below, not silently absorbed.

---

## Task Completion

All 31 tasks (T1-T31) show `[x]` in tasks.md and have corresponding commits (`7c48fab`..
`9531d07`). Spot-checked commit messages against task table — commit subjects match the
`Commit:` field for T1, T6, T11, T17, T20, T29, T30, T31. No blocked/partial tasks found.

**Caveat**: "Done when" boxes are self-checked by the author (per the skill's own per-task
verification model) — this Verifier re-derives evidence independently below rather than trusting
the checkmarks.

---

## Spec-Anchored Acceptance Criteria

### P1: World Explorer — fluxo vertical completo

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: World View mostra mapa nível mundo + resumo | Oakbridge + 1-2 assentamentos vizinhos, resumo derivado do fixture | `tests/views/WorldView.test.tsx` renders `WORLD_FIXTURE.world.summary`; fixture has 3 settlements (`oakbridge`, `millbrook`, `stonehaven`) — `src/fixture/oakbridge.ts:15-60` | ✅ PASS |
| AC2: Clique em Oakbridge → Settlement View com Pulse | valores exatos do fixture (pop/food/employment/migration/construction) | `tests/views/SettlementView.test.tsx` (4 tests) — snapshot against fixture values | ✅ PASS |
| AC3: Clique em household Valen → membros Mira/Tomas/Eli/Nora | membros exatos + árvore/estoque/eventos | `tests/views/HouseholdView.test.tsx:1-30`ish — "Mostra Mira/Tomas/Eli/Nora conforme doc#124" | ✅ PASS |
| AC4: Clique em Mira → Agent View completo | identidade/profissão/intent/condição/corpo/household/relações/eventos + botão Why? | `tests/views/AgentView.test.tsx` (8 tests) | ✅ PASS |
| AC5: Why? mostra fatores em linguagem humana | "household food is low"/"grain prices rose"/"she is hungry", ≥1 clicável | `tests/views/WhyPanel.test.tsx` (4 tests); fixture `whyFactors` — `src/fixture/oakbridge.ts:127-131` matches doc text exactly | ✅ PASS |
| AC6: Clique num fator → Causal Explorer com WHY?/CONSEQUENCES | doc#117-118 exact chain (Valen reduced purchases → Mira VeryHungry → left work early; Baker reduced production; Migration pressure) + 6 systems | `tests/views/CausalExplorer.test.tsx:17-35` — `toHaveTextContent` asserts each exact sentence and all 6 systems (`Agriculture/Economy/Household/Needs/Decision/Employment`) | ✅ PASS |
| AC7: Clique em evento do Causal Explorer → Timeline preservando breadcrumb | navega pra Timeline, breadcrumb intacto | `tests/views/CausalExplorer.test.tsx:43-48` (`nav.current()` becomes `{kind:"timeline"}`); breadcrumb preservation asserted in `tests/flow/verticalSlice.test.tsx` | ✅ PASS |
| AC8: Voltar preserva estado de navegação | back() retorna à tela anterior, não reseta pra World | `tests/components/Breadcrumb.test.tsx` + `tests/nav/NavigationStore.test.ts` (`back` pop-stack tests) | ✅ PASS |

**Status**: ✅ All 8 P1 ACs covered with precise assertions.

### P1b: Zoom Semântico + Redesenho

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: nível mundo — sem prédio/NPC | zero `IsoTile` de prédio, zero `NpcToken` | `tests/map/SemanticZoomMap.test.tsx` — "Nível 'mundo' não renderiza nenhum IsoTile de prédio nem NpcToken" | ✅ PASS |
| AC2: nível distrito — prédios, sem NPC | prédios do settlement, sem NPC | `tests/map/SemanticZoomMap.test.tsx` (district-level assertions) | ✅ PASS |
| AC3: nível agente — NPCs clicáveis | NpcTokens posicionados, `onSelectNpc` disparado | `tests/map/SemanticZoomMap.test.tsx` | ✅ PASS |
| AC4: clique no mapa == clique na lista | mesma navegação, qualquer nível | `tests/views/WorldView.test.tsx` / `SettlementView.test.tsx` compare map-click vs list-click outcome | ✅ PASS |
| AC5: visual novo, não reprodução de `web/` | terreno/prédio/cidade redesenhados | `web-demo/src/map/isoPalette.ts` (new palette) — no import from `web/src/map-engine/**` confirmed via grep (zero hits) | ✅ PASS |

**Status**: ✅ All 5 P1b ACs covered.

### P2: Timeline, Life, Follow, Feed

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: Timeline filtrável por escopo, de qualquer ponto de entrada | filtro World/Settlement/Household/Agent/tipo | `tests/views/Timeline.test.tsx` (5 tests, includes household-Valen filter test) | ✅ PASS |
| AC2: Life View com marcos do fixture | 8 milestones doc#122 (nascimento...atual) | `src/fixture/oakbridge.ts:117-126` — 8 milestones match doc order exactly; `tests/views/LifeView.test.tsx` (2 tests) | ✅ PASS |
| AC3: Follow toggla, não altera fixture, persiste na sessão | toggle semantics, no fixture field written | `tests/state/followStore.test.ts:15-19` (un-follow on 2nd click), `:31-33` ("never mutates the fixture") | ✅ PASS |
| AC4: World Feed cronológico agrupado, priorizado | eventos agrupados/ordenados | `tests/views/WorldFeed.test.tsx` (3 tests) | ✅ PASS |

**Status**: ✅ All 4 P2 ACs covered. **Note**: AC1's "de qualquer ponto de entrada" is satisfied for Timeline (entry buttons exist in World/Settlement/Household/Agent views — confirmed via `nav.push({kind:"timeline"...})` call sites in `WorldView.tsx:37`, `SettlementView.tsx:83`, `HouseholdView.tsx:72`, `AgentView.tsx:86`). The equivalent is **not** true for Feed/Life/Threads views (see Code Quality / Gap section) — but the spec text only requires this for Timeline, so this is not an AC failure.

### P3: Story Threads, Experience/Debug Mode, Search

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: Story Threads card com números exatos | "18 events · 4 households · 11 Agents · 6 systems" | `src/fixture/oakbridge.ts:496-535` — 18 eventIds, 4 householdIds, 11 agentIds, 6 systemsTouched, counted directly; `tests/views/StoryThreads.test.tsx` (2 tests) | ✅ PASS |
| AC2: Experience ↔ Debug troca linguagem sem trocar navegação | campos técnicos aparecem, seleção mantida | `tests/views/CausalExplorer.test.tsx:65-72` (doc#116 test) — asserts technical string appears, same event still selected | ✅ PASS |
| AC3: busca global agrupada, filtra por texto | People/Places/Households/Events/Threads groups | `tests/search/SearchIndex.test.ts` (8 tests) including case-insensitivity and empty-result-set shape | ✅ PASS |

**Status**: ✅ All 3 P3 ACs covered.

**Overall spec-anchored tally: 20/20 ACs matched spec-defined outcome. 0 spec-precision gaps** — every AC in P1/P1b/P2/P3 has an exact-value or exact-behavior assertion traceable to spec text, not a vague "renders without crashing" check.

---

## Edge Cases

| Edge Case | Result | Evidence |
| --- | --- | --- |
| Deep link direto (`/agent/mira-valen`) carrega estado correto | ✅ Handled | `tests/nav/NavigationStore.test.ts` — deep-link section |
| Deep link com id inexistente redireciona pra World | ✅ Handled | `tests/nav/NavigationStore.test.ts:131-135` — asserts `{kind:"world"}` + path `/`; **confirmed by discrimination sensor** (mutation 2 below) that this is a real, enforced check, not a coincidental pass |
| Causal Explorer raiz sem causa → "sem causa anterior conhecida" | ✅ Handled | `tests/views/CausalExplorer.test.tsx:38-42` — `getByTestId("no-known-cause")`, `src/views/CausalExplorer.tsx:89-93` renders "No known earlier cause." literal string |
| Follow em entidade já seguida → toggle, não duplica | ✅ Handled | `tests/state/followStore.test.ts:15-19` |
| Resize abaixo do breakpoint desktop não quebra | ⚠️ Spec-precision gap | No test found for this edge case (CSS-only layout, `src/styles/tokens.css` not viewed for breakpoints). Spec explicitly says "não precisa ficar bonito, precisa não quebrar" — low bar, but zero test evidence either way. Not blocking (spec doesn't demand automated proof), flagged as unverified. |
| Busca sem resultado → estado vazio explícito | ✅ Handled | `tests/search/SearchIndex.test.ts:34-37` — exact empty-shape assertion `{people:[],places:[],households:[],events:[],threads:[]}` |

**5/6 edge cases have direct test evidence; 1 (resize) is unverified — not a spec violation since the spec doesn't require a test artifact, but noted as a real gap in proof.**

---

## Discrimination Sensor

Performed in this Verifier's own isolated worktree (already git-isolated from the orchestrator's
real tree); each mutation applied via `Edit`, run against the targeted test file, then reverted
with `git checkout --` and confirmed via `git status --short` (clean) before the next mutation.

| # | File:line | Description | Killed? |
| --- | --- | --- | --- |
| 1 | `web-demo/src/map/IsoProjection.ts:31` | `toGrid`: flipped `y: (b - a) / 2` → `y: (b + a) / 2` (sign flip in isometric inverse) | ✅ Killed — 5/10 tests in `IsoProjection.test.ts` failed (round-trip and fractional-tile tests) |
| 2 | `web-demo/src/nav/NavigationStore.ts:67` | `pathToRoute`: removed `exists.agent(id)` check for `"agent"` case, accepting any id | ✅ Killed — `NavigationStore.test.ts` "redirects to World View when the deep-link id doesn't exist" failed (`{kind:"agent",id:"does-not-exist"}` returned instead of `{kind:"world"}`) |
| 3 | `web-demo/src/views/CausalExplorer.tsx:24-27` | `descendantsOf`: removed recursive `flatMap`, returning only direct children (breaks multi-level consequence-chain walk) | ✅ Killed — `CausalExplorer.test.tsx` "lists the systems involved" failed (`Needs` system missing from result, since it's 2 levels deep) |

**Sensor depth**: lightweight (3 targeted mutations, default tier)
**Result**: 3/3 killed — ✅ PASS. Working tree confirmed clean (`git status --short`, no output) after each revert.

---

## Code Quality

| Principle | Status | Notes |
| --- | --- | --- |
| Minimum code | ✅ | 27 source files, no dead abstractions found |
| Surgical changes | ✅ | Zero cross-imports from `web/src/**` confirmed via grep |
| No scope creep | ⚠️ | Two undocumented additions not in tasks.md: `App.tsx` (composition root) and `src/styles/tokens.css` (design tokens). Judged **justified, not creep**: T1-T31 build 20+ standalone views/stores but no task explicitly wires them into one app shell or defines a shared visual theme — without `App.tsx` the 31 tasks would produce an unusable pile of unconnected components, and without `tokens.css` every view would need ad-hoc inline styling. Both are small, single-purpose, and were flagged transparently in README.md's "O que foi portado vs. redesenhado" table rather than smuggled in silently. This is the kind of gap a competent senior engineer fills without a ceremony task, not scope creep. |
| Matches existing patterns | ✅ | `NavigationStore`/`followStore`/`modeStore` use the same `subscribe`/`useSyncExternalStore` idiom as `web/src/state/*.ts`, self-documented in comments |
| Would senior engineer approve? | ✅ (with one exception) | The missing `design.md` (see Process Note) is a real process gap — 15+ citations to a document that was never written is not something a careful senior engineer should let stand, regardless of how good the resulting code is. Does not affect the working demo/tests. |
| Tests map to ACs, non-shallow | ✅ | Spot-checked `CausalExplorer.test.tsx` — every assertion targets exact spec-quoted strings, not generic "renders" checks |
| Spec-anchored outcome check | ✅ | See table above — 20/20 ACs traced to exact-value assertions |
| Every test maps to a spec requirement | ✅ | No unclaimed test files found; `tests/` mirrors `src/` 1:1 per the Test Coverage Matrix |
| Documented guidelines followed | `web-demo/tests/**` convention inherited from `web/package.json`/`web/tests/**` per tasks.md Test Coverage Matrix — followed |

---

## Self-Reported Gaps Review (`docs/ui/living-world-experience-checklist.md`)

Verified each of the 5 self-reported gaps against actual code (not just trusting the doc):

1. **Settlement Pulse/Timeline read as "dashboard"** — plausible, subjective UX judgment; not independently falsifiable by this Verifier, but consistent with both screens being `dl`/list-based per the views' source.
2. **LifeView has no entry point in `App.tsx`** — **confirmed accurate**. `grep -rn "kind: *\"life\"" src/` shows the route type is defined and URL-parsed (`NavigationStore.ts:10,82`) but `grep -rn "nav.push" src/views src/components` returns **zero** calls pushing `{kind:"life",...}` from any view. Only reachable via manually typed URL.
3. **Map camera not centered** — plausible based on code read (`SemanticZoomMap.tsx` uses a fixed `viewBox` without a centering transform based on settlement positions); not pixel-verified in a real browser by this Verifier, but the described mechanism (no camera/viewBox-fitting logic) is consistent with the source.
4. **No event markers on the map** — **confirmed accurate**: no marker-rendering code tied to `events` found in `src/map/**`.
5. **AgentView body detail thin** — **confirmed accurate**: `bodySummary: { build: string }` is the only field in the fixture type (`src/fixture/oakbridge.ts:108` etc.), a one-line string, consistent with the doc's own explanation (fixture limitation, not UI bug).

**None of the 5 gaps contradict a spec AC or Success Criterion.** All fall inside the spec's own
escape hatch ("se não, vira gap documentado, não fechamento forçado" — P1's Success Criteria
allow "mostly yes" rather than requiring unanimous "yes" on the two central questions). The
framing is honest.

**Additional gap found by this Verifier, not self-reported in the checklist**: `WorldFeed`
(T24) and the `StoryThreads` list view (`{kind:"threads"}`, distinct from the single-thread
route `{kind:"thread"}` that IS wired from `SearchBar.tsx:108`) have **the same
no-UI-entry-point problem as LifeView** — `grep -rn "kind: *\"feed\"\|kind: *\"threads\""
src/ tests/` shows these route kinds are only referenced in `NavigationStore.ts` (definition +
URL parsing) and in `NavigationStore.test.ts`, never pushed from any view or component. This is
not a spec-AC violation (P2 AC4 / P3 AC1 only require the views to render correctly when opened,
not that they have a nav entry point — T25's "Done when" also only names Timeline, not
Feed/Threads), but the checklist's "Resumo de gaps" section undersells the app's actual
UI-reachability problem by only naming LifeView.

**Doc-renumbering claim (spec.md/tasks.md "doc#147" vs. checklist's "doc#147 is actually 'Deep
Links', real checklist is §190-193")**: **unverifiable by this Verifier** — the user's original
source document is not accessible from this repo/worktree. Noted as an unverified claim, neither
confirmed nor disputed.

---

## Gate Check

- **Gate command**: `npm --prefix web-demo test` (Full) and `npm --prefix web-demo run build` (Build)
- **Test result**: 26 test files, **136/136 passed**, 0 failed, 0 skipped
- **Build result**: `tsc -b && vite build` — succeeded, no type errors, produced `dist/` bundle (178.68 kB JS, 4.24 kB CSS)
- **Test count before feature**: 0 (new isolated project, `web-demo/` did not exist before this feature)
- **Delta**: +136 new tests
- **Skipped tests**: none
- **Failures**: none

---

## Requirement Traceability Update

All 20 WEBX requirements checked against spec-anchored evidence above; the existing `spec.md`
traceability table status of "Verified" is **confirmed accurate** for all 20 (WEBX-01..08,
WEBX-11..15, WEBX-21..24, WEBX-31..33). No change needed to the table.

---

## Summary

**Overall**: ✅ Ready (with 2 non-blocking process/documentation gaps noted)

**Spec-anchored check**: 20/20 ACs matched spec outcome, 0 spec-precision gaps on ACs (1 minor gap on an edge case with no test — resize behavior)
**Sensor**: 3/3 mutations killed
**Gate**: 136 passed, 0 failed; build green

**What works**: Full P1 vertical slice (World→Settlement→Household→Agent→Why→CausalExplorer→Timeline), P1b semantic zoom + redesigned isometric map, P2 Timeline/Life/Follow/Feed, P3 Threads/Debug Mode/Search — all with precise, spec-quoted test assertions, not shallow render checks. Zero cross-imports from `web/`. Discrimination sensor found no weak spots in the 3 highest-risk pieces of logic tested (isometric math, deep-link id validation, causal chain walk).

**Issues found**:
1. `design.md` was never written despite being cited 15+ times by `tasks.md`/README/checklist as the source of architecture/data-model/tech decisions — process gap, not a functional defect (fix: retroactively author it from the actual code, or strike the citations).
2. `WorldFeed` and `StoryThreads` (list) views have no UI entry point (same class of gap as the self-reported `LifeView` issue) but weren't listed in the checklist's gap summary — minor documentation completeness issue, not a spec violation.
3. Window-resize-below-breakpoint edge case has no test evidence either way.

**Next steps**: None blocking. If the team wants full self-consistency, either write `design.md` retroactively or remove its citations; optionally add `nav.push({kind:"feed"})`/`nav.push({kind:"threads"})` entry points and update the checklist's gap list to include them.
