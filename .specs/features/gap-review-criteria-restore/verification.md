# Gap review criteria restore verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 4f5a41d..4057cf2
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

C6 (fast gate time) was run by the orchestrator: `verify.sh --full` is green; Unit gate 39 s against 35-37 s before (about +6%, within 110%).

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | hysteresis off fails; strict mean, 12/20, physical ceiling | Hysteresis tests, Release, exit 0 (6 passed, 1 skipped baseline recorder); mutation (both arms `HysteresisEnabled = true`) FAILED "110.04 deveria ficar abaixo de 110.04", file restored, porcelain clean | `BehaviorDecisionSystemHysteresisTests.cs:77` `Assert.True(withHysteresis.Average() < withoutHysteresis.Average()` · `:79` `Assert.True(fewer >= 12` · `:100` `const double ceiling = 20 * 24 / 2.0` | PASS |
| C2 | skill "20" tests vary rate, master level, gene | PairedScenarioTests Release, exit 0 (15 passed, 2 skipped) | `PairedScenarioTests.cs:71` `Assert.Equal(20, wins)` after `MakePracticeOnlyRules(0.02 + 0.001 * step)`; `:140` master level `cap * (0.5 + 0.025 * step)`; `:171` gene `1.1 + 0.025 * step` | PASS |
| C3 | baby of two masters has no skill | NatalitySystemTests.A_baby_is_born_without_skills, Release, exit 0 (1 passed) | `NatalitySystemTests.cs:366` `Assert.Equal(0, baby.Skills.Get(new SkillType(0)))` | PASS |
| C4 | every weak item has AD and rewritten criterion | `grep -n AD-095` hits all four files | `docs/decisions-log.md:45` · `phase-04-needs.md:40` · `phase-05-economy.md:48` · `phase-gap-review.md:29` | PASS |
| C5 | audit moved to Fechadas; golden unchanged; OverlapCoefficient gone | `check-docs.sh` OK; GoldenHashesTests 4 passed, 1 skipped; grep of `src` and `tests` sources empty | `phase-gap-review.md:29`; no `tests/baselines`/`src` file in the commit diff; the only grep hits are stale Debug `.dll` binaries | PASS |
| C6 | fast gate within 110% | `verify.sh --full` run by the orchestrator: Unit 39 s against 35-37 s before | +6% | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| weak items of the audit list (6) | `phase-gap-review.md` diff | F4 C1 · F5 price C4 · F5 wage C4 · gates C4 · F7 C4,C5 · F6 C2,C3 | - |

## Findings (non-blocking)

- Doc inaccuracy: `phase-05-economy.md:40` says the 100-year conservation run is "Heavy/nightly"; `MoneyConservationTests.cs:66-70` tags it `Category=Scenario` (only 1/10/100-year all Scenario). Same wording in AD-095 is fine; fix the roadmap word.
- Residual weakness: F6 seed-less loops (`seed: 1` fixed) are deterministic by design; the byte-identical test (`:176-186`) is a determinism check, not variation. Acceptable, documented in the class summary.
- F5 price test (`ScarcityPriceCausalTests.cs:74`) asserts the rewritten criterion; its "mutation reprova" claim in the roadmap was not mutation-run here.
- FAM-32 remains open by design (AD-088, decision D), stated in the audit.

## Gate

`dotnet test -c Release` filters above - all green; verify.sh not run by the Verifier.
