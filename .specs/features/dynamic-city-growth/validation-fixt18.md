# FixT18 Validation — dynamic-city-growth

**Date**: 2026-08-23  
**Diff**: `c671467..working-tree`  
**Verifier**: second independent sub-agent (author != verifier)  
**Source of truth**: `tasks.md:1110-1139` + ADR-0019  
**Verdict**: ✅ PASS for FixT18; the repository-wide gate remains blocked only by documented pre-existing causal tests.

## Evidence-or-zero re-check

| Required outcome | `file:line` + assertion | Result |
| --- | --- | --- |
| Adjacent daughter schedules once at exact `OrganizationTicks` | `SpatialSettlementFoundingSystemTests.cs:349-355` — two ticks, `Assert.Single`, exact `TargetTick`, payload and marker | ✅ |
| Fire-time geometry change cancels without merge and permits retry | `SpatialSettlementFoundingSystemTests.cs:374-384` — tombstone/marker null, two active; adjacency restored and a new marker/event asserted | ✅ |
| Buildings, workplaces, household/NPC and construction move | `SpatialSettlementFoundingSystemTests.cs:434-445` — exact owner IDs, mother queue contains project, daughter queue empty | ✅ |
| Stock and aggregate population conserve exact state | `SpatialSettlementFoundingSystemTests.cs:446-452` — mother totals `12` and `(3,30,120)`; daughter stock/pool/IDs empty; exact ID count | ✅ |
| Pending migration closes for absorbed origin and redirects from third city | `SpatialSettlementFoundingSystemTests.cs:437-440,457-459` — daughter-origin pending null; third-city origin retained and destination becomes mother before/after migration tick | ✅ |
| Daughter remains in canonical `Cities` as inactive tombstone | `SpatialSettlementFoundingSystemTests.cs:434-435,453-454` — exact alias, contained in `Cities`, excluded from `ActiveCities` | ✅ |
| Old IDs resolve through a merge chain to final active city | `SpatialSettlementFoundingSystemTests.cs:495-508` — leaf→middle→root and exactly one active root | ✅ |
| Tombstone disappears from visual projections | `SpatialSettlementFoundingSystemTests.cs:455-456` — city projection fails and global projection omits daughter | ✅ |
| Operational systems consume the tested active-city view | `SpatialSettlementFoundingSystemTests.cs:453-454,507-508` asserts the view; consumers use it at `CityGrowthSystem.cs:34`, `ConstructionSystem.cs:19`, `MigrationSystem.cs:50`, `SettlementFoundingSystem.cs:33`, `SpatialSettlementFoundingSystem.cs:30` | ✅ compositional |
| Canonical state survives snapshot before and after confirmation | `SpatialSettlementFoundingSystemTests.cs:477-491` — scheduled marker survives; hashes equal both rounds; tombstone alias, city counts and active count survive | ✅ |
| `CityMerged` records exact confirmation tick and IDs | `SpatialSettlementFoundingSystemTests.cs:460-463` — single event, exact kind/tick/payload | ✅ |

## Implementation correspondence

- Scheduling/revalidation/event: `SpatialSettlementFoundingSystem.cs:149-191`.
- Alias resolution and deterministic transfer loops: `WorldState.cs:594-626`.
- Projections use the active view: `CityProjector.cs:43-46`, `GlobalProjector.cs:41-58`, `LivingScopeState.cs:71-83,199`.
- Determinism rules hold: ordered causal loops; no machine clock, random GUID, unseeded RNG or parallel mutation introduced.

## Focused gate

- Command: `bash scripts/test.sh --filter "FullyQualifiedName~SpatialSettlementFoundingSystemTests"` via Git Bash.
- Result: .NET **16 passed, 0 failed, 0 skipped**; script-mandated web suite **413 passed, 0 failed**.
- Reported surrounding gates: build/lint, golden and composition passed.
- Full `verify.sh`: not green solely because existing `ScarcityPriceCausalTests` (5/10) and `FamineCausalChainTests` (1/10) remain documented in `STATE.md`; not attributed to FixT18.

## New discrimination sensors

All mutations ran in `C:\Users\Alan-\AppData\Local\Temp\livingworld-fixt18-sensor-20260823-b`, never in the real tree; the scratch copy was removed.

| Mutation | Expected discriminator | Result |
| --- | --- | --- |
| Drop daughter's aggregate `Count/WealthSum/HealthSum` during absorption | exact aggregate conservation at test line 448 | ✅ killed: 1/16 failed |
| Resolve `FindActiveCity` for only one alias hop | leaf→middle→root assertion at line 507 | ✅ killed: 1/16 failed |
| Redirect pending destination only when origin was the absorbed city | third-city destination assertion at line 440 | ✅ killed: 1/16 failed |

**Sensor**: 3 behavior-level mutants, 3 killed, 0 survived.

## Ranked gaps

None blocking. The prior evidence gaps for conservation, canonical retention, alias chains, snapshot round-trip, third-city migration, projections, confirmation tick and retry are closed by exact assertions.

## Summary

**Overall FixT18**: ✅ Verified PASS. No implementation defect or surviving mutant found.  
**Repository-wide readiness**: ⚠️ unchanged; only the separately documented pre-existing causal-test failures prevent a green `verify.sh`.
