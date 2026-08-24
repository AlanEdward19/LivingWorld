# Phase 16 Powers — Validation T1/T2 + web/visual

**Date**: 2026-08-24  
**Verifier**: independent sub-agent (author != verifier)  
**Scope**: POW-01..04 and POW-07; POW-05/06 explicitly deferred  
**Verdict**: ✅ PASS — 5/5 selected ACs evidenced.

## Task completion

| Task | Status | Verification |
| --- | --- | --- |
| T1 | ✅ Verified | POW-01..04 evidenced; invalid input is transactional |
| T2 + web/visual | ✅ Verified | POW-07, dedicated UI and capability consumer evidenced |
| T3–T5 | ⏭️ Out of scope | POW-05/06 not evaluated |

## Spec-anchored outcomes

| AC | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| POW-01 | absent/disabled yields no carriers, events, extraordinary systems | `ExtraordinaryScenarioLoaderTests.cs:14-17`; `ExtraordinaryIntegrationTests.cs:57-61` assert exact zero tuple/no system | ✅ |
| POW-02 | every axis remains generic queryable data | `ExtraordinaryScenarioLoaderTests.cs:27-34`; `ExtraordinaryIntegrationTests.cs:18-23` assert exact ten-axis/optional-state tuples | ✅ |
| POW-03 | named failures create zero runtime | `ExtraordinaryScenarioLoaderTests.cs:46-65` asserts all named failures; `:73-77` asserts invalid `Load` is failure with `Value == null`; `ExtraordinaryRuntimePlan.cs:14-19` materializes only after success | ✅ |
| POW-04 | enabled registers only explicit systems, stable order, no global RNG | `ExtraordinaryIntegrationTests.cs:57-61,96-99` asserts one named system and equal hashes; build previously passed banned-API enforcement | ✅ |
| POW-07 | separate appearance/needs/senescence; zero senescence does not prevent other deaths | `ExtraordinaryIntegrationTests.cs:18-23,103-118` asserts separate fields and starvation death/event at multiplier zero; visual projection/editor assertions below | ✅ |

## Web, visual and capability outcomes

- Inactive carrier equals ordinary rendering: `renderer.test.ts:417-419` exact draw/tint/trail equality. ✅
- Manifested scale/tint/trail: `renderer.test.ts:365-366,392-393` exact scale and conditional calls. ✅
- Editor contract and dedicated fields: `scenarioDefaults.test.ts:56-70,85-95`; `panels.test.tsx:75-80,105-109`. ✅
- Runtime consumer is declared: `LivingWorldCapabilityCatalog.cs:64-65` maps EXTRAORDINARY to `inspector.npc.extraordinary`; `frontendCapabilityConsumers.ts:53` maps it to NPC scope. ✅

## Official focused gates

- `bash scripts/test.sh --filter Extraordinary` — .NET **18/18 passed**; web **443/443 passed**.
- `bash scripts/test.sh --filter 'FullyQualifiedName~CapabilityCoverageTests|FullyQualifiedName~FrontendCapabilityContractTests'` — .NET **11/11 passed**; web **443/443 passed**.
- Full verify intentionally not rerun in this reverify round; prior build gate was green.

## Discrimination sensors

All mutations ran only in copied trees outside the repository.

| Mutation | Gate/result | Status |
| --- | --- | --- |
| R1: invalid descriptor returns an empty successful runtime plan | Extraordinary: 1/18 failed at `ExtraordinaryScenarioLoaderTests.cs:75` | ✅ Killed |
| R1: zero-senescence carrier bypasses starvation processing | Extraordinary: 1/18 failed at `ExtraordinaryIntegrationTests.cs:116` | ✅ Killed |
| Prior: disabled config registers extraordinary system | Extraordinary: failed at `ExtraordinaryIntegrationTests.cs:57` | ✅ Killed |
| Prior: remove carriers from canonical hash | exact `WorldSnapshotTests` failed at line 174 | ✅ Killed |
| Prior: apply manifested scale to inactive carrier | web failed at `renderer.test.ts:417` | ✅ Killed |
| Prior: serialize appearance scale as value + 1 | web failed at `scenarioDefaults.test.ts:56` | ✅ Killed |

**Sensors**: reverify 2/2 killed; cumulative 6/6 killed, 0 survived.

## Quality and ranked gaps

- Transaction boundary is centralized in `ExtraordinaryRuntimePlan.Load`; failure cannot expose a plan. ✅
- Senescence remains orthogonal to starvation; the test observes both death state and causal event. ✅
- Capability registration connects simulation state to the dedicated NPC inspector consumer. ✅
- **Ranked gaps: none in the selected POW-01..04/07 slice.**

**Overall**: ✅ Verified complete for the selected Phase 16 slice; POW-05/06 remain future work.
