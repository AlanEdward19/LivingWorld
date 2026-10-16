# Gap review criteria restore checks

Profile: light
Plan: `.specs/features/gap-review-criteria-restore/plan.md`

6 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Critérios fracos resolvidos

**C1** - The hysteresis check fails when hysteresis is switched off (equal arms): it demands a strictly lower mean and fewer switches in at least 12 of 20 seeds, and the ceiling is physical, not the p99 of its own values (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~BehaviorDecisionSystemHysteresisTests"`; mutation: forcing `HysteresisEnabled = true` in both arms makes `Hysteresis_lowers_mean_action_switches...` fail ("110.04 deveria ficar abaixo de 110.04")

**C2** - The skill "20 seeds" tests vary what changes the result (practice rate, master level, gene value) instead of seeds that no skill system reads, and still pass (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~PairedScenarioTests"`

**C3** - A baby is born without skills even when both parents are masters, proven at the production point (`NatalitySystem.HandleEvent`) (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~NatalitySystemTests.A_baby_is_born_without_skills"`

**C4** - Every remaining weak item has an AD and a rewritten roadmap criterion: price under scarcity and partial wage (phase 5), gates by daily sampling, hysteresis (phase 4), FAM-32 (AD-088) (AC 1)
Proof: `grep -n "AD-095" docs/decisions-log.md docs/roadmap/phase-04-needs.md docs/roadmap/phase-05-economy.md docs/audits/phase-gap-review.md`

### S2 - Lista atualizada e gate rápido

**C5** - The audit moved the section to "Fechadas" with the AD or test cited, the golden hash is unchanged by this spec, and the dead `OverlapCoefficient` is gone (AC 4, AC 5)
Proof: `bash scripts/check-docs.sh`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~GoldenHashesTests"`; `grep -rn OverlapCoefficient tests src` (no output)

**C6** - The fast gate stays within 110% of its time (AC 3)
Proof: `bash scripts/verify.sh`; the Unit gate time goes in `verification.md`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| weak items of the audit list (6) | F4 C1 · F5 price C4 · F5 wage C4 · F3-F5 gates C4 · F7 C4 and C5 · F6 C2 and C3 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: n/a - tests and docs only
- failure modes: C1 (the mutation must fail)
- idempotency: n/a
- authorization: n/a
- concurrency: n/a
- data lifecycle: C5 (audit list)
- dependency failure: n/a
- state transitions: n/a
- observability: n/a

## Out of scope

- FAM-32 and the "same tick" migration (decision D); removing `ExampleCounterSystem`; new resources and professions - see the plan

## Deviations (kept true)

- F5 price under scarcity keeps "never lower and higher on some day": a strict every-tick rule is impossible by construction (convergence to the floor); it is now a registered AD (AD-095), and the existing test already fails when the price ignores scarcity.
- The skill tests no longer sweep seeds (no skill system reads RNG); the 20 repetitions now vary the input that matters. Runtime is unchanged.
- The hysteresis mean difference is asserted on the current code (110.0 switches per day control); the `action-switches.json` baseline is unchanged.

## Handoff

Four test files, three docs, one builder.
