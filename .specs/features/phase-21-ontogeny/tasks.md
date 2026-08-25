# Fase 21 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-21-ontogeny/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 4/6/7 (xUnit, mundo
> controle/tratado, determinismo por seed, guard de reflexão de cobertura total já usado em
> `ActionCatalogTests`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`MilestoneDefinition`, `MilestoneRequirement`, `DevelopmentState`, `OntogenyRules`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Ontogeny/**` (novo) | `dotnet test --filter "FullyQualifiedName~Ontogeny"` |
| `ActionCatalog.RequiredMilestone` + guard | unit + enumeração por reflexão | 1:1 a ONT-01..04 (clone de `ActionCatalogTests`) | `tests/LivingWorld.Tests/Behavior/MilestoneCoverageGuardTests.cs` (novo) | `dotnet test --filter "FullyQualifiedName~MilestoneCoverageGuard"` |
| `ExposureAccumulator` | unit + par negligência | 1:1 a ONT-10..12, 18/20 seeds | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| `MilestoneProgressSystem`/`WindowClosureSystem` | unit + 3 braços pareados | 1:1 a ONT-20..22, 18/20 seeds | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| `MilestoneRegressionSystem` | unit + auditoria 10/100 anos | 1:1 a ONT-30..33 | `tests/LivingWorld.Tests/Ontogeny/**` (gate) + nightly | mesmo comando |
| Predisposição (`RateGene` integração) | unit + par genes idênticos/diferentes | 1:1 a ONT-40..42, 20/20 seeds nos 2 sentidos | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| `LanguageFluencyResolver` | unit + par órfão | 1:1 a ONT-50..52, 20/20 seeds | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| Canal ambiental (integração) | unit | 1:1 a ONT-60..61 — reusa suíte existente da Fase 7 | `tests/LivingWorld.Tests/Population/HeredityServiceTests.cs` (existente, verificar cobertura) | `dotnet test --filter "FullyQualifiedName~Heredity"` |
| Agregado/materialização | unit + round-trip resample | 1:1 a ONT-70..72 | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| `MilestoneEligibilityFilter` | unit + custo | 1:1 a ONT-80..82 | `tests/LivingWorld.Tests/Behavior/**` | mesmo comando |
| `OntogenyLifecycleGate` | unit + custo por tick | 1:1 a ONT-90..92 | `tests/LivingWorld.Tests/Ontogeny/**` | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `Population*`/`Behavior*`/`Family*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Ontogeny) | Yes | Mundo próprio por teste | Fase 4/6/7 |
| par base/tratamento (negligência, janela, idioma) | Yes | Mundo controle/tratado por teste (`PairedScenarioTests.cs`) | Fase 7 |
| enumeração por reflexão | Yes | Mesmo padrão de `ActionCatalogTests` | Fase 4 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |
| nightly (100 anos, regressão sem causa) | Não roda no gate padrão | Job separado | Fase 21 spec |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Ontogeny"` |
| Full (integração) | Após tasks que tocam `Behavior`/`Population` | `dotnet test --filter "Category!=Scenario&(FullyQualifiedName~Ontogeny\|FullyQualifiedName~Behavior)"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Catálogo de ações + filtro (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Exposição (Parallel OK, depende de Phase 1)

```
T3 → T6
```

### Phase 4: Progresso e janela (depende de Phase 2, 3)

```
T5, T6 → T7 → T8
```

### Phase 5: Regressão (depende de Phase 4)

```
T8 → T9
```

### Phase 6: Idioma (depende de Phase 3)

```
T6 → T10
```

### Phase 7: Agregado/materialização (depende de Phase 4)

```
T8 → T11
```

### Phase 8: Ciclo de vida do sistema (última — depende de tudo)

```
T9, T10, T11 → T12
```

---

## Task Breakdown

### T1: `DevelopmentAxis`, `MilestoneDefinition`, `MilestoneRequirement`

**What**: Enum de 6 eixos + records de domínio.
**Where**: `src/LivingWorld.Domain/Ontogeny/DevelopmentAxis.cs` (novo)
**Depends on**: None
**Reuses**: mesmo padrão de dado-de-cenário já usado por `ActionCatalog`
**Requirement**: ONT-01

**Done when**:
- [ ] `MilestoneDefinition` sem `RequiredMilestoneIds` (marco raiz) é caso válido

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add DevelopmentAxis and MilestoneDefinition`

---

### T2: `DevelopmentState`, `OntogenyRules`

**What**: Estado por NPC (`Progress`/`Ceiling` por eixo) + regra de cenário.
**Where**: `src/LivingWorld.Domain/Ontogeny/DevelopmentState.cs` (novo),
`src/LivingWorld.Domain/Ontogeny/OntogenyRules.cs` (novo)
**Depends on**: T1
**Reuses**: nenhum
**Requirement**: (suporta ONT-20..22)

**Done when**:
- [ ] Todo NPC recém-criado tem `Progress` inicial 0 em todos os eixos, `Ceiling` inicial 1.0

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add DevelopmentState and OntogenyRules`

---

### T3: `WorldEventKind` — 3 valores novos

**What**: `MilestoneWindowClosed`, `MilestoneRegressed`, `MilestoneAcquired`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add ontogeny WorldEventKind values`

---

### T4: `ActionCatalog.RequiredMilestone` + guard de cobertura

**What**: Dicionário aditivo `ActionType -> MilestoneRequirement`, validado em `Create()` com o
mesmo padrão de `MaxDurationHours`.
**Where**: `src/LivingWorld.Domain/Behavior/ActionCatalog.cs` (modificado, aditivo)
**Depends on**: T3
**Reuses**: exato padrão de validação já usado por `MaxDurationHours`
**Requirement**: ONT-02, ONT-03

**Done when**:
- [ ] `Create()` falha nomeando a ação sem `RequiredMilestone` declarado (clone de `Create_fails_naming_the_action_missing_a_declared_duration`)
- [ ] Enumeração por reflexão de 100% dos `ActionType` cobre o dicionário

**Tests**: unit + enumeração por reflexão
**Gate**: quick
**Commit**: `feat(behavior): add RequiredMilestone to ActionCatalog with completeness guard`

---

### T5: `MilestoneEligibilityFilter`

**What**: Filtra `AllActions` ANTES de `SelectByUtility` iterar, removendo ações cujo limiar
não foi atingido.
**Where**: `src/LivingWorld.Simulation/Behavior/MilestoneEligibilityFilter.cs` (novo), hook em
`BehaviorDecisionSystem.SelectByUtility` (modificado, aditivo — chamada ao filtro antes do loop)
**Depends on**: T4
**Reuses**: `BehaviorDecisionSystem`/`AllActions` (Fase 4), sem modificar a lógica de pontuação
**Requirement**: ONT-04, ONT-80, ONT-81, ONT-82

**Done when**:
- [ ] Recém-nascido só tem ações de limiar 0 no conjunto candidato
- [ ] Chorar compete normalmente via `Deficit(fome)`, nenhum fallback forçado
- [ ] Custo de montar candidatos proporcional ao filtro, não ao catálogo completo (medido)

**Tests**: unit + custo
**Gate**: quick
**Commit**: `feat(behavior): filter action candidates by milestone eligibility before utility scoring`

---

### T6: `ExposureAccumulator`

**What**: Soma exposição por eixo a partir de ações de rotina de adultos do `Household.Members`.
**Where**: `src/LivingWorld.Simulation/Ontogeny/ExposureAccumulator.cs` (novo)
**Depends on**: T3
**Reuses**: `Household.Members`/ações de rotina existentes (Fase 4/7), nenhum medidor novo
**Requirement**: ONT-10, ONT-11, ONT-12

**Done when**:
- [ ] Sem interação de cuidador, exposição do tick é 0 (ou piso do cenário)
- [ ] Par negligência: criança negligenciada atinge menos marcos, 18/20 seeds

**Tests**: unit + par base/tratamento
**Gate**: quick
**Commit**: `feat(ontogeny): accumulate exposure from existing household routine actions`

---

### T7: `MilestoneProgressSystem`

**What**: Dificuldade multi-fator (idade+exposição+`RateGene`), rolagem `Resolver.Resolve` perfil
`Agregado`, aplica delta de progresso.
**Where**: `src/LivingWorld.Simulation/Ontogeny/MilestoneProgressSystem.cs` (novo)
**Depends on**: T5, T6
**Reuses**: `Resolver.Resolve`/`VarianceProfileCatalog.Get("Agregado")` (ADR-0011), `Npc.RateGene` (Fase 6, mesmo padrão de `SkillPracticeSystem`)
**Requirement**: ONT-20

**Done when**:
- [ ] Rolagem usa perfil `Agregado` (confirmado sem crítico), dificuldade documentada como função só dos 3 fatores declarados

**Tests**: unit
**Gate**: quick
**Commit**: `feat(ontogeny): implement milestone progress via Agregado-profile resolution`

---

### T8: `WindowClosureSystem` — teto permanente e recuperação parcial

**What**: Janela fechada sem exposição mínima reduz `Ceiling` permanentemente; exposição tardia
recupera parte via `LateExposureRecoveryFactor < 1.0`.
**Where**: `src/LivingWorld.Simulation/Ontogeny/WindowClosureSystem.cs` (novo)
**Depends on**: T7
**Reuses**: `MilestoneProgressSystem` (T7)
**Requirement**: ONT-21, ONT-22

**Done when**:
- [ ] Teto reduzido nunca retorna ao original por exposição futura
- [ ] 3 braços pareados (prazo/tardia/nenhuma): ordem estrita prazo>tardia>nenhuma, 18/20 seeds; tardia nunca alcança prazo (garantido por construção via `LateExposureRecoveryFactor < 1.0`)

**Tests**: unit + 3 braços pareados
**Gate**: quick
**Commit**: `feat(ontogeny): implement permanent ceiling reduction with bounded late recovery`

---

### T9: `MilestoneRegressionSystem`

**What**: Delta negativo por trauma/doença; recente sem piso forte, consolidado com piso; evento
com causa nomeada no mesmo tick.
**Where**: `src/LivingWorld.Simulation/Ontogeny/MilestoneRegressionSystem.cs` (novo)
**Depends on**: T8
**Reuses**: `WorldEventKind.MilestoneRegressed` (T3)
**Requirement**: ONT-30, ONT-31, ONT-32, ONT-33

**Done when**:
- [ ] Toda regressão gera evento no mesmo tick com causa nomeada
- [ ] Marco recente cai mais que marco consolidado sob o mesmo trauma
- [ ] Auditoria de 10 anos (gate) / 100 anos (nightly) não encontra queda sem evento

**Tests**: unit + auditoria 10/100 anos
**Gate**: quick (unit) + nightly
**Commit**: `feat(ontogeny): implement dated milestone regression with named cause`

---

### T10: `LanguageFluencyResolver`

**What**: Alvo de fluência é o idioma de maior exposição entre cuidadores, não a etnia.
**Where**: `src/LivingWorld.Simulation/Ontogeny/LanguageFluencyResolver.cs` (novo)
**Depends on**: T6
**Reuses**: `ExposureAccumulator` (T6)
**Requirement**: ONT-50, ONT-51, ONT-52

**Done when**:
- [ ] Órfão de cuidador de idioma diferente termina com fluência maior no idioma do cuidador, 20/20 seeds
- [ ] Nenhuma fluência maior que zero ao nascer
- [ ] Desempate determinístico quando exposição é exatamente igual entre idiomas

**Tests**: unit + par órfão
**Gate**: quick
**Commit**: `feat(ontogeny): resolve language fluency target from caregiver exposure, not ethnicity`

---

### T11: `MilestoneProgressSum` no pool + resample na materialização

**What**: Campo aditivo em `AggregatePopulationPool` (mesma forma de `WealthSum`/`HealthSum`);
`MaterializationSystem.MaterializeOne` resample determinístico a partir da média do pool.
**Where**: `src/LivingWorld.Domain/Cities/AggregatePopulationPool.cs` (modificado, aditivo),
`src/LivingWorld.Simulation/Cities/MaterializationSystem.cs` (modificado, aditivo)
**Depends on**: T8
**Reuses**: exato padrão de `wealthPerHead`/`healthPerHead` já existente
**Requirement**: ONT-70, ONT-71, ONT-72

**Done when**:
- [ ] Criança em região agregada acumula `MilestoneProgressSum` por eixo sem custar rotina completa
- [ ] Materialização resample `progressPerHeadPerAxis` deterministicamente pela mesma seed
- [ ] Custo medido não escala com toda a população infantil materializada

**Tests**: unit + medição de custo
**Gate**: quick
**Commit**: `feat(cities): add milestone progress to aggregate pool with resample-on-materialize`

---

### T12: `OntogenyLifecycleGate`

**What**: Retorna inerte assim que a última janela fecha; nenhum sistema desta fase reavalia o
NPC depois disso.
**Where**: `src/LivingWorld.Simulation/Ontogeny/OntogenyLifecycleGate.cs` (novo), hook nos
sistemas T5/T6/T7/T8/T9/T10 (modificado, checagem de entrada)
**Depends on**: T9, T10, T11
**Reuses**: nenhum
**Requirement**: ONT-90, ONT-91, ONT-92

**Done when**:
- [ ] NPC adulto desenvolvido custa O(1) por tick neste conjunto de sistemas (checagem única, sem recálculo)
- [ ] Custo por tick do mundo escala só com NPCs ainda em janela, medido com população majoritariamente adulta
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit + custo por tick
**Gate**: build
**Commit**: `feat(ontogeny): gate all systems inert after final developmental window closes`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Parallel, depends on Phase 1):
  T3 ──→ T6

Phase 4 (Sequential, depends on Phase 2, 3):
  T5, T6 ──→ T7 ──→ T8

Phase 5 (depends on Phase 4):
  T8 ──→ T9

Phase 6 (Parallel, depends on Phase 3):
  T6 ──→ T10

Phase 7 (depends on Phase 4):
  T8 ──→ T11

Phase 8 (last — depends on Phase 5, 6, 7):
  T9, T10, T11 ──→ T12
```

8 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fase 3
(exposição) e Fase 6 (idioma) são ramos independentes que correm em paralelo.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4, T5 | 1 extensão de catálogo + 1 filtro | ✅ Granular |
| T6 | 1 acumulador | ✅ Granular |
| T7, T8 | 1 sistema de progresso + 1 sistema de janela (mesmo domínio, 2 responsabilidades) | ✅ Granular |
| T9 | 1 sistema de regressão | ✅ Granular |
| T10 | 1 resolvedor de idioma | ✅ Granular |
| T11 | 1 campo de pool + resample | ✅ Granular |
| T12 | 1 gate de ciclo de vida | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T1, T2 | T1,T2→T3 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T3 | T3→T6 (paralelo à Fase 2) | ✅ Match |
| T7 | T5, T6 | T5,T6→T7 | ✅ Match |
| T8 | T7 | T7→T8 | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T6 | T6→T10 | ✅ Match |
| T11 | T8 | T8→T11 | ✅ Match |
| T12 | T9, T10, T11 | T9,T10,T11→T12 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T3 | Domain/business-logic | unit | unit | ✅ OK |
| T4 | Extensão de catálogo existente | unit + enumeração | unit + enumeração | ✅ OK |
| T5 | Filtro de comportamento | unit + custo | unit + custo | ✅ OK |
| T6, T10 | Acumulador/resolvedor | unit + par pareado | mesmo | ✅ OK |
| T7, T8, T9 | Sistemas de progresso/janela/regressão | unit (+ 3 braços em T8, auditoria em T9) | mesmo | ✅ OK |
| T11 | Pool + materialização | unit + custo | unit + custo | ✅ OK |
| T12 | Gate final + build gate | unit + custo, build | mesmo | ✅ OK |

No task defers its own tests to a later task.
