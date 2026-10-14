# Specs closeout verification

**Verdict**: PASS
**Profile**: light
**Diff range**: HEAD..working tree (docs only, uncommitted)
**Round**: 1 - full
**Verifier**: self-check by the author agent, with seven investigators per directory group (degraded gate: no fresh Verifier sub-agent; proofs below were re-run by the author)

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | every dir declares state | `bash scripts/specs-state.sh` printed nothing | `scripts/specs-state.sh:9` checks `Estado:` on line 1; `.specs/features/specs-closeout/plan.md:1` | PASS |
| C2 | no bare Verified row | grep of table rows ending in Verified: 0 hits after fixing `dead-code-audit/spec.md:214` | `.specs/features/dead-code-audit/spec.md:214` now cites report.md | PASS |
| C3 | tasks.md only removed for closed phases | loop over remaining tasks.md: none sits in an Entregue dir; 17..23 and trilha-c keep theirs | `git status`: only `real-household-workplace-buildings/tasks.md` deleted (Entregue) | PASS |
| C4 | AGENTS.md, rules/, ROADMAP.md untouched | `git status --porcelain -- AGENTS.md rules ROADMAP.md` empty | `scripts/specs-state.sh:1` | PASS |
| C5 | docs <=100 lines | `bash scripts/check-docs.sh` -> OK | `scripts/check-docs.sh:12` | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| old-format dirs with state line (37) | `ls .specs/features` | C1, table-driven over all 37 | - |
| `tasks.md` kept or removed (9 found) | `find -name tasks.md` | C3, table-driven over all 9 | - |

## Findings (non-blocking)

- Rows marked Verified rest on grep of test names, not on running tests (no dotnet run allowed); some cite a tagged id or method name only (e.g. ECON-10..13, SKILL-15/16, FAM-25, PERF-07).
- Divergences left for a human: ROADMAP says `pendente` for phases 10 and 12 though specs are Entregue; phase 16 `spec` in ROADMAP vs concluded in its roadmap doc; phase 28 `fechada` but COG-11 Pending.
- `real-household-workplace-buildings/tasks.md` was deleted with its T5 full-suite checkbox unchecked; revert if a gate run is wanted first.

## Gate

No dotnet command was run (another agent builds in the tree); `verify.sh` not run.

## Follow-up (issue #43, 2026-10-13)

- Weak Verified rows re-run with real tests and now cite them by name: ECON-04/10..13/19, SKILL-15/16, FAM-25, LLM-14..16, phase-09 PERF-05/06/14, dead-code-audit EXEC-04/08/09/11/17. ECON-10/13 gained two tests; ECON-19 one.
- Downgraded: phase-16 PERF-05 (Deferred, the 3x stability run never happened), phase-09 PERF-07 (Parcial: `BuildingOccupancySystem` still scans `world.Npcs`).
- Cross-platform hashes: root cause was `Math.Log`/`Math.Cos` in `BodyGeneration` (1 ulp of one NPC's muscle mass). Fixed with `DeterministicMath` (AD-108); Windows and Linux musl now give identical hashes over a year for 3 seeds. Goldens and baselines re-recorded once. `verify.sh --full` exits 0.
- Also fixed on the way: food price stuck at the floor (AD-109), `LLM-14` guard now real (`EgressGuard`, `LW_BLOCK_EGRESS=1` in `test.sh`).
- `.specs/STATE.md` split into an index plus `.specs/state/*.md`; `check-docs.sh` covers them.
- Left to the owner: ROADMAP status for phases 10, 12, 16, 28; EXEC-16 (text quality). The `real-household-workplace-buildings` T5 gate is superseded by the full gate above.
