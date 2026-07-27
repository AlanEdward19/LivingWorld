# Fase 6 — Habilidades e Aprendizado — Validation

**Date**: 2026-07-27
**Spec**: `.specs/features/phase-06-skills/spec.md`
**Diff range**: `c4ae166..HEAD` (19 tasks + 1 mid-flight fix commit, 30 files, +2181/-17)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status | Commit | Notes |
|---|---|---|---|
| T1 SkillType enum | ✅ Done | `d40e059` | 13 values, matches roadmap order |
| T2 SkillCurve | ✅ Done | `4859542` | pure fn, killed by sensor mutation #1 |
| T3 SkillGainSource enum | ✅ Done | `b52011b` | 6 values |
| T4 SkillSet | ✅ Done | `1ff962a` + fix `07b31dd` | fix commit added public ctor/props for JSON round-trip (pre-existing known gap) |
| T5 RateGene | ✅ Done | `ee7d891` | killed by sensor mutation #2 |
| T6 SkillsRules | ✅ Done | `890a3b4` | |
| T7 Npc extensions | ✅ Done | `382d7dd` | round-trip test present |
| T8 SkillPracticeSystem | ✅ Done | `9e46f33` | |
| T9 SkillTeachingSystem | ✅ Done | `c25a28d` | killed by sensor mutation #3 |
| T10 ProductionSystem | ✅ Done | `d49d001` | quantity effect only, see AC-4 gap below |
| T11 BehaviorDecisionSystem | ✅ Done | `a3de96e` | |
| T12 Wiring | ✅ Done | `b42fe0e` | golden hashes + baseline regenerated |
| T13 SkillsRulesCoverageTests | ✅ Done | `0c0c129` | |
| T14 Cenário especialista vs trocador | ✅ Done | `836b897` | 20/20, baseline recorded |
| T15 Cenário mestre-topo vs mestre-piso | ✅ Done | `96bdbbd` | 20/20 |
| T16 Cenário gene muda resultado | ✅ Done | `d0f5ff0` | 20/20 both directions |
| T17 Correlação pai/filho | ✅ Done | `a605a14` | SPEC_DEVIATION — see below |
| T18 Cenário oficina rende mais | ✅ Done | `46aaa9a` | 10/10 |
| T19 Sensores de hash | ✅ Done | `9b91407` | both (a) and (b) |

All 19 tasks done, commits match `tasks.md` and `git log` exactly.

---

## Spec-Anchored Acceptance Criteria

### P1: Habilidade sobe por prática e muda produção

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: 13 skills, floor 0, cap per scenario | all 13 `SkillType` at same starting value | `tests/LivingWorld.Tests/Population/SkillSetTests.cs:12-18` — `Initial_sets_all_13_skills_to_the_same_starting_value` | ✅ PASS |
| AC2: employed NPC working own profession gains mapped skill | skill increases via curve | `tests/LivingWorld.Tests/Population/SkillPracticeSystemTests.cs:46-61` — `Employed_worker_working_present_at_own_workplace_gains_mapped_skill` asserts `> 0` | ✅ PASS |
| AC3: gain at cap does not move `Hash(world)` | `Hash(world)` unchanged | `tests/LivingWorld.Tests/Population/SkillHashSensorTests.cs:14-31` — `Hash_is_unchanged_when_practice_gain_is_applied_to_an_npc_already_at_the_cap`, `Assert.Equal(hashBefore, hashAfter)` | ✅ PASS |
| AC4: production scales **quantity and price-base** by average worker skill | higher-skill workshop produces **more, and priced better** | `tests/LivingWorld.Tests/Economy/ProductionSystemSkillTests.cs:66-79` (10/10 seeds) + `ProductionSystemTests.cs:155-175` — both assert only `workplace.Stock[...]` (quantity); no test asserts a price effect | ⚠️ Spec-precision gap (quantity covered, price-base **not** asserted anywhere) |
| AC5: flag off → 10-year `Hash(world)` diverges | hashes differ, same seed | `tests/LivingWorld.Tests/Population/SkillHashSensorTests.cs:37-45` — `Assert.NotEqual(hashOn, hashOff)` | ✅ PASS |

### P2: Curva de retornos decrescentes

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: `Gain(n+1) <= Gain(n)` for `n` in 1..1000 | monotonic non-increasing over full range | `tests/LivingWorld.Tests/Population/SkillCurveTests.cs:12-23` — loop asserts `current <= previous` for n=2..1000 | ✅ PASS (killed by sensor #1) |
| AC2: one curve, cenário-parametrized, applies to all 13 skills | single shared param, no per-skill curve | `src/LivingWorld.Domain/Population/SkillsRules.cs:7-11,34-38` — `Cap`/rate dictionaries are not keyed by `SkillType`, structurally enforced (no dedicated test, but no code path exists to diverge per skill) | ✅ PASS (by construction) |
| AC3: pure function | same input → same output, no external state read | `tests/LivingWorld.Tests/Population/SkillCurveTests.cs:26-32` — `Gain_is_pure_same_input_always_produces_same_output` | ✅ PASS |

### P3: Fontes de ganho além da prática

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: DeliberateTraining, own rate | skill increases via own-source rate | `tests/LivingWorld.Tests/Population/SkillTeachingSystemTests.cs:66-78` | ✅ PASS |
| AC2: School, own rate | skill increases | `SkillTeachingSystemTests.cs:111-119` | ✅ PASS |
| AC3: Parental, own rate | skill increases when parent employed & cohabiting | `SkillTeachingSystemTests.cs:135-148` | ✅ PASS |
| AC4: Observation, own rate, less than Tutoring | `tutoringGain > observationGain`, both `> 0` | `SkillTeachingSystemTests.cs:170-189` | ✅ PASS |
| AC5: Tutoring rate depends on `min(masterSkill,cap)` and master `Teaching`; master-top > master-bottom, 20/20 | apprentice of top-of-range master ends with higher skill | `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs:120-133` — `Assert.Equal(20, wins)`; exact formula also pinned in `SkillTeachingSystemTests.cs:191-213` (`Observation_excludes_own_mentor...`, precision:10 equality) | ✅ PASS (killed by sensor #3, both the scenario test and the precise-formula unit test) |

### P3: Genética como multiplicador de taxa

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: `RateGene` at birth = `mãe*0.5+pai*0.5+mutação`, never ≤0 | positive value, blended | `src/LivingWorld.Domain/Population/RateGene.cs:33-38`; `tests/LivingWorld.Tests/Population/RateGeneTests.cs:42-53,69-81` (`Inherit_never_produces_zero_or_negative...`, `Inherit_centers_around_parents_average_value`) | ✅ PASS (killed by sensor #2) |
| AC2: different genes, identical practice → different skill, 20/20 | `skillA != skillB` in all 20 seeds | `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs:152-164` — `Assert.Equal(20, wins)` | ✅ PASS |
| AC3: identical genes, identical practice → byte-identical, 20/20 | `skillA == skillB` in all 20 seeds | `PairedScenarioTests.cs:166-178` | ✅ PASS |
| AC4+AC5: IC95(skill-parent↔skill-child) contains 0 AND IC95(gene-parent↔gene-child) entirely >0, both required together, 200 births | both conditions in the same test | `PairedScenarioTests.cs:184-245` — single test, two `Assert.True` calls on the same dataset | ✅ PASS, **with a logged `SPEC_DEVIATION`**: the 200 births are built by direct family construction (mother/father `RateGene`, `RateGene.Inherit`), not by running `NatalitySystem`/`ScenarioRunner.Create` end-to-end. The comment in the test (`PairedScenarioTests.cs:188-201`) documents why (default scenario goes extinct ~57 births before reaching 200). This exercises the same production function (`RateGene.Inherit`) but **never exercises `NatalitySystem.HandleEvent`'s own wiring** of that function (`src/LivingWorld.Simulation/Population/NatalitySystem.cs:62`) — see gap below. |

### P3: Escolha e troca de profissão

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: score = skill + personality + vacancy | higher skill in candidate profession wins, vacancy tilts ties | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemProfessionSwitchTests.cs:95-108,144-156` | ✅ PASS |
| AC2: `SwitchProfession` preserves old skill (stagnation, no reset) | old skill value unchanged after switch | `tests/LivingWorld.Tests/Population/NpcSkillMutatorsTests.cs:20-33`; also `BehaviorDecisionSystemProfessionSwitchTests.cs:110-123` | ✅ PASS |
| AC3: specialist (20y same profession) > switcher (every 2y), 20/20; ratio → baseline, ±30% = alert not fail | `Assert.Equal(20, wins)`; ratio persisted, deviation only logged | `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs:44-92`; `tests/baselines/skill-specialization-ratio.json` present (`1.339540504622127`) | ✅ PASS, **documented SPEC_DEVIATION**: uses a test-local `SkillsRules` (`practiceRate: 0.03`) instead of `ScenarioRunner.DefaultSkillsRules` (`0.3`), because the default rate saturates both arms near the cap within the 20-year horizon, hiding the effect. This is scoped to the test only (confirmed: `ScenarioRunner.DefaultSkillsRules` in `src/LivingWorld.Simulation/ScenarioRunner.cs:105-117` still declares `0.3`, no golden-hash regeneration was needed or done for this reason — checked, no unrelated diff to `ScenarioRunner.cs`'s `DefaultSkillsRules` beyond the T12 wiring commit). |

**Status**: ✅ 20/22 ACs matched spec outcome exactly · 1 spec-precision gap (AC4/P1: price-base effect unasserted) · 1 SPEC_DEVIATION with a real, unaddressed side-gap (T17 never exercises `NatalitySystem`'s own `RateGene.Inherit` call site).

---

## Discrimination Sensor

All three mutations were injected directly into the working tree (git-tracked files, no pre-existing local diff on any of them — confirmed via `git diff --stat` before/after), each restored via `git checkout --` immediately after, verified clean.

| # | File:line | Mutation | Tests run | Killed? |
|---|---|---|---|---|
| 1 | `src/LivingWorld.Domain/Population/SkillCurve.cs:15` | `1.0 - currentSkill/cap` → `currentSkill/cap` (flips decreasing-returns to increasing-returns) | `--filter FullyQualifiedName~SkillCurveTests` | ✅ Killed — 2/7 failed (`Gain_never_increases_as_level_rises_from_1_to_1000`, `Gain_decreases_as_baseRate_alone_scales_the_curve...`) |
| 2 | `src/LivingWorld.Domain/Population/RateGene.cs:33-38` | `Inherit` ignores both parents, returns an unrelated roll (breaks "taxa herdada") | `--filter FullyQualifiedName~RateGeneTests\|FullyQualifiedName~PairedScenarioTests` | ✅ Killed — `Inherit_centers_around_parents_average_value` (unit) AND `Skill_correlation_contains_zero_while_rate_gene_correlation_is_entirely_above_zero_across_200_births` (T17 scenario, IC95 `[-0.052,0.224]` no longer >0) both failed |
| 3 | `src/LivingWorld.Simulation/Population/SkillTeachingSystem.cs:161` | `masterFactor = (masterSkill/cap)*(1+masterTeaching/cap)` → hardcoded `1.0` (master quality no longer affects tutoring gain) | `--filter FullyQualifiedName~SkillTeachingSystemTests\|FullyQualifiedName~PairedScenarioTests` | ✅ Killed — 3 failures: `Tutoring_gain_is_higher_with_higher_master_skill_and_teaching`, `Observation_excludes_own_mentor_leaving_gain_to_tutoring_formula_only` (unit, precision-10 formula pin), and T15 `Apprentice_of_top_of_range_master...` scenario (20/20 → 0/20) |

**Note on tiering**: the suggested 3rd mutation in the brief ("break the T17 conjunction so only one IC95 check is required") targets test-code logic, not production code, so it isn't a valid fault-injection target per validate.md ("inject into the new code introduced by this feature" = production). Substituted with a production-code mutation in the other highest-risk new logic (`SkillTeachingSystem`'s tutoring quality formula, SKILL-08/16) — this also happens to be the code path T15's 20/20 scenario and two `SkillTeachingSystemTests` unit tests all independently cover, giving triple confirmation of a real kill rather than a single fragile assertion.

**Sensor depth**: lightweight (3 targeted mutations, per default tier)
**Result**: 3/3 killed — ✅ PASS

---

## Code Quality

| Principle | Status |
|---|---|
| Minimum code | ✅ — `SkillTeachingSystem`'s 5 methods in one class is an intentional design.md decision (avoids 5x Daily population passes), not scope creep |
| Surgical changes | ✅ — `ProductionSystem`/`BehaviorDecisionSystem` changes are additive (`SkillsRules? = null` preserves Phase 5/4 behavior byte-for-byte, confirmed by `ProductionSystemTests.cs:139-153` and `BehaviorDecisionSystemProfessionSwitchTests.cs:82-93`) |
| No scope creep | ✅ — no new `ActionType`, no school building, no genetics model beyond the single `RateGene` scalar, matching Out of Scope table |
| Matches patterns | ✅ — `SkillsRules.Create`/`Result<T>` mirrors `NeedsRules`/`EconomyRules`; `SkillSet` mirrors `Personality`; mutators mirror `Hire`/`Fire`/`JoinHousehold` |
| Spec-anchored outcome check | ⚠️ — see AC4/P1 gap above |
| Per-layer coverage expectation met | ✅ — domain 1:1 AC mapping confirmed; systems have happy+edge path tests |
| Every test maps to a spec AC/edge case/Done-when | ✅ — no unclaimed tests found in sampled files |
| Documented guidelines followed | tasks.md Test Coverage Matrix — followed (unit/integração leve/Scenario split matches `[Trait("Category","Scenario")]` usage throughout) |

---

## Edge Cases

- [x] NPC never employed: other skill gains still apply — `SkillPracticeSystem.cs:32` guards `Employer is not {} employerId → continue`; School/Parental paths don't require employment (`SkillTeachingSystemTests.cs:111-119,135-148`). Note: no test explicitly names an *unemployed adult* NPC for `SkillPracticeSystem` (all practice tests hire the NPC); the guard clause is simple enough that this is a low-severity gap, not a missing behavior.
- [x] `Workplace` with zero workers present: neutral multiplier — covered indirectly via `ProductionSystemTests.cs:177-201` (`Worker_with_unmapped_profession_contributes_neutral_multiplier`, same `mapped==0` code path as zero-workers) and pre-existing `Workplace_with_zero_workers_present_produces_exactly_zero`.
- [x] Mentor dies mid-tutoring: `ClearMentor()`, no exception — `SkillTeachingSystemTests.cs:238-252` (`Tutoring_dead_mentor_clears_mentor_reference_without_exception_and_grants_no_gain`).
- [x] Curve at level 0/negative: non-negative, no throw — `SkillCurveTests.cs:34-43`.

---

## Gate Check

- **Gate command**: `bash scripts/verify.sh` (build + lint + test, `Category!=Scenario`)
- **Result**: 500 passed, 0 failed, 4 skipped
- **Skipped tests** (all pre-existing baseline-recording tests, self-documenting `[Skip = "regravação manual..."]`, none new-and-unjustified):
  - `ResolverBaselineTests.ZZZ_record_baseline`
  - `PairedScenarioTests.ZZZ_record_specialization_baseline` (Fase 6, T14's own baseline recorder)
  - `BehaviorDecisionSystemHysteresisTests.ZZZ_record_action_switches_baseline`
  - `PopulationBaselineTests.ZZZ_record_baseline`
- **Scenario gate**: `bash scripts/test.sh --filter Category=Scenario` → 9 passed, 0 failed, 0 skipped (covers T14, T15, T16×2, T17, T18, T19b, plus 2 pre-existing Fase 5 scenario tests)
- **Failures**: none

---

## Fix Plans (if issues found)

### Gap 1: AC4/P1 — no test asserts a price effect from skill, only quantity

- **Root cause**: `ProductionSystem.SkillMultiplierOf` (`src/LivingWorld.Simulation/Economy/ProductionSystem.cs:87-102`) scales only `produced` (quantity). The spec's Assumption A3 claims "qualidade entra no preço via Fase 5" by feeding the *same* multiplier into `MarketPricingSystem` — but `MarketPricingSystem` (`src/LivingWorld.Simulation/Economy/MarketPricingSystem.cs`) has no `SkillsRules` dependency at all; it only reacts to `supplyOffered / demand` ratio. So skill's only path to price is *indirect*: more skill → more stock → higher supply ratio → **lower** price under the existing scarcity model (not a "quality raises price" effect the spec's framing implies). No test in the diff asserts any price outcome tied to skill.
- **Fix task**: Either (a) add an explicit test showing the indirect quantity→stock→price relationship (documenting that "quality" is modeled purely as increased supply, not a separate price premium), or (b) if a genuine price premium is intended, wire `SkillsRules` into `MarketPricingSystem` and add a dedicated test. This is a Design-level clarification, not just a test gap — recommend routing to the spec/design owner before writing a fix task blindly.
- **Priority**: Minor (the spec assumption itself flagged this as a possible gray area — "Design pode revisar se o critério de preço exigir separação real" — so this is a known, logged risk maturing into an actual gap, not a surprise)

### Gap 2: T17's `RateGene.Inherit` correlation test never exercises `NatalitySystem`'s own wiring

- **Root cause**: `NatalitySystem.HandleEvent` (`src/LivingWorld.Simulation/Population/NatalitySystem.cs:62`) calls `RateGene.Inherit(mother.RateGene, father?.RateGene ?? mother.RateGene, ctx.Rng($"rategene-{babyId.Value}"))` — this exact call site has **zero** test coverage. `NatalitySystemTests.cs` (pre-existing Fase 4 file) asserts on `Personality`/`Profession` for runtime births but never touches `RateGene`. `PairedScenarioTests.cs` T17 tests the same production function (`RateGene.Inherit`) in isolation via direct family construction, never through `NatalitySystem`. A regression that broke the wiring specifically (e.g., accidentally passing `mother.RateGene` twice instead of `father?.RateGene`, or dropping the `rateGene:` argument entirely so every baby defaults to `RateGene(1.0)`) would pass every existing test in the suite, including T17 and the golden-hash/determinism tests (which don't assert on `RateGene` values).
- **Fix task**: Add one assertion to `NatalitySystemTests.cs` — e.g., mother/father with distinct `RateGene` values, trigger a birth, assert `baby.RateGene.Value` is plausible/derived from both parents (or at minimum, not the bare default `1.0`) and deterministic for a fixed seed.
- **Priority**: Minor (this is a real coverage hole for a genuinely swappable wiring line, but the function itself is thoroughly tested and the call site is a one-line, low-complexity pass-through; not a currently-observed defect)

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
|---|---|---|
| SKILL-01 | Pending | ✅ Verified |
| SKILL-02 | Pending | ✅ Verified |
| SKILL-03 | Pending | ✅ Verified |
| SKILL-04 | Pending | ✅ Verified |
| SKILL-05 | Pending | ✅ Verified |
| SKILL-06 | Pending | ✅ Verified |
| SKILL-07 | Pending | ✅ Verified |
| SKILL-08 | Pending | ✅ Verified |
| SKILL-09 | Pending | ✅ Verified (with logged SPEC_DEVIATION, see gap 2) |
| SKILL-10 | Pending | ⚠️ Verified — quantity only (see gap 1) |
| SKILL-11 | Pending | ⚠️ Needs Fix — price/quality effect not asserted (see gap 1) |
| SKILL-12 | Pending | ✅ Verified |
| SKILL-13 | Pending | ✅ Verified |
| SKILL-14 | Pending | ✅ Verified |
| SKILL-15 | Pending | ✅ Verified (with logged SPEC_DEVIATION, test-local practice rate — no ScenarioRunner impact) |
| SKILL-16 | Pending | ✅ Verified |

---

## Summary

**Overall**: ⚠️ Issues (2 minor gaps, feature is otherwise solid and shippable)

**Spec-anchored check**: 20/22 ACs matched spec outcome exactly · 2 spec-precision/coverage gaps flagged
**Sensor**: 3/3 mutations killed
**Gate**: 500 passed (verify.sh) + 9 passed (Scenario) = 509 total, 0 failed, 4 justified skips

**What works**: All 8 roadmap verification criteria pass exactly as worded (specialization 20/20, gene-vs-skill heredity pair 200-births, curve monotonicity 1..1000, cap-neutral hash, master-quality tutoring 20/20, gene-changes-result 20/20 both directions, workshop-owner-quality 10/10, flag-off hash divergence). Money conservation confirmed intact after `SkillTeachingSystem`'s deliberate-training charge (credits `Workplace.Treasury`, verified by dedicated test asserting the exact Treasury delta). `SkillSet` JSON round-trip confirmed fixed and tested. Golden hashes and specialization baseline correctly regenerated in the wiring commit, no other file touched Fase 0-5 golden data.

**Issues found**:
1. AC4/P1 (SKILL-10/11): "quality → price" is only quantity → stock → indirect price under existing scarcity model; no test pins a price outcome. Needs a design decision, not a blind test add.
2. T17/SKILL-09: `NatalitySystem.HandleEvent`'s own `RateGene.Inherit` call site is untested; only the standalone function is proven correct.

**Next steps**: Route gap 1 to the spec/design owner (Design decision on whether "quality" needs a real price hook or the current quantity-only behavior is acceptable and just needs re-wording in spec.md). Route gap 2 to a small fix task (one new assertion in `NatalitySystemTests.cs`). Neither gap blocks shipping the feature — both are coverage/precision issues on already-correct-looking code, not observed defects.
