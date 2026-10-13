# Specs closeout checks

Profile: light
Plan: `.specs/features/specs-closeout/plan.md`

5 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Rastreio honesto

**C1** - Every `.specs/features` directory declares its state on line 1 of spec.md or plan.md (`Estado:`) or carries verification.md; the listing is empty (AC 3)
Proof: `bash scripts/specs-state.sh` (no output)

**C2** - No tracing row is left bare "Verified" without evidence and none is left as a stale Pending: a Verified row cites a test or file, a Pending row cites a reason (AC 1, AC 2)
Proof: `grep -rnE "^\|.*Verified *\|? *$" .specs/features --include=spec.md --include=closeout.md` (no output)

### S2 - Efêmeros limpos

**C3** - Only closed phases lost `tasks.md`; phases 17..23 keep theirs and no closed phase keeps one (AC 4, door 1)
Proof: `bash -c 'for f in .specs/features/*/tasks.md; do head -n1 "$(dirname $f)/spec.md" | grep -q "Estado: Entregue" && echo "$f"; done'` (no output); `ls .specs/features/phase-17-divinity/tasks.md`

**C4** - AGENTS.md, rules/ and ROADMAP.md are untouched (AC 5, no phase criterion closed)
Proof: `git status --porcelain -- AGENTS.md rules ROADMAP.md` (no output)

**C5** - Every doc stays within 100 lines (AC 5)
Proof: `bash scripts/check-docs.sh`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| old-format dirs with state line (37) | C1, table-driven over all 37 | - |
| `tasks.md` kept or removed (9 found) | C3, table-driven over all 9 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: C1
- failure modes: C2
- idempotency: n/a - docs only, re-running changes nothing
- authorization: n/a - no runtime surface
- concurrency: n/a - another agent builds src/ in the same tree, this work never touches it (C4)
- data lifecycle: C3
- dependency failure: n/a - no runtime surface
- state transitions: C1
- observability: n/a - no logging requirement in this slice

## Out of scope

- Implementing any `spec` phase, converting old specs to lean, renaming or deleting spec/design - see the plan

## Handoff

Docs only, seven read-only investigators each owning disjoint directories, one writer for STATE.md and the script: under the 150k budget per slice - parallel by directory group, no shared file.
