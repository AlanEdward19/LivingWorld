# phase-16-1-power-engine Validation

**Date**: 2026-08-25
**Iteration**: Re-verify 1/3 (code-review only)
**Spec**: `.specs/features/phase-16-1-power-engine/spec.md`
**Diff range**: working tree vs HEAD (uncommitted; Extraordinary/Population/Economy/Behavior/Map + Domain)
**Verifier**: independent sub-agent (author ≠ verifier)
**Mode**: static evidence only — no Verifier-run gate; no discrimination sensor mutations
**P3**: PWR-40/41 deferred — not scored

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T8 | ⚠️ Partial checkboxes | Implementation + tests on disk; `tasks.md` Done-when still `[ ]` for early tasks |
| T9–T28 | ✅ Done (tasks.md) | Checkboxes `[x]`; tests colocated under `tests/.../Extraordinary` (+ Behavior) |

Not a coverage pass by itself — AC evidence below.

---

## Spec-Anchored Acceptance Criteria

Result legend: ✅ static evidence matches spec outcome · ❌ missing/wrong assertion · ⚠️ spec-precision / weak mapping. **Runtime not confirmed by this Verifier.**

### P1 — Registry (PWR-01..05)

| ID | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PWR-01 | Resolve via registered mechanic, not growing effect switch | `ExtraordinaryMechanicRegistryTests.cs:12-29` — `Assert.IsType<…>(registry.Resolve(token))`; `Assert.Null` unknown | ✅ |
| PWR-02 | New mechanic = class + composition root | Registry `CreateDefault` / `Default` lists classes; no AC test edits Invoke loop | ✅ (review) |
| PWR-03 | Unregistered → `"Effects: alvo não suportado '<chave>'"` | `ExtraordinaryInvocationEngineTests.cs:40-42` — `(false, "Effects: alvo não suportado 'unknown.token'", …)` | ✅ |
| PWR-04 | Deterministic / no unseeded RNG | `ExtraordinaryInvocationEngineTests` authored-resolution pair; luck/combat same-seed asserts | ✅ |
| PWR-05 | Reject before effects (atomicity) | `ExtraordinaryInvocationEngineTests.cs:56-58` — `(false, 50, 5L)` unfunded | ✅ |

### P1 — Area (PWR-06..09)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-06 | NPCs in radius, order by Id | `AreaTargetResolverTests.cs:23` — `(65, 50, 3L)` inside/outside | ✅ |
| PWR-07 | Radius remeasured from current position | `AreaTargetResolverTests.cs:27-43` — move then second invoke → `(65, 65, 3L)` | ✅ |
| PWR-08 | Zero targets succeed | `:57-58` — `IsSuccess`, health unchanged | ✅ |
| PWR-09 | Cost once | `:76-78` — stock `3L`, `CostsPaid == 1` | ✅ |

### P1 — Transfer (PWR-10..13)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-10 | Debit/credit atomic | `TransferMechanicTests.cs:37` — `(80, 70)` | ✅ |
| PWR-11 | Insufficient fails, no credit | `:50-52` — `insuficiente`, `(10, 50)` | ✅ |
| PWR-12 | Clamp at ceiling | `:66` — `(80, 100)` | ✅ |
| PWR-13 | Cost before transfer | `:19-24` — success + event order `…CostPaid` then `…EffectApplied` | ✅ |

### P2 — Senescence (PWR-20..23)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-20 | Rate &lt;1 delays death age | `SenescenceMechanicTests.cs:49` — `halfSum > fullSum` | ✅ |
| PWR-21 | Rate 0: no age-death schedule | `:15-17` DoesNotContain mortality payload | ✅ |
| PWR-22 | No rewrite of already scheduled | `:87` same event Id/tick | ✅ |
| PWR-23 | Min of two powers | `:53-73` — `Assert.Equal(0.25, resolved.SenescenceRateMultiplier)` | ✅ |

### P2 — Luck (PWR-24..27)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-24 | Bonus adds to capacity | `LuckMechanicTests` treated vs control Resolve | ✅ |
| PWR-25 | Curse `n` for declared ticks | `:56` — `(10, ctx.CurrentTick + 100)`; impl `LuckMechanic.TryParseCurse` two-part | ✅ (static) |
| PWR-26 | Same seed identical | tuple equality on resolutions | ✅ |
| PWR-27 | Capacity clamp 0 | huge curse Resolve capacity `0` | ✅ |

### P2 — Mind (PWR-28..31)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-28 | Read public fields only | `MindMechanicTests.cs:113-115` agreeableness/household, no secret | ✅ |
| PWR-29 | Alter via authoring path | `:31-34` AuthoringCommandApplied + personality | ✅ |
| PWR-30 | Revert when manifestation ends | `:52` Agreeableness 50 | ✅ |
| PWR-31 | Last invocation wins | `:96` Agreeableness 90 | ✅ |

### P2 — Lifespan / Transmute (PWR-32..38)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-32 | ±10 years | `LifespanTransferTests.cs:20-21` | ✅ |
| PWR-33 | Insufficient years fails | `:39-40` insuficiente | ✅ |
| PWR-34 | Dead / consumed fails | dead-NPC fail path in same file | ✅ |
| PWR-35 | Destroyed + Minted | `MatterTransmuteMechanicTests.cs:19-21` | ✅ |
| PWR-36 | Insufficient: no credit | `:35-37` | ✅ |
| PWR-37 | Events explain stock | conservation asserts in file | ✅ |
| PWR-38 | Rate uncapped | rate-3 dest stock assert | ✅ |

### P2 — Strength / production (PWR-50..55)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-50 | Pickup above cap rejected | `CarryCapacityTests.cs:12-15` | ✅ |
| PWR-51 | strength:3 → 3× | `:23` | ✅ |
| PWR-52 | Dormant = base | `:27` | ✅ |
| PWR-53 | Production ×2 | `AttributeStrengthProductionTests.cs:29-31` 15 vs 30 | ✅ |
| PWR-54 | Cease restores | cease test restores skill-only | ✅ |
| PWR-55 | Respects workplace cap | cap assert in same file | ✅ |

### P2 — Perception / reaction / combat (PWR-56..65)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-56 | perception:8 flees at 6 | `BehaviorPerceptionTests.cs:21-22` Travel vs Work | ✅ |
| PWR-57 | No power: adjacency only | `:34-35` | ✅ |
| PWR-58 | Per-carrier radii | `:45-46` | ✅ |
| PWR-59 | reaction-speed:2 halves wake | `:64-65` 8 vs 4 | ✅ |
| PWR-60 | Combined flees first | `:80-81` | ✅ |
| PWR-61 | Cease restores cadence | `:95-96` 8 | ✅ |
| PWR-62 | Strike Resolver + health | `CombatMechanicTests.cs:27-28` | ✅ |
| PWR-63 | `CombatResolved` dedicated | `:29`, `:43-45` | ✅ |
| PWR-64 | Same seed | `:65-66` payload equality | ✅ |
| PWR-65 | Enabled=false | `:79` `"Extraordinary.Enabled: false"` | ✅ |

### P2 — Gravity (PWR-70..73)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-70 | gravity.self → CanFly/speed | `GravityMechanicTests.cs:18`; compose keeps dash when flying (`GravityMechanic.cs:54,89-94`) | ✅ (static) |
| PWR-71 | gravity.target reduces budget | `:61-62` steps 3 vs 1 | ✅ |
| PWR-72 | `movement.*` synonym + flight+speed | `ExtraordinaryArchetypeScenarioTests.cs:54-55` Kryptoniano expects `(true, 2)`; visual `:18-29` expects speed `3d` with flight+speed-multiplier:3 | ✅ (static) |
| PWR-73 | self×target compose | `GravityMechanicTests.cs:86` `1d/expectedGravity` | ✅ |

### P2 — Temperature (PWR-74..76)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-74 | Deterministic base temp | `EnvironmentTemperatureMechanicTests.cs:36-38`; `MapCell` default `Temperature = 0` + `WithDerivedTemperature` | ✅ (static) |
| PWR-75 | Delta then revert | `:22-27` | ✅ |
| PWR-76 | Crop hook + no RNG drift | `:63-65`; `:47-50` | ✅ |
| Snapshot | `EnvironmentTemperatureAdjustments` classified | `WorldState.cs:134-135` `[Canonical]`; hydrate in `WorldSnapshot.cs:176+` | ✅ (static) |

### P2 — Fauna / flora / memory (PWR-77..83, 101..103)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-77 | Animal entity | `FaunaMechanicTests` constructs/moves `Animal` | ✅ |
| PWR-78 | Dominate / infect | `:17-19`; infect asserts in file | ✅ |
| PWR-79 | Disabled | disabled fauna test | ✅ |
| PWR-80 | read-memory real facts | `MindMechanicTests` read-memory lists fact ids | ✅ |
| PWR-81 | erase = forgotten metadata | erase + `Assert.Same` Fact | ✅ |
| PWR-82 | implant existing only | implant success + fail paths | ✅ |
| PWR-83 | Disabled | Enabled=false mind path | ✅ |
| PWR-101 | Plant entity | `FloraMechanicTests` | ✅ |
| PWR-102 | 5× growth | `:17-20` stages 10 vs 2 | ✅ |
| PWR-103 | Disabled | disabled flora test | ✅ |

### P2 — Passive / vulnerability / skill / fertility (PWR-90..100)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-90 | Auto tick | `ExtraordinaryPassiveTickSystemTests.cs:16-19` 60 then 70 | ✅ |
| PWR-91 | Cost skip, keep power | `:35-39` manifested still true | ✅ |
| PWR-92 | Unmanifest stops | unmanifest stops in file | ✅ |
| PWR-93 | Typed vuln × factor | `VulnerabilityMechanicTests.cs:21` 30 vs 40 (typed `npc.health:sunlight`) | ✅ |
| PWR-94 | No match = normal | `:53-54` | ✅ |
| PWR-95 | Untyped unchanged | untyped path | ✅ |
| PWR-96 | skill.copy exact | `SkillMechanicTests.cs:28` `(40d, 40d)` | ✅ |
| PWR-97 | learn-rate 5× | learn-rate assert | ✅ |
| PWR-98 | cease no residue | `:105-107` | ✅ |
| PWR-99 | fertility multiplies Natality rate | `FertilityMechanicTests.cs:48` asserts helper `2.5`, not Natality counts | ⚠️ |
| PWR-100 | fertility:0 never conceives | `:29` | ✅ |

### P2 — Instantiation / identity / bond / soul / dimension / foresight (PWR-104..122)

| ID | Outcome | Evidence | Result |
| --- | --- | --- | --- |
| PWR-104 | clone new Id, copy | `NpcInstantiationMechanicTests.cs:31-35` | ✅ |
| PWR-105 | split-on-death N | split asserts | ✅ |
| PWR-106 | reincarnate fraction | reincarnate skill assert | ✅ |
| PWR-107 | `NpcInstantiated` | `:42` | ✅ |
| PWR-108 | possess attribution | `ControlMechanicTests` possess log to target | ✅ |
| PWR-109 | body-swap personality | `:77-80` | ✅ |
| PWR-110 | impersonate cosmetic | ImpersonatingId without NpcId swap | ✅ |
| PWR-111 | restore on cease | body-swap revert test | ✅ |
| PWR-112 | bond.share each tick | `BondMechanicTests.cs:17` `(40,40)` | ✅ |
| PWR-113 | oath consequence | oath health assert | ✅ |
| PWR-114 | death undoes bond | BondPartnerId null | ✅ |
| PWR-115 | ghost queryable | `SoulMechanicTests.cs:20-26` | ✅ |
| PWR-116 | no power → not ghost | `:44-47` | ✅ |
| PWR-117 | pocket no Destroyed | `DimensionMechanicTests.cs:31-33` | ✅ |
| PWR-118 | portal A↔B | `:70`, `:87` | ✅ |
| PWR-119 | cease stops portal | cease stays on cellA | ✅ |
| PWR-120 | preview = Resolver, no Fact | `ForesightMechanicTests.cs:30-32` | ✅ |
| PWR-121 | stream unchanged | `:50` | ✅ |
| PWR-122 | later world can diverge | diverge assert | ✅ |

### Success criteria (spec, not PWR-*)

| Criterion | Evidence | Result |
| --- | --- | --- |
| ~15–20 sample powers as scenario data | `ExtraordinaryScenarioLoaderTests.cs:192-232` — 18 descriptors load | ✅ count |
| Cover listed main categories | Pack has luck/mind/transfer/flight/combat/gravity/temp/fauna; **missing** explicit senescence, strength, perception, read-memory tokens | ⚠️ |
| Locomotion suite + flight+speed compose | Archetype/visual asserts present (runtime deferred) | ✅ (static) |
| Full gate green | Not run by Verifier | ⛔ DEFERRED TO USER |

**P3 PWR-40/41**: Deferred — not scored.

**Status**: ⚠️ Static AC mostly covered; **overall Ready blocked** until user gate. Spec-precision: PWR-99, sample category completeness.

---

## Discrimination Sensor

**Sensor deferred — user owns full gate.** No mutations injected by this Verifier (per mandatory rule).

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Deferred | ⛔ DEFERRED TO USER |

**Sensor depth**: n/a (deferred)
**Result**: DEFERRED TO USER

---

## Interactive UAT Results

Skipped (backend/domain).

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | ⚠️ large surface, mostly spec-sized |
| Surgical changes | ✅ (static) MapCell default Temperature; snapshot classification present |
| No scope creep | ✅ vs P1/P2 list |
| Matches patterns | ✅ registry + paired tests |
| Spec-anchored outcome check | ⚠️ PWR-99 helper-only; sample categories incomplete |
| Per-layer Coverage Expectation | ✅ domain unit tests map 1:1 for most PWR |
| Every test maps to a spec requirement | ✅ Extraordinary suite maps to ACs / edges / Done-when |
| Documented guidelines followed | `rules/tests.md` paired-world / behavior naming |

---

## Edge Cases

- [x] Longest / duplicate prefix — `ExtraordinaryMechanicRegistryTests.cs:33-37`, longest-prefix test in file
- [x] Unregistered Invoke contract message (PWR-03) — `ExtraordinaryInvocationEngineTests.cs:40-42`
- [x] `Extraordinary.Enabled == false` on new mechanics — combat/fauna/flora/mind/passive/soul/bond/control
- [x] Persistent climate bag classified — `WorldState.cs:134-135` `[Canonical]`
- [x] Timeline labels for new kinds — `LivingEventPresentationCatalog.cs:50-52`

---

## Gate Check

- **Gate command** (user must run): see compact summary / commands below
- **Result**: ⛔ **DEFERRED TO USER** — Verifier did not execute `dotnet test` / `scripts/test.sh` / `npm test` on this iteration
- **Prior Verifier note**: iteration 0 saw runtime fails (luck.curse, flight+speed, MapCell snapshot, timeline labels). Static re-read shows fixes + regression tests present; **runtime confirmation is the user's gate**
- **Test count before/after**: not measured (no gate)
- **Skipped tests**: n/a (no run)

---

## Fix Plans (if issues found)

### Soft gaps (non-blocking for static review; optional strengthen)

1. **PWR-99** — Assert Natality conception outcome under non-zero multiplier (paired), not only `FertilityMultiplier` helper.
2. **Sample pack categories** — Add descriptors for senescence / `attribute.strength` / `attribute.perception` / `mind.read-memory` to match success-criteria parenthetical list.

No Blocker/Major static ❌ remaining from prior FAIL list (PWR-03/07/13/23/25/70/72/MapCell/sample count) under evidence-or-zero review.

---

## Requirement Traceability Update

Verifier does not edit `spec.md`. Suggested after **user gate PASS**: mark P1/P2 PWR-01..122 → Verified; keep PWR-40/41 Deferred. If user gate fails, keep Needs Fix on failing IDs.

---

## Summary

**Overall**: ❌ Not Ready — gate not run by user (code-review only)

**Spec-anchored check (static)**: ~93/95 P1+P2 ✅ · 2 ⚠️ (PWR-99, sample category completeness) · 0 static ❌ on prior FAIL IDs
**Sensor**: DEFERRED TO USER
**Gate**: DEFERRED TO USER

**What looks fixed on disk (vs iteration 0 FAIL)**: PWR-03 Invoke message; area remeasure; cost→transfer order; senescence Min; luck.curse two-part parse; gravity flight+dash compose; MapCell Temperature default + snapshot classification; 18-descriptor sample pack; timeline labels for new event kinds.

**Next steps**: User runs gate commands below; if green, Verifier can treat Ready; if red, route failures as Fix tasks (iteration 2/3).
