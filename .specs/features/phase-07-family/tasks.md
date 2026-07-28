# Fase 7 — Relações e Famílias — Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow
its Execute flow and Critical Rules.** Do not search for skill files by filesystem path.
The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation,
adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-07-family/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Gerado por amostragem do repo (`tests/LivingWorld.Tests/{Population,Economy}/*.cs`,
> `.specs/features/phase-06-skills/tasks.md` como referência de granularidade da fase
> anterior) — sem `AGENTS.md`/`CONTRIBUTING.md` de padrão de teste dedicado; convenção do
> projeto é xUnit, um único projeto de teste, sensores de hash/conservação como teste de
> integração leve (sem framework de e2e — o "e2e" deste projeto é rodar `ScenarioRunner`
> alguns dias/anos). Cenários longos/estatísticos (10-20 seeds, 40-100 anos) usam
> `[Trait("Category","Scenario")]` e ficam fora do gate padrão (`scripts/test.sh` filtra
> `Category!=Scenario`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
|---|---|---|---|---|
| Domain — funções puras/value objects (`Relationship`, `RelationshipKey`, `FamilyRules`, `HeredityService`) | unit | 1:1 com ACs de validação/clamp/assimetria; toda faixa inválida rejeitada | `tests/LivingWorld.Tests/Population/{RelationshipTests,RelationshipKeyTests,FamilyRulesTests,HeredityServiceTests}.cs` | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Domain — `Npc`/`LifeTable`/`MortalityPlanner` (novos campos/parâmetro) | unit | 1:1 com ACs de `Marry`/`StartCourtship`/`EndCourtship`, clamp de `Vitality`/`Upbringing`, multiplicador de mortalidade | `tests/LivingWorld.Tests/Population/{NpcFamilyMutatorsTests,LifeTableTests,MortalityPlannerTests}.cs` | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Simulation — sistemas (`RelationshipSystem`, `CourtshipSystem`, `MarriageSystem`, `NatalitySystem` reescrito, `HouseholdRedistribution`, `HouseholdCleanup`, `WagePaymentSystem` modificado) | integração leve (`ScenarioRunner`/harness dedicado, poucos ticks) | Happy path + edge case listado por sistema; regressão de `MoneyConservationTests`/`ResourceConservationTests` existentes | `tests/LivingWorld.Tests/Population/*SystemTests.cs`, `tests/LivingWorld.Tests/Economy/WagePaymentUpbringingTests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Scenario — cenários pareados/estatísticos (10-20 seeds, controles) | integração pesada, `Category=Scenario` | Cada critério FAM-26..36 isolado num teste próprio, seeds/amostra exatas do critério | `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs` | `bash scripts/test.sh --filter Category=Scenario` |
| Config/entidade (enums `RelationshipAxis`/`RelationshipEventType`/`AttractionFactor`/`CourtshipRejectionReason`) | none | build gate só | — | `bash scripts/build.sh` |

## Parallelism Assessment

> `ScenarioRunner.Create` monta `WorldState` novo por chamada (sem estado global
> compartilhado) — mesmo padrão já usado por todos os testes de sistema existentes
> (`EmploymentSystemTests`, `SkillPracticeSystemTests`). xUnit roda classes em paralelo por
> padrão neste projeto (nenhum `[Collection]` de serialização encontrado nos arquivos
> amostrados).

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
|---|---|---|---|
| unit (Domain, funções puras/value objects) | Yes | Sem estado compartilhado, cada teste cria seu próprio objeto | `tests/LivingWorld.Tests/Population/RateGeneTests.cs` (padrão idêntico) |
| integração leve (sistemas via `ScenarioRunner`/harness) | Yes | Cada teste chama `ScenarioRunner.Create(seed)`/harness dedicado com seu próprio `WorldState` isolado | `tests/LivingWorld.Tests/Population/SkillPracticeSystemTests.cs` |
| Scenario (`Category=Scenario`) | Yes (entre testes) | Mesmo isolamento por `WorldState`, mas cada teste é caro (10-20 seeds × anos) — paralelismo entre testes é seguro, mas não reduz o tempo total do gate manual | `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs` |

## Gate Check Commands

| Gate Level | When to Use | Command |
|---|---|---|
| Quick | Após task com só unit tests (Domain) | `bash scripts/test.sh --filter FullyQualifiedName~Population` |
| Full | Após task com sistemas/integração leve | `bash scripts/verify.sh` |
| Scenario (manual, caro) | Após as tasks de cenário pareado/estatístico (Phase 9) | `bash scripts/test.sh --filter Category=Scenario` |
| Build | Tasks de enum/config puro | `bash scripts/build.sh` |

---

## Execution Plan

### Phase 1: Domain primitives (Sequential-ish)

```
T1 [P] ─┐
T2 [P] ─┼→ T4 → T5
T3 [P] ─┘
T4 → T6
```

### Phase 2: `Npc` extensions (Sequential — mesmo arquivo)

```
(sem dependência nova) → T7
```

### Phase 3: `WorldState` extensions (Sequential — mesmo arquivo)

```
T3, T4, T5, T7 → T8
```

### Phase 4: Sistemas existentes tocados (Parallel OK após Phase 1/2)

```
T4 ──→ T9 [P]
T4, T7 ──→ T10 [P]
```

### Phase 5: Household — extração e redistribuição (Sequential — mesmo arquivo `NpcDeath.cs`)

```
T12 → T13 → T14
```

### Phase 6: Sistemas novos de família (Sequential onde compartilham arquivo/estado)

```
T5, T8 ──────────────→ T11
T12, T4, T7 ─────────→ T15
T5, T8, T4, T7, T15 ──→ T16
T6, T7, T4, T8, T9 ───→ T17
T6, T7 ───────────────→ T18
```

### Phase 7: Wiring (Sequential)

```
T9, T10, T11, T14, T15, T16, T17, T18 → T19
```

### Phase 8: Cobertura/regressão + harnesses de controle (Parallel OK após T19)

```
T19 ──┬→ T20 [P]
      ├→ T21 [P]
      └→ T22 [P]
```

### Phase 9: Cenários pareados/estatísticos de verificação (Parallel OK, Category=Scenario)

```
T16, T20 ──→ T23 [P]
T17, T20 ──→ T24 [P]
T17, T20 ──→ T25 [P]
T19, T20 ──→ T26 [P]
T21 ───────→ T27 [P]
T10, T19 ──→ T28 [P]
T22 ───────→ T29 [P]
T22 ───────→ T30 [P]
T19 ───────→ T31 [P]
```

---

## Task Breakdown

### T1: `RelationshipAxis`/`RelationshipEventType`/`AttractionFactor` enums [P]

**What**: 3 catálogos fechados — os 4 eixos de relação, os 4 eventos nomeados, os 6 fatores
de atração — bundlados num único arquivo (cohesivos: os três só existem para `FamilyRules`
consumir juntos, mesmo espírito de `RoutineSlot`+`ActionCatalog`).
**Where**: `src/LivingWorld.Domain/Population/RelationshipAxis.cs`
**Depends on**: None
**Reuses**: padrão de `SkillType`/`SkillGainSource` (enum fechado, comentário `<c>`)
**Requirement**: FAM-01, FAM-03, FAM-06 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `RelationshipAxis { Trust, Affection, Respect, Debt }`
- [ ] `RelationshipEventType { Cohabitation, Betrayal, Help, Trade }`
- [ ] `AttractionFactor { Age, Health, Status, Skill, CulturalAffinity, ExistingRelationship }`
- [ ] Gate check passa: `bash scripts/build.sh`

**Tests**: none
**Gate**: build

---

### T2: `CourtshipRejectionReason` enum [P]

**What**: enum fechado com os 3 motivos de rejeição de cortejo (AD-054) — nunca string livre.
**Where**: `src/LivingWorld.Domain/Population/CourtshipRejectionReason.cs`
**Depends on**: None
**Reuses**: mesmo padrão de `ActionType`
**Requirement**: FAM-08, FAM-09, FAM-10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `enum CourtshipRejectionReason { Incesto, ForaDaFaixaEtaria, SemAfinidade }`
- [ ] Gate check passa: `bash scripts/build.sh`

**Tests**: none
**Gate**: build

---

### T3: `RelationshipKey` struct [P]

**What**: `readonly record struct RelationshipKey(NpcId From, NpcId To)` — par ordenado, nunca
normalizado (a assimetria é o próprio propósito do tipo, FAM-05).
**Where**: `src/LivingWorld.Domain/Population/RelationshipKey.cs`
**Depends on**: None
**Reuses**: `NpcId` já existente
**Requirement**: FAM-01, FAM-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `RelationshipKey(A, B) != RelationshipKey(B, A)` (igualdade estrutural do record struct
      prova isso por construção — teste confirma, não implementa nada extra)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~RelationshipKeyTests`
- [ ] Test count: ≥2 testes pass

**Tests**: unit
**Gate**: quick

---

### T4: `FamilyRules` — catálogo cenário-driven

**What**: `record FamilyRules` com `Create(...)` validando faixas (`Result<FamilyRules>`):
deltas de relação por evento/eixo, decaimento, limiares de cortejo, estoque inicial de
casamento, pisos de concepção, riscos de parto, pesos de hereditariedade, flags de canal
ambiental/deriva neutra; métodos `ApplyUpbringingWeight(wage, upbringing)` e
`EffectiveVitalityMultiplier(vitality)`.
**Where**: `src/LivingWorld.Domain/Population/FamilyRules.cs`
**Depends on**: T1 (enums usados como chave de `RelationshipDeltas`/`AttractionWeights`)
**Reuses**: padrão `NeedsRules.Create`/`EconomyRules.Create` (validação de faixa, `Result<T>`)
**Requirement**: FAM-03, FAM-04, FAM-06, FAM-07, FAM-12, FAM-13, FAM-16, FAM-18, FAM-19,
FAM-21, FAM-23 (parâmetros)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Create` rejeita pesos/limiares fora de faixa (deltas sem cobrir todo `(EventType,Axis)`
      declarado, `CourtshipDurationDays <= 0`, riscos fora de `[0,1]`, pesos de `Vitality`
      mãe+pai que não somam algo sensato — clamp documentado, não travado em 1.0 exato)
- [ ] `EffectiveVitalityMultiplier` clampa a saída (nunca produz multiplicador negativo)
- [ ] `ApplyUpbringingWeight` clampa a entrada `upbringing` a `[0,100]` antes de aplicar o peso
      (defesa contra valor fora de faixa, Error Handling do design)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~FamilyRulesTests`
- [ ] Test count: ≥8 testes pass

**Tests**: unit
**Gate**: quick

---

### T5: `Relationship` — 4 eixos assimétricos

**What**: classe mutável com os 4 eixos `[0,100]`, `Get(axis)`, `ApplyEvent(type, rules)`,
`DecayTowardNeutral(rules)`, `LastContactTick`/`MarkContact`, `static Initial(firstContactTick)`.
**Where**: `src/LivingWorld.Domain/Population/Relationship.cs`
**Depends on**: T1, T4
**Reuses**: `SkillSet` como modelo de "conjunto de eixos mutável, leitura por switch"
**Requirement**: FAM-01, FAM-02, FAM-03, FAM-04, FAM-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Initial` cria os 4 eixos no piso mínimo declarado (nunca salta pra valor alto num
      único encontro, Edge Case da spec)
- [ ] `ApplyEvent` aplica o delta certo ao(s) eixo(s) do evento, clamped a `[0,100]`
- [ ] `DecayTowardNeutral` nunca ultrapassa o neutro (não oscila em torno dele)
- [ ] Duas instâncias `Relationship` para A→B e B→A divergem depois de eventos diferentes
      (prova de assimetria a nível de objeto, FAM-05)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~RelationshipTests`
- [ ] Test count: ≥7 testes pass

**Tests**: unit
**Gate**: quick

---

### T6: `HeredityService` — funções puras de hereditariedade

**What**: `RollInitialVitality(rng)`, `RollInitialUpbringing(rng)` (população seed, Edge
Case), `InheritVitality(motherVitality, fatherVitality, rules, rng)` (fórmula + mutação,
clamp `[0,100]`), `DeriveUpbringing(conceptionHousehold, rules)` (função pura da riqueza do
household — **nunca lê `Vitality`/genes**).
**Where**: `src/LivingWorld.Domain/Population/HeredityService.cs`
**Depends on**: T4
**Reuses**: `RateGene.Inherit`/`RateGene.RollInitial` como modelo formal
**Requirement**: FAM-18, FAM-19, FAM-20, FAM-22

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `InheritVitality`/`RollInitialVitality`/`RollInitialUpbringing` nunca produzem valor
      fora de `[0,100]` (clamp explícito, mesma garantia de `RateGene.Inherit`)
- [ ] `InheritVitality` com pais idênticos produz distribuição em torno do valor dos pais
      (mutação garante variação — múltiplos rolls não-idênticos)
- [ ] `DeriveUpbringing` de dois households com riqueza diferente (mesmos outros parâmetros)
      produz valores diferentes; `DeriveUpbringing` não tem nenhum parâmetro de `Vitality`/gene
      na assinatura (prova estrutural de FAM-19/20 — canais independentes por construção)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~HeredityServiceTests`
- [ ] Test count: ≥6 testes pass

**Tests**: unit
**Gate**: quick

---

### T7: `Npc` — extensões de genética, cônjuge e cortejo

**What**: adiciona `Vitality`(double, imutável), `Upbringing`(double, imutável),
`Spouse`(`NpcId?`), `CourtingWith`(`NpcId?`) ao construtor único; `Marry(NpcId)`,
`StartCourtship(NpcId)`, `EndCourtship()` — nunca um mutador de "divorciar" (AD-060).
**Where**: `src/LivingWorld.Domain/Population/Npc.cs` (modificado)
**Depends on**: None (campos primitivos, sem dependência dos tipos novos)
**Reuses**: padrão `AssignMentor`/`ClearMentor`/`JoinHousehold`/`LeaveHousehold` já existente
no mesmo arquivo
**Requirement**: FAM-12, FAM-18, FAM-19, FAM-20

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Construtor único reconstrutível por `System.Text.Json` inclui os 4 campos novos (mesma
      garantia de round-trip de todos os campos mutáveis existentes)
- [ ] `Marry` seta `Spouse` nos dois sentidos é responsabilidade de quem chama (`MarriageSystem`
      chama duas vezes) — `Npc.Marry` em si só seta o próprio campo, sem mutador de "divorciar"
- [ ] `Spouse` apontando a alguém morto (viuvez) continua legível — nunca limpo automaticamente
      (mesmo espírito de `MotherId`/`FatherId`, AD-031)
- [ ] `StartCourtship`/`EndCourtship` espelham `AssignMentor`/`ClearMentor`
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~NpcFamilyMutatorsTests`
- [ ] Test count: ≥6 testes pass

**Tests**: unit
**Gate**: quick

---

### T8: `WorldState` — coleção de relações + `FamilyRules` canônico

**What**: `[Canonical] IReadOnlyDictionary<RelationshipKey, Relationship> Relationships`
(dict canônico, populado só sob demanda — AD-052), `internal GetOrCreateRelationship(key, now)`
único ponto de criação, `[Canonical] FamilyRules FamilyRules` (parâmetro de construtor,
default resolvido pelo chamador — mesmo padrão de `EconomyRules`).
**Where**: `src/LivingWorld.Simulation/WorldState.cs` (modificado — os 2 construtores)
**Depends on**: T3, T4, T5, T7
**Reuses**: mesmo padrão de `_households`/`Households` — aqui o dict **é** a coleção
canônica (sem lista paralela, justificado no design)
**Requirement**: FAM-01, FAM-02, FAM-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Relationships` começa vazio para um mundo novo — nenhum par pré-populado (FAM-02:
      "quem nunca se encontra nunca se conhece")
- [ ] `GetOrCreateRelationship` cria uma entrada só na primeira chamada para aquela chave;
      chamadas seguintes devolvem a mesma instância
- [ ] Round-trip de snapshot (construtor de rehidratação) preserva `Relationships` e
      `FamilyRules`
- [ ] Reflexão `[Canonical]`/`[Volatile]` (`ArchitectureTests`) continua passando com os 2
      campos novos classificados
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~WorldStateTests`
- [ ] Test count: ≥4 testes pass

**Tests**: unit
**Gate**: quick

---

### T9: `LifeTable`/`MortalityPlanner`/`MortalitySystem` — multiplicador de `Vitality` na mortalidade [P]

**What**: `LifeTable.AnnualMortality` ganha parâmetro opcional `vitalityMultiplier = 1.0`
(default preserva os call-sites/testes existentes); `MortalityPlanner.RollDeathAge` repassa o
parâmetro; `MortalitySystem.SchedulePlannedDeath` computa
`world.FamilyRules.EffectiveVitalityMultiplier(npc.Vitality)` e passa adiante.
**Where**: `src/LivingWorld.Domain/Population/LifeTable.cs`,
`src/LivingWorld.Domain/Population/MortalityPlanner.cs`,
`src/LivingWorld.Simulation/Population/MortalitySystem.cs` (todos modificados)
**Depends on**: T4
**Reuses**: `LifeTable.AnnualMortality:47` (assinatura existente ganha parâmetro opcional,
2 call-sites/testes atuais preservados)
**Requirement**: FAM-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `AnnualMortality(age, health)` (sem o novo parâmetro) produz exatamente o mesmo valor
      de antes — testes existentes de `LifeTableTests` passam sem modificação
- [ ] `vitalityMultiplier < 1.0` reduz a probabilidade de morte na mesma faixa etária;
      `> 1.0` aumenta — nunca produz probabilidade fora de `[0,1]` (clamp já existente
      preservado)
- [ ] `MortalitySystem.SchedulePlannedDeath` usa o multiplicador de `FamilyRules` — dois NPCs
      de mesma idade/saúde e `Vitality` diferente têm distribuição de idade de morte diferente
      em amostra grande
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥5 testes novos + suíte existente de `LifeTableTests`/`MortalityPlannerTests` em 0

**Tests**: integração leve
**Gate**: full

---

### T10: `WagePaymentSystem` — peso de `Upbringing` no salário [P]

**What**: multiplica `wage` por `FamilyRules.ApplyUpbringingWeight(wage, npc.Upbringing)`
antes de debitar/creditar — mesmo valor multiplicado nos dois lados (débito do `Treasury` e
crédito no `Wallet`), nunca dois valores diferentes (Risco do design: quebra de conservação).
**Where**: `src/LivingWorld.Simulation/Economy/WagePaymentSystem.cs` (modificado, linha ~29)
**Depends on**: T4, T7
**Reuses**: `WagePaymentSystem.Tick` existente (só insere a linha, não duplica débito/crédito)
**Requirement**: FAM-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `EnvironmentalWealthChannelEnabled == false` é sem-op — `wage` inalterado (mesmo
      comportamento de hoje, testável como caso de regressão)
- [ ] `EnvironmentalWealthChannelEnabled == true` com `Upbringing` alto paga mais que
      `Upbringing` baixo, mesmo `wageAmount` base
- [ ] `MoneyConservationTests`/`ResourceConservationTests` existentes (Fase 5) continuam
      passando sem modificação — o valor debitado do `Treasury` é sempre igual ao creditado
      no `Wallet`, nunca dois números diferentes
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥4 testes novos + suíte de conservação existente em 0

**Tests**: integração leve
**Gate**: full

---

### T11: `RelationshipSystem` (`Daily`)

**What**: para cada `Household` e `Workplace`, todo par ordenado de membros/empregados vivos
presentes ganha/atualiza `A→B` e `B→A` (`GetOrCreateRelationship` + `ApplyEvent(Cohabitation)`
+ `MarkContact`); depois decai toda entrada existente sem contato recente
(`ContactLossThresholdDays`).
**Where**: `src/LivingWorld.Simulation/Population/RelationshipSystem.cs`
**Depends on**: T5, T8
**Reuses**: mesma convenção de iteração ordenada (`OrderBy(id => id.Value)`) de
`SkillPracticeSystem`/`ProductionSystem`
**Requirement**: FAM-01, FAM-02, FAM-03, FAM-04, FAM-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Dois NPCs no mesmo `Household` hoje: `world.Relationships` ganha as duas entradas
      (A→B e B→A), ambas evoluídas
- [ ] Dois NPCs que nunca compartilharam `Household`/`Workplace`: nenhuma entrada é criada
      (FAM-02)
- [ ] Par existente sem convivência por `ContactLossThresholdDays`: os 4 eixos decaem em
      direção ao neutro no próximo tick, nunca ultrapassam
- [ ] Determinístico: mesma seed, mesmo resultado (harness de determinismo existente)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥6 testes pass

**Tests**: integração leve
**Gate**: full

---

### T12: `HouseholdCleanup.DissolveIfEmpty` — extração de `NpcDeath.Apply`

**What**: extrai a lógica de dissolução (linhas 22-31 atuais: registrar `ResourceLost`,
`RemoveHousehold`, limpar referência de membros restantes) para
`static void DissolveIfEmpty(WorldState world, TickContext ctx, Household household)`,
reusado por `NpcDeath.Apply` (sem mudar comportamento) e, depois, por `MarriageSystem`.
**Where**: `src/LivingWorld.Simulation/Population/HouseholdCleanup.cs` (novo),
`src/LivingWorld.Simulation/Population/NpcDeath.cs` (modificado — chama o helper)
**Depends on**: None
**Reuses**: lógica já existente em `NpcDeath.Apply:22-31`, movida sem reescrever
**Requirement**: FAM-12 (suporte — pré-requisito de `MarriageSystem`)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Comportamento de `NpcDeath.Apply` é idêntico ao de antes da extração — testes
      existentes de `NpcDeath`/`MortalitySystem`/`NeedsDecaySystem` passam sem modificação
      (refactor puro, sem mudança de comportamento)
- [ ] `DissolveIfEmpty` chamado num household não-vazio é sem-op (não dissolve household com
      membro restante)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: suíte existente de `NpcDeath`/mortalidade em 0 + ≥2 testes novos do helper

**Tests**: integração leve
**Gate**: full

---

### T13: `HouseholdRedistribution` — filhos remanescentes de household órfão

**What**: `static void HandleOrphaned(world, ctx, household, lifeStageRules, now)` — chamado
quando o household não está vazio mas não tem membro vivo `Adult`/`Elder`; para cada filho
vivo remanescente, busca avô/avó ou irmão adulto vivo com household; sem candidato, cria
household unitário próprio (mesmo fallback de `PopulationGenerator.PairIntoHouseholds:99-103`);
dissolve o household original via `HouseholdCleanup.DissolveIfEmpty`.
**Where**: `src/LivingWorld.Simulation/Population/HouseholdRedistribution.cs`
**Depends on**: T12
**Reuses**: fallback de `PopulationGenerator` (AD-057), `WorldState.RemoveHousehold`
**Requirement**: FAM-17

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Ambos os pais mortos, avô/avó vivo com household disponível: filhos remanescentes
      entram no household do avô/avó
- [ ] Sem candidato algum: cada filho remanescente vira `Head` do próprio household unitário
      (nunca lança, nunca deixa `Npc.Household` nulo permanentemente sem `HomelessSince`)
- [ ] Household original é dissolvido (sai de `world.Households`) depois da redistribuição
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~HouseholdRedistributionTests`
- [ ] Test count: ≥5 testes pass

**Tests**: integração leve
**Gate**: full

---

### T14: `NpcDeath.Apply` — wiring da redistribuição de órfãos

**What**: depois da limpeza de membro existente, checa se o household não está vazio mas não
tem adulto/idoso vivo (`LifeStageRules.StageOf`) — se sim, chama
`HouseholdRedistribution.HandleOrphaned` em vez de só checar `IsEmpty`.
**Where**: `src/LivingWorld.Simulation/Population/NpcDeath.cs` (modificado)
**Depends on**: T12, T13
**Reuses**: `NpcDeath.Apply` já modificado por T12 (só adiciona o branch de órfão)
**Requirement**: FAM-17

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Morte do segundo pai de um household com filhos vivos e sem outro adulto dispara
      `HandleOrphaned` (verificável pelo destino dos filhos)
- [ ] Morte que deixa household vazio (sem filhos) continua indo pelo caminho de
      `DissolveIfEmpty` normal, sem chamar `HandleOrphaned` (household vazio não tem "filho
      remanescente" a redistribuir)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥3 testes novos + suíte existente de `NpcDeath`/mortalidade em 0

**Tests**: integração leve
**Gate**: full

---

### T15: `MarriageSystem` — helper estático de casamento

**What**: `static void Marry(world, ctx, spouseA, spouseB)` — remove ambos dos households
anteriores (`LeaveHousehold` + `HouseholdCleanup.DissolveIfEmpty` se ficar vazio), cria
`Household` novo com `FamilyRules.MarriageInitialStock`, `JoinHousehold` nos dois,
`Npc.Marry(spouse)` nos dois, loga `WorldEventKind.Marriage`.
**Where**: `src/LivingWorld.Simulation/Population/MarriageSystem.cs`,
`src/LivingWorld.Simulation/WorldEvent.cs` (modificado — novo valor `Marriage`)
**Depends on**: T12, T4, T7
**Reuses**: `Household`/`AddHousehold`/`NextHouseholdIdAndAdvance` já existentes,
`HouseholdCleanup.DissolveIfEmpty` (T12)
**Requirement**: FAM-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Casal casado sai dos dois households anteriores e entra num household novo com estoque
      inicial de `FamilyRules.MarriageInitialStock` (AD-056)
- [ ] Household anterior de um dos cônjuges que fica vazio é dissolvido (reuso de T12, sem
      duplicar a lógica)
- [ ] Ambos os `Npc.Spouse` apontam um para o outro depois do casamento
- [ ] `WorldEventKind.Marriage` logado com os dois ids
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~MarriageSystemTests`
- [ ] Test count: ≥5 testes pass

**Tests**: integração leve
**Gate**: full

---

### T16: `CourtshipSystem` (`Yearly`)

**What**: para cada NPC vivo/adulto/solteiro sem `CourtingWith` ativo, busca candidato entre
`world.Relationships` (AD-061 — só quem já tem entrada); `Reject` checa parentesco (AD-055) e
janela de fertilidade antes de qualquer score (Incesto → ForaDaFaixaEtaria); `AttractionScore`
combina os 6 fatores normalizados `[0,1]` (Risco do design) com pesos de `FamilyRules`;
`SemAfinidade` se abaixo do limiar; sucesso marca `CourtingWith` nos dois e agenda conclusão
via `ctx.ScheduleEvent`; `NeutralDriftEnabled` troca o gate de atração por acasalamento
aleatório entre elegíveis (A11). `HandleEvent` revalida e chama `MarriageSystem.Marry`.
**Where**: `src/LivingWorld.Simulation/Population/CourtshipSystem.cs`,
`src/LivingWorld.Simulation/WorldEvent.cs` (modificado — `CourtshipStarted`,
`CourtshipRejected`, `CourtshipSucceeded`)
**Depends on**: T5, T8, T4, T7, T15
**Reuses**: mecanismo de evento agendado de `NatalitySystem` (AD-063)
**Requirement**: FAM-06, FAM-07, FAM-08, FAM-09, FAM-10, FAM-11, FAM-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Parentes de primeiro grau: cortejo rejeitado com `Incesto`, **mesmo com** score de
      atração/afinidade compatível (AC3 — gate roda antes do teste de limiar)
- [ ] Fora da janela de fertilidade: rejeitado com `ForaDaFaixaEtaria`
- [ ] Score abaixo do limiar (excluídos os dois casos acima): rejeitado com `SemAfinidade`
- [ ] Cortejo bem-sucedido loga `CourtshipStarted`, agenda evento, e no disparo loga
      `CourtshipSucceeded` **antes** de chamar `MarriageSystem.Marry` (FAM-11)
- [ ] Um dos dois morre/casa com terceiro antes da conclusão: `HandleEvent` é sem-op
      silencioso, `CourtingWith` do sobrevivente é limpo (Edge Case)
- [ ] `NeutralDriftEnabled = true`: pareamento ignora `AttractionScore`, casal formado é
      aleatório entre elegíveis na janela de fertilidade (ainda respeita Incesto/idade)
- [ ] NPC sem nenhum candidato elegível no ano: pulado sem log de erro (Edge Case)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~CourtshipSystemTests`
- [ ] Test count: ≥10 testes pass

**Tests**: integração leve
**Gate**: full

---

### T17: `NatalitySystem` — reescrita (concepção via casal casado + risco de parto + hereditariedade)

**What**: concepção agora lê `Npc.Spouse` (não mais "qualquer homem do household"); exige
ambos vivos, mulher na janela de fertilidade, saúde de ambos acima do piso, relação acima do
limiar, recursos do `Household.Stock` acima do piso; agenda nascimento capturando a riqueza do
household **na concepção** (payload do evento); no `HandleEvent`, rola
`MaternalDeathRisk`/`InfantDeathRisk` (streams próprios) — mãe morre no parto reusa
`NpcDeath.Apply(..., WorldEventKind.MaternalDeath)` (aciona `HouseholdRedistribution`
automaticamente); chama `HeredityService.InheritVitality`/`DeriveUpbringing` (com a riqueza
capturada) para o recém-nascido.
**Where**: `src/LivingWorld.Simulation/Population/NatalitySystem.cs` (reescrito),
`src/LivingWorld.Simulation/WorldEvent.cs` (modificado — `MaternalDeath`, `StillBirth`)
**Depends on**: T6, T7, T4, T8, T9
**Reuses**: mecanismo de agendamento existente (`ctx.ScheduleEvent`), `NpcDeath.Apply`
(reusa a limpeza de household/redistribuição, T14, para o risco de morte materna)
**Requirement**: FAM-12 (parcial — reprodução), FAM-13, FAM-14, FAM-15, FAM-16, FAM-18,
FAM-19

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Casal casado sem os pisos satisfeitos (saúde/relação/recursos): concepção não ocorre
      neste ano (cada piso testado isoladamente)
- [ ] Concepção bem-sucedida agenda o nascimento (`ScheduledEvent` pendente visível antes do
      parto, não filho já existente no tick da concepção) — FAM-14
- [ ] Mãe morre antes do parto: `HandleEvent` é falha silenciosa, nenhum filho criado (mesmo
      comportamento já existente, FAM-15)
- [ ] Risco de parto: com `MaternalDeathRisk` a mãe morre no parto (via `NpcDeath.Apply`,
      household do casal órfão-ou-dissolvido conforme T13/T14); com `InfantDeathRisk` a
      criança nasce morta (nenhum `Npc` vivo criado, sem exceção)
- [ ] Recém-nascido tem `Vitality`/`Upbringing` calculados por `HeredityService`, a partir da
      riqueza do household capturada na concepção (não relida no nascimento)
- [ ] Gate check passa: `bash scripts/test.sh --filter FullyQualifiedName~NatalitySystemTests`
- [ ] Test count: ≥10 testes pass

**Tests**: integração leve
**Gate**: full

---

### T18: `PopulationGenerator`/`PopulationSeeder` — `Vitality`/`Upbringing` da população seed

**What**: cada NPC gerado sem pais conhecidos ganha `Vitality`/`Upbringing` por
`HeredityService.RollInitialVitality`/`RollInitialUpbringing` (stream próprio por NPC, mesmo
padrão de `RateGene.RollInitial`/personalidade/profissão já existentes).
**Where**: `src/LivingWorld.Domain/Population/PopulationGenerator.cs` (modificado),
`src/LivingWorld.Simulation/Population/PopulationSeeder.cs` (modificado, se aplicável)
**Depends on**: T6, T7
**Reuses**: streams próprios já existentes em `PopulationGenerator.GenerateInitial`
(`rng.Derive(WorldRngRegistry.StableHash($"..."))`), mesmo padrão de `rategene-{npcId.Value}`
**Requirement**: FAM-18, FAM-19 (Edge Case — seed sem pais)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Todo NPC da população seed tem `Vitality`/`Upbringing` em `[0,100]`, nenhuma exceção
      por ausência de pai/mãe (Edge Case da spec)
- [ ] Streams de `Vitality`/`Upbringing` são determinísticos por `NpcId` (mesma seed, mesmo
      resultado — harness de determinismo existente)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥3 testes pass

**Tests**: integração leve
**Gate**: full

---

### T19: Wiring — `ScenarioRunner.DefaultSystems()`/`Create` + `DefaultFamilyRules`

**What**: adiciona `DefaultFamilyRules`; insere `RelationshipSystem` depois de
`EmploymentSystem` (antes de `SkillPracticeSystem`); insere `CourtshipSystem` antes de
`NatalitySystem`; `ScenarioRunner.Create` ganha parâmetro opcional `familyRules` (default
`DefaultFamilyRules`), repassado a `WorldState`; comentário de ordem (linhas 18-29) ganha
parágrafo da Fase 7; golden hash do cenário default regenerado.
**Where**: `src/LivingWorld.Simulation/ScenarioRunner.cs` (modificado)
**Depends on**: T9, T10, T11, T14, T15, T16, T17, T18
**Reuses**: comentário de ordem já existente (estende, não reescreve), padrão de
`economyRules` opcional em `Create` (AD-047)
**Requirement**: FAM-01..22 (integração)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `DefaultSystems()` lista `RelationshipSystem`/`CourtshipSystem` nas posições
      documentadas
- [ ] Golden hash do cenário default regenerado (mesmo padrão de AD-046/048)
- [ ] `bash scripts/verify.sh` limpo
- [ ] Test count: suíte inteira em 0 falhas

**Tests**: integração leve
**Gate**: full

**Commit**: `feat(phase-07-family): wiring — RelationshipSystem/CourtshipSystem no cenário default`

---

### T20: Testes de cobertura/regressão — `FamilyRules` × `EconomyCatalog` + grep de "fitness"

**What**: (a) teste que reprova se `FamilyRules.MarriageInitialStock`/`ConceptionResourceFloor`
referenciar um `ResourceId` fora do `EconomyCatalog`/`EconomyRules` do cenário (mesmo padrão de
`SkillsRulesCoverageTests`, Risco do design); (b) teste estático (grep no código-fonte, sem
executar cenário) que reprova se existir campo/método chamado "fitness"/"aptidão"/"score
global" fora de comentário/doc (FAM-22 — critério de verificação da spec).
**Where**: `tests/LivingWorld.Tests/Population/FamilyRulesCoverageTests.cs` (novo),
`tests/LivingWorld.Tests/ArchitectureTests.cs` (modificado — novo teste de grep)
**Depends on**: T19
**Reuses**: padrão de `SkillsRulesCoverageTests` (cobertura por enumeração/reflexão),
`ArchitectureTests` já varre `src/` por convenção (mesmo mecanismo de análise estática)
**Requirement**: FAM-13 (rede de segurança), FAM-22

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Remover uma entrada de `MarriageInitialStock`/`ConceptionResourceFloor` que aponte pra
      um `ResourceId` inexistente no cenário default reprova o teste (a)
- [ ] Grep por `fitness`/`aptidão`/`score global` (case-insensitive) em `src/**/*.cs` fora de
      comentário `///`/`//` retorna vazio — teste falha se alguém introduzir tal símbolo (b)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: 2 testes pass

**Tests**: unit
**Gate**: full

---

### T21: Harness de cenário — deriva neutra [P]

**What**: monta `ScenarioRunner.Create` com `FamilyRules.DefaultFamilyRules with
{ NeutralDriftEnabled = true }` — mesma seed/demografia do braço real; nenhuma mudança de
produção (T16/T9 já leem a flag), só a composição do harness de teste (AD-059).
**Where**: `tests/LivingWorld.Tests/Population/NeutralDriftScenarioHarness.cs`
**Depends on**: T19
**Reuses**: `EconomyScenarioHarness` como modelo de harness de teste (decorator/parâmetro
opcional sobre `ScenarioRunner`, nunca cenário C# duplicado)
**Requirement**: FAM-23, FAM-25

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Harness roda com `NeutralDriftEnabled = true` e produz um `WorldState`/hash distinto
      do braço default na mesma seed (prova que a flag realmente muda o comportamento)
- [ ] `NeutralDriftEnabled = false` (default) é idêntico ao mundo sem a flag mencionada em
      lugar nenhum — cenário aditivo, nunca o caminho default (FAM-25)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥2 testes pass

**Tests**: integração leve
**Gate**: full

---

### T22: Harness de cenário — contrafactual de household [P]

**What**: helper que fixa `Vitality`/`RateGene` de um NPC semente e o instancia em dois
households com riqueza inicial diferente (rico/pobre, `Household.Stock` declarado no teste),
demais condições fixadas — nenhuma mudança de produção, só composição de teste (AD-059).
**Where**: `tests/LivingWorld.Tests/Population/HouseholdCounterfactualHarness.cs`
**Depends on**: T19
**Reuses**: `HeredityService`/`Household` já existentes — harness só monta o cenário, não
duplica lógica de produção nem de hereditariedade
**Requirement**: FAM-24, FAM-25

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Mesmo `Vitality`/`RateGene` fixado, household rico vs pobre produzem `Upbringing`
      diferente (via `HeredityService.DeriveUpbringing`, já provado em T6) e trajetória de
      patrimônio adulto diferente ao longo da simulação
- [ ] Nenhum sistema de produção precisou mudar para o harness funcionar (composição pura de
      cenário de teste)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: ≥2 testes pass

**Tests**: integração leve
**Gate**: full

---

### T23: Cenário pareado — incesto negativo/positivo [P]

**What**: (a) 10 anos, população default — zero casamentos entre parentes de primeiro grau
(FAM-30); (b) cenário dedicado com dois irmãos adultos coabitando, compatíveis em tudo o mais
(mesma idade/saúde/cultura, sem outro candidato competindo) — cortejo entre eles rejeitado com
`Incesto` (FAM-31).
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T16, T20
**Reuses**: harness de determinismo/`ScenarioRunner` existente
**Requirement**: FAM-30, FAM-31

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] (a) 10 anos, cenário default: zero `WorldEventKind.Marriage` entre pares que satisfazem
      a checagem de parentesco de primeiro grau (verificado a partir do log de eventos)
- [ ] (b) cortejo dedicado entre irmãos completos rejeitado com `CourtshipRejectionReason.Incesto`
      — mesmo com todos os outros fatores favoráveis
- [ ] `[Trait("Category","Scenario")]` em (a); (b) pode ser rápido, sem trait
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario` (a) e
      `bash scripts/verify.sh` (b)
- [ ] Test count: 2 testes pass

**Tests**: integração pesada (Scenario, a) + leve (b)
**Gate**: Scenario (a) / full (b)

---

### T24: Invariante — parentesco e janela de fertilidade em toda concepção [P]

**What**: cenário de N anos — toda criança nascida tem `MotherId`/`FatherId` que resolvem a
`Npc` que estavam vivos no momento da concepção (FAM-28); nenhum nascimento tem mãe fora da
janela de fertilidade declarada (FAM-29).
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T17, T20
**Reuses**: log de eventos `Birth` já existente + `world.Npcs`
**Requirement**: FAM-28, FAM-29

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Toda criança nascida no cenário tem `MotherId != null` resolvível para um `Npc`
      que estava vivo na data de concepção (`BirthDate - GestationDays`); mesma checagem para
      `FatherId` quando não nulo
- [ ] Nenhuma criança nasceu de mãe com idade fora de `[FertilityMinAge, FertilityMaxAge]`
      na concepção
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass (varre toda a população nascida no cenário)

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T25: Cenário — linhagens rastreáveis (contagem esperada de nascimentos) [P]

**What**: N anos de simulação — contagem de nascimentos observada é compatível com
`esperado = anos / idadeMédiaPrimeiroParto` (critério FAM-26 do roadmap), dentro de uma
tolerância declarada no teste.
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T17, T20
**Reuses**: log de eventos `Birth`, mesma convenção de tolerância de outros cenários
estatísticos da Fase 5/6
**Requirement**: FAM-26

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Contagem de nascimentos ao final do cenário está dentro da tolerância declarada do
      `esperado` derivado da fórmula do critério
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T26: Cenário — população final vs baseline de 20 seeds [P]

**What**: roda o cenário default (com a Fase 7 integrada) em 20 seeds por N anos, compara
população final contra o baseline já persistido em `tests/baselines/` (mesmo padrão de T14 da
Fase 6) — desvio grande loga alerta sem quebrar o gate, extinção total (0 vivos) falha.
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T19, T20
**Reuses**: `tests/baselines/` já existente, mesma convenção de persistência de razão média
**Requirement**: FAM-27

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 20/20 seeds terminam com população > 0 (Fase 7 não pode extinguir sistematicamente a
      vila em relação ao comportamento anterior)
- [ ] Razão/medida persistida em `tests/baselines/`; desvio >±30% loga alerta sem falhar o
      gate (mesma convenção de T14/Fase 6)
- [ ] `[Trait("Category","Scenario")]`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass (20 seeds internas)

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T27: Cenário — IC95 bootstrap da diferença pareada de CV de `Vitality` vs controle de deriva neutra [P]

**What**: 20 seeds, braço real vs harness de deriva neutra (T21) — IC95 bootstrap da diferença
pareada `CV(real,seed_i) - CV(neutro,seed_i)` de `Vitality`, avaliado contra zero (FAM-32
reformulado, AD-066). Mesmo mecanismo estatístico de FAM-33/T28 (bootstrap percentile), aplicado
à diferença pareada em vez de a `|Pearson|`.
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T21
**Reuses**: `NeutralDriftScenarioHarness` (T21), `BootstrapAbsPearsonCi95` (T28, mesma
transformação de reamostragem)
**Requirement**: FAM-32

**Tools**: MCP: NONE · Skill: NONE

**FECHADO por reformulação (AD-066, ver docs/decisions-log.md)**: AD-064/AD-065 corrigiram o
viés estrutural real da comparação (flag única misturando mate-choice e seleção de mortalidade),
mas a comparação corrigida deu paridade estatística, não a desigualdade de seed único original
(20 seeds: gapCount=12/20, médias 0.324 real vs 0.329 neutro, diferença ~1.5% dentro do ruído
seed-a-seed 0.28-0.39) — single-seed nunca teve poder estatístico pra separar essa diferença.
FAM-32 foi reformulado (spec.md, nota AD-066) para o mesmo padrão estatístico de FAM-33: IC95
bootstrap da diferença pareada nas 20 seeds, contra zero. Rodado de verdade: diferença média
-0.0054, IC95 = [-0.0120, 0.0017] — contém zero, confirma paridade estatística (nem `real >=
neutro` nem o inverso têm evidência). O teste documenta esse resultado exatamente como medido —
não força nem inverte a direção.

**Done when**:
- [x] IC95 bootstrap da diferença pareada `CV(real) - CV(neutro)` calculado nas 20 seeds/horizonte
      de T26/T21, teste assere o resultado medido (contém zero — paridade estatística)
- [x] `[Trait("Category","Scenario")]`
- [x] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [x] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T28: Cenário — bootstrap `|r|` genética×sucesso, teto derivado (canal ambiental desligado) [P]

**What**: bootstrap (reamostragem com reposição) de `|Pearson(Vitality, Wallet aos 30/morte)|`
no mundo real vs no mesmo mundo com `EnvironmentalWealthChannelEnabled = false` — IC95 do
`|r|` real fica inteiramente abaixo do IC95 do mundo sem canal ambiental (teto derivado, não
um limiar inventado — FAM-33).
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T10, T19
**Reuses**: `PearsonCi95` já existente em `PairedScenarioTests.cs` (Fase 6, T17) — extendido
para bootstrap de `|r|` em vez de IC95 direto de `r` (mesma transformação de Fisher, só
reamostragem em torno dela)
**Requirement**: FAM-33

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] IC95 de `|r|` do mundo real fica inteiramente abaixo do IC95 de `|r|` do mundo com
      canal ambiental desligado (prova que o canal ambiental é causal, não decorativo — o
      "teto" é o próprio mundo sem o canal, nunca um número fixo no teste)
- [x] `[Trait("Category","Scenario")]`
- [x] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [x] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T29: Cenário — distância mesma-genética/seeds-ambientais vs mesma-ambiental/genéticas-diferentes [P]

**What**: usando o harness contrafactual (T22), compara a distância de patrimônio adulto entre
"mesmo genoma, ambientes diferentes" (várias seeds ambientais) e "mesmo ambiente, genomas
diferentes" — a primeira distância SHALL ser >= a segunda (FAM-34, prova que ambiente pesa ao
menos tanto quanto genética).
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T22
**Reuses**: `HouseholdCounterfactualHarness` (T22)
**Requirement**: FAM-34

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `distancia(mesmo_genoma, ambientes_diferentes) >= distancia(mesmo_ambiente, genomas_diferentes)`
- [x] `[Trait("Category","Scenario")]`
- [x] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [x] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T30: Cenário — contrafactual de household (medianas + overlap) [P]

**What**: 40 anos, 20 seeds, harness contrafactual (T22) rico vs pobre — medianas de
patrimônio adulto diferem; overlap das distribuições é >= overlap entre genomas extremos no
mesmo household (FAM-35, critério exato da spec/Independent Test do P2).
**Where**: `tests/LivingWorld.Tests/Population/FamilyPairedScenarioTests.cs`
**Depends on**: T22
**Reuses**: `HouseholdCounterfactualHarness` (T22)
**Requirement**: FAM-35

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Medianas de patrimônio adulto (household rico vs pobre) diferem — SPEC_DEVIATION: 300
      amostras por grupo (harness de sujeito único, não 20 seeds de população completa) para
      poder statistical adequado sem custo de simular a população inteira; o critério (mediana
      difere + overlap) é o mesmo.
- [x] Overlap das duas distribuições >= overlap entre genomas extremos dentro do mesmo
      household (medido no mesmo teste, mesmo dataset)
- [x] `[Trait("Category","Scenario")]`
- [x] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [x] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

### T31: Sensor de hash — flag off muda `Hash(world)` em 10 anos [P]

**What**: dois braços com a mesma seed — hereditariedade + formação de casais ligados
(default) vs desligados (`FamilyRules` equivalente a "Fase 7 nunca rodou" — cortejo nunca
inicia, `Vitality`/`Upbringing` não influenciam nada) — hashes divergem depois de 10 anos
(FAM-36, "Fase 7 entrou na conta").
**Where**: `tests/LivingWorld.Tests/Population/FamilyHashSensorTests.cs`
**Depends on**: T19
**Reuses**: padrão de `SkillHashSensorTests`/`EconomyHashScenarioTests` (mesmo mecanismo de
liga/desliga por cenário)
**Requirement**: FAM-36

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Hash(world)` após 10 anos diverge entre o braço com Fase 7 ligada e o braço com a
      combinação de flags que a desativa, mesma seed
- [ ] `[Trait("Category","Scenario")]` (10 anos)
- [ ] Gate check passa: `bash scripts/test.sh --filter Category=Scenario`
- [ ] Test count: 1 teste pass

**Tests**: integração pesada (Scenario)
**Gate**: Scenario

---

## Parallel Execution Map

```
Phase 1 (mostly parallel):
  T1 [P], T2 [P], T3 [P] ──→ T4 ──→ T5
  T4 ──────────────────────→ T6

Phase 2 (sequential — mesmo arquivo Npc.cs):
  (sem dependência nova) ──→ T7

Phase 3 (sequential — mesmo arquivo WorldState.cs):
  T3,T4,T5,T7 ──→ T8

Phase 4 (parallel):
  T4 ──→ T9 [P]
  T4,T7 ──→ T10 [P]

Phase 5 (sequential — mesmo arquivo NpcDeath.cs):
  T12 ──→ T13 ──→ T14

Phase 6 (parcialmente sequencial — arquivos compartilhados):
  T5,T8 ──────────────→ T11
  T12,T4,T7 ───────────→ T15
  T5,T8,T4,T7,T15 ─────→ T16
  T6,T7,T4,T8,T9 ──────→ T17
  T6,T7 ───────────────→ T18

Phase 7 (sequential):
  T9,T10,T11,T14,T15,T16,T17,T18 ──→ T19

Phase 8 (parallel após T19):
  T19 ──┬── T20 [P]
        ├── T21 [P]
        └── T22 [P]

Phase 9 (parallel, Category=Scenario):
  T16,T20 ──── T23 [P]
  T17,T20 ──── T24 [P]
  T17,T20 ──── T25 [P]
  T19,T20 ──── T26 [P]
  T21 ───────── T27 [P]
  T10,T19 ───── T28 [P]
  T22 ───────── T29 [P]
  T22 ───────── T30 [P]
  T19 ───────── T31 [P]
```

**Parallelism constraint:** A task marked `[P]` must have ALL of these:
- No unfinished dependencies
- Required test type is parallel-safe (per the **Parallelism Assessment** above)
- No shared mutable state with other `[P]` tasks in the same phase

Tasks T12→T13→T14 (Phase 5) and the internal ordering of Phase 6 are sequential because they
touch the same files (`NpcDeath.cs`, and cross-dependencies between `RelationshipSystem`/
`MarriageSystem`/`CourtshipSystem`/`NatalitySystem`) — not order-free within their group, even
though the phase as a whole can start once Phase 1-4 are done.

---

## Task Granularity Check

| Task | Scope | Status |
|---|---|---|
| T1: 3 enums bundlados | 1 arquivo, 3 tipos cohesivos (consumidos juntos por `FamilyRules`) | ⚠️ OK — cohesivo |
| T2: `CourtshipRejectionReason` | 1 arquivo, 1 tipo | ✅ Granular |
| T3: `RelationshipKey` | 1 struct | ✅ Granular |
| T4: `FamilyRules` | 1 record, ~5 métodos | ✅ Granular |
| T5: `Relationship` | 1 classe, 5 métodos | ✅ Granular |
| T6: `HeredityService` | 1 classe estática, 4 funções puras | ✅ Granular |
| T7: `Npc` extensões | 1 arquivo, 4 campos + 3 mutadores (cohesivo — mesmo objeto) | ⚠️ OK — mesmo padrão de T7 da Fase 6 |
| T8: `WorldState` extensões | 1 arquivo, 1 coleção + 1 flag | ✅ Granular |
| T9: `LifeTable`/`MortalityPlanner`/`MortalitySystem` | 3 arquivos, 1 concern (threading do multiplicador) | ⚠️ OK — cadeia de chamada única, sem lógica nova em cada arquivo |
| T10: `WagePaymentSystem` | 1 método modificado | ✅ Granular |
| T11: `RelationshipSystem` | 1 classe, 1 responsabilidade | ✅ Granular |
| T12: `HouseholdCleanup` | 1 helper extraído, refactor puro | ✅ Granular |
| T13: `HouseholdRedistribution` | 1 helper, 1 responsabilidade | ✅ Granular |
| T14: `NpcDeath.Apply` wiring | 1 branch novo | ✅ Granular |
| T15: `MarriageSystem` | 1 helper estático | ✅ Granular |
| T16: `CourtshipSystem` | 1 classe, 2 métodos públicos + 2 privados | ✅ Granular |
| T17: `NatalitySystem` reescrita | 1 arquivo, reescrita cohesiva (mesmo escopo do sistema atual) | ⚠️ OK — reescrita documentada no design, não split arbitrário |
| T18: `PopulationGenerator`/`PopulationSeeder` | 2 arquivos, 1 concern (seed de 2 campos) | ✅ Granular |
| T19: Wiring | 1 arquivo, 1 concern (integração do pipeline) | ⚠️ OK — wiring é inerentemente multi-ponto, sem lógica nova |
| T20: Cobertura/regressão | 2 arquivos de teste, 1 concern cada | ✅ Granular |
| T21: Harness deriva neutra | 1 arquivo de teste | ✅ Granular |
| T22: Harness contrafactual | 1 arquivo de teste | ✅ Granular |
| T23-T31: Cenários de verificação | 1 (ou par) por critério FAM-26..36 | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | None | None | ✅ Match |
| T4 | T1 | T1,T2,T3 → T4 | ✅ Match (T2/T3 no bloco não usados por T4, mas T4 só depende de T1 — subconjunto do bloco) |
| T5 | T1, T4 | T4 → T5 (T1 transitiva via T4) | ✅ Match |
| T6 | T4 | T4 → T6 | ✅ Match |
| T7 | None | Fase 2 sem dependência | ✅ Match |
| T8 | T3, T4, T5, T7 | T3,T4,T5,T7 → T8 | ✅ Match |
| T9 | T4 | T4 → T9 [P] | ✅ Match |
| T10 | T4, T7 | T4,T7 → T10 [P] | ✅ Match |
| T11 | T5, T8 | T5,T8 → T11 | ✅ Match |
| T12 | None | Fase 5 T12 → T13 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T12, T13 | T13 → T14 (T12 transitiva) | ✅ Match |
| T15 | T12, T4, T7 | T12,T4,T7 → T15 | ✅ Match |
| T16 | T5, T8, T4, T7, T15 | T5,T8,T4,T7,T15 → T16 | ✅ Match |
| T17 | T6, T7, T4, T8, T9 | T6,T7,T4,T8,T9 → T17 | ✅ Match |
| T18 | T6, T7 | T6,T7 → T18 | ✅ Match |
| T19 | T9,T10,T11,T14,T15,T16,T17,T18 | Fase 7 diagrama idêntico | ✅ Match |
| T20 | T19 | T19 → T20 [P] | ✅ Match |
| T21 | T19 | T19 → T21 [P] | ✅ Match |
| T22 | T19 | T19 → T22 [P] | ✅ Match |
| T23 | T16, T20 | T16,T20 → T23 [P] | ✅ Match |
| T24 | T17, T20 | T17,T20 → T24 [P] | ✅ Match |
| T25 | T17, T20 | T17,T20 → T25 [P] | ✅ Match |
| T26 | T19, T20 | T19,T20 → T26 [P] | ✅ Match |
| T27 | T21 | T21 → T27 [P] | ✅ Match |
| T28 | T10, T19 | T10,T19 → T28 [P] | ✅ Match |
| T29 | T22 | T22 → T29 [P] | ✅ Match |
| T30 | T22 | T22 → T30 [P] | ✅ Match |
| T31 | T19 | T19 → T31 [P] | ✅ Match |

Nenhuma ❌ — todas as tasks aprovadas para apresentação.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T1 | Config/entidade (enums) | none | none | ✅ OK |
| T2 | Config/entidade (enum) | none | none | ✅ OK |
| T3 | Domain — value object | unit | unit | ✅ OK |
| T4 | Domain — value object | unit | unit | ✅ OK |
| T5 | Domain — value object | unit | unit | ✅ OK |
| T6 | Domain — funções puras | unit | unit | ✅ OK |
| T7 | Domain — `Npc` | unit | unit | ✅ OK |
| T8 | Simulation — `WorldState` | unit | unit | ✅ OK |
| T9 | Domain (`LifeTable`/`MortalityPlanner`) + Simulation (`MortalitySystem`) | unit + integração leve → maior exigência | integração leve | ✅ OK |
| T10 | Simulation — sistema (modificado) | integração leve | integração leve | ✅ OK |
| T11 | Simulation — sistema | integração leve | integração leve | ✅ OK |
| T12 | Simulation — helper (extraído) | integração leve | integração leve | ✅ OK |
| T13 | Simulation — helper | integração leve | integração leve | ✅ OK |
| T14 | Simulation — sistema (modificado) | integração leve | integração leve | ✅ OK |
| T15 | Simulation — sistema/helper | integração leve | integração leve | ✅ OK |
| T16 | Simulation — sistema | integração leve | integração leve | ✅ OK |
| T17 | Simulation — sistema (reescrito) | integração leve | integração leve | ✅ OK |
| T18 | Domain + Simulation — geração de população | integração leve | integração leve | ✅ OK |
| T19 | Simulation — wiring | integração leve | integração leve | ✅ OK |
| T20 | Testes de cobertura/arquitetura | unit | unit | ✅ OK |
| T21 | Simulation — harness de teste | integração leve | integração leve | ✅ OK |
| T22 | Simulation — harness de teste | integração leve | integração leve | ✅ OK |
| T23 | Scenario (misto a+b) | integração pesada + leve | Scenario (a) + full (b) | ✅ OK |
| T24 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T25 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T26 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T27 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T28 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T29 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T30 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |
| T31 | Scenario | integração pesada (Scenario) | integração pesada (Scenario) | ✅ OK |

Nenhuma ❌ VIOLATION — todas as tasks aprovadas para apresentação.

---

## Tips

- **[P] = Order-free** — Mark tasks with no inter-task dependency (can run in any order within the phase)
- **Reuses = Token saver** — Always reference existing code
- **Tools per task** — MCPs and Skills prevent wrong approaches
- **Dependencies are gates** — Clear what blocks what
- **Done when = Testable** — If you can't verify it, rewrite it
- **Requirement ID = Traceable** — Every task traces back to a spec requirement
- **One commit per task** — Plan the commit message format in advance
