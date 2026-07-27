# Fase 5 — Economia — Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill
is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy
review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-05-economy/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/LivingWorld.Tests/**`) and project guidelines.
> Guidelines found: `rules/eval-criteria.md` (R1–R5), `rules/simulation-determinism.md`,
> `rules/implementation.md`. No `jest.config`/coverage-threshold file — this is a .NET repo;
> the gate is `dotnet test` via `scripts/test.sh`, filtered by `[Trait("Category", ...)]`.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain value objects/records (`Workplace`, `EconomyRules`, `EconomyCatalog`, `MarketTransaction`, `Npc` extension) | unit | All branches; 1:1 to ECON-01..17, 26/27; every listed edge case (spec.md § Edge Cases) has a test | `tests/LivingWorld.Tests/Economy/*Tests.cs` | `bash scripts/test.sh` |
| Simulation systems (`EmploymentSystem`, `ProductionSystem`, `MarketPricingSystem`, `WagePaymentSystem`) | unit + scenario (long-horizon) | All branches for unit; 10-year assert-per-tick for conservation/integrity criteria (R2); 10-seed base/treatment for causal criteria (R4) | `tests/LivingWorld.Tests/Economy/*Tests.cs` (unit); `tests/LivingWorld.Tests/Economy/*ScenarioTests.cs` `[Trait("Category","Scenario")]` for the 100-year nightly variant | `bash scripts/test.sh` (unit + 10-year gate); `bash scripts/test.sh --filter Category=Scenario` (100-year nightly, **not run in this phase per user constraint**) |
| Determinism / hash coverage (`ECON-04`, `ECON-05`, sweep, snapshot round-trip) | unit (reflection-driven, mirrors existing `WorldSnapshotTests`/`ReferentialIntegritySweepTests`) | Every new `[Canonical]` property covered by hash test; `WorkplaceId` covered by sweep; on/off flag changes hash | `tests/LivingWorld.Tests/WorldSnapshotTests.cs`, `tests/LivingWorld.Tests/ReferentialIntegritySweepTests.cs` (extended, not new files) | `bash scripts/test.sh` |
| Architecture/banned-API (`ArchitectureTests.cs`, `BannedApiAnalyzerTests.cs`) | unit (Roslyn in-memory compile, existing pattern) | No new violation introduced by economy systems (no `Random`/`DateTime.Now`/`Parallel` in Domain/Simulation) | `tests/LivingWorld.Tests/ArchitectureTests.cs`, `BannedApiAnalyzerTests.cs` (existing, no new file) | `bash scripts/test.sh` |
| Scenario/config loader (`EconomyScenarioLoader`) | unit | Key parse paths + every named error path (missing field, invalid range) — mirrors `BehaviorScenarioLoaderTests` | `tests/LivingWorld.Tests/Economy/EconomyScenarioLoaderTests.cs` | `bash scripts/test.sh` |
| Golden hashes (`GoldenHashesTests.cs`) | unit (regen + compare) | Regenerated once, after all economy systems are wired into `ScenarioRunner.DefaultSystems()` | `tests/LivingWorld.Tests/GoldenHashesTests.cs`, `tests/baselines/*.json` | `bash scripts/test.sh` |
| Build/lint/docs gate | none (build gate only) | — | — | `bash scripts/verify.sh` (check-docs + build + lint + test) |

**Coverage Expectation defaults applied**: domain/business logic maps 1:1 to spec ACs (strong
default, no repo-specific override found beyond R1–R5); every edge case in `spec.md` has a
dedicated test; R2/R4 (long-horizon + control-arm) are hard requirements from
`rules/eval-criteria.md`, not optional depth.

## Parallelism Assessment

> Generated from codebase — confirm before Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Domain unit tests (`Workplace`, `EconomyRules`, `MarketTransaction`, etc.) | Yes | Each test builds its own `WorldState`/`Workplace`/`TransactionContext` in-memory, no shared static/backing store | Existing pattern: `tests/LivingWorld.Tests/Population/HouseholdTests.cs`, `MoneyTests.cs` — no shared fixture, no DB |
| Simulation system tests (`ProductionSystem`, `EmploymentSystem`, etc.) | Yes | Each test calls `ScenarioRunner.Create(seed)` fresh — no cross-test state (same isolation as `BehaviorDecisionSystemTests.cs`) | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemHysteresisTests.cs:16` — per-test `WorldState`, no static mutable field |
| 10-year gate scenario tests (conservation, integrity, causal) | No | Long-running (thousands of ticks) in a single test method; sequential is safer for wall-clock predictability and matches existing `LifeTable100YearScenarioTests` style (single-threaded `dotnet test` default in this repo) | `tests/LivingWorld.Tests/Population/LifeTable100YearScenarioTests.cs` — single sequential run per test, no parallel attribute used anywhere in the suite |
| Golden hash regeneration test | No | Mutates/reads `tests/baselines/*.json` on disk — shared file, must run alone relative to other baseline writers | `tests/LivingWorld.Tests/GoldenHashesTests.cs`, `tests/LivingWorld.Tests/Baselines/BaselineFixture.cs` |
| Architecture/banned-API tests | Yes | In-memory Roslyn compilation per test (`InMemoryCompiler.cs`), no shared state | `tests/LivingWorld.Tests/InMemoryCompiler.cs` |

## Gate Check Commands

> Generated from codebase — confirm before Execute.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks touching only Domain unit tests | `bash scripts/test.sh` |
| Full | After tasks touching a simulation system, `WorldState`, snapshot, or sweep | `bash scripts/test.sh` (10-year gate tests are `Category!=Scenario` by default, already included) |
| Build | After phase completion, config/entity-only tasks, or golden-hash regeneration | `bash scripts/verify.sh` (check-docs + build + lint + test) |

> **Explicit constraint carried over from the user for this run**: `--filter Category=Scenario`
> (100-year nightly) is **never** executed during Specify/Design/Tasks — it is execution/
> verification work. Tasks below reference it only as the eventual nightly gate; the atomic
> tasks themselves are verified at the `Category!=Scenario` (10-year) gate.

---

## Execution Plan

### Phase 1: Domain Foundation (Sequential)

```
T1 → T2 → T3 → T4 → T5 → T6
```

### Phase 2: Atomic Transaction & Money Supply (Sequential, depends on Phase 1)

```
T7 → T8 → T9
```

### Phase 3: World Integration (Sequential, depends on Phase 2)

```
T10 → T11 → T12 → T13
```

### Phase 4: Simulation Systems (Sequential — each reuses the previous system's wiring pattern)

```
T14 → T15 → T16 → T17
```

### Phase 5: Behavior Integration — Consumption & Buy (Sequential, depends on Phase 4)

```
T18 → T19
```

### Phase 6: Wiring, Golden Hashes & Invariant Harness (Sequential, depends on Phase 5)

```
T20 → T21 → T22
```

### Phase 7: Causal & Conservation Verification (Sequential, depends on Phase 6)

```
T23 → T24 → T25 → T26
```

---

## Task Breakdown

### T1: `WorkplaceId` value type

**What**: Add `readonly record struct WorkplaceId(long Value)` to `Ids.cs`, same shape as
`NpcId`/`HouseholdId`.
**Where**: `src/LivingWorld.Domain/Ids.cs` (extend existing file)
**Depends on**: None
**Reuses**: `NpcId`/`HouseholdId` pattern in the same file
**Requirement**: ECON-04 (infra for referential integrity)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `WorkplaceId` compiles, `ToString()` returns `"workplace-{Value}"` (same convention)
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: none (value type, no business logic — covered indirectly once consumed)
**Gate**: quick

---

### T2: `EconomyRules` record + `Create` factory

**What**: `EconomyRules` record (`Enabled`, `FoodResourceId`, `WaterResourceId`,
`CapacityByResourceLocation`, `SpoilagePerDayByResource`, `WageByProfession`, `PriceFloor`,
`PriceCeiling`, `PriceSensitivity`, `DemandBaselinePerNpc`) with `Result<EconomyRules> Create(...)`
validating ranges (mirrors `NeedsRules.Create`).
**Where**: `src/LivingWorld.Domain/Economy/EconomyRules.cs` (new file)
**Depends on**: None
**Reuses**: `NeedsRules.Create` validation pattern, `Result<T>`
**Requirement**: ECON-01, ECON-03, ECON-05, ECON-21, ECON-24

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Create` fails naming the field for every invalid range (negative capacity, negative
      spoilage, floor > ceiling, negative wage)
- [ ] Unit tests: one per validation branch + one happy-path `Create`
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: N tests pass (no silent deletions) — N = branch count in `Create`

**Tests**: unit
**Gate**: quick

---

### T3: `EconomyCatalog` + `ProductionRecipe` record

**What**: `EconomyCatalog` record (`Recipes`, `MarketLocationTypeIds`,
`LocationTypeByProfession`) and `ProductionRecipe` record (`Inputs`, `Outputs`,
`RequiresCellResource`, `MaxWorkersPerCycle`).
**Where**: `src/LivingWorld.Domain/Economy/EconomyCatalog.cs` (new file)
**Depends on**: None
**Reuses**: `ActionCatalog` record-with-factory shape
**Requirement**: ECON-06, ECON-07, ECON-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `ProductionRecipe.MaxWorkersPerCycle <= 0` rejected via a `Result`-returning factory
- [ ] Unit tests cover: recipe with empty `Inputs` (agricultor-style), recipe with
      `RequiresCellResource` set, `LocationType` absent from `Recipes` (service profession)
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 4 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T4: `Workplace` entity ([P] with T5)

**What**: `Workplace` sealed class — `Id`, `LocationType`, `Location`, `MaxVacancies`,
`Employees`, `Stock`, `Treasury`, `Prices`; methods `Hire`/`Fire`/`Deposit`/`Withdraw`/
`CreditTreasury`/`TryDebitTreasury`.
**Where**: `src/LivingWorld.Domain/Economy/Workplace.cs` (new file)
**Depends on**: T1 (`WorkplaceId`)
**Reuses**: `Household` class shape (list + `[JsonIgnore]` computed props, single reconstruction
constructor)
**Requirement**: ECON-01, ECON-02, ECON-18, ECON-19, ECON-20

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Hire` fails when `Employees.Count >= MaxVacancies` (ECON-20)
- [ ] `Deposit` clamps at `EconomyRules.CapacityOf(resource, locationType)` and returns the
      lost amount (ECON-02) — 0 lost when under capacity, `> 0` when over
- [ ] `Withdraw` fails `Result` when insufficient, never goes negative
- [ ] Unit tests: hire past capacity fails; deposit over capacity reports loss; withdraw
      insufficient fails without mutating stock
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 6 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T5: `Npc` extension — `Wallet`/`Employer` ([P] with T4)

**What**: Add `Wallet : Money` (private set, `CreditWallet`/`TryDebitWallet`) and `Employer :
WorkplaceId?` (private set, `Hire`/`Fire` mirroring `JoinHousehold`/`LeaveHousehold`) to `Npc`;
update the single reconstruction constructor with the 2 new optional parameters.
**Where**: `src/LivingWorld.Domain/Population/Npc.cs` (modify)
**Depends on**: T1 (`WorkplaceId`)
**Reuses**: `Npc.JoinHousehold`/`LeaveHousehold` pattern, `Money.TryDebit`
**Requirement**: ECON-09, ECON-18, ECON-19, ECON-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `WorldSnapshotTests` round-trip case added for `Wallet`/`Employer` **before** touching
      the constructor (per design.md Risks & Concerns — protects against param-order bugs)
- [ ] `TryDebitWallet` delegates to `Money.TryDebit`, never allows negative wallet
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T6: `ActionType.Buy` + `ActionCatalog` wiring

**What**: Add `Buy = 6` to `ActionType` enum; update any scenario's `ActionCatalog.Create`
call (including `ScenarioRunner.DefaultActionCatalog`) to declare `MaxDurationHours[Buy]`.
**Where**: `src/LivingWorld.Domain/Behavior/ActionType.cs` (modify), `ScenarioRunner.cs`
(modify `DefaultActionCatalog`)
**Depends on**: None
**Reuses**: `ActionCatalog.Create`'s existing loop over `Enum.GetValues<ActionType>()` (AD-040
— the enum-completeness check already exists, no new test needed for coverage)
**Requirement**: AD-040 (supports ECON-09 as the trigger path)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `ActionCatalog.Create` still passes with `Buy` declared (existing
      `ActionCatalogTests.cs` catches a missing entry automatically — no new test file needed,
      confirm existing test still passes)
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: unit (existing `ActionCatalogTests.cs` covers this by construction)
**Gate**: quick

---

### T7: `WorldEventKind` new values

**What**: Add `Hired`, `Fired`, `WageUnpaid`, `ResourceLost`, `Minted`, `Destroyed` to
`WorldEventKind` enum.
**Where**: `src/LivingWorld.Simulation/WorldEvent.cs` (modify)
**Depends on**: Phase 1 complete (T1-T6)
**Reuses**: existing `WorldEventKind`/`WorldEvent` shape
**Requirement**: ECON-02, ECON-18, ECON-19, ECON-22, ECON-26

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Enum compiles; no consumer yet (wired in later tasks)
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: none (enum value, exercised by consumers in later tasks)
**Gate**: quick

---

### T8: `MarketTransaction` — atomic steps + fault-injection hook

**What**: `TransactionContext` (immutable record: `BuyerWallet`, `SellerWallet`, `SellerStock`,
`BuyerStock`, `Resource`, `UnitPrice`, `Quantity`), `TransactionStep` record, `Steps` ordered
array (debit buyer → debit seller stock → credit seller → credit buyer stock), `Execute(ctx,
failAtStep)` that composes the 4 steps over the immutable context and only then exposes a
commit-ready result.
**Where**: `src/LivingWorld.Domain/Economy/MarketTransaction.cs` (new file)
**Depends on**: Phase 1 complete (`Money`, `ResourceType` already exist)
**Reuses**: `Money.TryDebit`, `Result<T>`
**Requirement**: ECON-09, ECON-10, ECON-11, ECON-12, ECON-13

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Happy path: all 4 effects present in the returned context, in order
- [ ] Insufficient buyer funds → `Result.Fail`, returned context (if any) discarded by the
      caller, none of the 4 effects observable (ECON-11)
- [ ] Insufficient seller stock → same guarantee
- [ ] `failAtStep = i` for every `i` in `1..Steps.Count` aborts at that step — parametrized
      `[Theory]` test iterating `MarketTransaction.Steps.Count` (never a hardcoded list of
      indices) — this is the mechanism that makes ECON-13 self-enforcing
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: `Steps.Count + 3` tests pass (happy path, insufficient funds, insufficient
      stock, one per step index) — no silent deletions

**Tests**: unit
**Gate**: quick

---

### T9: Money supply counters — mint/destroy

**What**: `WorldState` gains `MoneyMinted`/`MoneyDestroyed : Money` counters (monotonic,
`[Canonical]`) and `Mint(Money, string reason)`/`Destroy(Money, string reason)` operations that
increment the counter and log `WorldEventKind.Minted`/`Destroyed` via `IWorldEventSink`.
**Where**: `src/LivingWorld.Simulation/WorldState.cs` (modify)
**Depends on**: T7 (`WorldEventKind`)
**Reuses**: `WorldState`'s existing monotonic-counter pattern (`_nextEventId`,
`_nextNpcId`)
**Requirement**: ECON-26, ECON-27

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Mint`/`Destroy` never invoked implicitly by `MarketTransaction`/wage payment (only by a
      named test/scenario call — AD-042)
- [ ] Unit test: mint increases `MoneyMinted` and the named event is logged; destroy requires
      sufficient total supply (fails `Result` otherwise, mirroring `Money.TryDebit`)
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T10: `WorldState` wiring — canonical economy fields

**What**: Add `Workplaces : IReadOnlyList<Workplace>` (+ `_workplaceById`, `NextWorkplaceId`,
`AddWorkplace`, `FindWorkplace`), `EconomyRules`, `EconomyCatalog` as `[Canonical]`
properties; update **both** `WorldState` constructors (fresh + rehydration).
**Where**: `src/LivingWorld.Simulation/WorldState.cs` (modify)
**Depends on**: T2, T3, T4, T9
**Reuses**: `WorldState.Households`/`_householdById` pattern exactly
**Requirement**: ECON-04, ECON-05, ECON-14, ECON-15, ECON-26

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Both constructors compile and accept the new parameters; existing callers
      (`ScenarioRunner.Create`, test helpers) updated
- [ ] `WorldSnapshotTests` gains a canonical/volatile classification case for every new
      property (no unclassified property — the existing reflection-based coverage test
      catches this automatically)
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: existing `WorldSnapshotTests` suite + N new classification cases pass

**Tests**: unit
**Gate**: quick

---

### T11: `ReferentialIntegritySweep` — register `WorkplaceId`

**What**: Add `[typeof(WorkplaceId)] = w => w.Workplaces.Select(wp => (object)wp.Id).ToHashSet()`
to `ValidIdResolvers`.
**Where**: `src/LivingWorld.Simulation/ReferentialIntegritySweep.cs` (modify)
**Depends on**: T10
**Reuses**: existing resolver dictionary pattern
**Requirement**: ECON-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Every_id_type_in_the_domain_assembly_has_a_registered_resolver` (existing test) passes
      without modification — proves `WorkplaceId` is now covered by construction
- [ ] New mutation-sensor case: an `Npc.Employer` pointing to a nonexistent `WorkplaceId` is
      flagged (mirrors the existing dangling-household-member test)
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 1 new test passes, existing sweep tests unchanged

**Tests**: unit
**Gate**: quick

---

### T12: `WorldSnapshot` — round-trip for new `WorldState` properties

**What**: Update `Serialize`/`Deserialize` in `WorldSnapshot.cs` to round-trip `Workplaces`,
`EconomyRules`, `EconomyCatalog`, `MoneyMinted`, `MoneyDestroyed`, `NextWorkplaceId`.
**Where**: `src/LivingWorld.Simulation/WorldSnapshot.cs` (modify)
**Depends on**: T10
**Reuses**: existing per-property `Deserialize<T>` node pattern
**Requirement**: ECON-04, ECON-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Full round-trip (`Serialize` → `Deserialize`) test on a world with `>= 1` `Workplace`
      with non-empty `Stock`/`Treasury`/`Employees` produces byte-identical canonical hash
      before/after
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 1 new round-trip test passes

**Tests**: unit
**Gate**: quick

---

### T13: `EconomyScenarioLoader`

**What**: Parse `EconomyRules`/`EconomyCatalog`/initial `Workplaces` from cenário JSON,
mirroring `BehaviorScenarioLoader`.
**Where**: `src/LivingWorld.Simulation/Economy/EconomyScenarioLoader.cs` (new file)
**Depends on**: T10
**Reuses**: `BehaviorScenarioLoader`'s manual-parse + `Result` convention
**Requirement**: ECON-01, ECON-03, ECON-05, ECON-06, ECON-23, ECON-24

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Missing required field → `Result.Fail` naming the field (mirrors
      `BehaviorScenarioLoaderTests`)
- [ ] Happy path parses a full economy block into `EconomyRules`/`EconomyCatalog`/`Workplace`
      list
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 5 tests pass (happy path + 4 named-error paths)

**Tests**: unit
**Gate**: quick

---

### T14: `EmploymentSystem`

**What**: `Daily` system matching unemployed adult NPCs to `Workplace`s with a vacancy via
`EconomyCatalog.LocationTypeByProfession`, ordered by `NpcId.Value`/`WorkplaceId.Value`; fires
employees of dead/removed `Workplace`s before matching new hires.
**Where**: `src/LivingWorld.Simulation/Economy/EmploymentSystem.cs` (new file)
**Depends on**: T10, T11
**Reuses**: `Workplace.Hire/Fire`, `NatalitySystem`'s ordered-iteration discipline
**Requirement**: ECON-18, ECON-19, ECON-20

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Unemployed NPC with matching profession + open vacancy gets hired within 1 `Daily` tick,
      `WorldEventKind.Hired` logged
- [ ] Dead employee is removed from `Workplace.Employees` and `Npc.Employer` cleared in the
      same tick, `Fired`/consistent with death — no orphan reference survives a full tick
- [ ] 10-year gate test: no `Workplace` ever exceeds `MaxVacancies`, every `Npc.Employer`
      resolves to an existing `Workplace` (ECON-20, checked every tick)
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 4 tests pass (no silent deletions)

**Tests**: unit + 10-year gate scenario (sequential, not `[P]` — see Parallelism Assessment)
**Gate**: full

---

### T15: `ProductionSystem`

**What**: `Daily` system: for each `Workplace` with a declared recipe, gate on worker presence
(ECON-07) and cell-resource availability (ECON-08), consume `Inputs`, deposit `Outputs` via
`Workplace.Deposit` (capacity+loss already handled by T4), apply
`EconomyRules.SpoilagePerDayByResource` (ECON-03).
**Where**: `src/LivingWorld.Simulation/Economy/ProductionSystem.cs` (new file)
**Depends on**: T14
**Reuses**: `Workplace.Deposit/Withdraw`, `MapCell.Resources` (Fase 2, unchanged)
**Requirement**: ECON-03, ECON-06, ECON-07, ECON-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Workplace` with a worker present and matching cell resource produces `> 0` output
- [ ] `Workplace` with 0 workers present produces exactly 0 (ECON-07)
- [ ] `Workplace` requiring a cell resource absent from its cell produces 0 even with a worker
      present (ECON-08)
- [ ] Spoilage reduces stock by the declared rate; rate 0 leaves stock untouched
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 4 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T16: `MarketPricingSystem`

**What**: `Daily` system recalculating `Workplace.Prices` for every resource in a
market-flagged `Workplace` from `EstoqueOfertado / DemandaEstimada`, clamped to
`PriceFloor`/`PriceCeiling`.
**Where**: `src/LivingWorld.Simulation/Economy/MarketPricingSystem.cs` (new file)
**Depends on**: T15
**Reuses**: `EconomyRules` parameters only, no NPC decision logic
**Requirement**: ECON-23, ECON-24

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Price rises when supply/demand ratio drops between two ticks, falls when it rises (unit
      test with two crafted stock levels)
- [ ] Price never leaves `[PriceFloor, PriceCeiling]` even with an extreme stock input
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T17: `WagePaymentSystem`

**What**: `Monthly` system paying `EconomyRules.WageByProfession` from `Workplace.Treasury` to
each employee's `Npc.Wallet`, ordered by `NpcId.Value`; emits `WageUnpaid` on insufficient
treasury without mutating either balance.
**Where**: `src/LivingWorld.Simulation/Economy/WagePaymentSystem.cs` (new file)
**Depends on**: T14
**Reuses**: `Workplace.TryDebitTreasury`, `Money.TryDebit`
**Requirement**: ECON-21, ECON-22

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Workplace` with sufficient `Treasury` pays every employee, `Treasury` decreases by the
      exact sum
- [ ] `Workplace` with insufficient `Treasury` emits `WageUnpaid` for the affected employee(s)
      and leaves `Treasury`/`Wallet` byte-identical to before the attempt (ECON-22)
- [ ] Mutation-sensor test: disable the balance check by test flag → the "no side effect"
      assertion fails, proving the check measures something real (per
      `rules/eval-criteria.md` "teste de mutação para gate de segurança")
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 tests pass (no silent deletions)

**Tests**: unit
**Gate**: quick

---

### T18: Consumption — `Eat` requires stock

**What**: Modify `BehaviorDecisionSystem.ApplyActionEffect(Eat)` to withdraw 1 unit of
`EconomyRules.FoodResourceId`/`WaterResourceId` from the NPC's `Household`'s associated
`Workplace`-independent home stock **[see Note]** before restoring `Hunger`/`Thirst`; no stock
→ no restoration, no exception.

> **Note**: `Household` itself needs a `Stock` field for this to work (it is not a
> `Workplace`) — this task also adds a minimal `Stock : IReadOnlyDictionary<ResourceType,long>`
> + `Withdraw`/`Deposit` to `Household`, reusing the exact same clamp/loss logic factored out
> of `Workplace` in T4 (extract a shared `ResourceStock` helper type to avoid duplicating the
> capacity/loss code — see design.md Tech Decisions addendum below).

**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modify),
`src/LivingWorld.Domain/Population/Household.cs` (modify), `src/LivingWorld.Domain/Economy/ResourceStock.cs` (new — shared helper extracted from T4's `Workplace.Deposit/Withdraw`)
**Depends on**: T4, T15
**Reuses**: `ResourceStock` (factored out in this task from `Workplace`), existing
`ApplyActionEffect` switch
**Requirement**: ECON-16, ECON-17

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Household` with food+water in stock: `Eat` completes, stock decremented by 1 each,
      `Hunger`/`Thirst` restored to 100 (ECON-16)
- [ ] `Household` with no food in stock: `Eat` completes, `Hunger` NOT restored, no exception,
      no negative stock (ECON-17)
- [ ] Existing NEEDS-03 (starvation) test still passes unmodified — proves the new gate does
      not break the Fase 4 starvation path when stock is legitimately empty
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 new tests pass, all Fase 4 Behavior tests still pass (no silent deletions)

**Tests**: unit
**Gate**: full (touches an existing Fase 4 system — run the broader suite)

---

### T19: `Buy` action wiring

**What**: `ActionCatalog.RoutineOf`/`UtilityBaseOf`/`RefineForLocation` gain `Buy`: utility
score grows with the `Household`'s projected food/water deficit; `RefineForLocation` routes to
the nearest market-flagged `Workplace` (via `Travel`, same mechanism as `Sleep`→`Travel`,
NEEDS-14); on arrival, `Buy` executes a `MarketTransaction` (NPC `Wallet` → market
`Workplace.Treasury`, market `Workplace.Stock` → `Household.Stock`).
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modify)
**Depends on**: T8, T16, T18
**Reuses**: `MarketTransaction.Execute`, `RefineForLocation`/`SleepDestinationOf` pattern
**Requirement**: ECON-09 (trigger path), ECON-16, ECON-17

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC with low household food stock and sufficient `Wallet` balance travels to and buys
      from the nearest market `Workplace`, `Household.Stock` increases, `Wallet` decreases by
      exactly `UnitPrice × Quantity`
- [ ] NPC with insufficient `Wallet` balance does not buy (transaction fails cleanly, no stock
      change) — falls back to existing routine/utility choice
- [ ] Gate check passes: `bash scripts/test.sh`
- [ ] Test count: 3 tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T20: Wire all economy systems into `ScenarioRunner.DefaultSystems()`

**What**: Add `EmploymentSystem`, `ProductionSystem`, `MarketPricingSystem`,
`WagePaymentSystem` to `ScenarioRunner.DefaultSystems()` (in that order, after
`BehaviorDecisionSystem`); add `ScenarioRunner.DefaultEconomyRules`/`DefaultEconomyCatalog`/
`DefaultWorkplaces` constants (medieval: farm/lumber-mill/forge/market/tavern-ish set) and wire
them into `WorldState`/`Create`.
**Where**: `src/LivingWorld.Simulation/ScenarioRunner.cs` (modify)
**Depends on**: T14, T15, T16, T17, T19
**Reuses**: `ScenarioRunner`'s existing `DefaultXRules` constant pattern
**Requirement**: ECON-04, ECON-05 (order affects the hash, must be declared once, deliberately)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `ScenarioRunner.Create` builds a world with `>= 1` `Workplace` per default profession
      (farm, lumber, forge, market) at `DefaultVillageLocation`
- [ ] Determinism test (`DeterminismTwoProcessTests`) still passes with the economy wired in
- [ ] Gate check passes: `bash scripts/verify.sh`

**Tests**: none (wiring only; behavior already covered by T14-T19's unit tests)
**Gate**: build

---

### T21: Regenerate golden hashes

**What**: Regenerate `tests/baselines/golden-hashes.json` (or equivalent) now that the
economy changes the canonical hash of the default 10-year scenario.
**Where**: `tests/baselines/*.json` (regenerated), `tests/LivingWorld.Tests/GoldenHashesTests.cs`
(no code change, just re-run the regen procedure already used by Fase 3/4)
**Depends on**: T20
**Reuses**: existing golden-hash regeneration procedure (same one used at the end of Fase 4)
**Requirement**: ECON-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `GoldenHashesTests` passes with the regenerated baseline
- [ ] `git diff` on the baseline file shows only the expected hash values changed (no
      unrelated scenario touched)
- [ ] Gate check passes: `bash scripts/verify.sh`

**Tests**: unit (existing `GoldenHashesTests.cs`, regenerated baseline)
**Gate**: build

---

### T22: Economy on/off hash-divergence test (ECON-05)

**What**: `[Fact]` running the default 10-year scenario twice, same seed, once with
`EconomyRules.Enabled = true` and once `false` (all economy systems become no-ops when
disabled), asserting `Hash(world_on) != Hash(world_off)`.
**Where**: `tests/LivingWorld.Tests/Economy/EconomyHashScenarioTests.cs` (new file, mirrors
`UtilityAiHashScenarioTests.cs` from Fase 4/NEEDS-04)
**Depends on**: T20
**Reuses**: `UtilityAiHashScenarioTests.cs` structure
**Requirement**: ECON-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Test passes: hashes differ between the two arms
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: unit (scenario-style but within the 10-year gate horizon, `Category!=Scenario`)
**Gate**: quick

---

### T23: Money conservation invariant (10-year gate)

**What**: `[Fact]` ticking the default scenario 10 years, asserting after **every tick** that
`sum(Npc.Wallet) + sum(Workplace.Treasury) == initial + MoneyMinted - MoneyDestroyed`.
**Where**: `tests/LivingWorld.Tests/Economy/MoneyConservationTests.cs` (new file)
**Depends on**: T20
**Reuses**: `LifeTable100YearScenarioTests`-style tick loop, but asserting every tick (not just
at the end) — same idiom as `BehaviorDecisionSystemHysteresisTests`'s per-tick loop
**Requirement**: ECON-14, ECON-27

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 10-year (`Category!=Scenario`) test passes with the assert running every tick
- [ ] A companion `[Trait("Category","Scenario")]` 100-year variant is written (not run in
      this phase per the user's explicit constraint — file exists, marked, and is exercised
      only by the nightly `--filter Category=Scenario` run, never here)
- [ ] Mutation-sensor case: a test-only path that skips incrementing `MoneyMinted` on a mint
      call makes this test fail — proves the invariant is not decorative (ECON-27)
- [ ] Gate check passes: `bash scripts/test.sh` (10-year variant only)

**Tests**: unit (10-year gate, sequential) + scenario (100-year, not executed this phase)
**Gate**: full

---

### T24: Resource conservation invariant (10-year gate)

**What**: `[Fact]` per `ResourceType`, ticking 10 years, asserting after every tick
`produced == consumed + stocked + lost` using auditable counters accumulated by
`ProductionSystem`/consumption/`Workplace.Deposit` loss.
**Where**: `tests/LivingWorld.Tests/Economy/ResourceConservationTests.cs` (new file)
**Depends on**: T20
**Reuses**: same tick-loop idiom as T23
**Requirement**: ECON-15

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 10-year test passes for every `ResourceType` in the default catalog
- [ ] Companion 100-year `[Trait("Category","Scenario")]` variant exists, not run this phase
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: unit (10-year gate) + scenario (100-year, not executed this phase)
**Gate**: full

---

### T25: Scarcity → price causal test with control arm (ECON-25)

**What**: Extend the base/tratamento harness (per design.md) to accept `ProductionMultiplier`;
`[Theory]` over 10 seeds asserting `preçoTrat[t] > preçoBase[t]` for every tick in
`[t0, t0+30]` after halving wheat production.
**Where**: `tests/LivingWorld.Tests/Economy/EconomyScenarioHarness.cs` (new, test-only
harness), `tests/LivingWorld.Tests/Economy/ScarcityPriceCausalTests.cs` (new)
**Depends on**: T20
**Reuses**: Fase 3's existing base/tratamento test pattern (same seed, paired arms)
**Requirement**: ECON-28, ECON-25

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Harness applies `ProductionMultiplier` as a decorator over `ProductionSystem`, no
      duplicated `ScenarioRunner.Create` logic (ECON-28)
- [ ] 10/10 seeds show `preçoTrat[t] > preçoBase[t]` for every tick in the declared window
- [ ] Gate check passes: `bash scripts/test.sh`

**Tests**: unit (10-seed loop, within the 10-year+30-day window — not a `Category=Scenario`
100-year run)
**Gate**: full

---

### T26: Full causal chain with control — famine → hunger (ECON-29)

**What**: `[Theory]` over 10 seeds, base/tratamento (tratamento = wheat production cut to 0
from `t0`), asserting the count of NPCs with `Hunger` below `NeedsRules.UrgencyThreshold`
(Fase 4) is strictly greater in the treatment arm at some declared checkpoint, 10/10 seeds.
**Where**: `tests/LivingWorld.Tests/Economy/FamineCausalChainTests.cs` (new)
**Depends on**: T25
**Reuses**: `EconomyScenarioHarness` from T25, `NeedsRules.UrgencyThreshold` (Fase 4)
**Requirement**: ECON-29

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 10/10 seeds show treatment hunger-count strictly greater than base at the declared
      checkpoint
- [ ] Gate check passes: `bash scripts/verify.sh` (phase-closing build gate)

**Tests**: unit (10-seed loop)
**Gate**: build

---

## Parallel Execution Map

```
Phase 1 (Sequential, foundation types):
  T1 ──→ T2 ──→ T3 ──┬──→ T4 [P] ──┐
                      └──→ T5 [P] ──┼──→ T6
                                    │
Phase 2 (Sequential, depends on Phase 1):
  T7 ──→ T8 ──→ T9

Phase 3 (Sequential, depends on Phase 2):
  T10 ──→ T11 ──→ T12 ──→ T13

Phase 4 (Sequential, each system reuses the previous wiring):
  T14 ──→ T15 ──→ T16 ──→ T17

Phase 5 (Sequential, depends on Phase 4):
  T18 ──→ T19

Phase 6 (Sequential, depends on Phase 5):
  T20 ──→ T21 ──→ T22

Phase 7 (Sequential, depends on Phase 6):
  T23 ──→ T24 ──→ T25 ──→ T26
```

**Parallelism constraint:** `T4`/`T5` are `[P]` — both depend only on `T1` (`WorkplaceId`),
touch disjoint files (`Workplace.cs` new vs. `Npc.cs` modify), and their required test type
(Domain unit, per Parallelism Assessment) is parallel-safe. `T6` depends on both because it
edits `ScenarioRunner.DefaultActionCatalog`, which is easiest to get right once `Workplace`
exists conceptually (no hard code dependency, but sequenced for review clarity). Every other
task in this breakdown has a real inter-task dependency (each system/task builds on state the
previous one introduced), so no other `[P]` pair exists — with 7 phases (> 3), this feature
qualifies for the phase-worker offer per `sub-agents.md` at Execute time.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: `WorkplaceId` | 1 value type | ✅ Granular |
| T2: `EconomyRules` | 1 record + factory | ✅ Granular |
| T3: `EconomyCatalog` | 1 record + factory (2 types, same file, cohesive) | ✅ Granular |
| T4: `Workplace` entity | 1 class | ✅ Granular |
| T5: `Npc` extension | 1 file, 2 related fields (wallet+employer, same round-trip concern) | ✅ Granular |
| T6: `ActionType.Buy` | 1 enum value + 1 constant update | ✅ Granular |
| T7: `WorldEventKind` values | 1 enum, 6 values (no logic) | ✅ Granular |
| T8: `MarketTransaction` | 1 class (steps + execute) | ✅ Granular |
| T9: Money supply counters | 1 file modify, 1 concept (mint/destroy) | ✅ Granular |
| T10: `WorldState` wiring | 1 file modify, 1 concept (canonical fields) | ✅ Granular |
| T11: Sweep registration | 1 dictionary entry | ✅ Granular |
| T12: Snapshot round-trip | 1 file modify | ✅ Granular |
| T13: `EconomyScenarioLoader` | 1 class | ✅ Granular |
| T14: `EmploymentSystem` | 1 system | ✅ Granular |
| T15: `ProductionSystem` | 1 system | ✅ Granular |
| T16: `MarketPricingSystem` | 1 system | ✅ Granular |
| T17: `WagePaymentSystem` | 1 system | ✅ Granular |
| T18: Consumption gate on `Eat` | 1 behavior change + 1 shared helper extraction | ✅ Granular |
| T19: `Buy` action wiring | 1 behavior change (3 related methods, same system) | ✅ Granular |
| T20: Wire systems into `ScenarioRunner` | 1 file, 1 concept (registration) | ✅ Granular |
| T21: Regenerate golden hashes | 1 artifact regeneration | ✅ Granular |
| T22: Economy on/off hash test | 1 test file | ✅ Granular |
| T23: Money conservation invariant | 1 test file | ✅ Granular |
| T24: Resource conservation invariant | 1 test file | ✅ Granular |
| T25: Scarcity→price causal test | 1 harness + 1 test file (cohesive pair) | ✅ Granular |
| T26: Famine→hunger causal test | 1 test file | ✅ Granular |

**Granularity check**: every task is 1 component/file-concept/system — none bundles multiple
unrelated deliverables.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | None | None | ✅ Match |
| T4 | T1 | T1 → T4 | ✅ Match |
| T5 | T1 | T1 → T5 | ✅ Match |
| T6 | T4, T5 | T4/T5 → T6 | ✅ Match |
| T7 | Phase 1 complete (T1-T6) | T6 → T7 (phase boundary) | ✅ Match |
| T8 | Phase 1 complete | T7 → T8 | ✅ Match |
| T9 | T7 | T8 → T9 | ✅ Match |
| T10 | T2, T3, T4, T9 | Phase 2 → T10 (phase boundary) | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T10 | T11 → T12 | ✅ Match |
| T13 | T10 | T12 → T13 | ✅ Match |
| T14 | T10, T11 | Phase 3 → T14 (phase boundary) | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T14 | T16 → T17 (sequenced for review; T17 only hard-depends on T14) | ✅ Match |
| T18 | T4, T15 | Phase 4 → T18 (phase boundary) | ✅ Match |
| T19 | T8, T16, T18 | T18 → T19 | ✅ Match |
| T20 | T14, T15, T16, T17, T19 | Phase 5 → T20 (phase boundary) | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |
| T22 | T20 | T21 → T22 (sequenced for review; T22 only hard-depends on T20) | ✅ Match |
| T23 | T20 | Phase 6 → T23 (phase boundary) | ✅ Match |
| T24 | T20 | T23 → T24 (sequenced for review; T24 only hard-depends on T20) | ✅ Match |
| T25 | T20 | T24 → T25 (sequenced for review; T25 only hard-depends on T20) | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |

**Note on same-phase sequential tasks without a hard dependency** (T16↔T17, T21↔T22, T23↔T24↔T25):
these are drawn as sequential arrows for reading order and review clarity, but the task bodies
correctly declare their real dependency (often the phase's first task). None are marked `[P]`
regardless, because their required test type is scenario/integration-style (Parallelism
Assessment: 10-year gate tests and golden-hash regeneration are **not** parallel-safe) — so
sequential execution is correct either way, and the diagram does not overstate a dependency
that would block parallelism the tasks don't need.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1: `WorkplaceId` | Entity/value type | none (build gate only) | none | ✅ OK |
| T2: `EconomyRules` | Domain value object | unit | unit | ✅ OK |
| T3: `EconomyCatalog` | Domain value object | unit | unit | ✅ OK |
| T4: `Workplace` | Domain value object | unit | unit | ✅ OK |
| T5: `Npc` extension | Domain value object | unit | unit | ✅ OK |
| T6: `ActionType.Buy` | Entity/config (enum value) | none / unit (existing test covers it) | unit | ✅ OK |
| T7: `WorldEventKind` | Entity/config (enum values, no logic) | none (build gate only) | none | ✅ OK |
| T8: `MarketTransaction` | Domain value object | unit | unit | ✅ OK |
| T9: Money supply counters | Domain/Simulation logic | unit | unit | ✅ OK |
| T10: `WorldState` wiring | Simulation system / snapshot coverage | unit | unit | ✅ OK |
| T11: Sweep registration | Determinism/hash coverage | unit | unit | ✅ OK |
| T12: Snapshot round-trip | Determinism/hash coverage | unit | unit | ✅ OK |
| T13: `EconomyScenarioLoader` | Scenario/config loader | unit | unit | ✅ OK |
| T14: `EmploymentSystem` | Simulation system | unit + scenario | unit + 10-year gate | ✅ OK |
| T15: `ProductionSystem` | Simulation system | unit + scenario | unit | ✅ OK (conservation covered by T23/T24, not deferred — see note) |
| T16: `MarketPricingSystem` | Simulation system | unit + scenario | unit | ✅ OK (causal price test covered by T25, not deferred — see note) |
| T17: `WagePaymentSystem` | Simulation system | unit + scenario | unit | ✅ OK |
| T18: Consumption gate | Simulation system (behavior modify) | unit | unit | ✅ OK |
| T19: `Buy` wiring | Simulation system (behavior modify) | unit | unit | ✅ OK |
| T20: `ScenarioRunner` wiring | Entity/config (wiring only) | none (build gate only) | none | ✅ OK |
| T21: Golden hashes | Determinism/hash coverage | unit | unit | ✅ OK |
| T22: Economy on/off hash | Determinism/hash coverage | unit | unit | ✅ OK |
| T23: Money conservation | Simulation system (scenario-level invariant) | unit + scenario | unit + scenario | ✅ OK |
| T24: Resource conservation | Simulation system (scenario-level invariant) | unit + scenario | unit + scenario | ✅ OK |
| T25: Scarcity→price causal | Simulation system (scenario-level causal) | unit + scenario | unit | ✅ OK (test type still "unit" per matrix's `Category!=Scenario` 10-year band — only the 100-year nightly variant is "scenario"/`Category=Scenario`, correctly deferred, never test-deferred) |
| T26: Famine→hunger causal | Simulation system (scenario-level causal) | unit + scenario | unit | ✅ OK (same note as T25) |

**Note on T15/T16 vs. T23/T24/T25**: the Test Coverage Matrix lists conservation and causal
checks as requirements of the "Simulation systems" layer as a whole, not of any single system
in isolation — `ProductionSystem` alone cannot be asserted for money/resource conservation
(conservation is a property of the *whole* economy loop: production + consumption + market +
wage together). This is a **merge-forward** per tasks.md's "Resolving compilation
dependencies" rule: the conservation/causal tests are written once the full loop exists (T20
onward), in T23-T26, rather than duplicated per system. Each individual system task (T14-T19)
still ships its own component-level unit tests for its own branches — nothing is deferred
without a test, only the cross-system invariant is written where it becomes meaningfully
checkable.

---

## Tips
- `EconomyRules`/`EconomyCatalog` validation errors should read exactly like
  `NeedsRules`/`ActionCatalog`'s ("Campo: motivo") — keeps the whole codebase's error style
  consistent for anyone grepping logs later.
- Write T8's fault-injection test (`failAtStep` loop) before T15-T19 consume
  `MarketTransaction` — it is the cheapest place to catch an atomicity bug, per design.md's
  Tips section.
- T23/T24 are the two criteria the roadmap calls "most important" — do not let scope pressure
  push them to the end of the loop; if time runs short, they take priority over T25/T26.
