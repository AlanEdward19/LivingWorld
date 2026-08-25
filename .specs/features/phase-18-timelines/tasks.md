# Fase 18 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-18-timelines/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 16/16.2/17 (xUnit, mundo
> controle/tratado, determinismo por seed, par de mutação, hash canônico via
> `WorldSnapshot.CanonicalHash`).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`TimelineJumpRequest`, `TimelineSeedDerivation`, `BranchAnchor`, `TimelineRules`) | unit | Construção/invariantes + determinismo puro | `tests/LivingWorld.Tests/Timelines/**` (novo) | `dotnet test --filter "FullyQualifiedName~Timelines"` |
| `TimelineJumpOrchestrator` | unit + par de mutação | 1:1 a TML-01..04, TML-30..34 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| `BranchFactory`/`TimelineSeedDerivation` | unit + cross-process | 1:1 a TML-10..14 (hash idêntico em 2 processos — teste out-of-process) | `tests/LivingWorld.Tests/Timelines/CrossProcessBranchHashTests.cs` (novo) | `dotnet test --filter "FullyQualifiedName~CrossProcessBranchHash"` |
| Dificuldade/inércia | unit + 4 pares base/tratamento | 1:1 a TML-20..22, 10/10 seeds cada fator | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| `AnchorTracker`/`BranchCollectionSystem` | unit + nightly | 1:1 a TML-40..43 | `tests/LivingWorld.Tests/Timelines/**` (gate) + `tests/nightly/**` (50/100/200 anos) | mesmo comando + `bash scripts/nightly.sh` |
| `BranchTreeQuery` | unit + CLI/API parity | 1:1 a TML-50..53 | `tests/LivingWorld.Tests/Timelines/BranchTreeQueryTests.cs`, `tests/LivingWorld.Tests/Timelines/BranchTreeCliTests.cs` (novos) | mesmo comando |
| Viajante materializado | unit | 1:1 a TML-60..62 | `tests/LivingWorld.Tests/Timelines/**` | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `History*`/`Extraordinary*`/`Population*`/`Cities*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Timelines) | Yes | Mundo próprio por teste | Padrão já usado em `ExtraordinaryInvocationEngineTests.cs` |
| par base/tratamento (inércia) | Yes | Mundo controle/tratado por teste (`PairedScenarioTests.cs`) | Padrão já usado nesses testes |
| cross-process (hash de branch) | Yes, mas isolado | Processo separado real (mesmo padrão de `InspectNpcCliTests.cs` out-of-process) | Fase 8 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |
| nightly (50/100/200 anos) | Não roda no gate padrão | Job separado, mesmo padrão já referenciado no critério de coleta | Fase 18 spec |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Timelines"` |
| Cross-process | Após T4 (seed derivation) e T6 (branch factory) | `dotnet test --filter "FullyQualifiedName~CrossProcessBranchHash"` |
| Full (integração) | Após tasks que tocam `MaterializationSystem`/CLI/API | `dotnet test --filter "Category!=Scenario&(FullyQualifiedName~Timelines\|FullyQualifiedName~Cities)"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Seed e snapshot (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Orquestração do salto (depende de Phase 2)

```
T5 → T6 → T7
```

### Phase 4: Âncora e coleta (depende de Phase 3)

```
T7 → T8 → T9
```

### Phase 5: Viajante materializado (Parallel OK, depende de Phase 3)

```
T7 → T10
```

### Phase 6: Árvore consultável (depende de Phase 4)

```
T9 → T11 → T12
```

---

## Task Breakdown

### T1: `BranchAnchor`, `AnchorKind`, `TimelineRules`

**What**: Records de domínio: `BranchAnchor(Branch, Kind, RefId)`, enum `AnchorKind`,
`TimelineRules(MaxLiveBranches, CollectionGraceTicks)`.
**Where**: `src/LivingWorld.Domain/Timelines/BranchAnchor.cs` (novo),
`src/LivingWorld.Domain/Timelines/TimelineRules.cs` (novo)
**Depends on**: None
**Reuses**: mesmo padrão de record de regra de cenário (`PerfRules`/`HistoryRules`)
**Requirement**: (suporta TML-40..43)

**Done when**:
- [ ] Cenário sem `TimelineRules` declarado usa defaults documentados

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add BranchAnchor and TimelineRules`

---

### T2: `TimelineJumpRequest`, `TimelineJumpOutcome`

**What**: `record TimelineJumpRequest(OriginBranch, DivergenceTick, InterventionId, TravelerId)`,
`enum TimelineJumpOutcome { Stillborn, NoBranch, PartialSuccess, Success, CriticalSuccess }`.
**Where**: `src/LivingWorld.Domain/Timelines/TimelineJumpResult.cs` (novo)
**Depends on**: None
**Reuses**: nenhum tipo de resultado paralelo ao `ResolutionResult` do `Resolver` — mapeamento 1:1 documentado
**Requirement**: TML-30..34

**Done when**:
- [ ] `TimelineJumpOutcome` mapeia 1:1 aos 5 níveis de `ResolutionResult` (documentado em comentário)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add TimelineJumpRequest and outcome mapping`

---

### T3: `WorldEventKind` — 2 valores novos

**What**: `TimelineJumped`, `BranchCollected`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria, suporta TML-01, TML-41)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add timeline WorldEventKind values`

---

### T4: `TimelineSeedDerivation`

**What**: Função pura `Derive(seedOrigin, divergenceTick, interventionId) -> long`, hash estável
(nunca `string.GetHashCode`), recursiva pra branch-de-branch.
**Where**: `src/LivingWorld.Domain/Timelines/TimelineSeedDerivation.cs` (novo)
**Depends on**: T3
**Reuses**: mesma disciplina de hash estável já usada por `WorldRngRegistry.Stream`'s `StableHash`
**Requirement**: TML-11, TML-12, TML-13

**Done when**:
- [ ] Mesma entrada produz mesma saída em processos separados (teste cross-process)
- [ ] Derivação recursiva (`seed_neto = Derive(seed_filho, ...)`) funciona sem tratamento especial

**Tests**: unit + cross-process
**Gate**: cross-process
**Commit**: `feat(timelines): add stable cross-process seed derivation`

---

### T5: `BranchFactory` — snapshot + copy-on-write

**What**: Constrói `BranchId` novo a partir de `PersistentWorldRunner.LoadAt(divergenceTick)`,
grava só o delta via `IWorldRepository`; recusa se `TimelineRules.MaxLiveBranches` atingido.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchFactory.cs` (novo)
**Depends on**: T4
**Reuses**: `WorldSnapshot.Serialize/Deserialize`, `PersistentWorldRunner.LoadAt`, `IWorldRepository` (todos Fase 1/3, sem modificação)
**Requirement**: TML-10, TML-14

**Done when**:
- [ ] Branch referencia snapshot de T e grava só divergência (medido: armazenamento não escala com população da mãe)
- [ ] Teto de branches vivos recusa criação (nunca coleta antecipada por pressão)
- [ ] Salto pra tick futuro (sem snapshot) recusa antes de qualquer rolagem

**Tests**: unit + cross-process
**Gate**: cross-process
**Commit**: `feat(timelines): add copy-on-write branch factory over existing snapshot/replay`

---

### T6: `TimelineJumpOrchestrator` — rolagem e evento anexado

**What**: Calcula dificuldade (delega ao modelo de inércia da Fase 10), chama
`Resolver.Resolve` sobre `WorldRngRegistry.Stream("timeline-jump")` da mãe, anexa
`WorldEventKind.TimelineJumped` sempre (inclusive falha), delega pra `BranchFactory` conforme
outcome.
**Where**: `src/LivingWorld.Simulation/Timelines/TimelineJumpOrchestrator.cs` (novo)
**Depends on**: T5
**Reuses**: `Resolver.Resolve`/`VarianceProfileCatalog.Get("Dramatico")` (ADR-0011), dificuldade já calculada pela Fase 10, `WorldRngRegistry.Stream`
**Requirement**: TML-01, TML-20, TML-21, TML-22, TML-30, TML-31, TML-32, TML-33, TML-34

**Done when**:
- [ ] Todo salto anexa `TimelineJumped` ao log da mãe, nunca `UPDATE`
- [ ] `CriticalFailure` → branch natimorto; `Failure` → nenhum `BranchId` novo
- [ ] `PartialSuccess` sempre carrega consequência declarada; `Success`/`CriticalSuccess` sem consequência negativa
- [ ] 4 pares base/tratamento (significância, testemunhas, registro escrito, grau causal) — cada um isoladamente reduz taxa de sucesso, 10/10 seeds
- [ ] Resultado reproduzível pela mesma seed/origem/tick/intervenção

**Tests**: unit + 4 pares base/tratamento
**Gate**: quick
**Commit**: `feat(timelines): implement jump orchestrator with resolver-based outcome mapping`

---

### T7: `TimelineJumpOrchestrator` — mãe intocada + proteção contra reescrita

**What**: Garantir e testar que nenhuma ação em B altera `Hash(mãe)`; escrita retroativa real na
mãe retorna `Failure`.
**Where**: `src/LivingWorld.Simulation/Timelines/TimelineJumpOrchestrator.cs` (modificado, se
necessário; provavelmente já correto por construção — task é de verificação/hardening)
**Depends on**: T6
**Reuses**: `WorldSnapshot.CanonicalHash`, `IncrementalHasher` (sem modificação — só consumido pelo teste)
**Requirement**: TML-02, TML-03, TML-04

**Done when**:
- [ ] Hash de A capturado no tick de divergência permanece idêntico após 10 anos simulados de atividade em B (mortes, guerra, coleta)
- [ ] Escrita retroativa real tentada no log de A retorna `Failure`, `Hash(A)` inalterado
- [ ] Par de mutação: desligar a proteção por flag de teste derruba o critério anterior

**Tests**: unit + par de mutação
**Gate**: quick
**Commit**: `test(timelines): prove mother line stays byte-identical, add mutation-tested write guard`

---

### T8: `AnchorTracker`

**What**: Adiciona/remove `BranchAnchor` por `BranchId`; consulta "sem âncora" O(1).
**Where**: `src/LivingWorld.Simulation/Timelines/AnchorTracker.cs` (novo)
**Depends on**: T7
**Reuses**: `BranchAnchor`/`AnchorKind` (T1)
**Requirement**: (suporta TML-40)

**Done when**:
- [ ] Branch com pelo menos 1 âncora nunca aparece na lista de elegíveis pra coleta
- [ ] Remover a última âncora torna o branch elegível no próximo tick de avaliação

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): add anchor tracker for branch viability`

---

### T9: `BranchCollectionSystem`

**What**: `ISimulationSystem` que coleta branches sem âncora há `CollectionGraceTicks`, ordem
determinística (`BranchId` crescente), anexa `BranchCollected` no log da própria linha
coletada.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchCollectionSystem.cs` (novo)
**Depends on**: T8
**Reuses**: mesma cadência de sistema Daily/Hourly já usada por `MaterializationSystem`; disciplina de budget+coleta de `ColdTierArchive` (Fase 9) como precedente estrutural
**Requirement**: TML-40, TML-41, TML-42, TML-43

**Done when**:
- [ ] Sem âncora, coletado em ≤ K ticks (`K` do cenário)
- [ ] Evento de coleta anexado no log da própria linha coletada, entra no hash canônico dela
- [ ] Teto atingido: priorização determinística (ordem por `BranchId`), nunca varredura oportunista
- [ ] Teste nightly de 50/100/200 anos: regressão linear do total de branches vivos sem inclinação positiva

**Tests**: unit + nightly
**Gate**: quick (unit) + nightly job separado
**Commit**: `feat(timelines): implement deterministic branch collection system`

---

### T10: Viajante materializado — extensão de `HasFormalRole`

**What**: Viajante recém-chegado qualifica como papel formal em `MaterializationSystem`
(materializado completo desde a chegada); dali em diante é `Npc` comum sujeito à LOD normal.
**Where**: `src/LivingWorld.Simulation/Cities/MaterializationSystem.cs` (modificado, aditivo)
**Depends on**: T7
**Reuses**: `MaterializationSystem.HasFormalRole`/`EnsureMaterialized` (Fase 8), sem novo conceito de LOD
**Requirement**: TML-60, TML-61, TML-62

**Done when**:
- [ ] Viajante nasce sempre como `Npc` completo no branch (nunca "meio-materializado")
- [ ] Viajante sem observação por 20 anos simulados é agregado pela mesma mecânica da Fase 9/`MaterializationSystem`, sem exceção lançada
- [ ] Ao retomar observação, estado é coerente com a agregação ocorrida (idade avançou, eventos aplicados)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(cities): materialize timeline travelers via existing formal-role LOD path`

---

### T11: `BranchTreeQuery`

**What**: `Inspect(WorldState, BranchId? root) -> Result<BranchTreeDto>` — somente leitura,
cadeia completa até a raiz, sem limite de profundidade.
**Where**: `src/LivingWorld.Simulation/Timelines/BranchTreeQuery.cs` (novo)
**Depends on**: T9
**Reuses**: mesmo padrão estrutural de `NpcInspectionQuery` (Fase 8)
**Requirement**: TML-50, TML-51, TML-53

**Done when**:
- [ ] Resposta inclui, por branch: `BranchId`, origem, tick de divergência, intervenção, âncoras ativas, estado (`Alive`/`Collected`/`Stillborn`)
- [ ] Cadeia completa até a raiz, testado com 3+ gerações
- [ ] Nenhuma escrita ocorre como efeito colateral da consulta

**Tests**: unit
**Gate**: quick
**Commit**: `feat(timelines): add read-only branch tree query`

---

### T12: API + CLI parity pra `BranchTreeQuery`

**What**: `MapGet` em `src/LivingWorld.Api/Program.cs` + verbo CLI em
`src/LivingWorld.Workers/Program.cs`, ambos chamando `BranchTreeQuery.Inspect`.
**Where**: `src/LivingWorld.Api/Program.cs` (modificado), `src/LivingWorld.Workers/Program.cs`
(modificado), `tests/LivingWorld.Tests/Timelines/BranchTreeCliTests.cs` (novo, out-of-process
mesmo padrão de `InspectNpcCliTests.cs`)
**Depends on**: T11
**Reuses**: exato seam de `NpcInspectionQuery`/`inspect-npc` (Fase 8)
**Requirement**: TML-52

**Done when**:
- [ ] CLI e API retornam dados consistentes pro mesmo `BranchId` (mesmo modelo subjacente, sem drift)
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit + CLI/API parity
**Gate**: build
**Commit**: `feat(api,cli): expose branch tree query via matching endpoints`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Sequential, depends on Phase 2):
  T5 ──→ T6 ──→ T7

Phase 4 (Sequential, depends on Phase 3):
  T7 ──→ T8 ──→ T9

Phase 5 (Parallel, depends on Phase 3):
  T7 ──→ T10

Phase 6 (Sequential, depends on Phase 4):
  T9 ──→ T11 ──→ T12
```

6 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fase 5
(T10) é ramo independente que pode correr em paralelo com Fase 4.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4 | 1 função pura | ✅ Granular |
| T5 | 1 factory (snapshot + copy-on-write + teto) | ✅ Granular |
| T6, T7 | 1 orquestrador (rolagem+evento) + 1 task de hardening/prova dedicada | ✅ Granular |
| T8, T9 | 1 tracker + 1 sistema | ✅ Granular |
| T10 | 1 extensão pontual de sistema existente | ✅ Granular |
| T11, T12 | 1 query + 1 exposição dupla (API+CLI) | ✅ Granular |

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
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T7 | T7→T10 (paralelo à Fase 4) | ✅ Match |
| T11 | T9 | T9→T11 | ✅ Match |
| T12 | T11 | T11→T12 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T4 | Domain/business-logic | unit (+ cross-process em T4) | unit / unit + cross-process | ✅ OK |
| T5 | Simulation factory | unit + cross-process | unit + cross-process | ✅ OK |
| T6, T7 | Orquestração + prova de invariante | unit + par base/tratamento / unit + mutação | mesmo | ✅ OK |
| T8, T9 | Sistema + coleta | unit (+ nightly em T9) | unit (+ nightly) | ✅ OK |
| T10 | Extensão de sistema existente | unit | unit | ✅ OK |
| T11, T12 | Query + exposição | unit / unit + parity | mesmo | ✅ OK |

No task defers its own tests to a later task.
