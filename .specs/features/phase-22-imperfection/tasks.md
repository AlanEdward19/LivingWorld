# Fase 22 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-22-imperfection/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 4/7/10/21 (xUnit, mundo
> controle/tratado, determinismo por seed, guards de reflexão clonados de
> `PersonalityWeighting`/`FactTests`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`Condition`, `Disease`, `ContagionRecord`, `DisclosureRecord`, `ImperfectionRules`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Imperfection/**` (novo) | `dotnet test --filter "FullyQualifiedName~Imperfection"` |
| `ConditionThresholdRule`/`ConditionCeilingApplier` | unit | 1:1 a IMP-01..03, IMP-20..22 | `tests/LivingWorld.Tests/Imperfection/**` | mesmo comando |
| `DiseaseTransmissionSystem` | unit + auditoria 10/100 anos | 1:1 a IMP-10..14 | `tests/LivingWorld.Tests/Imperfection/**` (gate) + nightly | mesmo comando |
| `CulturalReactionResolver` | unit + par base/tratamento | 1:1 a IMP-30..32 | `tests/LivingWorld.Tests/Imperfection/**` | mesmo comando |
| Guard "nenhum campo de moralidade" | unit + enumeração por reflexão | 1:1 a IMP-40..42 | `tests/LivingWorld.Tests/Population/NoMoralityFieldGuardTests.cs` (novo) | mesmo comando |
| `LuckTerm` | unit + 20 seeds + braço de controle | 1:1 a IMP-50..55 | `tests/LivingWorld.Tests/Imperfection/LuckTermTests.cs` (novo) | mesmo comando |
| `DisclosureTransitionSystem` | unit + par tolerância oposta | 1:1 a IMP-60..64, 18/20 seeds | `tests/LivingWorld.Tests/Imperfection/**` | mesmo comando |
| `CourtshipSystem.Reject` (extensão) | unit + auditoria 10 anos | 1:1 a IMP-70..73 | `tests/LivingWorld.Tests/Population/CourtshipSystemTests.cs` (existente, estendido) | `dotnet test --filter "FullyQualifiedName~Courtship"` |
| Full regression | build gate | Backend inteiro verde, sem regressão em `Population*`/`Family*`/`Behavior*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Imperfection) | Yes | Mundo próprio por teste | Fase 4/7/21 |
| par base/tratamento (cultura, tolerância) | Yes | Mundo controle/tratado (`PairedScenarioTests.cs`) | Fase 7 |
| 20 seeds + braço de controle (sorte) | Yes | Mundo por seed, stream de sorte livre entre braços | Decisão confirmada com o usuário |
| enumeração por reflexão | Yes | Mesmo padrão de `PersonalityWeightingTests`/`FactTests` | Fase 4/10 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Imperfection"` |
| Full (integração) | Após tasks que tocam `Population`/`Courtship` | `dotnet test --filter "Category!=Scenario&(FullyQualifiedName~Imperfection\|FullyQualifiedName~Courtship)"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Condição genética + consequência funcional (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Doença (Parallel OK, depende de Phase 1)

```
T3 → T6
```

### Phase 4: Sorte (Parallel OK, depende de Phase 1)

```
T3 → T7
```

### Phase 5: Moralidade emergente (Parallel OK, depende de Phase 1)

```
T3 → T8 → T9
```

### Phase 6: Reação cultural (depende de Phase 2)

```
T5 → T10
```

### Phase 7: Orientação e divulgação (depende de Phase 1)

```
T3 → T11 → T12
```

### Phase 8: Cortejo (última — depende de Phase 7)

```
T12 → T13
```

---

## Task Breakdown

### T1: `ConditionOrigin`, `ConditionCourse`, `Condition`

**What**: Enums e record de domínio.
**Where**: `src/LivingWorld.Domain/Imperfection/Condition.cs` (novo)
**Depends on**: None
**Reuses**: `DevelopmentAxis` (Fase 21) referenciado no campo `FunctionalConsequence`
**Requirement**: IMP-01

**Done when**:
- [ ] `Condition` sem `FunctionalConsequence` (condição sem deficiência funcional) é caso válido

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add Condition, ConditionOrigin and ConditionCourse`

---

### T2: `TransmissionVector`, `ImmunityKind`, `Disease`, `ContagionRecord`

**What**: Enums e records de domínio.
**Where**: `src/LivingWorld.Domain/Imperfection/Disease.cs` (novo)
**Depends on**: None
**Reuses**: nenhum
**Requirement**: IMP-10

**Done when**:
- [ ] `ContagionRecord` sempre exige `SourceCase` (não-nullable) — construção sem fonte é erro de compilação, não runtime

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add Disease, TransmissionVector and ContagionRecord`

---

### T3: `WorldEventKind` — 4 valores novos

**What**: `ConditionDiagnosed`, `DiseaseContracted`, `DisclosureChanged`,
`CourtshipRejectedOrientation`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add imperfection WorldEventKind values`

---

### T4: `ConditionThresholdRule`

**What**: Classifica `RateGene.Value` extremo cruzando `GeneticThreshold` como condição
nomeada.
**Where**: `src/LivingWorld.Simulation/Imperfection/ConditionThresholdRule.cs` (novo)
**Depends on**: T3
**Reuses**: `Npc.RateGene`/`RateGene.Value` (Fase 6/21), sem modificação
**Requirement**: IMP-01, IMP-02, IMP-03

**Done when**:
- [ ] Condição genética classificada é literalmente o mesmo `RateGene.Value`, nenhum campo genético paralelo criado
- [ ] `WorldEventKind.ConditionDiagnosed` logado no cruzamento do limiar

**Tests**: unit
**Gate**: quick
**Commit**: `feat(imperfection): classify extreme RateGene values as named genetic conditions`

---

### T5: `ConditionCeilingApplier`

**What**: Reduz `DevelopmentAxis.Ceiling` (Fase 21) pela `FunctionalConsequence` declarada;
múltiplas condições no mesmo eixo usam o mínimo entre reduções.
**Where**: `src/LivingWorld.Simulation/Imperfection/ConditionCeilingApplier.cs` (novo)
**Depends on**: T4
**Reuses**: `DevelopmentAxis`/`Ceiling`/`MilestoneEligibilityFilter` (Fase 21), sem modificação
**Requirement**: IMP-20, IMP-21, IMP-22

**Done when**:
- [ ] Ação que exige limiar acima do novo teto desaparece do conjunto candidato via o filtro já existente (integração, não novo filtro)
- [ ] Múltiplas condições no mesmo eixo resolvem pelo mínimo, nunca soma negativa
- [ ] Restauração de teto só ocorre por regra explícita de recuperação declarada

**Tests**: unit
**Gate**: quick
**Commit**: `feat(imperfection): apply functional consequence to development axis ceiling`

---

### T6: `DiseaseTransmissionSystem`

**What**: Rolagem `Resolver.Resolve` (`Dramatico`, resistência como modificador) pro curso
individual; `ContagionRecord` sempre com `SourceCase`.
**Where**: `src/LivingWorld.Simulation/Imperfection/DiseaseTransmissionSystem.cs` (novo)
**Depends on**: T3
**Reuses**: `Resolver.Resolve`/`VarianceProfile.Dramatico` (ADR-0011), sem modificação
**Requirement**: IMP-11, IMP-12, IMP-13, IMP-14

**Done when**:
- [ ] Dois NPCs expostos à mesma doença podem ter desfechos diferentes (resistência modifica a rolagem)
- [ ] Auditoria de 10 anos (gate)/100 anos (nightly): todo caso tem `SourceCase` rastreável dentro da janela de incubação
- [ ] Conjunto de doenças instanciadas é sempre subconjunto do catálogo (garantido por construção — erro de config se referenciar doença fora do catálogo)
- [ ] Doença extinta com imunidade permanente só reintroduz por evento de cenário explícito, nunca sozinha

**Tests**: unit + auditoria 10/100 anos
**Gate**: quick (unit) + nightly
**Commit**: `feat(imperfection): implement disease transmission with individual resolution roll`

---

### T7: `LuckTerm`

**What**: Stream `"luck"` dedicada, perfil `Raro`, alimenta `w_sorte` do `MoralOutcomeBreakdown`.
**Where**: `src/LivingWorld.Domain/Imperfection/LuckTerm.cs` (novo)
**Depends on**: T3
**Reuses**: `WorldRngRegistry.Stream("luck")`, `Resolver.Resolve`/`VarianceProfile.Raro` (ADR-0011, confirmado 4% tail event, bypass da régua normal)
**Requirement**: IMP-50, IMP-51

**Done when**:
- [ ] `w_gene`/`w_env`/`w_sorte` consultáveis separadamente via `MoralOutcomeBreakdown`, nunca só a soma
- [ ] `LuckTerm` nunca compartilha stream com `DiseaseTransmissionSystem` ou qualquer outro sistema

**Tests**: unit
**Gate**: quick
**Commit**: `feat(imperfection): implement named luck term via dedicated RNG stream and Raro profile`

---

### T8: `MoralOutcomeBreakdown` — calibração de `w_sorte`

**What**: Peso default documentado, calibrado contra `tests/baselines/` da Fase 7.
**Where**: `src/LivingWorld.Domain/Imperfection/MoralOutcomeBreakdown.cs` (novo)
**Depends on**: T7
**Reuses**: baselines existentes da Fase 7
**Requirement**: IMP-52, IMP-53, IMP-54, IMP-55

**Done when**:
- [ ] 20 seeds com `w_sorte` default: ≥1 contradição, dentro da faixa de baseline (calibração ativa nesta task — ajustar peso até fechar)
- [ ] 20 seeds com `w_sorte=0`: exatamente 0 contradições
- [ ] Hash canônico muda entre `w_sorte` default e zero, medido em 10 anos

**Tests**: unit + 20 seeds + braço de controle
**Gate**: quick
**Commit**: `feat(imperfection): calibrate default luck weight against phase-7 baselines`

---

### T9: `MoralPatternQuery` + guard "nenhum campo de moralidade"

**What**: Leitura pura combinando `Personality` + event log; guard de reflexão clonado de
`PersonalityWeighting`/`FactTests`.
**Where**: `src/LivingWorld.Simulation/Imperfection/MoralPatternQuery.cs` (novo),
`tests/LivingWorld.Tests/Population/NoMoralityFieldGuardTests.cs` (novo)
**Depends on**: T8
**Reuses**: `Npc.Personality`, `WorldEventKind` (Fase 4), padrão de `PersonalityWeighting.AllTraitNames`/`FactTests` reflection guard
**Requirement**: IMP-40, IMP-41, IMP-42

**Done when**:
- [ ] Enumeração por reflexão do esquema do NPC não encontra escalar de alinhamento/karma/bondade
- [ ] Todo campo novo do esquema é classificado explicitamente (moral ou não-moral) — falha se algum ficar sem classificação
- [ ] `MoralPatternQuery` nunca escreve campo, sempre deriva

**Tests**: unit + enumeração por reflexão
**Gate**: quick
**Commit**: `feat(imperfection): implement moral pattern as derived query, guard against alignment fields`

---

### T10: `CulturalReactionResolver` + `CorruptionEffectApplier`

**What**: Reação calculada de `CityCulture` (interface assumida, Fase 13); corrupção modifica
sistemas concretos, nunca `Corruption`/`IsEvil`.
**Where**: `src/LivingWorld.Simulation/Imperfection/CulturalReactionResolver.cs` (novo),
`src/LivingWorld.Simulation/Imperfection/CorruptionEffectApplier.cs` (novo)
**Depends on**: T5
**Reuses**: `CityCulture` (Fase 8, stub) — defaults neutros documentados até Fase 13
**Requirement**: IMP-30, IMP-31, IMP-32

**Done when**:
- [ ] Par base/tratamento (culturas com valores opostos) diverge na direção prevista
- [ ] Esquema de `Condition` não tem campo de reação padrão — reação sempre calculada no ponto de observação
- [ ] Corrupção altera `Personality`/percepção diretamente, nunca escreve campo booleano de maldade

**Tests**: unit + par base/tratamento
**Gate**: quick
**Commit**: `feat(imperfection): resolve cultural reaction and corruption via concrete system modifiers`

---

### T11: `SexualOrientation` (catálogo), `DisclosureState`, `DisclosureRecord`

**What**: Orientação como catálogo de cenário; estado de divulgação como record.
**Where**: `src/LivingWorld.Domain/Imperfection/Orientation.cs` (novo)
**Depends on**: T3
**Reuses**: mesmo padrão de dado-de-catálogo já usado por condição/profissão/recurso
**Requirement**: IMP-60, IMP-62

**Done when**:
- [ ] Orientação atribuída no nascimento independente da cultura declarada (distribuição vem do cenário geral, não filtrada por cultura)
- [ ] `DisclosureState.Denied` é estado ativo — NPC "sabe" sua própria orientação (fato consultável, não incerteza)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(imperfection): add catalog-driven sexual orientation and disclosure state`

---

### T12: `DisclosureTransitionSystem`

**What**: Transição por tolerância local, vínculo de quem sabe, eventos de exposição/prova.
**Where**: `src/LivingWorld.Simulation/Imperfection/DisclosureTransitionSystem.cs` (novo)
**Depends on**: T11
**Reuses**: `CulturalReactionResolver` (T10) pra tolerância local
**Requirement**: IMP-61, IMP-63, IMP-64

**Done when**:
- [ ] Toda transição tem causa rastreável (`WorldEventKind.DisclosureChanged` com evento causador)
- [ ] Exposição/prova desmente "negado", deixando gancho pronto (sem implementar chantagem aqui)
- [ ] Par tolerância oposta: taxa de "assumido" diverge (18/20 seeds) E distribuição de orientação é byte-idêntica nos 2 braços

**Tests**: unit + par tolerância oposta
**Gate**: quick
**Commit**: `feat(imperfection): implement disclosure state transitions with traceable cause`

---

### T13: `CourtshipRejectionReason.OrientacaoIncompativel` + integração

**What**: Novo valor no enum existente, inserido na mesma ordem de prioridade de `Incesto`/
`ForaDaFaixaEtaria`; orientação e divulgação entram no cálculo de compatibilidade.
**Where**: `src/LivingWorld.Domain/Population/CourtshipRejectionReason.cs` (modificado,
aditivo), `src/LivingWorld.Simulation/Population/CourtshipSystem.cs` (modificado, aditivo)
**Depends on**: T12
**Reuses**: `CourtshipSystem.Reject` (Fase 7), ordem de prioridade existente
**Requirement**: IMP-70, IMP-71, IMP-72, IMP-73

**Done when**:
- [ ] Rejeição por orientação incompatível é veto duro (mesma prioridade de `Incesto`), motivo nomeado
- [ ] Auditoria de 10 anos: zero pares formados violando orientação declarada
- [ ] Prova positiva: par compatível em tudo menos orientação é rejeitado com motivo nomeado (evita a armadilha de "ninguém se encontrou")
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit + auditoria 10 anos + prova positiva
**Gate**: build
**Commit**: `feat(population): add orientation-incompatible courtship rejection reason`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Parallel, depends on Phase 1):
  T3 ──→ T6

Phase 4 (Parallel, depends on Phase 1):
  T3 ──→ T7 ──→ T8

Phase 5 (Parallel, depends on Phase 1):
  T3 ──→ T8 (compartilhado com Phase 4) ──→ T9

Phase 6 (depends on Phase 2):
  T5 ──→ T10

Phase 7 (Sequential, depends on Phase 1):
  T3 ──→ T11 ──→ T12

Phase 8 (last — depends on Phase 7):
  T12 ──→ T13
```

8 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fases
2, 3, 4/5 e 7 são ramos independentes que correm em paralelo assim que a Fase 1 termina.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4, T5 | 1 regra de classificação + 1 aplicador de teto | ✅ Granular |
| T6 | 1 sistema de transmissão | ✅ Granular |
| T7, T8 | 1 termo puro + 1 task de calibração dedicada | ✅ Granular |
| T9 | 1 query + 1 guard de reflexão (mesmo domínio, 2 artefatos coesos) | ✅ Granular |
| T10 | 1 resolvedor + 1 aplicador (mesmo domínio: reação/corrupção) | ✅ Granular |
| T11, T12 | 1 catálogo/estado + 1 sistema de transição | ✅ Granular |
| T13 | 1 extensão de enum + integração num sistema existente | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | T1, T2 | T1,T2→T3 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T3 | T3→T6 (paralelo) | ✅ Match |
| T7 | T3 | T3→T7 (paralelo) | ✅ Match |
| T8 | T7 | T7→T8 | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T5 | T5→T10 | ✅ Match |
| T11 | T3 | T3→T11 (paralelo) | ✅ Match |
| T12 | T11 | T11→T12 | ✅ Match |
| T13 | T12 | T12→T13 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T7 | Domain/business-logic | unit (+ auditoria em T6) | unit / unit + auditoria | ✅ OK |
| T8 | Task de calibração dedicada | unit + 20 seeds + controle | mesmo | ✅ OK |
| T9 | Query + guard de reflexão | unit + enumeração | mesmo | ✅ OK |
| T10 | Resolvedores | unit + par base/tratamento | mesmo | ✅ OK |
| T11, T12 | Catálogo/estado + sistema | unit / unit + par | mesmo | ✅ OK |
| T13 | Extensão de sistema existente + build gate final | unit + auditoria + prova positiva, build | mesmo | ✅ OK |

No task defers its own tests to a later task.
