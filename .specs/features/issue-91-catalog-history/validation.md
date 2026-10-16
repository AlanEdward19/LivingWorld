# Validation: issue-91-catalog-history — PASS

Date: 2026-10-16. Verifier: independent sub-agent (author ≠ verifier).
Diff range: `9e0a003..304820b` (HEAD `304820b31780590374c7e6fed7fe113f50bff34c`).
Spec: `.specs/features/issue-91-catalog-history/spec.md`.
All 9 acceptance criteria match their specified outcomes. Ranked gaps: none.

## Evidence-or-zero acceptance check
Paths below use these prefixes:
U = `tests/LivingWorld.Tests.Unit/History/`;
I = `tests/LivingWorld.Tests.Integration/`;
L = `tests/LivingWorld.Tests.LongRunning/`.

| AC | Expected outcome | Assertion evidence | Result |
|---|---|---|---|
| 1: catalog creation | History enabled for all five periods | I/World/CatalogHistoryTests.cs:30 `Assert.True(host.Current.HistoryRules.Enabled)`; period cases :14–18 | PASS |
| 2: synchronous fact | One fact with exact tick/kind/payload/location/participants before sink | U/RuntimeHistoryTests.cs:33 `Assert.Single(world.Facts)`; :34–35 tuple equality in sink callback; no-sink path :75 | PASS |
| 3: disabled/irrelevant | No facts | U/RuntimeHistoryTests.cs:51 `Assert.Empty(world.Facts)`, cases :45–46 | PASS |
| 4: explicit producers/metadata | No second fact | U/RuntimeHistoryTests.cs:64 `Assert.Empty(world.Facts)`, five kinds :55–59 at threshold zero | PASS |
| 5: last witness | Report and canon entry after death | U/RuntimeHistoryTests.cs:75–78 `Assert.Single(...Starvation)`, `Assert.Single(...OriginFactId == fact.Id)`, `Assert.Contains(...CanonSlots, ...report.Id)` | PASS |
| 6: three years | Facts/reports/beliefs and anchored biography/chronicle claims | I/World/CatalogHistoryTests.cs:31–42 three-year run, nonempty Facts/reports/Claims and belief ReportId; anchor contract I/Narrative/NarrativeEndpointTests.cs:57, :87 `Assert.Contains(fact.Id.Value, ...Claims.SelectMany(c => c.EventIds))` | PASS |
| 7: causal consumer | Buy +40 in identical context; factual payload preserved | U/RuntimeHistoryTests.cs:76 exact victim payload; :80 hunger belief; :84 `Assert.Equal(40, Score(ctx) - Score(ctx with { RelevantBeliefs = [] }), precision: 8)` | PASS |
| 8: processes/seeds | Same hash across two processes; other seed differs | L/CatalogHistoryDeterminismTests.cs:12 `Assert.Equal(first, second)`; :15–16 facts/reports >0; :17 `Assert.NotEqual(parts[0], ...92...Split(';')[0])` | PASS |
| 9: cost | Five warmed alternating pairs, 1000 NPCs ×720 ticks, median ratio ≤1.03 | L/Performance/RuntimeHistoryCostTests.cs:15–37 measurement protocol; :44 `Assert.True(after / before <= 1.03, ...)` | PASS |

Anchor evidence is layered: new catalog tests prove real histories reach unchanged routes;
existing API tests assert exact fact IDs, and I/Narrative/ClaimAnchorValidatorIntegrationTests.cs:36
asserts `lookup.IsSuccess` for emitted anchors. No new route/error contract was introduced.
Cache edge cases: U/RuntimeHistoryTests.cs:100–108 assert same-size canon replacement,
unchanged canonical hash on read, snapshot reconstruction and isolation of another world.

## Independent execution
- `bash scripts/verify.sh`: exit 0; 2352 passed, 0 failed, 3 skipped, total 2355.
  Log: `/tmp/issue91-independent-gate.log`; build and format checks passed.
- `bash scripts/test.sh --project integration --filter 'CatalogHistoryTests|NarrativeEndpointTests|ClaimAnchorValidatorIntegrationTests'`:
  exit 0; 12 passed, 0 failed/skipped (`/tmp/issue91-independent-integration.log`).
- `bash scripts/test.sh --project longrunning --filter CatalogHistoryDeterminismTests`:
  exit 0; 1 passed (`/tmp/issue91-independent-determinism.log`).
- `LW_HISTORY_COST_OUT=/tmp/issue91-independent-cost-measurement.txt bash scripts/test.sh --project longrunning --filter RuntimeHistoryCostTests`:
  exit 0; 1 passed; run alone after mutation tests completed.
  Disabled median **0.786720**, enabled **0.781674 µs/NPC-tick**, ratio **0.993586 ≤1.03**.
  Log: `/tmp/issue91-independent-cost.log`; samples retained in measurement file.
- Existing full integration evidence inspected: 448 passed, 0 failures (`/tmp/issue91-integration.log`).

## Discrimination sensor
`git archive HEAD` extracted into `/tmp/issue91-independent.4b4ebH`; only scratch sources changed.
Each mutation ran `bash scripts/test.sh --filter RuntimeHistoryTests` in scratch.
Original source restored with fresh mtimes between mutants; no compile failures counted.

| Mutation | Production location | Actual failing assertion | Outcome |
|---|---|---|---|
| Disable central recording condition | src/LivingWorld.Simulation/Scheduling/TickContext.cs:24 | U/RuntimeHistoryTests.cs:33 `Assert.Single(world.Facts)` empty (3 failures, 7 passes) | KILLED |
| Remove starvation hunger seed | src/LivingWorld.Simulation/History/Distortion/DistortionEngine.cs:36 | U/RuntimeHistoryTests.cs:80 `Assert.Contains("hunger", ...)` absent (1 failure, 9 passes) | KILLED |
| Use canon count instead of list identity for cache | src/LivingWorld.Simulation/History/Queries/NpcBeliefQuery.cs:33 | U/RuntimeHistoryTests.cs:103 expected `0|hunger`, actual `0|food` (1 failure, 9 passes) | KILLED |

Logs: `/tmp/issue91-independent-mutant1.log`, `...-mutant2.log`, `...-mutant3.log`.
Sensor: 3 injected, 3 killed, 0 survived. Scratch deleted.
Real porcelain before/after matched byte-for-byte (`/tmp/issue91-verifier-baseline`,
`/tmp/issue91-verifier-after-sensor`), checked again before writing this report.

## Integrity, scope and limitations
- No test deleted or assertion relaxed. Unit case delta +10 (previous total 2345 inferred
  from unchanged suite plus added cases); integration +5; longrunning +2.
- Three skips predate the diff: PeriodEvolutionHorizonBaselineTests.ZZZ_record_baseline,
  HistoryIndexTests.Record_history_index_baseline and SignificanceCalculatorTests.Record_history_collapse_baseline.
  All are explicit baseline-recording helpers; normal baseline assertions remain enabled.
- Independently compared `/tmp/issue91-history-before.json` and `...-after.json` as event multisets:
  only +2 ReportConverted in seed11 and +1 in seed17; zero removals. Existing tick/kind/payload
  preserved. Comparison excludes EventId/CauseEventId, which may shift with added events.
- Code review: catalog defaults, central recording, digest deduplication, hunger consumer and
  derived cache all serve the spec. No new dependency, route, UI or architecture layer.
  Guidelines checked: rules/tests.md, rules/simulation-determinism.md, rules/living-world-cohesion.md.
- Performance result applies to the specified 1000-NPC/720-tick fixture; below-control noise
  is not a speedup claim. No client source changed; no Godot FPS claim or new UI UAT required.
- Original GitHub issue could not be refetched due network restrictions; scope checked against
  supplied original-issue expectations and approved spec. No unsupported completion claim.
- Clean PASS has no grounded lesson signal; no lessons file changed.
