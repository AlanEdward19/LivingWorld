# Fase 20 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-20-interdimensional/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 16/17/18 (xUnit,
> determinismo cross-process, par de mutação, enumeração por reflexão, baseline de 20 seeds em
> `tests/baselines/`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`PresenceRecord`, `InterdimensionalRules`, `CatchUpResult`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Timelines/**` (mesma pasta da Fase 18) | `dotnet test --filter "FullyQualifiedName~Timelines"` |
| `BranchCatchUpEngine` | unit + cross-process | 1:1 a ITD-01..03, ITD-10..11 (test-catchup pareado, 2 processos) | `tests/LivingWorld.Tests/Timelines/CatchUpEngineTests.cs`, `CrossProcessCatchUpTests.cs` (novos) | mesmo comando |
| `BranchLodResolver`/`PresenceLedger` | unit | 1:1 a ITD-20..22 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| `BranchPrewarmScheduler` | unit + par pré-aquecido/sob-demanda | 1:1 a ITD-30..31 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| Orçamento de catch-up | unit + baseline de 20 seeds | 1:1 a ITD-40..43 | `tests/LivingWorld.Tests/Timelines/**` + `tests/baselines/` | mesmo comando |
| Trânsito (`TransitArrivalResolver`) | unit | 1:1 a ITD-50..52 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| Identidade (`TransitArrivalResolver`) | unit + conservação de população | 1:1 a ITD-60..63 | `tests/LivingWorld.Tests/Timelines/TransitIdentityTests.cs` (novo) | mesmo comando |
| Âncora do viajante | unit | 1:1 a ITD-70..72 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| Guard "nenhuma consulta mistura linhas" | unit + enumeração por reflexão + par de mutação | Critério de verificação da spec | `tests/LivingWorld.Tests/Timelines/TemporalQuerySeparationGuardTests.cs` (novo) | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `History*`/`Extraordinary*`/`Cities*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Timelines) | Yes | Mundo próprio por teste | Fase 18 |
| cross-process (test-catchup) | Yes, mas isolado | Processo separado real | Mesmo padrão de `CrossProcessBranchHashTests` (Fase 18) |
| par pré-aquecido/sob-demanda | Yes | Mundo controle/tratado por teste | `PairedScenarioTests.cs` |
| enumeração por reflexão + mutação | Yes | Mesmo padrão de guards de Fase 10/17 | Fase 10/17 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Timelines"` |
| Cross-process | Após T4 (catch-up engine) | `dotnet test --filter "FullyQualifiedName~CrossProcessCatchUp"` |
| Full (integração) | Após tasks que tocam trânsito/identidade | `dotnet test --filter "Category!=Scenario&FullyQualifiedName~Timelines"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Presença e LOD (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Motor de catch-up (depende de Phase 2)

```
T5 → T6 → T7
```

### Phase 4: Preguiçoso == eager (depende de Phase 3)

```
T7 → T8
```

### Phase 5: Pré-aquecimento (Parallel OK, depende de Phase 3)

```
T7 → T9
```

### Phase 6: Trânsito e chegada (depende de Phase 3)

```
T7 → T10 → T11
```

### Phase 7: Âncora do viajante (Parallel OK, depende de Phase 1)

```
T3 → T12
```

### Phase 8: Guard final de separação de linhas (depende de tudo que consulta estado temporal)

```
T8, T11, T12 → T13
```

---

## Task Breakdown

### T1: `PresenceRecord`, `LodResolution`, `InterdimensionalRules`

**What**: Records de domínio novos.
**Where**: `src/LivingWorld.Domain/Timelines/PresenceLedger.cs` (novo),
`src/LivingWorld.Domain/Timelines/InterdimensionalRules.cs` (novo)
**Depends on**: None
**Reuses**: mesma escala de `LodResolution` já usada pela Fase 8/9
**Requirement**: (suporta ITD-20..22)

**Done when**:
- [ ] `PresenceRecord` é imutável — nenhum método de "atualizar" existe, só "adicionar novo"

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add PresenceRecord and InterdimensionalRules`

---

### T2: `CatchUpResult`, `CatchUpOutcome`

**What**: `enum CatchUpOutcome { NoOp, Completed, PartialSuccess }`,
`record CatchUpResult(Branch, SimulatedUntilBefore, SimulatedUntilAfter, TicksExecuted, Outcome)`.
**Where**: `src/LivingWorld.Domain/Timelines/CatchUpResult.cs` (novo)
**Depends on**: None
**Reuses**: nenhum tipo de resultado paralelo — mapeamento documentado pros 3 casos da spec
**Requirement**: ITD-01, ITD-40

**Done when**:
- [ ] `TicksExecuted == 0` é invariante obrigatória quando `Outcome == NoOp`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add CatchUpResult and CatchUpOutcome`

---

### T3: `WorldEventKind` — 3 valores novos

**What**: `CatchUpCompleted`, `CatchUpPartial`, `TransitArrived`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add interdimensional WorldEventKind values`

---

### T4: `PresenceLedger` — registro append-only

**What**: Registra `PresenceRecord` sempre que um branch é observado; nunca sobrescreve.
**Where**: `src/LivingWorld.Simulation/Timelines/PresenceLedger.cs` (novo)
**Depends on**: T3
**Reuses**: mesma disciplina append-only do log de eventos (ADR-0006)
**Requirement**: ITD-22

**Done when**:
- [ ] Registrar um novo intervalo nunca modifica/remove registros anteriores

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): add append-only presence ledger`

---

### T5: `BranchLodResolver`

**What**: Função pura — `Resolve(ledger, branch, tick) -> LodResolution`; nunca aceita
fidelidade desejada do chamador.
**Where**: `src/LivingWorld.Domain/Timelines/BranchLodResolver.cs` (novo)
**Depends on**: T4
**Reuses**: `PresenceLedger` (T4)
**Requirement**: ITD-21, ITD-22

**Done when**:
- [ ] Intervalo já coberto em resolução `L` sempre retorna `L`, nunca aceita override
- [ ] Intervalo nunca observado retorna a resolução mínima do cenário (nunca erro)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): add pure LOD resolver over presence ledger`

---

### T6: `BranchCatchUpEngine` — caminho sem trabalho

**What**: `T <= simuladoAté` retorna `NoOp` sem tocar `PersistentWorldRunner`.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchCatchUpEngine.cs` (novo)
**Depends on**: T5
**Reuses**: `PersistentWorldRunner.LoadAt` (Fase 1/3, não modificado)
**Requirement**: ITD-01, ITD-02

**Done when**:
- [ ] `T <= simuladoAté` executa 0 ticks (contagem instrumentada), hash inalterado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): implement catch-up engine no-op path`

---

### T7: `BranchCatchUpEngine` — replay com orçamento

**What**: `T > simuladoAté` dispara `LoadAt(simuladoAté)` + tick até `T` ou até o orçamento
(`InterdimensionalRules.CatchUpWorkBudgetTicks`) esgotar; resultado `Completed`/`PartialSuccess`,
`simuladoAté` persistido append-only em ambos os casos.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchCatchUpEngine.cs` (modificado)
**Depends on**: T6
**Reuses**: `PersistentWorldRunner`/`WorldClock` (Fase 1/3)
**Requirement**: ITD-03, ITD-40, ITD-41, ITD-42, ITD-43

**Done when**:
- [ ] Orçamento esgotado retorna `PartialSuccess`, `simuladoAté` avança até onde deu (nunca `Failure` descartando)
- [ ] Chamada seguinte após `PartialSuccess` continua exatamente de onde parou, sem refazer
- [ ] Progresso consultável (`CatchUpProgress`) durante execução, somente leitura
- [ ] Custo medido (N anos fixos) dentro do baseline de 20 seeds, independente do `simuladoAté` inicial

**Tests**: unit + baseline de 20 seeds
**Gate**: quick
**Commit**: `feat(timelines): implement budgeted catch-up replay with append-only progress`

---

### T8: `test-catchup` pareado — preguiçoso == eager

**What**: Cenário pareado — branch simulado eager até T vs. em 2 lances (T/2, depois T), mesmo
registro de presença — hash idêntico, comparado em 2 processos separados.
**Where**: `tests/LivingWorld.Tests/Timelines/CrossProcessCatchUpTests.cs` (novo)
**Depends on**: T7
**Reuses**: mesmo padrão de `CrossProcessBranchHashTests` (Fase 18)
**Requirement**: ITD-10, ITD-11

**Done when**:
- [ ] Hash idêntico entre eager e 2-lances, 2 processos separados
- [ ] Teste falha explicitamente se os registros de presença dos dois cenários divergirem (prova que a cláusula "mesmo registro" é respeitada, não implícita)

**Tests**: unit + cross-process
**Gate**: cross-process
**Commit**: `test(timelines): prove lazy catch-up equals eager simulation given same presence record`

---

### T9: `BranchPrewarmScheduler`

**What**: Job de background, fora do caminho crítico do tick, chama o mesmo
`BranchCatchUpEngine` pra branches ancorados.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchPrewarmScheduler.cs` (novo)
**Depends on**: T7
**Reuses**: `BranchCatchUpEngine` (T6/T7), `AnchorTracker` (Fase 18, spec) pra listar branches ancorados
**Requirement**: ITD-30, ITD-31

**Done when**:
- [ ] Nenhuma consulta síncrona espera pelo pré-aquecimento (roda fora do tick crítico)
- [ ] Par de cenários idênticos (pré-aquecido vs. sob-demanda puro) produz hash idêntico

**Tests**: unit + par pré-aquecido/sob-demanda
**Gate**: quick
**Commit**: `feat(timelines): add background prewarm scheduler reusing catch-up engine`

---

### T10: Trânsito como invocação de potência

**What**: Pipeline `Prepare`/`PrepareEffects`/`Resolver.Resolve` (perfil `Dramatico`) pro
trânsito; falha aplica consequência declarada.
**Where**: `src/LivingWorld.Simulation/Timelines/InterdimensionalTransit.cs` (novo)
**Depends on**: T7
**Reuses**: `ExtraordinaryInvocationEngine`/`Resolver`/`VarianceProfileCatalog` (Fase 16/ADR-0011), sem rolagem paralela
**Requirement**: ITD-50, ITD-51, ITD-52

**Done when**:
- [ ] Trânsito usa exatamente o pipeline da Fase 16, `Reliability="ResolutionCheck"`
- [ ] Falha (`CriticalFailure`/`Failure`) aplica consequência declarada, nunca no-op
- [ ] Sucesso entrega o viajante na linha/tick pretendidos, sujeito ao catch-up daquela linha

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): implement interdimensional transit as extraordinary invocation`

---

### T11: `TransitArrivalResolver` — identidade por linhagem

**What**: Decide retorno (mesmo `NpcId`) vs. contraparte independente (`NpcId` novo + laço),
puramente por linhagem de `BranchId`.
**Where**: `src/LivingWorld.Simulation/Timelines/TransitArrivalResolver.cs` (novo)
**Depends on**: T10
**Reuses**: `BranchId`/linhagem (Fase 18, spec)
**Requirement**: ITD-60, ITD-61, ITD-62, ITD-63

**Done when**:
- [ ] Retorno ao `BranchId` de origem reintegra o `NpcId` existente, sem duplicata (conservação: total não muda)
- [ ] Chegada em `BranchId` com histórico próprio contendo o `NpcId` original cria `NpcId` novo com `LinkedCounterpart`, conservação soma exatamente 1
- [ ] Decisão nunca usa heurística além da linhagem de `BranchId` (documentado + testado com casos ambíguos que a linhagem resolve)
- [ ] Morte de um lado do laço nunca afeta o outro

**Tests**: unit + conservação de população
**Gate**: quick
**Commit**: `feat(timelines): resolve traveler identity by branch lineage, never fusion`

---

### T12: `TravelerAnchorBinding`

**What**: Registra `BranchAnchor(originBranch, AnchorKind.Traveler, npcId)` na partida; remove só
na morte permanente.
**Where**: `src/LivingWorld.Simulation/Timelines/TravelerAnchorBinding.cs` (novo)
**Depends on**: T3
**Reuses**: `AnchorTracker`/`AnchorKind.Traveler` (Fase 18, spec) — sem modificação
**Requirement**: ITD-70, ITD-71, ITD-72

**Done when**:
- [ ] Linha de origem mantém âncora do viajante ausente por N anos sem outra âncora, nunca coletada (integração com `BranchCollectionSystem` da Fase 18)
- [ ] Morte permanente do viajante remove a âncora, tornando a linha elegível pra coleta normal

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): bind traveler as origin branch anchor even while absent`

---

### T13: Guard "nenhuma consulta mistura linhas"

**What**: Enumeração por reflexão de toda a superfície de consulta temporal; cada handler
exercido em 2 branches com `simuladoAté` diferentes, reprova se vazar tick/evento/entidade de
outra linha; par de mutação (remover filtro de `BranchId` deve derrubar o critério).
**Where**: `tests/LivingWorld.Tests/Timelines/TemporalQuerySeparationGuardTests.cs` (novo)
**Depends on**: T8, T11, T12
**Reuses**: mesmo padrão de guard de Fase 10 (`HistoryQuerySeparationGuard`)/Fase 17
(`DivinityQuerySeparationGuard`)
**Requirement**: (critério de verificação da spec — "nenhuma consulta mistura linhas")

**Done when**:
- [ ] Enumeração cobre 100% dos handlers de consulta temporal introduzidos por esta fase; falha se algum ficar sem cobertura
- [ ] Par de mutação: remover o filtro de `BranchId` por flag de teste derruba o critério
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit + enumeração por reflexão + par de mutação
**Gate**: build
**Commit**: `test(timelines): guard that no temporal query mixes branches, reflection + mutation pair`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Sequential, depends on Phase 2):
  T5 ──→ T6 ──→ T7

Phase 4 (depends on Phase 3):
  T7 ──→ T8

Phase 5 (Parallel, depends on Phase 3):
  T7 ──→ T9

Phase 6 (Sequential, depends on Phase 3):
  T7 ──→ T10 ──→ T11

Phase 7 (Parallel, depends on Phase 1):
  T3 ──→ T12

Phase 8 (last — depends on Phase 4, 6, 7):
  T8, T11, T12 ──→ T13
```

8 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fases
5 (pré-aquecimento) e 7 (âncora) são ramos independentes que correm em paralelo com as demais.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4, T5 | 1 ledger + 1 função pura | ✅ Granular |
| T6, T7 | 1 caminho sem trabalho + 1 caminho de replay com orçamento (mesmo componente, 2 tasks por complexidade) | ✅ Granular |
| T8 | 1 suíte de prova cross-process dedicada | ✅ Granular |
| T9 | 1 scheduler | ✅ Granular |
| T10, T11 | 1 invocação de potência + 1 resolvedor de identidade | ✅ Granular |
| T12 | 1 binding de âncora | ✅ Granular |
| T13 | 1 suíte de guard dedicada | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | None | ✅ Match |
| T3 | T1, T2 | T1,T2→T3 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | T6 | T6→T7 | ✅ Match |
| T8 | T7 | T7→T8 | ✅ Match |
| T9 | T7 | T7→T9 (paralelo à Fase 4/6) | ✅ Match |
| T10 | T7 | T7→T10 | ✅ Match |
| T11 | T10 | T10→T11 | ✅ Match |
| T12 | T3 | T3→T12 (paralelo) | ✅ Match |
| T13 | T8, T11, T12 | T8,T11,T12→T13 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T7 | Domain/business-logic | unit (+ baseline em T7) | unit / unit + baseline | ✅ OK |
| T8 | Suíte de prova cross-process dedicada | unit + cross-process | mesmo | ✅ OK |
| T9 | Scheduler de background | unit + par pré-aquecido/sob-demanda | mesmo | ✅ OK |
| T10, T11 | Invocação + resolvedor | unit / unit + conservação | mesmo | ✅ OK |
| T12 | Binding de âncora | unit | unit | ✅ OK |
| T13 | Guard final + build gate | unit + enumeração + mutação, build | mesmo | ✅ OK |

No task defers its own tests to a later task.
