# Personality heredity verification

**Verdict**: PASS
**Profile**: light
**Diff range**: f82783f..6c5b468
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Proofs run at HEAD 6c5b468 in Release, `--logger "console;verbosity=normal"`; each named test appears individually as Passed. Unit (PersonalityInheritanceTests + NatalitySystemTests): 23 passed, 0 failed. LongRunning (MeasureHarness, DeterminismTwoProcessTests, GoldenHashesTests): 10 total, 9 passed, 1 skipped (`ZZZ_record_golden_hashes`, the recorder helper, not a check). Tests exist in `tests/LivingWorld.Tests.Unit/Population/PersonalityInheritanceTests.cs` and `.../Lifecycle/NatalitySystemTests.cs`.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | at h=0 the child is exactly the independent roll | Unit filter `With_weight_zero` Passed | `PersonalityInheritanceTests.cs:28` `Assert.Equal(Personality.RollFrom(Stream(9, id)), Personality.Inherit(..., 0.0))` over 50 ids; `Personality.cs` returns `rolled` before any extra draw when `heritability <= 0` | PASS |
| C2 | traits in [0,100], 1,000 births x 20 seeds and extreme parents | `Every_trait_stays_in_range` Passed | `:41` `Assert.InRange(trait, 0, 100)`; `:49-50` all-0 and all-100 parents at h=1 | PASS |
| C3 | same birth same personality, one and two processes | `The_same_birth` + DeterminismTwoProcessTests (5) Passed | `:60` `Assert.Equal(Inherit(...), Inherit(...))`; two-process tests passed on the new default | PASS |
| C4 | NatalitySystem uses scenario weight; two 50-parents baby in 40..60 at h=1 | `A_baby_of_two_average_parents` Passed | `NatalitySystemTests.cs:384` `Assert.InRange(trait, 40, 60)` for 10 traits | PASS |
| C5 | siblings closer than strangers, z >= 3, 200 pairs | `Siblings_are_closer` Passed | `PersonalityInheritanceTests.cs:87` `meanSibling < meanStranger`, `:88` `z >= 3` | PASS |
| C6 | h=0 control: no sibling effect; weight outside [0,1] and NaN rejected | `Without_inheritance` + `A_weight_outside` Passed | `:104` `Math.Abs(meanSibling - meanStranger) < 8`; `:121` `Assert.False(result.IsSuccess)` for -0.1, 1.1, NaN | PASS |
| C7 | us/NPC-tick <=103%, alloc/tick <=102%, hash identical in two processes | MeasureHarness HEAD vs scratch worktree of f82783f (removed), plus golden + determinism | scale1000 alloc/tick 241,592 vs 241,073 base = +0.22%; us/NPC-tick 0.743 vs 0.874 base (single run each; HEAD faster, inside machine noise, no regression visible); snapshot bytes/alive 10860 both; Golden (5 tests incl. 3650 and 17280 ticks) and 4 determinism tests Passed | PASS |

## Mutation

`Personality.Inherit` changed to `inherited = rolled + noise` (parents ignored, h>0 branch kept). Result: `Siblings_are_closer...` FAILED and `A_baby_of_two_average_parents...` FAILED (2 failed, 21 passed). Restored with `git checkout` of the path; `git status` clean. Mutant killed.

## Gap hunt

- Sibling z-test is real. My independent recompute (same construction, seed 11, 200 pairs): h=0.5 siblings 63.85, strangers 78.05, z=9.55. Control h=0: siblings 126.31, strangers 125.09, z=-0.48. So the z>=3 threshold has a wide margin and the control distance gap is 1.2 (limit 8), fine.
- RNG stream hazard: `StreamFor("personality", babyId)` has one consumer, `NatalitySystem.cs:113`. `rolled` draws first (10 draws) then 10 noise draws; no other reader of that stream. Other personality creation: `PopulationGenerator.cs:55` and `MaterializationSystem.cs:145` are founders/immigrants with no parents (independent roll correct); `WorldAuthoringCommands.RewritePersonality` is explicit author input. No path that should inherit is missed.
- Father null: `father?.Personality ?? mother.Personality` makes the mean equal the mother alone, so such children have about double the parent-mean variance. Acceptable, not tested (no check claims it).
- Clamp/rounding: `Math.Round` is banker's (ties to even); only matters for exact .5, bias negligible, not asserted. Clamp applied after rounding, h clamped to <=1 inside `Inherit`.
- FamilyRules exposure: the weight is NOT reachable from a scenario file. `ScenarioRunner.cs:127` hard-codes 0.5 and `FamilyRules.Create` defaults to 0, so any FamilyRules built elsewhere (tests, `FamilyRules.Disabled`) silently has no inheritance. Checks Deviations record this. Snapshot: `WorldSnapshot.cs:119` deserializes `FamilyRules` through System.Text.Json (record property, no custom converter for the record itself), so the new field round-trips; there is no dedicated test that a non-default weight survives a snapshot (gap 2).
- Distribution effect of h=0.5 (measured, 100 NPCs random pairing, 8 generations, trait Ambition, probe test deleted): mean stays about 48-54 (h=0: 43-57, both drift noise). Variance collapses from 808 (gen0) to about 230-325 from gen1 on (h=0: 724-935 throughout). So default inheritance shrinks trait spread by about 70%, std 28 to 15, in one generation: village personalities are more homogeneous than before. Theory agrees (stationary v ~ 252). Plausible for this formula but it is a behavioural change and extreme personalities (traits above 85 or below 15) become rare after gen 0.

## Gaps (ranked, none flips the verdict)

1. Default 0.5 compresses variance about 70% (above); not in plan/checks, and FAM-32 selection (the stated motivation) has less spread to act on. Worth a note in AD-099 or a variance-preserving mix (scale noise so child variance = parent variance).
2. No scenario-file source for the weight, and no snapshot round-trip test for a non-default `PersonalityHeritability`.
3. C7 uS compare is one run each; only alloc (+0.22%) is meaningfully resolved.
4. Father-null path and rounding ties untested.

## Gate

Unit 23 passed, 0 failed; LongRunning 9 passed, 1 skipped (recorder), 0 failed; mutation killed; `verify.sh` not run (light profile, per instruction).

## Round 2 (author, after the verifier's gap 1; not independent)

The verifier measured that the plain parent mean halves the trait variance each generation (808 down to 230-325). The formula
was replaced by the variance-preserving infinitesimal model (segregation of 35.7 points, weight `sqrt(1 - h^2)` on the independent
roll, default `h` 0.7). New tests: `The_population_variance_of_a_trait_is_preserved_across_generations` (ratio 0.8 to 1.2 over 6
generations), `A_child_with_an_unknown_father...`, `The_scenario_weight_survives_a_snapshot_round_trip`. Sibling z-test and the
NatalitySystem test were re-run on the new formula and pass; pins, golden and collapse were re-recorded; `verify.sh --full` is green
(Unit 2063, scenarios 11, Integration 359, LongRunning 122).
