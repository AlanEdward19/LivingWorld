# phase-16-3-world-cohesion Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/phase-16-3-world-cohesion/spec.md`
**Diff range**: `7bd3fe8..d1cc79f` (`feat/phase-16-3-world-cohesion`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Gate**: orchestrator `verify.sh` PASS — 2083 passed, 0 failed, 10 skipped (AD-009; not re-run)

## Task Completion

T1–T39 + closeout fixes: **Done** (progress.md; HEAD `d1cc79f`).

## Spec-Anchored ACs (COH-*)

| ID | Evidence (`file:line` + assertion) | Result |
| --- | --- | --- |
| COH-01 | `TickContextLogEventTests.cs:49-59` — EventId + SourceSystem + CauseEventId | ✅ |
| COH-02 | `CausalProvenanceTests.cs:31-33` — root `1` | ✅ |
| COH-03 | `TickContextLogEventTests.cs:28-39` — null cause, `Unknown` | ✅ |
| COH-04 | `EventLogRecordTests.cs:29-35` / `:56-59` nullable-safe | ✅ |
| COH-05 | `CausalChainPilotTests.cs:46` — `Assert.Equal(Run(), Run())` | ✅ |
| COH-11 | `BehaviorDecisionSystem.cs:474` `SelectByUtility(DecisionContext…)` | ✅ |
| COH-12 | `DecisionContextBuilderTests.cs:56-67,100-103,151,186,216-222` | ✅ |
| COH-13 | `DecisionContextIntegrationTests.cs` memory→Travel + scarcity belief→Buy | ✅ |
| COH-14 | `DecisionContextIntegrationTests.cs:108-112` trust→Socialize | ✅ |
| COH-15 | `DecisionContextIntegrationTests.cs:140-142` Eat vs Buy | ✅ |
| COH-16 | `DecisionContextIntegrationTests.cs:153-159` empty → Eat | ✅ |
| COH-21 | `BodyGenerationTests.cs:14-15`; `PopulationGeneratorTests.cs:72-74` | ✅ |
| COH-22 | `ProductionSystemTests.cs:257,261+`; `BodyMechanicTests.cs:56` | ✅ |
| COH-23 | `TravelResolutionTests.cs:64-68`; `BodyMechanicTests.cs:81` | ✅ |
| COH-24 | `BodyMechanicTests.cs:109-110,162-163` | ✅ |
| COH-25 | `living-world-cohesion-audit.md:24-25` FUTURE_DEPENDENCY | ✅ |
| COH-31 | `BehaviorDecisionSystemTests.cs:389-392` UsePower | ✅ |
| COH-32 | `PowerOpportunityProviderTests.cs:111-130` ≥27 mechanics | ✅ |
| COH-33 | `BehaviorDecisionSystemTests.cs:473-476` PowerInvoked | ✅ |
| COH-34 | STATE AD-011/AD-014 golden re-record | ✅ |
| COH-35 | `PowerUtilityMigrationTests.cs:25-27`; BDS `:408-410` | ✅ |
| COH-36 | `PowerUtilityMigrationTests.cs:59-61` possession | ✅ |
| COH-41 | `NpcTests.cs:161-164,209-212` Intent fields | ✅ |
| COH-42 | `BehaviorDecisionSystemTests.cs:522-525,552-554` | ✅ |
| COH-43 | `AttentionRouterTests.cs:45-48,87-89` | ✅ |
| COH-44 | `DecisionMetricsTests.cs:24-29` fewer wakeups | ✅ |
| COH-45 | `DecisionContextCacheTests.cs:63-66,87-88` | ✅ |
| COH-51 | `PressureModelTests.cs:45-47` | ✅ |
| COH-52 | `PressureModelTests.cs:72-76` ≥3 factors | ✅ |
| COH-53 | `OpportunityModelTests.cs:39,58` | ✅ |
| COH-54 | `DecisionTraceTests.cs:58-64` | ✅ |
| COH-61 | `docs/audits/living-world-cohesion-audit.md` | ✅ |
| COH-62 | `CausalDiagnosticsTests.cs:26-43` | ✅ |
| COH-63 | `WorldClockTests.cs:109-120` | ✅ |
| COH-64 | `LivingVillageScenarioTests.cs:38,47,57-61` ≥5 systems | ✅ |
| COH-65 | `LivingVillageScenarioTests.cs:72-80` | ✅ |
| COH-66 | gate PASS + STATE AD-011..014 | ✅ |

**Status**: ✅ 35/35 · soft ⚠️ belief→decision unasserted

## Discrimination Sensor

Static lightweight (Ask/AD-009 blocked live mutate):

1. RootCause returns leaf → killed by `CausalProvenanceTests.cs:31`
2. WorkCapacity always 1.0 → killed by `BodyMechanicTests.cs:56` / `ProductionSystemTests.cs:257`
3. Chain `<5` systems → killed by `LivingVillageScenarioTests.cs:59`

**Result**: 3/3 would-kill · PASS ✅

## Edge / follow-ups (non-blocking)

- ~~Mechanic eval exception isolation: missing~~ **done** — `SelectByUtility` isolates per-candidate scoring exceptions (`BehaviorDecisionSystemTests` throwing-candidate cases)
- ~~~54 LogEvent Unknown backlog (audit)~~ **done** — Simulation call sites pass explicit `SourceSystem`; `TickContextLogEventTests` scan + high-traffic assertions

## Summary

**Overall**: ✅ PASS — feature ready to close.
