# Fase 16.2 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-16-2-power-evolution/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicated — segue o padrão já usado nas specs 16/16.1 (xUnit,
> mundo controle/tratado, determinismo por seed).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`PowerEvolutionStage`, `ExtraordinaryCarrierState` novos campos) | unit | Construção/invariantes + consumo pelo sistema de estágio | `tests/LivingWorld.Tests/Extraordinary/**` | `dotnet test --filter "FullyQualifiedName~Extraordinary"` |
| `ExtraordinaryPowerStageSystem`/`PowerUseCounter` | unit | 1:1 a EVO-01..05, incluindo AND estrito (idade+uso) | `tests/LivingWorld.Tests/Extraordinary/**` | mesmo comando |
| `PowerInheritanceResolver`/`MixDescriptorBuilder` | unit | 1:1 a EVO-10..16, distribuição estatística dos 3 caminhos + determinismo por seed | `tests/LivingWorld.Tests/Population/**` (mesma pasta de `NatalitySystem`) | `dotnet test --filter "FullyQualifiedName~Population"` |
| Matriz de cobertura completa (EVO-20..22) | unit | 1 caso de estágio + 1 caso de mistura por categoria de mecânica da 16.1 (atributo, gravidade, mente, sorte, combate, transferência, instanciação, controle, vínculo, dimensional, ambiental/fauna/flora) | `tests/LivingWorld.Tests/Extraordinary/PowerEvolutionCoverageTests.cs` (novo) | `dotnet test --filter "FullyQualifiedName~PowerEvolutionCoverage"` |
| Full regression | build gate | Backend inteiro verde, sem regressão em `Extraordinary*`/`Population*`/`Natality*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Extraordinary) | Yes | Mundo próprio por teste, sem estado estático compartilhado | Padrão já usado em `ExtraordinaryInvocationEngineTests.cs` |
| unit (Population/herança) | Yes | Mundo controle/tratado por teste (`PairedScenarioTests.cs`) | Padrão já usado nesses testes |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | Já documentado em `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Extraordinary"` |
| Full (herança) | Após tasks que tocam `NatalitySystem` | `dotnet test --filter "Category!=Scenario&FullyQualifiedName~Population"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Data model (Sequential)

```
T1 → T2
```

### Phase 2: Estágios (Sequential, depende de Phase 1)

```
T2 → T3 → T4
```

### Phase 3: Fundação de herança (Parallel OK, independente de Phase 1/2)

```
T5 [P], T6 [P]
```

### Phase 4: Roll de herança (depende de Phase 3)

```
T5, T6 → T7
```

### Phase 5: Os 3 caminhos de resultado (Parallel OK, depende de T7)

```
T7 → T8 [P], T9 [P], T10 [P]
```

### Phase 6: Integração no nascimento (depende de Phase 5)

```
T8, T9, T10 → T11
```

### Phase 7: Cobertura completa (última — depende de tudo)

```
T4, T11 → T12
```

---

## Task Breakdown

### T1: `PowerEvolutionStage` + campo `Stages` em `PowerDescriptor`

**What**: Novo record `PowerEvolutionStage(AgeThreshold?, UseCountThreshold?, EffectTokens)`;
`PowerDescriptor.Stages` (aditivo, `null` = sem evolução).
**Where**: `src/LivingWorld.Domain/Extraordinary/PowerEvolutionStage.cs` (novo),
`PowerDescriptor.cs` (modificado)
**Depends on**: None
**Reuses**: mesma forma de token já usada em `PowerDescriptor.Effects`
**Requirement**: EVO-01, EVO-02

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] `PowerEvolutionStage` aceita idade, uso, ou ambos declarados
- [ ] `PowerDescriptor.Stages` é `null`-safe (descritor sem estágios continua funcionando idêntico a hoje)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add PowerEvolutionStage and PowerDescriptor.Stages field`

---

### T2: `UseCount`/`CurrentStageIndex` em `ExtraordinaryCarrierState`

**What**: Dois campos aditivos.
**Where**: `src/LivingWorld.Domain/Extraordinary/ExtraordinaryCarrierState.cs` (modificado)
**Depends on**: T1
**Reuses**: mesmo padrão de campo aditivo já usado pra `PreAlterationTraits` (16.1)
**Requirement**: EVO-04

**Done when**:
- [ ] Default `UseCount=0`, `CurrentStageIndex=0` — nenhum call site existente precisa mudar

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add UseCount and CurrentStageIndex to ExtraordinaryCarrierState`

---

### T3: `PowerUseCounter`

**What**: Incrementa `UseCount` uma vez por invocação bem-sucedida daquele poder.
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerUseCounter.cs` (novo), hook no mesmo
ponto que já loga `EffectApplied`
**Depends on**: T2
**Reuses**: log causal `EffectApplied`/`UseFailed` já existente (16.1)
**Requirement**: EVO-04

**Done when**:
- [ ] Sucesso incrementa exatamente uma vez; falha nunca incrementa

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): increment power use counter on successful invocation only`

---

### T4: `ExtraordinaryPowerStageSystem`

**What**: Resolve o estágio mais alto atingido (AND estrito quando idade+uso declarados) e
troca o `EffectTokens` efetivo lido por `PrepareEffects`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ExtraordinaryPowerStageSystem.cs` (novo)
**Depends on**: T3
**Reuses**: cadência de reavaliação já usada por `ExtraordinaryStateSystem` (16.1)
**Requirement**: EVO-01, EVO-02, EVO-03, EVO-05

**Done when**:
- [ ] Estágio mais alto atingido é aplicado, nunca um futuro nem mais de um simultâneo
- [ ] Estágio com idade+uso exige os dois (AND) — só um não conta
- [ ] Sem atingir o primeiro limiar, permanece no estágio 0 sem falhar
- [ ] Mesma seed/histórico produz o mesmo estágio corrente entre execuções

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add power stage resolution system (age and/or use-count gated)`

---

### T5: `DeterministicChoice` [P]

**What**: Utilitário puro `hash(seed, npcId, salt) → double [0,1)`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/DeterministicChoice.cs` (novo)
**Depends on**: None
**Reuses**: mesmo espírito de RNG seedado já usado por `Resolver.Resolve`
**Requirement**: EVO-16

**Done when**:
- [ ] Mesma seed+npcId+salt produz sempre o mesmo valor
- [ ] Distribuição visualmente uniforme em `[0,1)` numa amostra grande de npcIds (teste estatístico simples)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add deterministic hash-based choice utility`

---

### T6: `PowerInheritanceRules` (regra de cenário) [P]

**What**: `record PowerInheritanceRules(InheritanceChance, BothWeight, OneOfWeight, MixedWeight)`
com defaults documentados (uniforme 1/3 cada caminho).
**Where**: `src/LivingWorld.Domain/Extraordinary/PowerInheritanceRules.cs` (novo)
**Depends on**: None
**Reuses**: mesmo arquivo/padrão já usado por `AcquisitionRules`/`FamilyRules`
**Requirement**: (suporta EVO-10)

**Done when**:
- [ ] Cenário sem declaração explícita usa os defaults documentados, nunca falha

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add scenario-configurable PowerInheritanceRules with documented defaults`

---

### T7: `PowerInheritanceResolver` — os 2 rolls

**What**: Roll 1 (ocorre herança?) + roll 2 (qual dos 3 caminhos), delega pro caminho.
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerInheritanceResolver.cs` (novo)
**Depends on**: T5, T6
**Reuses**: mesmo espírito probabilístico de `AcquisitionRules`
**Requirement**: EVO-10, EVO-15, EVO-16

**Done when**:
- [ ] Sem os dois pais portadores, nenhum roll executa (custo O(1) de checagem antes)
- [ ] Roll 2 respeita os pesos declarados (ou default uniforme)
- [ ] Mesma seed produz o mesmo resultado (qual caminho) entre execuções

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add power inheritance resolver with occurrence and outcome rolls`

---

### T8: Caminho "Ambos" [P]

**What**: Filho recebe os 2 `PowerDescriptor`s originais, completos e independentes.
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerInheritanceResolver.cs` (modificado,
branch)
**Depends on**: T7
**Reuses**: cópia de descritor já usada por `npc.clone` (16.1)
**Requirement**: EVO-11

**Done when**:
- [ ] Filho manifesta os dois poderes normalmente, sem interferência um do outro

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): implement "both" inheritance outcome`

---

### T9: Caminho "Um só" [P]

**What**: Filho recebe exatamente um dos dois descritores, inalterado; qual pai também é
determinístico pela mesma semente.
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerInheritanceResolver.cs` (modificado,
branch)
**Depends on**: T7
**Reuses**: mesmo `DeterministicChoice` (T5)
**Requirement**: EVO-12

**Done when**:
- [ ] Filho manifesta exatamente 1 poder, cópia fiel do pai escolhido
- [ ] Escolha de qual pai é reproduzível pra mesma seed

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): implement "one-of" inheritance outcome`

---

### T10: Caminho "Mistura" — `MixDescriptorBuilder` [P]

**What**: Recombina por eixo (fonte/efeito/custo/condição/aquisição); chave de mecânica igual
nos dois pais agrega magnitude (soma, sem teto); chave só de um pai é incluída ou escolhida por
hash se colidir.
**Where**: `src/LivingWorld.Simulation/Extraordinary/MixDescriptorBuilder.cs` (novo)
**Depends on**: T7
**Reuses**: parsing de token já usado pelo registro de mecânicas (16.1)
**Requirement**: EVO-13, EVO-14, EVO-21

**Done when**:
- [ ] Chave presente nos dois pais agrega magnitude sem teto
- [ ] Chave presente em só um pai é incluída (ou escolhida por hash se colidir com outro eixo)
- [ ] Resultado passa pela mesma validação de contrato do 16.1 (`Prepare`); inválido descarta,
      filho nasce sem poder
- [ ] Mecânicas diferentes entre os pais nunca geram erro de "incompatibilidade"

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): implement "mixed" inheritance outcome via per-axis descriptor recombination`

---

### T11: Integração no nascimento (`NatalitySystem`) + auditoria

**What**: Hook `PowerInheritanceResolver` no ponto de nascimento; loga `WorldEventKind.PowerInherited`.
**Where**: `src/LivingWorld.Simulation/Population/NatalitySystem.cs` (modificado),
`src/LivingWorld.Domain/History/WorldEventKind.cs` (novo valor `PowerInherited`)
**Depends on**: T8, T9, T10
**Reuses**: mesmo ponto de criação de `Npc` já usado por `npc.reincarnate` (16.1)
**Requirement**: EVO-10 (integração), auditoria

**Done when**:
- [ ] Todo nascimento com os dois pais portadores dispara o resolver e loga `PowerInherited`
      com o caminho escolhido e os descritores resultantes
- [ ] Nascimento sem os dois pais portadores é idêntico ao custo/comportamento de hoje
- [ ] Gate: `dotnet test --filter "Category!=Scenario&FullyQualifiedName~Population"` verde

**Tests**: unit
**Gate**: full
**Commit**: `feat(population): wire power inheritance into NatalitySystem birth path`

---

### T12: Matriz de cobertura completa (EVO-20..22)

**What**: Suíte dedicada com 1 `PowerDescriptor` de amostra por categoria de mecânica da 16.1
(atributo, gravidade, mente, sorte, combate, transferência, instanciação, controle, vínculo,
dimensional, ambiental/fauna/flora) — 1 caso de estágio + participação em pelo menos 1 caso de
mistura cada.
**Where**: `tests/LivingWorld.Tests/Extraordinary/PowerEvolutionCoverageTests.cs` (novo)
**Depends on**: T4, T11
**Reuses**: fixtures de poder já usadas nos testes da 16.1 (uma por categoria)
**Requirement**: EVO-20, EVO-21, EVO-22

**Done when**:
- [ ] Cada categoria de mecânica da 16.1 tem pelo menos 1 caso de estágio testado
- [ ] Cada categoria participa de pelo menos 1 caso de mistura cruzada (com outra categoria)
- [ ] Nenhuma categoria exige tratamento especial no motor (falha do teste se precisar)
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit
**Gate**: build
**Commit**: `test(extraordinary): add full mechanic-category coverage matrix for power evolution`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2

Phase 2 (Sequential, depends on Phase 1):
  T2 ──→ T3 ──→ T4

Phase 3 (Parallel, independent of Phase 1/2):
  T5 [P], T6 [P]

Phase 4 (depends on Phase 3):
  T5, T6 ──→ T7

Phase 5 (Parallel, depends on T7):
  T7 ──┬→ T8 [P]
       ├→ T9 [P]
       └→ T10 [P]

Phase 6 (depends on Phase 5):
  T8, T9, T10 ──→ T11

Phase 7 (last — depends on Phase 2 and Phase 6):
  T4, T11 ──→ T12
```

7 phases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm),
mas a Fase 3/4/5 podem correr logo após a Fase 1/2 terminar (são ramos independentes até a
Fase 6 juntar tudo).

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2 | 1 modelo de dados cada | ✅ Granular |
| T3, T4 | 1 sistema cada | ✅ Granular |
| T5, T6 | 1 utilitário/regra cada | ✅ Granular |
| T7 | 1 resolver (2 rolls, mesmo componente) | ✅ Granular |
| T8, T9, T10 | 1 caminho de resultado cada | ✅ Granular |
| T11 | 1 ponto de integração | ✅ Granular |
| T12 | 1 suíte de teste dedicada (não um deliverable de produção) | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T2 | T2→T3 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5, T6 | None | [P], independent | ✅ Match |
| T7 | T5, T6 | T5,T6→T7 | ✅ Match |
| T8, T9, T10 | T7 | T7→[P] | ✅ Match |
| T11 | T8, T9, T10 | T8,T9,T10→T11 | ✅ Match |
| T12 | T4, T11 | T4,T11→T12 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T10 | Domain/business-logic | unit | unit | ✅ OK |
| T11 | Consumer-system (`NatalitySystem`) | unit + full gate on Population | unit, full | ✅ OK |
| T12 | Final coverage suite | build gate before Verifier | build | ✅ OK |

No task defers its own tests to a later task.
