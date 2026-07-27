# Fase 6 — Habilidades e Aprendizado — Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow
its Execute flow and Critical Rules.** Do not search for skill files by filesystem path.
The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation,
adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-06-skills/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Gerado por amostragem do repo (`tests/LivingWorld.Tests/{Behavior,Economy}/*.cs`) — sem
> `AGENTS.md`/`CONTRIBUTING.md` de padrão de teste; convenção do projeto é xUnit, um único
> projeto de teste, sensores de hash/conservação como teste de integração leve (sem
> framework de e2e — o "e2e" deste projeto é rodar `ScenarioRunner` alguns dias/anos).
> Cenários longos (20 seeds/20 anos) usam `[Trait("Category","Scenario")]` e ficam fora do
> gate padrão (`scripts/test.sh` filtra `Category!=Scenario`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
|---|---|---|---|---|
| Domain — funções puras (`SkillCurve`) | unit | Todo o range citado no AC (`n` em `1..1000`), sem `ScenarioRunner`, sem seed | `tests/LivingWorld.Tests/Population/SkillCurveTests.cs` | `bash scripts/test.sh --filter FullyQualifiedName~SkillCurveTests` |
| Domain — value objects (`SkillSet`, `RateGene`, `SkillsRules`) | unit | 1:1 com ACs de validação/clamp; toda faixa inválida rejeitada | `tests/LivingWorld.Tests/Population/{SkillSetTests,RateGeneTests,SkillsRulesTests}.cs` | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Domain — `Npc` (novos mutadores) | unit | 1:1 com ACs de `SwitchProfession`/`AssignMentor`/`ClearMentor` | `tests/LivingWorld.Tests/Population/NpcSkillMutatorsTests.cs` | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Simulation — sistemas (`SkillPracticeSystem`, `SkillTeachingSystem`, `ProductionSystem` modificado, `BehaviorDecisionSystem` troca de profissão) | integração leve (`ScenarioRunner`/harness dedicado, poucos ticks) | Happy path + edge case listado por sistema; regressão de `MoneyConservationTests`/`ResourceConservationTests` já existentes | `tests/LivingWorld.Tests/Population/*SystemTests.cs`, `tests/LivingWorld.Tests/Economy/ProductionSystemTests.cs` (modificado) | `bash scripts/test.sh --filter Category!=Scenario` |
| Scenario — cenários pareados 10-20 seeds | integração pesada, `Category=Scenario` | Cada critério do roadmap isolado num teste próprio, seeds exatas do critério (10 ou 20) | `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs` | `bash scripts/test.sh --filter Category=Scenario` |
| Config/entidade (enums `SkillType`/`SkillGainSource`) | none | build gate só | — | `bash scripts/build.sh` |

## Parallelism Assessment

> `ScenarioRunner.Create` monta `WorldState` novo por chamada (sem estado global
> compartilhado) — mesmo padrão já usado por todos os testes de sistema existentes
> (`EmploymentSystemTests`, `ProductionSystemTests`). xUnit roda classes em paralelo por
> padrão neste projeto (nenhum `[Collection]` de serialização encontrado nos arquivos
> amostrados).

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
|---|---|---|---|
| unit (Domain, funções puras/value objects) | Yes | Sem estado compartilhado, cada teste cria seu próprio objeto | `tests/LivingWorld.Tests/Behavior/PersonalityTests.cs` (padrão idêntico) |
| integração leve (sistemas via `ScenarioRunner`/harness) | Yes | Cada teste chama `ScenarioRunner.Create(seed)` com seu próprio `WorldState` isolado | `tests/LivingWorld.Tests/Economy/EmploymentSystemTests.cs` |
| Scenario (`Category=Scenario`) | Yes (entre testes) | Mesmo isolamento por `WorldState`, mas cada teste é caro (10-20 seeds × anos) — paralelismo entre testes é seguro, mas não reduz o tempo total do gate manual | `tests/LivingWorld.Tests/Economy/FamineCausalChainTests.cs` |

## Gate Check Commands

| Gate Level | When to Use | Command |
|---|---|---|
| Quick | Após task com só unit tests (Domain) | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Full | Após task com sistemas/integração leve | `bash scripts/verify.sh` |
| Scenario (manual, caro) | Após as tasks de cenário pareado (Phase 5) | `bash scripts/test.sh --filter Category=Scenario` |
| Build | Tasks de enum/config puro | `bash scripts/build.sh` |

---

## Execution Plan

### Phase 1: Domain primitives (Sequential)

```
T1 → T2
T1 → T3 → T4
T4 → T5
T1,T3,T4 → T6
```

### Phase 2: Npc extensions (Sequential — mesmo arquivo)

```
T4,T5 → T7
```

### Phase 3: Systems + Production/Behavior integration (Parallel OK after T7/T6)

```
T6,T7 ──┬→ T8 [P]
        ├→ T9 [P]
        ├→ T10 [P]
        └→ T11 [P]
```

### Phase 4: Wiring (Sequential)

```
T8,T9,T10,T11 → T12 → T13
```

### Phase 5: Cenários pareados de verificação (Parallel OK, Category=Scenario)

```
T13 ──┬→ T14 [P]
      ├→ T15 [P]
      ├→ T16 [P]
      ├→ T17 [P]
      ├→ T18 [P]
      └→ T19 [P]
```

---

## Task Breakdown

### T1: `SkillType` enum

**What**: catálogo fechado dos 13 ids de habilidade (`Agriculture`..`Magic`).
**Where**: `src/LivingWorld.Domain/Population/SkillType.cs`
**Depends on**: None
**Reuses**: padrão de `src/LivingWorld.Domain/Behavior/ActionType.cs`
**Requirement**: SKILL-01

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Enum com os 13 valores na ordem de `docs/roadmap/phase-06-skills.md` task 1
- [ ] Gate check passa: `bash scripts/build.sh`

**Tests**: none
**Gate**: build

---

### T2: `SkillCurve` — função pura de retornos decrescentes [P]

**What**: `static double Gain(double currentSkill, double cap, double baseRate)` — retornos
decrescentes, clamp `>= 0`.
**Where**: `src/LivingWorld.Domain/Population/SkillCurve.cs`
**Depends on**: T1
**Reuses**: nenhum (isolada de propósito, roadmap task 2)
**Requirement**: SKILL-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Gain(n+1) <= Gain(n)` para todo `n` em `1..1000` (teste parametrizado ou loop no teste)
- [ ] Função pura: mesma entrada → mesma saída, sem ler estado externo
- [ ] Nível 0/negativo não lança, retorna ganho não-negativo (Edge Case da spec)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~SkillCurveTests`
- [ ] Test count: ≥3 testes pass

**Tests**: unit
**Gate**: quick

---

### T3: `SkillGainSource` enum

**What**: as 6 fontes de ganho (`Practice`, `DeliberateTraining`, `School`, `Parental`,
`Observation`, `Tutoring`).
**Where**: `src/LivingWorld.Domain/Population/SkillGainSource.cs`
**Depends on**: T1
**Reuses**: mesmo padrão de `SkillType`
**Requirement**: SKILL-03..08 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Enum com os 6 valores
- [ ] Gate check passa: `bash scripts/build.sh`

**Tests**: none
**Gate**: build

---

### T4: `SkillSet` — valor por NPC, imutável com mutador dedicado

**What**: classe com 13 `double` (um por `SkillType`), `Get(SkillType)`,
`WithGain(SkillType, double delta, double cap)` (clamp `[0,cap]`), `Initial(double)`.
**Where**: `src/LivingWorld.Domain/Population/SkillSet.cs`
**Depends on**: T3
**Reuses**: `PersonalityWeighting.TraitValueOf` (switch, sem reflexão) como padrão de acesso
**Requirement**: SKILL-01, SKILL-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Initial(x)` cria as 13 no mesmo valor, dentro de `[0,cap]`
- [ ] `WithGain` nunca ultrapassa `cap` nem desce de 0 (ganho no teto absorvido, SKILL-12)
- [ ] `Get`/`WithGain` usam switch direto sobre `SkillType`, sem reflexão no hot path
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~SkillSetTests`
- [ ] Test count: ≥5 testes pass

**Tests**: unit
**Gate**: quick

---

### T5: `RateGene` — gene de taxa herdado (Assunção A1 da spec)

**What**: `record RateGene(double Value)` com `Create` (validação `>0`), `RollInitial(WorldRng)`
(sem pais), `Inherit(RateGene mother, RateGene father, WorldRng)`
(`mãe*0,5+pai*0,5+mutação`, clamp `>0`).
**Where**: `src/LivingWorld.Domain/Population/RateGene.cs`
**Depends on**: T4
**Reuses**: `Personality.RollFrom` (stream de RNG próprio do NPC) como padrão de roll
**Requirement**: SKILL-09

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `RollInitial`/`Inherit` nunca produzem `Value <= 0`
- [ ] `Inherit` com pais idênticos produz distribuição em torno do valor dos pais (mutação
      garante variação, testável por múltiplos rolls não-idênticos)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~RateGeneTests`
- [ ] Test count: ≥4 testes pass

**Tests**: unit
**Gate**: quick

---

### T6: `SkillsRules` — catálogo cenário-driven

**What**: `record SkillsRules` com `Create(cap, baseRateBySource, skillByProfession)`
validando faixas (`Result<SkillsRules>`), método `Gain(currentSkill, source, rateGene)`
delegando à `SkillCurve`.
**Where**: `src/LivingWorld.Domain/Population/SkillsRules.cs`
**Depends on**: T1, T3, T4
**Reuses**: padrão `NeedsRules.Create`/`EconomyRules.Create`
**Requirement**: SKILL-01..09 (parâmetros)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Create` rejeita `cap <= 0`, taxa negativa por fonte, mapeamento de profissão vazio
      quando `skillByProfession` é obrigatório para o cenário
- [ ] `Gain` retorna `SkillCurve.Gain(...) * rateGene` (gene multiplica taxa, nunca valor)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~SkillsRulesTests`
- [ ] Test count: ≥5 testes pass

**Tests**: unit
**Gate**: quick

---

### T7: `Npc` — campos e mutadores de habilidade/gene/tutoria/profissão

**What**: adiciona `Skills`, `RateGene`, `Mentor` ao construtor único de `Npc`; adiciona
`AssignMentor(NpcId)`, `ClearMentor()`, `SwitchProfession(ProfessionType)`.
**Where**: `src/LivingWorld.Domain/Population/Npc.cs` (modificado)
**Depends on**: T4, T5
**Reuses**: padrão `Hire`/`Fire`/`JoinHousehold`/`LeaveHousehold` já existente no arquivo
**Requirement**: SKILL-01, SKILL-09, SKILL-08, SKILL-14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Construtor único reconstrutível por `System.Text.Json` inclui os 3 campos novos
      (mesma garantia de round-trip de todos os campos mutáveis existentes)
- [ ] `SwitchProfession` troca `Profession` sem tocar `Skills` (estagnação por ausência de
      ganho, Tech Decision do design — nenhum campo novo de "profissão antiga")
- [ ] `AssignMentor`/`ClearMentor` espelham `JoinHousehold`/`LeaveHousehold`
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~NpcSkillMutatorsTests`
- [ ] Test count: ≥4 testes pass

**Tests**: unit
**Gate**: quick

---

### T8: `SkillPracticeSystem` [P]

**What**: `ISimulationSystem` `Daily` — ganho por prática no trabalho, único ponto que lê
`RateGene` para a fonte `Practice`.
**Where**: `src/LivingWorld.Simulation/Population/SkillPracticeSystem.cs`
**Depends on**: T6, T7
**Reuses**: iteração ordenada por `Id.Value` de `ProductionSystem`, `EconomyCatalog.LocationTypeByProfession`
**Requirement**: SKILL-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC empregado, `CurrentAction == Work`, presente no `Workplace` da própria profissão
      ganha habilidade mapeada por `SkillsRules.SkillByProfession`
- [ ] NPC sem profissão mapeada ou não presente não ganha nada nesse tick (sem exceção)
- [ ] Determinístico: mesma seed, mesmo resultado (reusa harness de determinismo existente)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥4 testes pass

**Tests**: integração leve
**Gate**: full

---

### T9: `SkillTeachingSystem` [P]

**What**: `ISimulationSystem` `Daily` — 5 métodos privados (treino deliberado, escola,
parental, observação, tutoria mestre→aprendiz).
**Where**: `src/LivingWorld.Simulation/Population/SkillTeachingSystem.cs`
**Depends on**: T6, T7
**Reuses**: `Npc.MotherId`/`FatherId` existentes, `Npc.Mentor` (T7), `CurrentLocation` existente
**Requirement**: SKILL-04, SKILL-05, SKILL-06, SKILL-07, SKILL-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Cada uma das 5 fontes tem método próprio testável isoladamente
- [ ] Tutoria: taxa do aprendiz depende de `min(habilidade do mestre, cap)` e da habilidade
      de `Teaching` do mestre (SKILL-08)
- [ ] Mentor morto no meio do tick: `ClearMentor()` chamado, sem exceção (Edge Case da spec)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥8 testes pass (mínimo 1 por fonte + 1 de mentor morto)

**Tests**: integração leve
**Gate**: full

---

### T10: `ProductionSystem` — multiplicador de habilidade na saída [P]

**What**: modifica `Produce` para escalar `produced` pela habilidade média dos
trabalhadores presentes, antes de `RecordResourceProduced`.
**Where**: `src/LivingWorld.Simulation/Economy/ProductionSystem.cs` (modificado)
**Depends on**: T6, T7
**Reuses**: `Produce` existente (linhas 33-68), só insere o fator — não duplica scale/clamp
**Requirement**: SKILL-10, SKILL-11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `produced` escala pela habilidade média dos `workersPresent` mapeada por
      `SkillsRules.SkillByProfession[workplace.LocationType → profissão]`
- [ ] `MoneyConservationTests`/`ResourceConservationTests` existentes (Fase 5) continuam
      passando sem modificação — a mudança não quebra conservação
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥3 testes novos + suíte de conservação existente em 0

**Tests**: integração leve
**Gate**: full

---

### T11: `BehaviorDecisionSystem` — escolha e troca de profissão [P]

**What**: pontua candidatas a profissão por habilidade atual + `Personality`
(`PersonalityWeighting`) + vagas abertas (`EmploymentSystem`); chama `Npc.SwitchProfession`.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modificado)
**Depends on**: T7
**Reuses**: `PersonalityWeighting.WeightOf` (mesmo padrão de peso, nunca trava)
**Requirement**: SKILL-13, SKILL-14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Score combina habilidade + personalidade + vaga aberta (todos como peso, nenhum trava)
- [ ] Troca de profissão chama `SwitchProfession`, habilidade antiga preservada (T7 já cobre
      a garantia; aqui só verifica que o sistema não zera nada por conta própria)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥4 testes pass

**Tests**: integração leve
**Gate**: full

---

### T12: Wiring — `ScenarioRunner.DefaultSystems()` + cenário default de habilidade

**What**: insere `SkillPracticeSystem`/`SkillTeachingSystem` entre `EmploymentSystem` e
`ProductionSystem`; adiciona `DefaultSkillsRules` (cap, taxas, `SkillByProfession` para
lavrador=Agriculture, ferreiro=Craft); `NatalitySystem`/`PopulationSeeder` passam a atribuir
`RateGene` (roll sem pais na seed inicial, `Inherit` em nascimento real).
**Where**: `src/LivingWorld.Simulation/ScenarioRunner.cs`, `src/LivingWorld.Simulation/Population/NatalitySystem.cs`, `src/LivingWorld.Simulation/Population/PopulationSeeder.cs` (modificados)
**Depends on**: T8, T9, T10, T11
**Reuses**: comentário de ordem já existente em `ScenarioRunner.cs:18-26` (estende, não reescreve)
**Requirement**: SKILL-01, SKILL-09 (integração)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `DefaultSystems()` lista os 2 sistemas novos na posição documentada
- [ ] Golden hash do cenário default regenerado (mesmo padrão de AD-046/048 na Fase 5)
- [ ] `bash scripts/verify.sh` limpo
- [ ] Test count: suíte inteira em 0 falhas

**Tests**: integração leve
**Gate**: full

**Commit**: `feat(phase-06-skills): wiring — SkillPracticeSystem/SkillTeachingSystem no cenário default`

---

### T13: Teste de cobertura — `SkillByProfession` completo

**What**: teste por reflexão/enumeração que reprova se alguma `ProfessionType` do
`PopulationCatalog` não tem entrada em `SkillsRules.SkillByProfession` (mesmo padrão de
`PersonalityWeighting.AllTraitNames`).
**Where**: `tests/LivingWorld.Tests/Population/SkillsRulesCoverageTests.cs`
**Depends on**: T6, T12
**Reuses**: padrão de `PersonalityWeightingTests` (cobertura por reflexão)
**Requirement**: SKILL-01 (rede de segurança)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Remover uma entrada de `SkillByProfession` do cenário default reprova o teste
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: 1 teste pass

**Tests**: unit
**Gate**: quick

---

### T14: Cenário pareado — especialista vs trocador [P]

**What**: 20 seeds, NPC que trabalha 20 anos na mesma profissão vs NPC de mesma idade/genes
que troca a cada 2 anos; razão média vai para `tests/baselines/`.
**Where**: `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs`
**Depends on**: T13
**Reuses**: `EconomyScenarioHarness` (padrão de harness base/tratamento já existente na Fase 5)
**Requirement**: SKILL-03, SKILL-15

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 20/20 seeds: especialista termina com habilidade maior
- [ ] Razão média persistida em `tests/baselines/`; desvio >±30% loga alerta sem falhar o gate
- [ ] `[Trait("Category","Scenario")]` — fora do gate padrão
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass (20 seeds internas)

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T15: Cenário pareado — mestre-topo vs mestre-piso [P]

**What**: 20 seeds, aprendiz de mestre no topo da faixa vs mestre no piso, idade/genes
fixados.
**Where**: `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs`
**Depends on**: T13
**Reuses**: mesmo harness de T14
**Requirement**: SKILL-08, SKILL-16

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 20/20 seeds: aprendiz de mestre-topo termina com habilidade maior
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T16: Cenário pareado — gene muda resultado, prática idêntica [P]

**What**: 20 seeds em cada sentido — genes diferentes/prática idêntica diverge; genes
idênticos/prática idêntica fica byte-idêntico.
**Where**: `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs`
**Depends on**: T13
**Reuses**: mesmo harness de T14
**Requirement**: SKILL-09

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 20/20 seeds nos dois sentidos
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 2 testes pass (um por sentido)

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T17: Correlação pai/filho — habilidade não herdada, gene herdado [P]

**What**: 200 nascimentos, IC95 de `habilidade(pai)↔habilidade(filho)` contém 0; IC95 de
`RateGene(pai)↔RateGene(filho)` inteiramente acima de 0 — os dois juntos.
**Where**: `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs`
**Depends on**: T13
**Reuses**: `NatalitySystem`/`RateGene.Inherit` (T12)
**Requirement**: SKILL-09

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 200 nascimentos simulados, ambas correlações computadas sobre o mesmo dataset
- [ ] Falha se só uma das duas condições passar (o par é o critério, não cada metade)
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T18: Cenário pareado — oficina rende mais com dono melhor [P]

**What**: base/tratamento mesma seed, mesma entrada, mesmo nº de trabalhadores; tratamento =
dono com habilidade maior; produção anual maior em 10/10 seeds.
**Where**: `tests/LivingWorld.Tests/Economy/ProductionSystemSkillTests.cs`
**Depends on**: T10, T13
**Reuses**: `EconomyScenarioHarness`
**Requirement**: SKILL-10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 10/10 seeds: tratamento produz mais
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T19: Sensores de hash — teto não move o mundo / flag off muda o mundo [P]

**What**: dois testes de `Hash(world)`: (a) ganho aplicado a NPC já no teto não muda o hash;
(b) desligar o sistema de habilidades por flag muda o hash após 10 anos.
**Where**: `tests/LivingWorld.Tests/Population/SkillHashSensorTests.cs`
**Depends on**: T13
**Reuses**: padrão de `EconomyHashScenarioTests`/flag `EconomyRules.Enabled` (mesmo mecanismo
de liga/desliga por cenário, aplicado a `SkillsRules.Enabled`)
**Requirement**: SKILL-01, SKILL-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] (a) hash idêntico com NPC no teto praticando de novo
- [ ] (b) hash diverge com sistema desligado após 10 anos, mesma seed
- [ ] `[Trait("Category","Scenario")]` só no teste (b) (10 anos); (a) pode ser rápido, sem trait
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario` (b) e
      `bash scripts/verify.sh` (a)
- [ ] Test count: 2 testes pass

**Tests**: integração leve (a) + pesada (Scenario, b)
**Gate**: full (a) / Scenario (b)

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T3 ──→ T4 ──→ T5
  T1 ──────────────────→ T2 [P after T1]
  T1,T3,T4 ──→ T6

Phase 2 (Sequential — mesmo arquivo Npc.cs):
  T4,T5 ──→ T7

Phase 3 (Parallel após T6,T7):
  T6,T7 ──┬── T8 [P]
          ├── T9 [P]
          ├── T10 [P]
          └── T11 [P]

Phase 4 (Sequential):
  T8,T9,T10,T11 ──→ T12 ──→ T13

Phase 5 (Parallel após T13, Category=Scenario):
  T13 ──┬── T14 [P]
        ├── T15 [P]
        ├── T16 [P]
        ├── T17 [P]
        ├── T18 [P] (depende também de T10)
        └── T19 [P]
```

---

## Task Granularity Check

| Task | Scope | Status |
|---|---|---|
| T1: `SkillType` enum | 1 arquivo, 1 tipo | ✅ Granular |
| T2: `SkillCurve` | 1 função pura | ✅ Granular |
| T3: `SkillGainSource` enum | 1 arquivo, 1 tipo | ✅ Granular |
| T4: `SkillSet` | 1 classe, 3 métodos | ✅ Granular |
| T5: `RateGene` | 1 record, 3 métodos | ✅ Granular |
| T6: `SkillsRules` | 1 record, 2 métodos | ✅ Granular |
| T7: `Npc` extensões | 1 arquivo, 3 campos + 3 mutadores (cohesivo — mesmo objeto) | ⚠️ OK — cohesivo, mesmo padrão dos mutadores existentes no mesmo arquivo |
| T8: `SkillPracticeSystem` | 1 classe, 1 responsabilidade | ✅ Granular |
| T9: `SkillTeachingSystem` | 1 classe, 5 métodos privados relacionados (mesma passada Daily, ver Risks do design) | ⚠️ OK — justificado no design.md (splitar viraria 5 iterações completas do mundo) |
| T10: `ProductionSystem` modificado | 1 método modificado | ✅ Granular |
| T11: `BehaviorDecisionSystem` modificado | 1 responsabilidade nova (score de profissão) | ✅ Granular |
| T12: Wiring | 3 arquivos, 1 concern (integração do pipeline) | ⚠️ OK — wiring é inerentemente multi-arquivo, sem lógica nova |
| T13: Teste de cobertura | 1 arquivo de teste | ✅ Granular |
| T14-T19: Cenários pareados | 1 teste (ou par) por critério do roadmap | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T1 | None | None | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T1 | T1 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T1, T3, T4 | T1,T3,T4 → T6 | ✅ Match |
| T7 | T4, T5 | T4,T5 → T7 | ✅ Match |
| T8 | T6, T7 | T6,T7 → T8 [P] | ✅ Match |
| T9 | T6, T7 | T6,T7 → T9 [P] | ✅ Match |
| T10 | T6, T7 | T6,T7 → T10 [P] | ✅ Match |
| T11 | T7 | T6,T7 → T11 [P] (T6 não usado por T11, mas presente no bloco — não quebra: T11 só depende de T7) | ✅ Match (dependência de T11 é subconjunto do bloco) |
| T12 | T8, T9, T10, T11 | T8,T9,T10,T11 → T12 | ✅ Match |
| T13 | T6, T12 | T12 → T13 (T6 transitiva via T12) | ✅ Match |
| T14 | T13 | T13 → T14 [P] | ✅ Match |
| T15 | T13 | T13 → T15 [P] | ✅ Match |
| T16 | T13 | T13 → T16 [P] | ✅ Match |
| T17 | T13 | T13 → T17 [P] | ✅ Match |
| T18 | T10, T13 | T13 → T18 [P] (depende também de T10) — anotado explicitamente no diagrama | ✅ Match |
| T19 | T13 | T13 → T19 [P] | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T1 | Config/entidade (enum) | none | none | ✅ OK |
| T2 | Domain — função pura | unit | unit | ✅ OK |
| T3 | Config/entidade (enum) | none | none | ✅ OK |
| T4 | Domain — value object | unit | unit | ✅ OK |
| T5 | Domain — value object | unit | unit | ✅ OK |
| T6 | Domain — value object | unit | unit | ✅ OK |
| T7 | Domain — `Npc` | unit | unit | ✅ OK |
| T8 | Simulation — sistema | integração leve | integração leve | ✅ OK |
| T9 | Simulation — sistema | integração leve | integração leve | ✅ OK |
| T10 | Simulation — sistema (modificado) | integração leve | integração leve | ✅ OK |
| T11 | Simulation — sistema (modificado) | integração leve | integração leve | ✅ OK |
| T12 | Simulation — wiring | integração leve | integração leve | ✅ OK |
| T13 | Domain — teste de cobertura | unit | unit | ✅ OK |
| T14 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T15 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T16 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T17 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T18 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T19 | Simulation + Scenario (misto) | integração leve + Scenario | integração leve (a) + pesada (b) | ✅ OK |

Nenhuma ❌ — todas as tasks aprovadas para apresentação.
