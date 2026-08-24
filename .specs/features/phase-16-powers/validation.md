# Phase 16 Powers Closeout Validation — Final

**Date**: 2026-08-24
**Spec**: `spec.md` + `spec-closeout.md`
**Diff**: `28d764e..workspace` (feature surface scoped explicitly)
**Verifier**: independent sub-agent (author != verifier)
**Verdict**: **PASS ✅**

## Task completion

| Task | Status | Note |
|---|---|---|
| T11 | ✅ Verified | complete origin×mode×manifestation matrix and resolution consequences |
| T12 | ✅ Verified | deterministic multi-descriptor prevalence and LOD conservation |
| T13 | ✅ Verified | causal gates and independent discrimination are green |

## Spec-anchored evidence

| AC | Spec outcome | `file:line` + assertion expression | Result |
|---|---|---|---|
| POW-13 boundary | unknown mode fails with null runtime | `ExtraordinaryScenarioLoaderTests.cs:99` — `Assert.False; Assert.Null; Assert.Contains(Mode)` | ✅ |
| POW-13 positive origins | Active/Conditional authored; Passive/Triggered causal | `ExtraordinaryArchetypeScenarioTests.cs:58` — generic invocation succeeds; `ExtraordinaryInvocationEngineTests.cs:197` — Triggered changes health to 65 | ✅ |
| POW-13 forbidden origins | authored Passive/Triggered and causal Active/Conditional reject | `ExtraordinaryInvocationEngineTests.cs:179` — `(false,50,5L,nextEventId)`; `:209` — `(false,50,5L)` | ✅ |
| POW-13 manifestation | dormant Conditional is zero-state; manifested pair succeeds/effects/costs | `ExtraordinaryInvocationEngineTests.cs:228` — dormant `(false,50,5L,id,hash)`; manifested `(true,65,3L)` | ✅ |
| POW-14 client authority | UI sends no result | `web/tests/inspector/NpcAuthoringControls.test.tsx:61` — no result field; `invokePower(..., undefined)` | ✅ |
| POW-14 Resolver/id | canonical Resolver result; valid attempt advances exactly one id including failure | `ExtraordinaryInvocationEngineTests.cs:123` — expected result equality and `invocationId+1` | ✅ |
| POW-14 Guaranteed/cost | Guaranteed preserves stream; success/failure pay same full cost | `ExtraordinaryInvocationEngineTests.cs:93`; `ExtraordinaryCloseoutScenarioTests.cs:11` — `(true,false,3L,3L)` | ✅ |
| POW-14 partial/failure | odd halves away from zero both signs; all failure modes causal | `ExtraordinaryInvocationEngineTests.cs:109,146,160` — targets 58/42; health 93 and two failure events | ✅ |
| POW-15 boundary/default | `[0,1]`, positive requires descriptor, default zero | `ExtraordinaryScenarioLoaderTests.cs:139,154`; `web/tests/scenarioDefaults.test.ts:32` | ✅ |
| POW-15 selection/pool | ordered ids, authored descriptor order/RNG, exact pool ids preserved | `ExtraordinaryPrevalenceTests.cs:54` — pool ids and expected `(Id,PowerId)` sequence equal | ✅ |
| POW-15 aggregate/LOD | 0 none; 1 all without aggregate mutation; global count only | `ExtraordinaryPrevalenceTests.cs:9,41` — exact pool tuple/count; marker lacks ids | ✅ |
| POW-16 effects/cost | exact target deltas and cost control | `ExtraordinaryCloseoutScenarioTests.cs:48`; `ExtraordinaryInvocationEngineTests.cs:275` | ✅ |
| POW-16 disabled/conservation | empty runtime/system and stock/money conserved per tick | `ExtraordinaryScenarioLoaderTests.cs:8`; `ExtraordinaryIntegrationTests.cs:51`; `ExtraordinaryCloseoutScenarioTests.cs:37` | ✅ |
| POW-16 heredity/culture/hash | no power copy; opposite reactions; system changes hash | `ExtraordinaryHeredityTests.cs:52`; `ExtraordinaryStateTransitionTests.cs:96`; `ExtraordinaryCloseoutScenarioTests.cs:84` | ✅ |

**Spec check**: POW-13..16 matched exact outcomes. POW-01..12 regression surface remained green.

## Final discrimination sensor

Scratch: `%TEMP%/LivingWorld-phase16-final-20260824-1818` (removed after execution).

| Mutation | Result |
|---|---|
| Remove only `ManifestationCondition` rejection at lines 101-102 | equivalent: `IsAvailable` still enforces `Conditional && isManifested`; 80/80 pass |
| Remove both manifestation enforcement sites | ✅ killed at `ExtraordinaryInvocationEngineTests.cs:247`: actual became `(true,65,3L,1,changedHash)` |

**Sensor**: 1/1 behavior-changing mutant killed — PASS. The literal requested mutant is equivalent, not surviving behavior.

## Gate

- Focused .NET: 80 passed, 0 failed, 0 skipped.
- Web regression: 451 passed, 0 failed; 70 files passed.
- Global functional gate: 1679 passed, 11 skipped .NET; 451 passed web; build/lint/docs green.
- OpenAPI TypeScript check, único passo inicialmente vermelho, foi regenerado e passou isoladamente
  por autorização explícita do usuário; a repetição integral foi dispensada.

## Quality and gaps

The authoritative predicate matches the clarified matrix; deterministic streams remain named and ordered; exact zero-state assertions include hash, cost and event id. No nominal power case or unrelated abstraction was introduced.

**Ranked gaps**: none.
**Overall**: ready for phase closeout. No new lesson recorded for this clean PASS.
