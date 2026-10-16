# PR #100 — merge verification

**Verdict**: PASS
**Profile**: standard
**Round**: merge — scoped, independent sub-agent (author != verifier), 2026-10-16
**Verified at**: f315563a692ac409d2a947c710756233939bedea
**Parents**: 64e03022d0bc8674b00cff0ecd9844571cd6abbf (#65/#67), bee5b1ed5874194cae2ee342588390ac52ed410d (develop/#47).

## Scope and binding sources

Verified at HEAD: diffs against BOTH parents, not only the combined merge diff.
Opened PR #100 and issues #65/#67 with gh; plans/checks for lodging-and-rent and saved-worlds,
object-catalog plan, AGENTS.md, rules/tests.md, database-entities.md and simulation-determinism.md,
plus tlc-spec-lean/references/verify.md including re-verification rules.
No external visual binding source; this round checks the integrated behavior and affected contracts.

## Own proof runs

All commands below ran at HEAD, outside the sandbox as required for this worktree/Godot.
Logs: /private/tmp/pr100-{verify,integration,client,catalog-proof}.log.

| Command | Result |
| --- | --- |
| bash scripts/verify.sh | exit 0; verify: OK; 2342 passed, 3 existing recording skips |
| bash scripts/test.sh --project integration | exit 0; 443 passed, 0 failed |
| bash scripts/client-test.sh | exit 0; 270 passed, 0 failed; each Godot test printed by name |
| bash scripts/test.sh --project integration --filter Loading_a_saved_world_preserves_its_server_object_catalog | exit 0; exactly 1 passed |

## Checks — verified at HEAD

Paths abbreviated GD=client/godot/tests/, IT=tests/LivingWorld.Tests.Integration/, U=tests/LivingWorld.Tests.Unit/.

| Check | Located assertion / independent evidence | Result |
| --- | --- | --- |
| Server catalog and variation survive furniture merge | GD/test_object_catalog.gd:211 `eq(...albedo_color, Color("224466").lightened(0.1), ...)`; :212 `eq(beds, units, ...)`; :213 lamps 1/0 for units 2/5 | PASS |
| Fallback without objects and missing id | GD/test_object_catalog.gd:77 `eq(view.furniture.get_child_count(), view.catalog.furniture_for(4).size(), ...)`; :85 expected 1; both named tests ran | PASS |
| No artificial twelve-bed ceiling | GD/test_asset_catalog.gd:162 `eq(beds, 18, ...)`; named test ran | PASS |
| All storeys and visual cutoff | GD/test_object_catalog.gd:216 `eq(upper.get_child_count(), 2, ...)`; :219 `check(not bed.is_visible_in_tree(), ...)`; :222 `check(bed.is_visible_in_tree(), ...)` | PASS |
| Catalog mesh shared between floors | GD/test_object_catalog.gd:208 `check(piece.mesh == bed_mesh, ...)` | PASS |
| Saved world with object catalog restores | IT/World/SavedWorldEndpointsTests.cs:58 `Assert.NotNull(host.Current.ObjectCatalog)`; :62 HTTP OK; :63 `Assert.Equal(before, WorldSnapshot.CanonicalHash(host.Current))` | PASS |
| Snapshot/hash carries catalog | U/Objects/ObjectCatalogTests.cs:146 count equal, :147 canonical hash equal; U/Snapshot/PositionalSnapshotTests.cs:61 requires every persistable property in encoded tree, fixture includes catalog at :54 | PASS |
| #65 behavior preserved | Diff first-parent→HEAD changes no Population implementation, family baseline or golden hashes; FamilyDiversityCalibrationTests diff is only ADR comment | PASS |
| State and ADR identity | ADR-0034 catalog, ADR-0035 saved worlds, ADR-0036 diversity; index, plan, roadmap, decisions-log and STATE agree; ADR-0036:46–47 explicitly preserves historical report numbering | PASS |

## Coverage and test policy — verified at HEAD

Recomputed integrated placement branches from interior_view.gd:75–121: objects absent/present,
server id present/absent, ground space available/full, upper floor needed/unneeded, cutoff 0/1.
All map to the located proofs above; no unproven merge branch.
Saved-world route statuses remain 200; 200/404; 204/404/409; create 200/400/409.
HTTP implementations unchanged by this merge; full integration rerun plus the new catalog boundary proof.
Observable decisions retain HTTP and local proofs; causal/floor changes have independent mutations below.
Swept existing: API policy unchanged; serialized save/load and outside-tick persistence implementations
unchanged versus first parent and covered by the full integration run. No new authorization decision.

## Faults injected — verified at HEAD

Isolated detached scratch /private/tmp/pr100-merge-mutant; real tree porcelain empty before/after.
Each counted run compiled/parsed; failures below are assertions, not compiler or script errors.

| Mutation | Narrow proof and assertion failure | Killed |
| --- | --- | --- |
| interior_view.gd:75 ignores objects (`var objects = null`) | test_object_catalog::test_server_objects_and_lodging_beds_share_the_available_floors; expected 2 server beds, actual 0; 0 passed/1 failed | yes |
| interior_view.gd:128 parents catalog objects under ground Furniture | same combined test; upper child count expected 2, actual 0; 0 passed/1 failed; no SCRIPT ERROR | yes |
| WorldSnapshot rehydrates with null catalog | Loading_a_saved_world_preserves_its_server_object_catalog; SavedWorldEndpointsTests.cs:62 Assert.Equal expected OK, actual InternalServerError; 0 passed/1 failed | yes |

Logs: /private/tmp/pr100-mutant-{catalog-narrow,floor-narrow,persistence}.log.
Auxiliary snapshot-only experiment failed by NullReferenceException; not counted as an assertion kill.
Scratch removed; only this new report was written in the real tree. No implementation/test/report edits.

## Explicit carry and performance

Unchanged lodging/backend/menu checks and causal mutations carried from 18953eb1e930db62b3797c11148e85a2d7849a37,
reports lodging-and-rent/verification.md and saved-worlds/verification.md, round 2; their own prior
performance provenance is 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3. Fresh gates above replace old totals.
Genetic long-run criteria/mutations carried from abbefa5dc899315b6bf5ecb87cdb96f36e58621b,
family-diversity-calibration/verification.md; unchanged code/baseline, not silently re-verified.
Untouched #47 loader/factory/projection decisions and tick-cost evidence carried from 5a0764734067e214976dadd7887a7d2f106b1e42,
object-catalog/verification.md; affected placement/persistence proofs freshly verified above.
Own client-stress.sh observations: first parent 74.0/74.0/74.7 FPS; merge 73.6/75.6/75.2 FPS.
Brief overlap between arms means these are observational, not a new strict serial performance comparison.
Logs /private/tmp/pr100-stress{,-parent}.log; previous serial performance evidence remains explicitly carried.
No actionable implementation regression found. PR description still uses historical ADR-0034 for #65;
orchestrator prepared the corrected body for publication with the push. STATE totals updated by orchestrator after gates; no src/test change.
