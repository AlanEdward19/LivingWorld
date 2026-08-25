# Fase 17 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-17-divinity/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicated — segue o padrão já usado nas specs 16/16.1/16.2 (xUnit,
> mundo controle/tratado, determinismo por seed, par de mutação pra guards de vazamento).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`Deity`, `NpcDevotion`, `DivinityRules`, `DeitySummary`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Divinity/**` (novo) | `dotnet test --filter "FullyQualifiedName~Divinity"` |
| `DeityBeliefPool`/`DevotionLedger` | unit | 1:1 a DIV-01, DIV-02, DIV-40, DIV-41, DIV-42 | `tests/LivingWorld.Tests/Divinity/**` | mesmo comando |
| `DeityDecaySystem`/`ColdTierArchive.TryArchiveDeity` | unit + par base/tratamento | 1:1 a DIV-10..14, 10/10 seeds pra perseguição | `tests/LivingWorld.Tests/Divinity/**` | mesmo comando |
| `DoctrineDeriver`/`NatureResolver` | unit + par controle/tratado | 1:1 a DIV-20..23 | `tests/LivingWorld.Tests/Divinity/**` | mesmo comando |
| Templo/sacerdote/dízimo (catálogo + `TithePaymentSteps`) | unit | 1:1 a DIV-30..34 | `tests/LivingWorld.Tests/Divinity/**` | mesmo comando |
| `DivineIntervention` | unit | 1:1 a DIV-50..53 | `tests/LivingWorld.Tests/Divinity/**` | mesmo comando |
| `Worshipped`/`FaithPowered` | unit + `test-worship-without-faith-power` dedicado | 1:1 a DIV-60..63 | `tests/LivingWorld.Tests/Divinity/WorshipWithoutFaithPowerTests.cs` (novo) | mesmo comando |
| `DivinityTruthQuery`/`DeityBeliefQuery` | unit + enumeração por reflexão + par de mutação | 1:1 a DIV-70..73 | `tests/LivingWorld.Tests/Divinity/DivinityQuerySeparationGuardTests.cs` (novo) | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `Extraordinary*`/`History*`/`Population*`/`Economy*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Divinity) | Yes | Mundo próprio por teste, sem estado estático compartilhado | Padrão já usado em `ExtraordinaryInvocationEngineTests.cs` |
| par controle/tratado (perseguição, distorção) | Yes | Mundo controle/tratado por teste (`PairedScenarioTests.cs`) | Padrão já usado nesses testes |
| enumeração por reflexão + mutação (Truth) | Yes | Mesmo padrão de `HistoryQuerySeparationGuard`/`...MutationTests.cs` | Fase 10 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | Já documentado em `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Divinity"` |
| Full (integração) | Após tasks que tocam `NatalitySystem`-like hooks ou `History` | `dotnet test --filter "Category!=Scenario&(FullyQualifiedName~Divinity\|FullyQualifiedName~History)"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Pool e devoção (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Decaimento e coleta (depende de Phase 2)

```
T5 → T6 → T7
```

### Phase 4: Doutrina e natureza (Parallel OK, depende só de Phase 1)

```
T3 → T8 → T9
```

### Phase 5: Culto como instituição (Parallel OK, depende só de Phase 1)

```
T3 → T10 → T11
```

### Phase 6: Cisma (depende de Phase 4)

```
T9 → T12
```

### Phase 7: Intervenção divina (depende de Phase 2)

```
T5 → T13
```

### Phase 8: Worshipped/FaithPowered (Parallel OK, depende de Phase 1)

```
T3 → T14
```

### Phase 9: Verdade e separação de consulta (depende de tudo que expõe estado)

```
T7, T9, T13, T14 → T15 → T16
```

---

## Task Breakdown

### T1: `Deity`, `DeityId`, `NpcDevotion`

**What**: Records de domínio novos: `Deity(Id, PowerDescriptorId, FoundingDoctrineId,
FaithPowered, FoundingCity)`, `DeityId`, `NpcDevotion(NpcId, DevotionByDeity, FaithlessShare)`.
**Where**: `src/LivingWorld.Domain/Divinity/Deity.cs` (novo), `NpcDevotion.cs` (novo)
**Depends on**: None
**Reuses**: `PowerDescriptor.Id` (16.1) referenciado, nunca duplicado
**Requirement**: DIV-01, DIV-40

**Done when**:
- [ ] `Deity` exige `PowerDescriptorId` válido (referência, não posse duplicada de dados)
- [ ] `NpcDevotion` construído com `Σ DevotionByDeity + FaithlessShare == 1` (epsilon declarado)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add Deity and NpcDevotion records`

---

### T2: `DivinityRules` (regra de cenário)

**What**: `record DivinityRules(NatureDivergenceThreshold, SchismDivergenceThreshold,
DecayEvaluationWindowTicks)`.
**Where**: `src/LivingWorld.Domain/Divinity/DivinityRules.cs` (novo)
**Depends on**: None
**Reuses**: mesmo arquivo/padrão de `HistoryRules`/`PerfRules`
**Requirement**: (suporta DIV-10, DIV-21, DIV-33)

**Done when**:
- [ ] Cenário sem declaração explícita usa defaults documentados, nunca falha

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add scenario-configurable DivinityRules with documented defaults`

---

### T3: `WorldEventKind` — 5 valores novos

**What**: `DeityManifested`, `DeityDecayed`, `DeityArchived`, `DeitySchismed`,
`DoctrineNatureShifted`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1
**Reuses**: enum existente, aditivo (mesma disciplina 16.1/16.2)
**Requirement**: (auditoria, suporta todos os AC de evento)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add divinity WorldEventKind values`

---

### T4: `DeityBeliefPool.Compute`

**What**: Função pura — soma devoção×frequência de retransmissão sobre fiéis correntes de um
`Deity`.
**Where**: `src/LivingWorld.Simulation/Divinity/DeityBeliefPool.cs` (novo)
**Depends on**: T3
**Reuses**: `ReportState`/histórico de retransmissão (Fase 10) como fonte de frequência
**Requirement**: DIV-01, DIV-02

**Done when**:
- [ ] Pool recalculado do zero a cada chamada — nenhum campo cacheado é fonte de verdade
- [ ] Pool de `Deity` com `FaithPowered=false` nunca é computado por nenhum sistema (verificado por não-chamada, não por retorno zero)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): add pure belief pool computation from believers and retransmission`

---

### T5: `DevotionLedger`

**What**: Sistema que realoca devoção entre `Deity`s e `FaithlessShare` mantendo soma == 1;
conversão/perseguição só move share existente.
**Where**: `src/LivingWorld.Simulation/Divinity/DevotionLedger.cs` (novo)
**Depends on**: T4
**Reuses**: mesma disciplina de invariante ativamente mantido (renormalização defensiva)
**Requirement**: DIV-40, DIV-41, DIV-42

**Done when**:
- [ ] Conversão realoca devoção de outro(s) `Deity`/`FaithlessShare` — nunca cria do zero
- [ ] Soma por NPC permanece 1 (epsilon) após N operações de conversão em sequência (teste de estresse simples)
- [ ] Crescimento de um `Deity` correlaciona com perda de share agregada de outro numa janela declarada

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): add conserved devotion ledger with reallocation on conversion`

---

### T6: `DeityDecaySystem`

**What**: A cada reavaliação, sem retransmissão na janela (`DivinityRules.
DecayEvaluationWindowTicks`), decai o pool monotonicamente; loga `DeityDecayed`.
**Where**: `src/LivingWorld.Simulation/Divinity/DeityDecaySystem.cs` (novo)
**Depends on**: T5
**Reuses**: cadência de reavaliação já usada por `ExtraordinaryStateSystem` (16.1)
**Requirement**: DIV-10, DIV-11, DIV-12, DIV-14

**Done when**:
- [ ] `poder(t+1) ≤ poder(t)` a cada tick sem retransmissão — nenhuma subida sem manifestação/fiel novo
- [ ] Retransmissão de relato realimenta o pool corretamente (integração com T4)
- [ ] Par base/tratamento (perseguição vs. baseline) na mesma seed: perda do tratado > decaimento natural, margem > spread entre seeds do baseline — 10/10 seeds

**Tests**: unit + par base/tratamento
**Gate**: quick
**Commit**: `feat(divinity): add monotonic belief decay system with retransmission feedback`

---

### T7: `DeitySummary` + `ColdTierArchive.TryArchiveDeity`/`LookupDeity`

**What**: Overload em `ColdTierArchive` (mesma classe, Fase 9) pra arquivar `Deity` com pool 0.
**Where**: `src/LivingWorld.Simulation/Population/ColdTierArchive.cs` (modificado),
`src/LivingWorld.Domain/Divinity/DeitySummary.cs` (novo)
**Depends on**: T6
**Reuses**: mesmo padrão de `TryArchive`/`NpcSummary` já existente
**Requirement**: DIV-13

**Done when**:
- [ ] Pool cruzando 0 aciona coleta no próximo tick de reavaliação, nunca invalida invocação em voo no tick corrente
- [ ] `LookupDeity` retorna `DeitySummary` pós-coleta; `Deity` sai de memória quente
- [ ] `dotnet test --filter "FullyQualifiedName~Divinity"` verde

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): reuse ColdTierArchive to collect deities at zero belief pool`

---

### T8: `DoctrineDeriver`

**What**: Aplica os 8 `DistortionOperator` (Fase 10, `DistortionEngine`) sobre o histórico de
`ReportState` da doutrina — retorna doutrina corrente, função pura.
**Where**: `src/LivingWorld.Simulation/Divinity/DoctrineDeriver.cs` (novo)
**Depends on**: T3
**Reuses**: `DistortionEngine`/`DistortionOperator` (Fase 10) — nenhuma probabilidade/lógica de operador reimplementada
**Requirement**: DIV-20

**Done when**:
- [ ] Doutrina fundadora sem histórico distorcido permanece inalterada indefinidamente
- [ ] Doutrina corrente recalculada do zero a cada leitura (nunca campo armazenado mutável)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): derive current doctrine from historical distortion operators`

---

### T9: `NatureResolver` + `NatureLabel`

**What**: `readonly record struct NatureLabel(int Id)` (catálogo) + resolvedor que troca o rótulo
quando a doutrina corrente diverge da fundadora além do limiar; loga
`DoctrineNatureShifted`.
**Where**: `src/LivingWorld.Domain/Divinity/NatureLabel.cs` (novo),
`src/LivingWorld.Simulation/Divinity/NatureResolver.cs` (novo)
**Depends on**: T8
**Reuses**: mesmo padrão de tipo-id-de-catálogo já usado por `ProfessionType`/`LocationType`
**Requirement**: DIV-21, DIV-22, DIV-23

**Done when**:
- [ ] Divergência além do limiar troca o rótulo sem decisão explícita de nenhum sistema
- [ ] Par controle (sem operadores) vs. tratado (com operadores) na mesma seed: tratado diverge ≥ N cultos declarados no cenário, controle = 0 divergências
- [ ] Mesma seed/histórico produz rótulo corrente byte-idêntico entre execuções

**Tests**: unit + par controle/tratado
**Gate**: quick
**Commit**: `feat(divinity): resolve deity nature label from doctrine divergence, paired control test`

---

### T10: Catálogo — `LocationType=Temple`, `ProfessionType=Sacerdote`

**What**: Entradas de catálogo de cenário — nenhum tipo C# novo, só dados.
**Where**: `src/LivingWorld.Scenarios/**` (catálogo, arquivo específico depende do formato já
usado por `LocationType`/`ProfessionType` existentes)
**Depends on**: T3
**Reuses**: `Workplace`(Fase 5)/`ProfessionType`(Fase 5/6) genéricos, sem modificação de shape
**Requirement**: DIV-30, DIV-31

**Done when**:
- [ ] Templo é um `Workplace` funcional (renda, `Employees`) sem nenhuma condicional especial no motor de `Workplace`
- [ ] Sacerdote é `ProfessionType` comum, atribuível via `Npc.SwitchProfession`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(scenario): add Temple location type and Priest profession to catalog`

---

### T11: `TithePaymentSteps`

**What**: Composição de `TransactionStep` (mesmo shape de `MarketTransaction`) — débito do fiel,
crédito no `Workplace.Treasury` do templo.
**Where**: `src/LivingWorld.Domain/Divinity/TithePaymentSteps.cs` (novo)
**Depends on**: T10
**Reuses**: `MarketTransaction`/`TransactionContext`/`Steps` (Fase 5) — mesma execução all-or-nothing
**Requirement**: DIV-30

**Done when**:
- [ ] Dízimo executa all-or-nothing (mesma garantia do `MarketTransaction.Execute`)
- [ ] Falha em qualquer step não deixa débito parcial

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): add tithe payment as composed MarketTransaction steps`

---

### T12: `SchismResolver`

**What**: Divergência de doutrina por sub-comunidade além de `SchismDivergenceThreshold` cria
`Deity` novo, realoca `DevotionByDeity` dos migrantes; loga `DeitySchismed`.
**Where**: `src/LivingWorld.Simulation/Divinity/SchismResolver.cs` (novo)
**Depends on**: T9
**Reuses**: `WorldRng.Stream("deity-schism")` (ADR-0011) pro `DeityId` determinístico
**Requirement**: DIV-33, DIV-34

**Done when**:
- [ ] Cisma cria `DeityId` novo com pool próprio calculado normalmente a partir dos fiéis migrantes
- [ ] `Deity` original mantém fiéis remanescentes, pool recalculado sem bônus/penalidade arbitrária
- [ ] Divergência no limiar sem população migrante real (0 NPCs) não cria `Deity` novo

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): implement schism resolver spawning new deity with inherited followers`

---

### T13: `DivineIntervention`

**What**: Wrapper sobre `ExtraordinaryInvocationEngine.InvokeAuthored`/`Invoke` — custo debitado
do pool independente de testemunha; testemunha gera `ReportState` (Fase 10).
**Where**: `src/LivingWorld.Simulation/Divinity/DivineIntervention.cs` (novo)
**Depends on**: T5
**Reuses**: `ExtraordinaryInvocationEngine` (16.1) sem bypass; `MarketTransaction`-style recusa por saldo insuficiente
**Requirement**: DIV-50, DIV-51, DIV-52, DIV-53

**Done when**:
- [ ] Intervenção usa `Reliability`/`Resolution` do `PowerDescriptor` do deus, sem rolagem paralela
- [ ] Custo excedendo pool corrente recusa antes de invocar o engine (nunca debita negativo)
- [ ] Falha em `ResolutionCheck` com testemunha gera consequência declarada no cenário, nunca falha silenciosa
- [ ] Sem testemunha: custo/efeito ocorrem, nenhum `ReportState` é gerado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): implement divine intervention as thin wrapper over invocation engine`

---

### T14: `Worshipped` vs `FaithPowered`

**What**: Campo `Worshipped` (atribuição social, independente) + garantia de que `FaithPowered`
só liga por evento explícito de cenário.
**Where**: `src/LivingWorld.Domain/Divinity/Worshipped.cs` (novo, ou campo em entidade
`Npc`/`Deity` conforme o alvo de culto), teste dedicado
**Depends on**: T3
**Reuses**: nenhum sistema de poder existente é tocado — campo puramente de leitura social
**Requirement**: DIV-60, DIV-61, DIV-62, DIV-63

**Done when**:
- [ ] Atribuição social marca `Worshipped=true` sem alterar `FaithPowered`
- [ ] `FaithPowered=false` bloqueia qualquer cômputo de pool (integração com T4, já coberto lá — teste aqui confirma o contrato do campo)
- [ ] Ligação explícita de `FaithPowered` ativa pool a partir do tick do evento, nunca retroativo
- [ ] `test-worship-without-faith-power`: NPC cultuado por N anos simulados sem `FaithPowered` não apresenta nenhum ganho mecânico mensurável

**Tests**: unit + teste dedicado
**Gate**: quick
**Commit**: `feat(divinity): decouple Worshipped social attribution from FaithPowered mechanical link`

---

### T15: `DivinityTruthQuery`

**What**: Canal único que resolve realidade do `Deity` (real/esvaziado vs. falso/mito) — mesmo
padrão isolado de `HistoryTruthQuery`.
**Where**: `src/LivingWorld.Simulation/Divinity/DivinityTruthQuery.cs` (novo)
**Depends on**: T7, T9, T13, T14
**Reuses**: `HistoryTruthQuery` como padrão estrutural (arquivo isolado, doc comment explícito de não-uso por handler de jogo)
**Requirement**: DIV-72 (parcial), DIV-73

**Done when**:
- [ ] Retorna corretamente real/esvaziado (há `PowerDescriptor` real) vs. falso/mito (não há)
- [ ] Nenhum outro arquivo do motor de jogo referencia este tipo (checagem manual + preparação pro guard de T16)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(divinity): add isolated DivinityTruthQuery channel`

---

### T16: `DeityBeliefQuery` + guard de separação (enumeração + par de mutação)

**What**: Extensão de view de crença pra `Deity` (pool/natureza observável, nunca verdade) +
teste de enumeração por reflexão que falha se algum handler expuser realidade, com par de
mutação (desligar checagem deve falhar o critério).
**Where**: `src/LivingWorld.Simulation/Divinity/DeityBeliefQuery.cs` (novo),
`tests/LivingWorld.Tests/Divinity/DivinityQuerySeparationGuardTests.cs` (novo)
**Depends on**: T15
**Reuses**: mesmo padrão de `HistoryQuerySeparationGuard`/`...MutationTests.cs` (Fase 10)
**Requirement**: DIV-70, DIV-71, DIV-72

**Done when**:
- [ ] Enumeração por reflexão cobre todos os handlers de consulta de crença/culto; falha se algum ficar sem cobertura
- [ ] Par de mutação: desligar a checagem por flag de teste faz o critério falhar (prova que detecta vazamento)
- [ ] Deus esvaziado e mito em ascensão com mesmo pool/sem manifestação na janela: toda a superfície de `DeityBeliefQuery` retorna byte-idêntico
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit + enumeração por reflexão + par de mutação
**Gate**: build
**Commit**: `test(divinity): add belief query separation guard with reflection enumeration and mutation pair`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Sequential, depends on Phase 2):
  T5 ──→ T6 ──→ T7

Phase 4 (Sequential, depends on Phase 1, parallel with Phase 2/3):
  T3 ──→ T8 ──→ T9

Phase 5 (Sequential, depends on Phase 1, parallel with Phase 2/3/4):
  T3 ──→ T10 ──→ T11

Phase 6 (depends on Phase 4):
  T9 ──→ T12

Phase 7 (depends on Phase 2):
  T5 ──→ T13

Phase 8 (depends on Phase 1, parallel with everything else):
  T3 ──→ T14

Phase 9 (last — depends on Phase 3, 4, 7, 8):
  T7, T9, T13, T14 ──→ T15 ──→ T16
```

9 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fases
4, 5 e 8 são ramos independentes que podem correr em paralelo assim que a Fase 1 termina.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4, T5, T6, T7 | 1 sistema/função cada (pool → ledger → decaimento → coleta) | ✅ Granular |
| T8, T9 | 1 função pura + 1 resolvedor | ✅ Granular |
| T10, T11 | 1 entrada de catálogo + 1 composição de transação | ✅ Granular |
| T12 | 1 resolvedor | ✅ Granular |
| T13 | 1 wrapper fino | ✅ Granular |
| T14 | 1 contrato de campo + teste dedicado | ✅ Granular |
| T15, T16 | 1 canal isolado + 1 suíte de guard dedicada | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | T1 | T1→T3 (T2 paralelo, sem dependência declarada) | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | T6 | T6→T7 | ✅ Match |
| T8 | T3 | T3→T8 | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T3 | T3→T10 | ✅ Match |
| T11 | T10 | T10→T11 | ✅ Match |
| T12 | T9 | T9→T12 | ✅ Match |
| T13 | T5 | T5→T13 | ✅ Match |
| T14 | T3 | T3→T14 | ✅ Match |
| T15 | T7, T9, T13, T14 | T7,T9,T13,T14→T15 | ✅ Match |
| T16 | T15 | T15→T16 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T14 | Domain/business-logic | unit (+ par controle/tratado onde aplicável) | unit / unit + par | ✅ OK |
| T15 | Isolated query channel | unit | unit | ✅ OK |
| T16 | Guard suite + build gate final | unit + enumeração + mutação + build gate | unit + enumeração + mutação, build | ✅ OK |

No task defers its own tests to a later task.
