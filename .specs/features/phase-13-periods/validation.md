# Fase 13 (Multiplos periodos) Validation

**Date**: 2026-08-06
**Spec**: `.specs/features/phase-13-periods/spec.md`
**Diff range**: `c0866ed..HEAD` (16 commits: T1–T12, T11 split into T11a/T11b)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status | Notes |
| --- | --- | --- |
| T1 | ✅ Done | `PeriodDynamicsLoader.cs` |
| T2 | ✅ Done | `PeriodDefinitionValidator.cs` |
| T3 | ✅ Done | `ScenarioLoaderV2.cs` |
| T4 | ✅ Done | `IPeriodTemplateRepository` + `SqlitePeriodTemplateRepository` + migration |
| T5 | ✅ Done | `PeriodsEndpoints.cs` (`POST/GET /periods`, `GET /periods/{id}`) |
| T6 | ✅ Done | `WorldStartEndpoints.cs` + `WorldStartService.cs` |
| T7 | ✅ Done | `docs/domain/period-authoring*.md` (4 files) |
| T8 | ✅ Done | `scenarios/periods/*.json` (5 files) + `ReferencePeriodTemplatesTests.cs` |
| T9 | ✅ Done | `PeriodArchitectureTests.cs`, `PeriodDeterminismTests.cs` |
| T10 | ✅ Done | `PeriodCausalTests.cs`, `PeriodEvolutionHorizonBaselineTests.cs` |
| T11a | ✅ Done | `SkillBias(int SkillId, ...)` in `PeriodDynamicsLoader.cs` |
| T11b | ✅ Done | `SkillType`/`SkillSet`/`SkillsRules` opened; `Values` bug fixed per task notes | tasks.md's own "Test Co-location Validation" table row for T11b marks status `✅ (não iniciada)` — a self-contradictory label (✅ but "not started") left over from drafting; the task body text confirms completion. Cosmetic doc inconsistency, not a functional gap. |
| T12 | ✅ Done | `GET /periods/{id}/catalog` in `PeriodsEndpoints.cs` |

All 12 tasks (13 counting the T11 split) show implementation artifacts and passing tests. See gaps below for functionality claimed by the spec but not actually reachable at runtime.

---

## Spec-Anchored Acceptance Criteria

### P1: Período como startpoint dinâmico

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: período carregado → startpoint de distribuições/pesos/restrições/gatilhos | `Dynamics.ProfessionBiases` becomes real sampling weight | `src/LivingWorld.Simulation/ScenarioLoaderV2.cs:28-33` (builds `ProfessionWeights` from `ProfessionBiases`) — `tests/.../PeriodCausalTests.cs:96-98` `Assert.True(seedsWithExpectedDirection >= 18)` | ✅ PASS (profession weight only — see AC2 gap for the rest of "regras"/"gatilhos") |
| AC2: ticks avançam → nascimento/fusão/divisão/desaparecimento de profissões/habilidades por regra do cenário | A tick-time system consumes `Dynamics.TransformationRules` and mutates the active profession/skill catalog | **no evidence** — `PeriodTransformationRule`/`TransformationRules` are referenced **only** inside `PeriodDynamicsLoader.cs` (parsing) and `PeriodDefinitionValidator.cs` (cross-reference check); `grep` across `src/` and `tests/` for `PeriodTransformationRule`/`TransformationSystem`/`ApplyTransformation` returns zero hits outside those two files. `ScenarioRunner.DefaultSystems()` (`src/LivingWorld.Simulation/ScenarioRunner.cs:39-60`) has no such system. No test advances ticks and observes a profession emerging/merging/splitting/disappearing. | ❌ GAP |
| AC3: transformação fora do contrato → erro determinístico com referência de campo/regra | `400`-style deterministic message naming field/rule | `src/LivingWorld.Simulation/Periods/PeriodDynamicsLoader.cs:181-192` (`ValidateCardinality`) + `PeriodDefinitionValidator.cs:65-77` (`ValidateProfessionReferences`) — `tests/.../PeriodDynamicsLoaderTests.cs:167-185` `Assert.Contains("Dynamics.TransformationRules[]:", result.Error)`, `tests/.../PeriodDefinitionValidatorTests.cs:158-174` `Assert.Contains("Dynamics.TransformationRules[]: ProfessionId 999", result.Error)` | ✅ PASS (registration-time rejection; there is no runtime transformation event to reject, per AC2 gap) |

### P1: Extensibilidade por dados

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: período novo → mundo válido sem editar `.cs` | 5 new period JSON files load and run without touching `src/` | `scenarios/periods/*.json` + `tests/.../ReferencePeriodTemplatesTests.cs:19-34` `Assert.True(result.IsSuccess)`, `Assert.True(world.Npcs.Count(n => n.IsAlive) > 0)` | ✅ PASS |
| AC2: testes de arquitetura → reprovar literais de período em Domain/Simulation | Banned-literal scan over `src/LivingWorld.Domain`+`Simulation` | `tests/.../PeriodArchitectureTests.cs:13-25` `Assert.True(offenders.Count == 0, ...)` | ✅ PASS (fixed 5-name blacklist, same pattern as `PopulationArchitectureTests`) |
| AC3: catálogo ativo consultado → só elementos derivados do período + evolução do mundo | endpoint returns ids derived from period **and from what the simulation evolved into** | `src/LivingWorld.Simulation/Periods/PeriodCatalog.cs:10-12` — `ProfessionIds`/`SkillIds` are derived **only** from the static `PeriodDefinition` (the registered template payload), never from a running/evolved `WorldState`. Confirmed by `PeriodsEndpointTests.cs:149-167`, which reads the catalog straight after registration, before any tick runs. | ⚠️ Spec-precision gap / partial — "derivados do período" ✅, "+ evolução do mundo" not evidenced (consistent with the AC2 gap above: there is no world-evolution mechanism to read from) |

### P1: Cadastro de período personalizado

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: `POST /periods` valida antes de persistir | validate-then-save order | `src/LivingWorld.Api/PeriodsEndpoints.cs:31-35` | ✅ PASS |
| AC2: válido → registra e disponibiliza como template | `201 Created` + listable via `GET /periods` | `PeriodsEndpointTests.cs:74-83` (`Assert.Equal(HttpStatusCode.Created, ...)`), `:111-121` (`Get_periods_lists_a_registered_period`) | ✅ PASS |
| AC3: inválido → rejeita com mensagem determinística de caminho/campo | `400` + field path in body | `PeriodsEndpointTests.cs:86-97` `Assert.Contains("Width:", body)` | ✅ PASS |
| AC4: período cadastrado → init por id com mesmo pipeline dos templates base | `POST /worlds/start` resolves template by id, same `ScenarioLoaderV2` pipeline | `src/LivingWorld.Api/WorldStartEndpoints.cs:20-27`, `src/LivingWorld.Simulation/Periods/WorldStartService.cs` — `WorldStartServiceTests.cs:60-70` `Assert.Equal(999UL, world.Seed)`, `Assert.Equal(100, world.Npcs.Count)` | ✅ PASS |

### P1: Documentação para IA externa

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: `period-authoring.md` contém contrato/schema/exemplos +/- /checklist | doc completeness | `docs/domain/period-authoring.md` (index) + `period-authoring-schema.md` (fields), `period-authoring-dynamics.md` (Dynamics block), `period-authoring-flow.md` (examples + checklist, not fully re-read line-by-line by this Verifier but confirmed present via `docs/domain/period-authoring.md:19-25` table of contents) | ✅ PASS (no test type per matrix — "build gate only"; evidence is document existence + cross-reference, not an assertion) |
| AC2: IA externa segue guia → payload compatível com rota sem transformação manual | doc examples match the exact `POST /periods` envelope | Not independently testable (no test type); the doc's schema matches the fields consumed by `PeriodDefinitionValidator`/`PeriodsEndpoints` (same field names cross-checked above) | ⚠️ Spec-precision gap (untestable claim, documentation-only evidence) |
| AC3: guia usável por humanos sem IA | — | same as AC2 | ⚠️ Spec-precision gap |
| — | **Accuracy concern** (not a separate AC, flagged here because it undermines AC1's "contrato canônico" claim) | `docs/domain/period-authoring.md:14-16` states Dynamics rules let professions "nascer, se fundir, se dividir ou desaparecer **ao longo da simulação**" — this overstates current behavior; per the AC2 gap above, `TransformationRules` are validated but never executed at any tick. `period-authoring-dynamics.md:36-38` is more honest for `TriggerTick` ("reserva de campo pra uso futuro"), but the top-level index doc is not. | ⚠️ Documentation-accuracy gap |

### P1: Determinismo e causalidade de vieses

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: mesmo período+seed → mesmo hash | hash equality | `tests/.../PeriodDeterminismTests.cs:14-22` `Assert.Equal(hashA, hashB)` | ✅ PASS |
| AC2: períodos distintos, mesma seed → hashes diferentes | hash inequality | `PeriodDeterminismTests.cs:25-38` `Assert.NotEqual(hashMedieval, hashFuturistic)` | ✅ PASS |
| AC3: viés testado com controle/tratamento, mesma seed, múltiplas seeds, direção esperada | control/treatment pair across N seeds, majority in expected direction | `PeriodCausalTests.cs:77-99` (20 seeds, `>=18/20` required), `Assert.True(seedsWithExpectedDirection >= 18, ...)` | ✅ PASS |

### P2: Pacotes de referência como regressão

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: pacotes de referência mantidos verdes | 5 reference periods pass | `ReferencePeriodTemplatesTests.cs:17-34` (Theory, 5 cases) + `tests/golden/world-hashes.json`, `tests/baselines/scale-sensor.json`, `tests/baselines/period-evolution-horizon.json` regenerated per T11b notes | ✅ PASS |
| AC2: período fora dos pacotes usa mesmo pipeline | non-reference period (`scenarios/default.json`) goes through `PeriodDefinitionValidator`/`ScenarioLoaderV2` same as reference periods | `PeriodDefinitionValidatorTests.cs` (uses `scenarios/default.json`, not `scenarios/periods/*`), `PeriodsEndpointTests.cs` (same) — both call the identical `Validate`/`LoadWorld` entry points used for reference periods | ✅ PASS |

### P1: Habilidade como catálogo aberto (added post-T10)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: período declara habilidade nova (id + nome opcional) → aceita sem alterar Domain/Simulation | id accepted openly; **name declaration also accepted** | id: `src/LivingWorld.Simulation/Periods/PeriodDynamicsLoader.cs:113-137` (`ParseSkillBiases`), `PeriodDynamicsLoaderTests.cs:47-49`. Name: **no evidence** — `SkillBias` (`PeriodDynamicsLoader.cs:20`) is `record SkillBias(int SkillId, double Weight)`, no `Name` field anywhere in the contract; `scenarios/periods/*.json` only use `"Name"` for `Settlements`, never for skill/profession declarations; no schema doc field for it. | ⚠️ Partial GAP — id half done, "nome opcional" half not implemented anywhere |
| AC2: testes de arquitetura reprovam nome de habilidade usado como literal de decisão (mesmo padrão de `PeriodArchitectureTests`/`PopulationArchitectureTests`) | a dedicated banned-literal architecture test for skill names | **no evidence** — grepped `tests/` for any skill-literal architecture test (pattern analogous to `PeriodArchitectureTests.BannedNames`/`PopulationArchitectureTests.BannedNames`); none exists. The only architecture tests in scope are period-id literals and population literals (pre-existing). | ❌ GAP |
| AC3: regra do motor que hoje depende de habilidade por identidade (ex.: tutoria por `Teaching`) → expressa como id declarado por regra de cenário, nunca enum fixo | `SkillsRules.TeachingSkill` replaces the `SkillType.Teaching` literal | `src/LivingWorld.Domain/Population/SkillsRules.cs:15` (`SkillType TeachingSkill` field), consumed at `src/LivingWorld.Simulation/Population/SkillTeachingSystem.cs:160` (`master.Skills.Get(_rules.TeachingSkill)`) — `tests/.../SkillTeachingSystemTests.cs:206-209` computes `masterTeaching` via `master.Skills.Get(new SkillType(6))` matching the scenario-declared id | ✅ PASS |

### P1: Leitura do catálogo ativo (added post-T10)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: catálogo consultado → expõe ids (e nomes, quando declarados) de profissão/habilidade | ids: yes; names: "quando declarados" | ids: `src/LivingWorld.Simulation/Periods/PeriodCatalog.cs:8-12`, `src/LivingWorld.Api/PeriodsEndpoints.cs:15-16` (`PeriodCatalogResponse`), `PeriodsEndpointTests.cs:149-167` `Assert.Equal([1,2], body.ProfessionIds)`, `Assert.Equal([7], body.SkillIds)`. Names: **no evidence** — same root cause as the "Habilidade" AC1 gap above: there is no field in the contract to ever declare a profession/skill name, so "quando declarados" is vacuously always false, not actually implemented. | ⚠️ Partial GAP — ids ✅, names never reachable |
| AC2: consulta por API reaproveita padrão de resposta de `GET /periods` | shared shape/convention | `PeriodCatalogResponse(PeriodId, Version, ...)` mirrors `PeriodSummaryResponse(PeriodId, Version, ...)` (`PeriodsEndpoints.cs:9,15-16`); `PeriodsEndpointTests.cs:159-167` | ✅ PASS |

**Status**: ❌ Gaps present (1 major functional gap — `TransformationRules` never applied at runtime — plus a real "name" contract gap repeated across two stories, plus a missing skill-literal architecture test)

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| --- | --- | --- | --- |
| 1 | `src/LivingWorld.Simulation/Periods/PeriodDynamicsLoader.cs:187` | `Merge` cardinality check weakened: `sources.Count < 2` → `sources.Count < 1` | ✅ Killed (`PeriodDynamicsLoaderTests.Transformation_rule_with_wrong_cardinality_is_rejected_naming_the_rule(kind: "Merge", sources: [1], targets: [3])` failed) |
| 2 | `src/LivingWorld.Domain/Population/PopulationCatalog.cs:45` | Weighted-roulette comparison flipped: `target < cumulative` → `target > cumulative` | ✅ Killed (`PeriodCausalTests.Declared_profession_bias_shifts_the_initial_population_toward_the_favored_profession_in_most_seeds` failed: 0/20 seeds vs. required 18/20) |
| 3 | `src/LivingWorld.Simulation/Periods/PeriodDefinitionValidator.cs:68` | Cross-reference check for `ProfessionBiases` disabled: `if (!catalog.IsValidProfession(...))` → `if (false && !catalog.IsValidProfession(...))` | ✅ Killed (`PeriodDefinitionValidatorTests.Profession_bias_referencing_id_outside_ProfessionIds_is_rejected` failed) |

**Sensor depth**: lightweight (3 targeted mutations)
**Result**: 3/3 killed — ✅ PASS

All mutations were reverted with `git checkout --` immediately after each run; `git status --short` / `git diff --stat` confirmed a clean tree before proceeding to the next mutation and at sensor completion.

---

## Code Quality

| Principle | Status | Notes |
| --- | --- | --- |
| Minimum code | ✅ | Loaders/validator follow the existing `*ScenarioLoader` manual-parse + `Result<T>` pattern; no new abstraction layers introduced beyond what T1/T2 needed |
| Surgical changes | ✅ | T11b's `SkillSet`/`SkillType` migration touched exactly the files needed (11 pre-existing test files + 6 src files), each change traceable to the enum→id-catalog requirement |
| No scope creep | ✅ | `CourtshipSystem.SkillFactor`'s `KnownSkillTypesForScoring` constant (`src/LivingWorld.Simulation/Population/CourtshipSystem.cs:336-343`) is explicitly marked `ponytail:` with a named ceiling (13 fixed ids) and an upgrade path, rather than silently making the score data-driven beyond what was asked |
| Matches patterns | ✅ | `PeriodCatalogResponse`/`PeriodDetailResponse` mirror `PeriodSummaryResponse`; `PeriodDynamicsLoader` mirrors `BehaviorScenarioLoader`'s manual-parse style |
| Spec-anchored outcome check | ⚠️ | Several ACs (see table above) have real behavior implemented, but a subset (TransformationRules runtime, skill-name contract, skill-literal architecture test) have **no** implementation despite being explicitly named in spec ACs — not just imprecise wording, actually absent |
| Per-layer Coverage Expectation met | ⚠️ | Domain/loader layer: 1:1 with what's implemented (well covered). Route layer: happy+edge+error present for `/periods`, `/periods/{id}`, `/periods/{id}/catalog`, `/worlds/start`. The gap is at the *design* layer (transformation rules were never wired to a runtime system), not a testing gap for the code that exists |
| No unclaimed tests | ✅ | Every test file maps to a task/AC (Periods/*, Population/Skill* for T11b) |
| Documented guidelines followed | ✅ | `rules/simulation-determinism.md` (determinism tests), `rules/llm-boundary.md` (no LLM runtime, doc-only authoring) — both satisfied |

---

## Edge Cases

- [ ] **"WHEN dois períodos definem aliases iguais para entidades semânticas diferentes THEN sistema SHALL rejeitar conflito no registro."** — NOT handled. There is no "alias" concept anywhere in the implemented contract (no Name field for profession/skill at all, see the two "name" gaps above), so there is nothing to conflict-check. No test exercises this.
- [ ] **"WHEN período personalizado omite regra obrigatória de transformação THEN sistema SHALL falhar no validador antes da criação do mundo."** — Ambiguous given the implementation: `TransformationRules` is entirely optional (empty list is valid) and nothing in `PeriodDefinitionValidator` treats any transformation rule as "obrigatória" for any period shape. If the intent was "if you removed a profession, you must also declare a migration rule," that policy doesn't exist. Effectively NOT handled (falls out of the AC2 gap above — there's no runtime consumer to make a rule "required").
- [ ] **"WHEN evolução remove profissão em uso THEN sistema SHALL aplicar política declarada de migração/reclassificação sem quebrar integridade referencial."** — NOT handled. Since profession removal-in-use never happens at runtime (no `TransformationRule` execution), there is no migration/reclassification policy implemented or tested.
- [x] **"WHEN startpoint tenta injetar ação fora do motor permitido THEN sistema SHALL rejeitar definição sem efeito parcial."** — Handled at validation time: `PeriodDefinitionValidator.Validate` short-circuits on the first loader failure and never returns a partial `PeriodDefinition` (`PeriodDefinitionValidator.cs:28-63`, each step `if (!result.IsSuccess) return Result<PeriodDefinition>.Fail(...)` before assembling anything). Confirmed by `PeriodDefinitionValidatorTests.cs:66-124` (missing-field tests never leave a partially-built object observable) and `ScenarioLoaderV2.LoadWorld` which never calls `PopulationSeeder`/`AddWorkplace`/`AddCity` unless `Validate` fully succeeded.

3 of 4 edge cases are not handled — all three trace back to the same root cause: `PeriodTransformationRule` (Emerge/Merge/Split/Disappear) is validated but never executed.

---

## Gate Check

- **Gate command**: `bash scripts/test.sh --filter "FullyQualifiedName~Periods"` (per Verifier task scope; this is a subset of the tasks.md "Quick" gate, narrowed to the feature's own test namespace)
- **Result**: 64 passed, 0 failed, 1 skipped
- **Skipped tests**: `PeriodEvolutionHorizonBaselineTests.ZZZ_record_baseline` — `[Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]`, the same manual-baseline-regeneration convention used elsewhere in the codebase (e.g. golden-hash `ZZZ_record_*` tests) — justified, not a hidden failure.
- **Failures**: none
- Full-repo `bash scripts/verify.sh` / `Category=Scenario` / 10-year/100-year tests were intentionally **not** run per this Verifier's scope (out of scope, per task instructions and per the already-flagged `LongRunScaleTests` note in tasks.md's T11b section, which this report does not re-litigate).

---

## Fix Plans (if issues found)

### Fix 1: `Dynamics.TransformationRules` are validated but never applied at runtime

- **Root cause**: `PeriodTransformationRule` is fully parsed and cross-reference-checked (`PeriodDynamicsLoader.cs`, `PeriodDefinitionValidator.cs`), but no `ISimulationSystem` reads `PeriodDynamicsData.TransformationRules` or `TriggerTick` during `WorldClock.Tick`. `ScenarioRunner.DefaultSystems()` has no such system, and `ScenarioLoaderV2.LoadWorld` discards `definition.Dynamics.TransformationRules` after validation (only `ProfessionBiases` is consumed, at `ScenarioLoaderV2.cs:28-33`).
- **Fix task**: Add a tick-time system (e.g. `PeriodTransformationSystem`) that, at each declared `TriggerTick`, applies `Emerge`/`Merge`/`Split`/`Disappear` to the active profession catalog and reassigns/reclassifies NPCs currently holding a removed profession per a declared migration policy (the "evolução remove profissão em uso" edge case). Wire it into `ScenarioRunner.DefaultSystems()` or an equivalent period-aware system list, and update `PeriodCatalog.From` to read from the evolved `WorldState` rather than only the static `PeriodDefinition` (closing the "Extensibilidade por dados" AC3 gap too).
- **Priority**: Major — this is the mechanism named in the feature's primary goal ("Profissões, habilidades e papéis sociais podem surgir, mudar e desaparecer em runtime") and in P1 story #1's AC2; without it, "período como startpoint dinâmico" only covers the initial weight, not runtime evolution.

### Fix 2: No "name" declaration for profession/skill anywhere in the contract

- **Root cause**: Two P1 ACs ("Habilidade como catálogo aberto" AC1, "Leitura do catálogo ativo" AC1) explicitly require an optional `Name` alongside the id for skill/profession declarations. `ProfessionBias`/`SkillBias` (`PeriodDynamicsLoader.cs:14,20`) and `PeriodCatalogResponse` (`PeriodsEndpoints.cs:15-16`) carry ids only. `scenarios/periods/*.json`'s only `"Name"` usage is for `Settlements`.
- **Fix task**: Add an optional `Name` field to the skill/profession declaration point in the `Dynamics`/`Population` block (parsed by `PeriodDynamicsLoader`/`PopulationScenarioLoader`), thread it through `PeriodCatalog.From` and `PeriodCatalogResponse`, and add the "duplicate alias for different semantic entities → reject" registration check (the currently-unhandled edge case).
- **Priority**: Minor/Major — the id-only mechanism works for everything the engine actually needs (AD-023/AD-025: engine never sees names), but the two ACs and the alias-conflict edge case explicitly promise a name channel that doesn't exist, so as written they're unmet.

### Fix 3: Missing skill-literal architecture test

- **Root cause**: "Habilidade como catálogo aberto" AC2 explicitly requires "mesmo padrão de `PeriodArchitectureTests`/`PopulationArchitectureTests`" — a banned-literal scan for skill names in `src/LivingWorld.Domain`/`Simulation`. No such test exists.
- **Fix task**: Add `SkillArchitectureTests` (or extend `PopulationArchitectureTests`) with a banned-name list for the old fixed skill names (e.g. the pre-Fase-13 13-value enum's names) and assert no `.cs` file under Domain/Simulation contains them as string literals, mirroring `PeriodArchitectureTests.cs:13-25`.
- **Priority**: Minor — this is cheap to add and closes an explicit, evidence-or-zero AC; low risk otherwise since `SkillType` is now a plain `int`-backed struct with no name literals in `src/` today (spot-checked, no regression currently exists — this is a missing *test*, not a missing *guarantee*, but per evidence-or-zero rules it counts as uncovered).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| PERIOD-01, 03 | Pending | ✅ Verified |
| PERIOD-02 | Pending | ❌ Needs Fix (Fix 1) |
| PERIOD-04, 05 | Pending | ✅ Verified |
| PERIOD-06 | Pending | ⚠️ Partial (Fix 1 — "+ evolução do mundo" half) |
| PERIOD-07..10 | Pending | ✅ Verified |
| PERIOD-11 | Pending | ✅ Verified |
| PERIOD-12, 13 | Pending | ⚠️ Spec-precision gap (untestable by nature) |
| PERIOD-14..16 | Pending | ✅ Verified |
| PERIOD-17, 18 | Pending | ✅ Verified |
| PERIOD-19 | Pending | ⚠️ Partial (Fix 2 — id done, name not) |
| PERIOD-20 | Pending | ❌ Needs Fix (Fix 3) |
| PERIOD-21 | Pending | ✅ Verified |
| PERIOD-22 | Pending | ⚠️ Partial (Fix 2 — ids done, names not) |
| PERIOD-23 | Pending | ✅ Verified |

Note: spec.md's Goals and Success Criteria checkboxes are all still unchecked (`- [ ]`) as of this diff — consistent with this report's findings that not everything claimed by the spec is actually implemented yet.

---

## Summary

**Overall**: ⚠️ Issues (not a clean PASS, but the core of the feature — dynamic startpoint via profession bias, persisted/validated templates, world bootstrap, determinism, reference-package regression, and the `SkillType`/`SkillSet` open-catalog migration — is solid and well-tested)

**Spec-anchored check**: 18/25 AC rows fully PASS, 4 partial/spec-precision gaps, 3 hard GAPs (no evidence)
**Sensor**: 3/3 mutations killed
**Gate**: 64 passed, 0 failed, 1 justified skip

**What works**: Profession-bias-driven sampling (causally proven across 20 seeds), full `PeriodDefinitionValidator` pipeline wired into `ScenarioLoaderV2`, persisted/versioned period templates with correct 200/400/409/404 API contract, `POST /worlds/start`, determinism (same-seed/same-hash and different-period/different-hash), 5 reference period packages green as regression baseline, and the `SkillType`/`SkillSet`/`SkillsRules` open-catalog migration (T11a/T11b) with the `Values`-serialization bug caught and fixed during the work itself.

**Issues found**:
1. `Dynamics.TransformationRules` (Emerge/Merge/Split/Disappear) are fully validated but never executed at any tick — the feature's headline goal ("profissões... podem surgir, mudar e desaparecer em runtime") is not implemented, only its input contract is. See Fix 1.
2. No profession/skill "name" field exists anywhere in the contract, despite two P1 ACs explicitly promising one ("id + nome opcional", "ids (e nomes, quando declarados)"). See Fix 2.
3. "Habilidade como catálogo aberto" AC2 (skill-literal architecture test) has zero test coverage, unlike its stated pattern-mate `PeriodArchitectureTests`/`PopulationArchitectureTests`. See Fix 3.
4. 3 of 4 spec Edge Cases are unhandled, all downstream of Issue 1.
5. `docs/domain/period-authoring.md`'s top-level description overstates what `Dynamics.TransformationRules` currently does ("ao longo da simulação") relative to the more honest per-block doc (`period-authoring-dynamics.md`).

**Next steps**: Route Fix 1 (runtime transformation system) as the priority fix task — it's the AC with the most spec weight (it's literally the feature's stated Goal #2) and the root cause of 3 of the 4 edge-case gaps. Fix 2 and Fix 3 are smaller, independent follow-ups.
