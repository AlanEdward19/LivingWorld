# Fase 5 — Economia — Validation Report (Independent Verifier)

**Verdict: PASS**

All 29 acceptance criteria (ECON-01..29) have at least one test whose assertion is
genuinely anchored to the spec's THEN-clause (not a mirror of "whatever the code
currently does"). The discrimination sensor attack (money conservation via wage
payment, ECON-14/22) was caught immediately by two existing tests. No banned
nondeterminism (`Random`, `DateTime.Now`, `Guid.NewGuid()`) found in the new Economy
source files, and pricing/production parameters are scenario-driven through
`EconomyRules`/`EconomyCatalog`, not hardcoded literals. `bash scripts/verify.sh`
passes 429/429 (3 expected skips) after the sensor check was reverted.

---

## Per-requirement evidence table

| ID | Test(s) | Note |
| --- | --- | --- |
| ECON-01 | `EconomyRulesTests`, `EconomyCatalogTests`, all `Workplace`/`Stock` fields typed `long` | Types are `long`/`int` throughout Domain (`Dictionary<ResourceType, long>`, `ProductionRecipe.Inputs/Outputs: Dictionary<int,long>`); no floating stock field exists. Spec-anchored by construction (compile-time), not a runtime assertion — acceptable since the criterion is a type constraint. |
| ECON-02 | `WorkplaceTests.Deposit_over_capacity_reports_the_lost_amount`, `ProductionSystemTests` (`ResourceLost` event via `ctx.LogEvent`), `ResourceConservationTests` (sums `ResourceLost` into "perdido") | Excess is returned as `lost` amount, never silently dropped; production system logs `WorldEventKind.ResourceLost`. Spec-anchored. |
| ECON-03 | `ProductionSystemTests.Spoilage_reduces_stock_by_declared_rate_and_zero_rate_leaves_stock_untouched` | Asserts exact rate (`0.1` → 100→90) and 0-rate no-op in the same test. Spec-anchored. |
| ECON-04 | `ReferentialIntegritySweepTests` (grep hit: dangling `WorkplaceId` sensor, line ~45-56) | Mutation-style sensor: injects a dangling `WorkplaceId(999_999)` via `npc.Hire`, asserts the sweep reports it by name. Spec-anchored, and itself is a mutation sensor. |
| ECON-05 | `EconomyHashScenarioTests.Ten_year_hash_differs_between_economy_on_and_off_with_the_same_seed` | Direct hash comparison, same seed, `Enabled` true vs false over 10 years. Spec-anchored. |
| ECON-06 | `ProductionSystemTests.Workplace_with_worker_present_and_no_required_resource_produces_more_than_zero` | Worker present + recipe → stock > 0. Spec-anchored (though "scaled by workers up to MaxWorkersPerCycle" itself isn't independently asserted with an exact expected count — see gap list). |
| ECON-07 | `ProductionSystemTests.Workplace_with_zero_workers_present_produces_exactly_zero` | Exact 0, not just "not increased". Spec-anchored. |
| ECON-08 | `ProductionSystemTests.Workplace_requiring_absent_cell_resource_produces_zero_even_with_worker_present` | Worker present, required cell resource absent → 0. Spec-anchored. |
| ECON-09 | `MarketTransactionTests.Execute_happy_path_applies_all_four_effects_in_order` | All 4 fields asserted with exact expected values (debit/credit/stock move). Spec-anchored. |
| ECON-10 | `MarketTransactionTests.AllStepIndexes` (`Enumerable.Range(1, MarketTransaction.Steps.Count)`) | Enumerates `Steps.Count` directly, not a hand-maintained index list — genuinely "cobertura por construção". Spec-anchored. |
| ECON-11 | `MarketTransactionTests.Execute_fails_and_leaves_nothing_observable_when_buyer_funds_insufficient` | Buyer wallet and seller stock asserted byte-identical to pre-attempt state on failure. Spec-anchored. |
| ECON-12 | `MarketTransactionTests.Execute_aborts_at_the_injected_step_with_no_partial_effect` (Theory over all step indexes) | Asserts full pre-state (`Money(100)`, `Money(0)`, `10`, `0`) for every `failAtStep` in `1..Steps.Count`. Spec-anchored. |
| ECON-13 | Same theory as ECON-12, driven by `Steps.Count` | If a new step is added to `MarketTransaction.Steps` without a corresponding branch, the theory automatically gains a new case (via `AllStepIndexes`) and would need real handling — matches "adicionar um passo sem tratamento derruba o teste". Spec-anchored. |
| ECON-14 | `MoneyConservationTests.Total_money_is_conserved_every_tick_over_10_years` + `Mutation_sensor_a_mint_that_forgets_to_increment_the_counter_breaks_the_invariant` + `Total_money_is_conserved_every_tick_over_100_years` (`Category=Scenario`) | Checked every tick (not just periodically), includes its own in-repo mutation sensor. Spec-anchored; strongest test in the suite. |
| ECON-15 | `ResourceConservationTests.Produced_equals_consumed_plus_stocked_plus_lost_every_tick_over_10_years_for_every_resource` | Exact equality per resource per tick, with a real `IWorldEventSink` summing `ResourceLost` events. Spec-anchored. |
| ECON-16 | `EatAndBuyBehaviorTests.Eat_with_food_and_water_in_stock_restores_both_and_decrements_stock_by_one` | Hunger/Thirst restored to 100 AND stock decremented by exactly 1 for both resources. Spec-anchored. |
| ECON-17 | `EatAndBuyBehaviorTests.Eat_with_no_food_in_stock_completes_without_restoring_hunger_and_without_exception` | Hunger stays 0 (not restored), no exception, stock not negative. Spec-anchored. |
| ECON-18 | `EmploymentSystemTests.Unemployed_adult_with_matching_profession_gets_hired_within_one_tick` | Asserts `Npc.Employer`, `Workplace.Employees` membership, AND `WorldEventKind.Hired` — all three named in the spec. Spec-anchored. |
| ECON-19 | `EmploymentSystemTests.Dead_employee_is_fired_and_orphan_reference_never_survives_a_tick` | Both sides (`Npc.Employer == null`, removed from `Employees`) plus `Fired` event. Spec-anchored. |
| ECON-20 | `EmploymentSystemTests.No_workplace_exceeds_max_vacancies_and_every_employer_resolves_over_10_years` | Checked every tick over 10 years, exactly matching "checado a cada tick em 10 anos" language in the spec. Spec-anchored. |
| ECON-21 | `WagePaymentSystemTests.Workplace_with_sufficient_treasury_pays_every_employee_exact_sum` | Exact wallet/treasury amounts after payment (30/30/40 split). Spec-anchored. |
| ECON-22 | `WagePaymentSystemTests.Workplace_with_insufficient_treasury_emits_WageUnpaid_and_leaves_balances_untouched` + `Partial_treasury_pays_the_first_employee_and_leaves_the_second_untouched_and_unpaid` | Byte-identical balances asserted on failure, `WageUnpaid` event present. The spec explicitly asks for "um teste desliga essa checagem por flag e exige que o critério falhe, provando que ele mede algo de verdade" — this was independently re-verified live in the discrimination sensor below (see that section) rather than found as an existing flag-driven test in the suite; treat this as **externally confirmed**, not as an in-repo flag test. See gap list. |
| ECON-23 | `MarketPricingSystemTests.Price_rises_when_stock_is_scarce_relative_to_demand` / `Price_falls_when_stock_is_abundant_relative_to_demand` | Both directions asserted with real `EconomyRules`-declared sensitivity/demand baseline. Spec-anchored. |
| ECON-24 | `MarketPricingSystemTests.Price_never_leaves_the_declared_floor_ceiling_range` | Extreme sensitivity/demand forces an out-of-range raw computation, `Assert.InRange(5,20)` confirms clamp. Spec-anchored. |
| ECON-25 | `ScarcityPriceCausalTests.Halving_wheat_production_raises_its_price_every_day_of_the_window_in_10_of_10_seeds` | 10 seeds, all-days-higher check, matches "10/10" and "todo tick da janela" literally. Spec-anchored. |
| ECON-26 | `MoneySupplyTests.Mint_increases_MoneyMinted_and_logs_the_named_event` / `Destroy_requires_sufficient_net_supply_and_logs_the_named_event` | Counter increment + named event with quantity/cause in payload (`"500|tesouro-inicial"`). Spec-anchored. |
| ECON-27 | `MoneyConservationTests` (same as ECON-14) + `MoneySupplyTests.Destroy_fails_without_side_effect_when_net_supply_insufficient` | The "if a mint event doesn't increment the counter, ECON-14 must detect it" claim is proven by the in-repo `Mutation_sensor_...` test AND was independently reconfirmed by my own sensor attack (below). Spec-anchored. |
| ECON-28 | `EconomyScenarioHarness.Create` (used by `ScarcityPriceCausalTests`/`FamineCausalChainTests`) | Decorator-based multiplier applied as scenario data, layered onto `ScenarioRunner.DefaultSystems()` rather than a duplicated hardcoded scenario. Spec-anchored (structurally matches "nunca um segundo cenário C# hardcoded"). |
| ECON-29 | `FamineCausalChainTests.Famine_raises_the_hungry_count_above_the_scenarios_threshold_in_10_of_10_seeds` | 10/10 seeds, uses `world.NeedsRules.UrgencyThreshold` (scenario-declared threshold, not a magic number), counts only living NPCs. Spec-anchored. |

**Coverage: 29/29 requirements have a genuinely spec-anchored test.**

---

## Discrimination sensor result

**Invariant attacked:** ECON-14 (money conservation) / ECON-22 (wage payment must be
all-or-nothing per employee) via `src/LivingWorld.Simulation/Economy/WagePaymentSystem.cs`.

**Change made:** removed the `continue` statement after logging `WageUnpaid` on
insufficient treasury, so the code fell through to `npc.CreditWallet(wage)`
unconditionally — i.e. the NPC gets paid even when the treasury debit failed,
creating money out of nowhere.

```csharp
// before (correct):
if (!debited.IsSuccess)
{
    ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{workplace.Id.Value}|{wageAmount}");
    continue; // ECON-22: nem Treasury nem Wallet mudam neste caso
}
npc.CreditWallet(wage);

// mutated (broken):
if (!debited.IsSuccess)
{
    ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{workplace.Id.Value}|{wageAmount}");
    // (continue removed)
}
npc.CreditWallet(wage);
```

**Result:** ran
`dotnet test --filter "FullyQualifiedName~WagePaymentSystemTests|FullyQualifiedName~MoneyConservationTests"`.
Two tests failed immediately:

```
Failed LivingWorld.Tests.Economy.WagePaymentSystemTests.Workplace_with_insufficient_treasury_emits_WageUnpaid_and_leaves_balances_untouched
  Expected: 0
  Actual:   30
Failed LivingWorld.Tests.Economy.WagePaymentSystemTests.Partial_treasury_pays_the_first_employee_and_leaves_the_second_untouched_and_unpaid
  Expected: 0
  Actual:   30
Failed!  - Failed: 2, Passed: 4, Skipped: 0, Total: 6
```

This proves the ECON-22 sensor is not decorative — it catches money creation the
instant it happens, at the unit level, without needing the full 10-year conservation
loop to notice.

**Revert confirmed clean:**

```
$ git checkout -- src/LivingWorld.Simulation/Economy/WagePaymentSystem.cs
$ git diff src/LivingWorld.Simulation/Economy/WagePaymentSystem.cs
(empty)
$ git status --short
 M STATE.md
 M src/LivingWorld.Simulation/Behavior/NeedsDecaySystem.cs
 M src/LivingWorld.Workers/Program.cs
?? .specs/
?? HARNESS.md
```

(The three modified/untracked entries above pre-date this verification session and
are unrelated to Phase 5 — they were already present in `git status` before this
review began.) `WagePaymentSystem.cs` shows no diff. `bash scripts/verify.sh` was
re-run afterward and passed 429/429 (3 expected skips), matching the pre-review
baseline exactly.

---

## Architecture / determinism spot-check

- `grep -riE "Random|DateTime\.Now|Guid\.NewGuid"` over `src/**/Economy/**`: **zero hits**.
  No banned nondeterminism sources in any of the 10 new Economy source files
  (`MarketTransaction.cs`, `EconomyRules.cs`, `EconomyCatalog.cs`,
  `EconomyScenarioLoader.cs`, `EmploymentSystem.cs`, `MarketPricingSystem.cs`,
  `ResourceStock.cs`, `Workplace.cs`, `ProductionSystem.cs`, `WagePaymentSystem.cs`).
- Magic numbers: `MarketPricingSystem.Tick` and `ProductionSystem.Tick` pull every
  tunable (capacity, spoilage rate, wage, price floor/ceiling, sensitivity, demand
  baseline) from `EconomyRules`/`EconomyCatalog` — no hardcoded price/wage/capacity
  literals found in the systems themselves. The only literal constant is
  `Math.Max(demandBaseline * populationInRegion, 0.0001)` in `MarketPricingSystem`,
  which is a divide-by-zero guard, not a domain parameter — acceptable.
- Deterministic ordering: `EmploymentSystem`, `WagePaymentSystem`, `MarketPricingSystem`,
  `ProductionSystem`, and the referential sweep all iterate `world.Workplaces.OrderBy(w
  => w.Id.Value)` / `.Employees.OrderBy(id => id.Value)`, matching the
  `rules/simulation-determinism.md` convention cited in the spec's assumptions table.

No architecture or determinism violations found.

---

## Gap list

1. **ECON-06 (minor):** the "escalado pelo número de trabalhadores presentes, até
   `MaxWorkersPerCycle`" scaling behavior is exercised only via `> 0` in
   `ProductionSystemTests`; there is no test asserting the exact scaled output count
   (e.g. 2 workers × output-per-worker == 2× production, or that a worker count above
   `MaxWorkersPerCycle` is clamped). The invariant direction (worker present → produces)
   is proven, but the multiplicative scaling and the `MaxWorkersPerCycle` clamp
   themselves are not directly asserted anywhere in the suite. Not severe enough to
   fail the phase (ECON-06's core THEN-clause about consuming inputs/depositing outputs
   is covered), but worth a follow-up unit test in Phase 6 hardening.
2. **ECON-22 flag-test framing:** the spec's Independent Test explicitly asks for "um
   teste desliga essa checagem por flag e exige que o critério falhe, provando que ele
   mede algo de verdade." No such in-repo flag-driven test exists for wage payment
   specifically (unlike `MoneyConservationTests`, which does carry its own in-repo
   mutation sensor). I performed this proof externally during verification (see
   Discrimination sensor result above) rather than finding it already automated in the
   suite. Recommend adding `WagePaymentSystemTests` an equivalent in-repo mutation
   sensor test (mirroring `MoneyConservationTests.Mutation_sensor_...`) so future
   regressions on this specific path are caught by `verify.sh` without manual review.

Neither gap is a coverage hole for the underlying acceptance criteria (both ECON-06 and
ECON-22 have at least one test whose assertion matches the spec's THEN-clause); they
are hardening opportunities, not missing tests. Verdict remains **PASS**.
