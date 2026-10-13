# Estate inheritance verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 34444ed..working tree (uncommitted)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

No faults injected: profile `light` does not run fault injection (cannot catch a test that passes under a wrong implementation).

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | wallet to spouse, else children (remainder lowest id), else parents, else siblings; balance unchanged | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter EstateSystemTests` exit 0, 8/8 listed passed | `EstateSystemTests.cs:71` - `Assert.Equal(1000, spouse.Wallet.Amount)`; `:92` - `Assert.Equal(334, a.Wallet.Amount)`; `:109` - `Assert.Equal(300, mother.Wallet.Amount)`; `:120` - `Assert.Equal(500, sib2.Wallet.Amount)` | PASS |
| C2 | no heir: city treasury, or stays with the dead when no city | same run | `EstateSystemTests.cs:136` - `Assert.Equal(400, city.Treasury.Amount)`; `:144` - `Assert.Equal(77, hermit.Wallet.Amount)` | PASS |
| C3 | daily identity wallets+treasuries == initial+minted-destroyed over a year, at least one death | Unit `Money_stays_balanced` passed; LongRunning `MoneyConservationTests` and `DeterminismTwoProcessTests` exit 0, 8 passed | `EstateSystemTests.cs:219` - `Assert.Equal(before + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, TotalMoney(world))`; `:222` death guard | PASS |
| C4 | pantry of an emptied household moves to the first heir's household, no ResourceLost | `EstateSystemTests` run | `EstateSystemTests.cs:164` - `Assert.Equal(40, childHome.Stock.GetValueOrDefault(...))` | PASS |
| C5 | heir gets inheritance relationship (trust, affection up) | `EstateSystemTests` run | `EstateSystemTests.cs:205` - `Assert.True(relationship.Trust > 0)` (Affection asserted on the next line) | PASS |
| C6 | owner workplace to lowest-id present heir, else lowest-id heir, else nobody | `EstateSystemTests` run | `EstateSystemTests.cs:184` - `Assert.Equal(present.Id, shop.Owner)`; `:188` absent heir; `:191` - `Assert.Null(shop.Owner)` | PASS |
| C7 | us/NPC-tick <=103%, alloc/tick <=102%, hash equal in two processes | `MeasureHarness` x2 (HEAD scratch worktree vs tree, removed after), `DeterminismTwoProcessTests` exit 0 | see Performance | PASS (noise caveat) |

## Coverage

Light profile: the join is not recomputed. Author table (door 1: 5 members; workplace outcomes: 3) read against tests above: every member has an assertion. No set rows beyond these.

## Performance (C7, scale1000, harness `LW_MEASURE_OUT`)

| Run | HEAD (before) us/NPC-tick | tree (after) us/NPC-tick | before alloc/tick | after alloc/tick |
| --- | --- | --- | --- | --- |
| 1 | 1.051 | 1.076 (102.4%) | 532029 | 532133 (100.02%) |
| 2 | 0.975 | 1.005 (103.1%) | 532048 | 532056 (100.00%) |

Mean 1.013 -> 1.041 (+2.7%). Run-to-run spread of identical code is 7.5%, larger than the 3% bar, so time is within noise but not proven under 103% with margin. Alloc +0.01%. snapshotBytesPerAlive 10859 -> 10860.

## Gaps and notes (non-blocking)

- `HeirsOf` scans `world.Npcs` (O(N)) twice per death with a non-zero estate (children, siblings), plus `OwnedWorkplaces` scans all workplaces. Early-out at EstateSystem.cs:25 skips poor dead NPCs. Fine at 1000; ponytail: index by parent if N reaches 100k.
- Event payload `wallet` (EstateSystem.cs:44) is logged even when nothing moved (no heir and no city): observability reads as settled while the money stayed.
- `PayHeirs` ignores `TryDebitWallet` (EstateSystem.cs:66); safe because `wallet` was read from the same single-threaded state and is > 0, so conservation holds. `world` parameter is unused there.
- Determinism: heirs ordered by `OrderBy(Id)`; stock moves iterate a Dictionary, but insertion order is itself deterministic and two-process test passed. Heir that shares the dead's household cannot be the stock target (`lastInHousehold` requires sole member).
- Heirs without a household: stock is not moved and falls to the later redistribution as before.
- Workplace.Owner: `JsonIgnore(WhenWritingNull)` keeps bytes for ownerless worlds (+1 byte/alive measured is noise from changed world state). Snapshot is a write/hash tree with no restore path in `src/`, so no deserialization of Owner is needed; an owner-set round trip was not run. No HTTP surface, no OpenAPI change.
- Deviation 4 (goldens and floors re-recorded) is documented in checks.md as AD-094.

## Gate

Unit `EstateSystemTests` 8 passed, 0 failed; LongRunning `MoneyConservationTests`+`DeterminismTwoProcessTests` 8 passed, 0 failed; `MeasureHarness` 1 passed (x4). `verify.sh` not run by instruction.
