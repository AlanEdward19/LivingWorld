# Handoff 2/4

### Histórico — gate hygiene (pré-16.3 Execute)
  scale gate só 1k; golden gate 100 ticks; Utility/Economy hash 1 mês; API sem
  `DisableParallelization` na collection (8 mutadores com fixture própria). Longos →
  `Category=Scenario`. Checkpoint anterior: `d0942bd`.
- **Gate hygiene (2026-08-25)**: conservação/invariantes longos no gate → **1 mês**
  (`30*24`h) com amostra diária; 1yr/10yr/100yr → `Category=Scenario`. Natality-hash
  sensor só em Scenario (≤90d não diverge no seed 42). Filtro focado: 20 passed ~3s.
- **API fixture hygiene (2026-08-25)**: `LivingWorldApiFactory` + collection de leitura
  (serial entre si; sem DisableParallelization de assembly). Mutadores: fixture própria.
- **Temperature omit / PERF-12 (2026-08-25)**: `MapCellJsonConverter` omite `Temperature`;
  golden + scale-sensor regravados em `d0942bd`. **PERF-12 implementado**: cache de
  fragmentos JSON canônicos por NPC + propriedades estáticas; `TouchCanonical` nos mutadores
  de `Npc`; `IncrementalHasher.MatchesCanonical` verde (1 ano).
- **Fase 16.1 Re-verify 1/3 (2026-08-25)**: AD-009 — só o usuário roda suíte completa.
  PWR-40/41 deferred. 16.2 blocked. WIP 16.1 em `d0942bd`; round-2 gate ainda uncommitted.
- **Round-4 post-ship fixes (2026-08-23) — wall-marker regression + migration hysteresis**: user
  reported cities rendering as tiny circular markers instead of walled footprints, right after the
  round-3 fixes above landed. Two real root causes, one a direct regression from round-3's own fix,
  one a pre-existing latent instability in a much older system that round-3 simply made reachable
  for the first time:
  1. **Regression from `433a219` (FixT13)**: `CityBoundsResolver.ClampSideAgainstOtherCities`'s
     shrink loop floored the candidate side at `1` (1x1) instead of the existing `MinSize` (3) that
     every other sizing path in the file already respects (`SideFor`). A 1x1/2x2 result flips the
     frontend's own size check (`renderer.ts`/`hitTest.ts`: `size.w > 1 || size.h > 1`) from "draw
     walled footprint" to "draw as a small marker" — exactly the reported circles. Fix: floor
     changed to `MinSize`; if even `MinSize` can't reach the required gap (two cities founded
     pathologically close), the gap is allowed to fall short rather than ever violating the map's
     own minimum-viable-city-size invariant (same "decline over degenerate result" philosophy as
     AD-007/`BuildingPlacementResolver`, adapted since this resolver has no `null` to return).
     Commit `015cabf` — `fix(cities): never shrink a city below its minimum viable size when
     avoiding a neighbor`.
  2. **Pre-existing latent instability, exposed (not introduced) by `dynamic-city-growth`**:
     `MigrationSystem` (Fase 8, T12 — predates this feature entirely) scored households against
     every city daily using live-recomputed `EmploymentLevel`/`FoodLevel` and relocated on a strict
     `score > bestScore`, no margin. Two cities close together is now a real, supported state
     (round-3's own fixes), and that made the daily re-scoring self-reinforcingly oscillate:
     relocating a household shifts the very population/food counts that feed tomorrow's score for
     the city it left. Fix: candidate must now beat the current score by `HysteresisMargin` (15%
     relative — `score > bestScore * 1.15`); relative because `ScoreOf`'s scale is weight-dependent
     (`CityRules` weights aren't normalized to sum to 1), so a fixed additive margin would be
     meaningless at one scale and overpowering at another. No cooldown/timer added — the margin
     alone stops the oscillation (proven by a 5-tick stabilization test) and every other rule in
     the file is already timer-free. Commit `703ffaa` — `fix(cities): add migration hysteresis so
     households don't flip-flop between close cities`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 236
    passed, 0 failed (232 baseline + 1 new test for fix 1 + 3 new tests for fix 2).
  - Both discrimination-checked: temporarily reverting each fix's core change made its own new
    test(s) fail (mutants killed) before the real fix was restored and committed.
  - Detail in `.specs/features/dynamic-city-growth/tasks.md` (FixT15, FixT16).

- **Round-3 post-ship fixes (2026-08-23) — population-box cross-city clamp + household poaching**:
  user reported cities' walls touching again AND population "jumping" between two adjacent
  cities, both after FixT8-FixT12 already landed. Two more real root causes, both in
  `dynamic-city-growth`'s own code, both committed:
  1. `CityBoundsResolver.Resolve`'s `otherCityBoundsToAvoid` clamp (FixT8) only ever applied to
     the MERGED overflow boxes — the base population-only `populationBox` was returned unclamped
     whenever there were no owned boxes to merge. Since households don't have real building
     positions yet, this population box is what's actually rendered/dominant on the map, so two
     cities could grow into each other purely from population increase. Fix: new private
     `ClampSideAgainstOtherCities` helper shrinks `populationBox` the same way `ClampOrigin`
     already shrinks it for the map edge. Two existing tests whose assertions assumed the
     population box was immune to cross-city clamping were rewritten against the production code
     path's own (now correctly shrunk) bounds — same precedent as FixT8's own test rewrite.
     Commit `433a219` — `fix(cities): clamp the population-derived bounds box against neighboring
     cities too`.
  2. `SpatialSettlementFoundingSystem.HandleEvent`'s household-reassignment loop had no check that
     a household belonged to the founding cluster's own mother city — it swept up ANY household
     whose head stood inside `clusterBounds`, including households already properly settled in a
     NEIGHBORING city. Combined with fix 1 above (cities can now legitimately sit closer
     together), this repeatedly poached already-settled households back and forth every monthly
     re-scan. Fix: `if (household.City != motherCityId) continue;` guard before the existing
     geometric check. Commit `499d6d1` — `fix(cities): stop spatial founding from poaching
     households that already belong elsewhere`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 232
    passed, 0 failed (230 baseline + 2 new tests).
  - Detail in `.specs/features/dynamic-city-growth/tasks.md` (FixT13, FixT14).

- **Ghost-town fixes (2026-08-23)** — usuário relatou que cidades recém-fundadas continuam
  virando cidade-fantasma (NPCs "vão trabalhar" numa cidade vizinha e voltam). Duas causas raiz
  reais, sem relação uma com a outra, ambas commitadas:
  1. `EmploymentSystem.Tick`/`VacancyIndex` contratavam o primeiro workplace com vaga livre NO
     MUNDO INTEIRO, sem checar `Workplace`/`Npc.City` — causa DIRETA do relato ("vai trabalhar,
     volta"). Fix: `Workplace` ganhou `CityId City` (setado por `ConstructionSystem` na
     construção), `VacancyIndex.FirstWorkplaceWithVacancy` ganhou overload escopado por cidade,
     `EmploymentSystem` usa o overload. Commit `27e9fcc` —
     `fix(economy): scope hiring to the NPC's own city`.
  2. `SpatialSettlementFoundingSystem.HandleEvent`'s reassignment de household pra cidade nova
     testava `household.Location` — campo gravado uma única vez na criação e nunca atualizado,
     então quase nunca refletia onde a família de fato morava no momento da fundação. Fix: troca
     o critério pra `Npc.CurrentLocation` do chefe do household (mesmo padrão já usado 2 linhas
     acima, pro limiar de concentração). `Household.cs` intocado (mutação compartilhada com o
     trabalho de relocation em progresso, não commitado). Commit `aeeb29a` —
     `fix(cities): reassign households by current NPC location, not stale household coordinates`.
  - Gate: `bash scripts/test.sh --filter "Category!=Scenario&(FullyQualifiedName~Cities|FullyQualifiedName~Economy)"`
    — 296 passed, 2 failed (`ScarcityPriceCausalTests`/`FamineCausalChainTests`, pré-existentes e
    já documentados abaixo, confirmados falhando idênticos antes destes 2 fixes).
  - Detalhe completo do Fix 2 em `.specs/features/dynamic-city-growth/tasks.md` (FixT12). Fix 1
    não pertence a nenhuma feature commitada nesta pasta (é `EmploymentSystem`, Fase 5).
