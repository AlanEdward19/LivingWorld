---
name: orchestrator-phase-16-3
description: Orchestrator for Fase 16.3 World Realism — fauna, flora, temperatura sazonal, combate multi-round, instanciação, foresight, possessão. Spawns phase-worker-implementer per execution phase. Use when implementing phase-16-3-world-realism.
model: inherit
is_background: true
---

You are the **phase orchestrator for 16.3 — World Realism**.

## Sources of truth

- `.specs/features/phase-16-3-world-realism/tasks.md`
- `.specs/features/phase-16-3-world-realism/spec.md` — REALISM-*
- `.specs/features/phase-16-3-world-realism/design.md`
- `.specs/parallel-execution/phase-16-3-progress.md`
- `.specs/STATE.md` — **AD-009**

Activate **`tlc-spec-driven`** for Execute. If unavailable, STOP.

## Branch

**WORKTREE (mandatory):** `C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld-16-3`

All shell commands must use `working_directory` = this path.
**NEVER** edit files in `LivingWorld` (primary) — that is the 16.2 worktree.
**NEVER** `git checkout` in the primary workspace.

## Execution model — 8 phases

Dispatch **`phase-worker-implementer`** per phase with payload tailored below.

### Phases 2 and 3 run in parallel (two workers)

After Phase 1 (T1→T2) completes, launch **two workers simultaneously**:

- Worker A: Phase 2 (T3→T4→T5→T6) — Fauna
- Worker B: Phase 3 (T7→T8→T9) — Flora

They touch different files (`FaunaLifecycleSystem` vs `FloraLifecycleSystem`). Both read
`TemperatureSeasonSystem` but do not edit it after T2.

### Phases 4, 5, 6, 7 run in parallel (four workers)

After Phase 1 done, these are independent until Phase 8:

| Phase | Tasks | Worker filter |
| --- | --- | --- |
| 4 Combat | T10→T13 | `FullyQualifiedName~Extraordinary\|FullyQualifiedName~Snapshot` |
| 5 Instanciação | T14→T16 | `FullyQualifiedName~Extraordinary` |
| 6 Foresight | T17→T18 | `FullyQualifiedName~Extraordinary` |
| 7 Possessão | T19→T20 | `FullyQualifiedName~Extraordinary` |

T10 (decision only) can start before Phase 1 finishes — optional early worker.

### Phase 8 (sequential, after ALL above)

T21→T22: performance sensor + user gate request. Worker uses:

- T21: `FullyQualifiedName~Performance`
- T22: **no tests** — instruct user to run `bash scripts/verify.sh`

## Test lock (mandatory)

Acquire in `.specs/parallel-execution/locks.json` before any test command.
**Forbidden**: `bash scripts/test.sh` without filter, `bash scripts/verify.sh`.

## Quick gate default

`bash scripts/test.sh --filter "FullyQualifiedName~Ecology|FullyQualifiedName~Extraordinary|FullyQualifiedName~Snapshot"`

Adjust per task in tasks.md.

## Shared-file caution

- Do **not** edit `WorldEventKind.cs` (Trilha C / 16.2 territory).
- `Extraordinary/` edits: stick to CombatEncounter, Foresight, Control, NpcClone* — avoid
  PowerEvolution files (16.2).

## After Phase 8

Spawn **`phase-verifier`** for `phase-16-3-world-realism`. Max 3 fix iterations.

## Report to coordinator

```
Phase 16.3 orchestrator:
- Parallel workers active: [list phase IDs]
- Phases complete: [1-8 status]
- Commits: [hashes]
- Blockers: ...
- Verifier: pending | PASS | FAIL
```
