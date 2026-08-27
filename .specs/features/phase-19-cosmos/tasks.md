# Fase 19 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-19-cosmos/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Guidelines found: none dedicada — segue o padrão já usado nas specs 8/16/17/18 (xUnit, mundo
> controle/tratado, determinismo por seed, round-trip de materialização, teste de conhecimento
> limitado da Fase 11).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`CelestialBody`, `OrbitalElements`, `AstronomicalEvent`, `CosmosRules`, `DelayedOrder`) | unit | Construção/invariantes | `tests/LivingWorld.Tests/Cosmos/**` (novo) | `dotnet test --filter "FullyQualifiedName~Cosmos"` |
| `EphemerisCalculator` | unit | 1:1 a COS-20, determinismo puro | `tests/LivingWorld.Tests/Cosmos/**` | mesmo comando |
| `AstronomicalProductionModifier`/`AstronomicalBeliefFilter` | unit + par base/tratamento | 1:1 a COS-21, COS-22, COS-23 | `tests/LivingWorld.Tests/Cosmos/**` | mesmo comando |
| `CosmosMaterializationBridge` | unit + conservação + round-trip | 1:1 a COS-01, COS-04, COS-10..12, COS-30..32 | `tests/LivingWorld.Tests/Cosmos/**` | mesmo comando |
| Isolamento sem contato (hash) | unit + par com/sem degrau | 1:1 a COS-02, COS-03 | `tests/LivingWorld.Tests/Cosmos/HashIsolationTests.cs` (novo) | mesmo comando |
| Reflexão "alien não é tipo novo" | unit + enumeração por reflexão | 1:1 a COS-40..42 | `tests/LivingWorld.Tests/Cosmos/AlienSurfaceGuardTests.cs` (novo) | mesmo comando |
| `ContactOutcomeResolver` | unit + parâmetros variados | 1:1 a COS-50..52 | `tests/LivingWorld.Tests/Cosmos/**` | mesmo comando |
| `DelayedOrderQueue`/`ColonyDivergenceTracker` | unit + par com/sem ordem (família Fase 11) | 1:1 a COS-60..62 | `tests/LivingWorld.Tests/Cosmos/**` | mesmo comando |
| Full regression | build gate | Backend inteiro verde, sem regressão em `Cities*`/`Economy*`/`History*` | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Cosmos) | Yes | Mundo próprio por teste | Padrão já usado em `ExtraordinaryInvocationEngineTests.cs` |
| par base/tratamento (eclipse, ordem, contato) | Yes | Mundo controle/tratado por teste (`PairedScenarioTests.cs`) | Fase 8/17/18 |
| round-trip (contato) | Yes | Mesmo padrão de `MaterializationRoundTripTests.cs` | Fase 8 |
| enumeração por reflexão | Yes | Mesmo padrão de guards de separação da Fase 10/17 | Fase 10/17 |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial | `.specs/STATE.md` |
| nightly (100 anos, isolamento de hash) | Não roda no gate padrão | Job separado | Fase 19 spec |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Após cada task de domínio/sistema | `dotnet test --filter "FullyQualifiedName~Cosmos"` |
| Full (integração) | Após tasks que tocam `Cities`/`Economy` | `dotnet test --filter "Category!=Scenario&(FullyQualifiedName~Cosmos\|FullyQualifiedName~Cities)"` |
| Build | Última task (antes do Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Fundação de dados (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Efeméride (depende de Phase 1)

```
T3 → T4 → T5
```

### Phase 3: Consequência dupla (depende de Phase 2)

```
T5 → T6 → T7
```

### Phase 4: Materialização/contato (Parallel OK, depende de Phase 1)

```
T3 → T8 → T9
```

### Phase 5: Isolamento sem contato (depende de Phase 4)

```
T9 → T10
```

### Phase 6: Alien não é tipo novo (depende de Phase 4)

```
T9 → T11
```

### Phase 7: Assimetria tecnológica (depende de Phase 4)

```
T9 → T12
```

### Phase 8: Colônia e atraso (Parallel OK, depende de Phase 1)

```
T3 → T13 → T14
```

---

## Task Breakdown

### T1: `CelestialBodyId`, `OrbitalElements`, `CelestialBody`, `SystemAggregatePool`

**What**: Records de domínio novos.
**Where**: `src/LivingWorld.Domain/Cosmos/CelestialBody.cs` (novo)
**Depends on**: None
**Reuses**: mesmo shape conceitual de `AggregatePopulationPool` (Fase 8), um nível acima
**Requirement**: COS-10

**Done when**:
- [ ] `CelestialBody` sem `OrbitalElements` (estrela central) é caso válido, não exceção

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add CelestialBody, OrbitalElements and SystemAggregatePool`

---

### T2: `CosmosRules`, `DelayedOrder`

**What**: `record CosmosRules(ContactMortalityRate, ColonyIndependenceThreshold,
OrbitalDistanceToTickFactor)`, `record DelayedOrder(ColonyId, IssuedAtTick, DeliveryTick,
PayloadId)`.
**Where**: `src/LivingWorld.Domain/Cosmos/CosmosRules.cs` (novo)
**Depends on**: None
**Reuses**: mesmo padrão de record de regra de cenário
**Requirement**: (suporta COS-51, COS-60..62)

**Done when**:
- [ ] `ContactMortalityRate=0` é caso default documentado (desfecho de doença desligado)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add CosmosRules and DelayedOrder`

---

### T3: `WorldEventKind` — 4 valores novos

**What**: `CelestialContactEstablished`, `AstronomicalEventOccurred`, `OrderDelivered`,
`ColonyMarkedIndependent`.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (modificado, aditivo)
**Depends on**: T1, T2
**Reuses**: enum existente, aditivo
**Requirement**: (auditoria)

**Done when**:
- [ ] Nenhum valor existente do enum muda de posição/significado

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add cosmos WorldEventKind values`

---

### T4: `EphemerisCalculator`

**What**: Função pura, elementos orbitais → lista de `AstronomicalEvent` numa janela de ticks.
**Where**: `src/LivingWorld.Domain/Cosmos/EphemerisCalculator.cs` (novo)
**Depends on**: T3
**Reuses**: nenhuma dependência de RNG (mecânica celeste é cálculo, não sorteio)
**Requirement**: COS-20

**Done when**:
- [ ] Mesmos elementos orbitais + mesma janela produzem sempre os mesmos eventos (determinismo puro)
- [ ] Corpo sem `OrbitalElements` nunca gera evento próprio, só participa como referencial

**Tests**: unit
**Gate**: quick
**Commit**: `feat(cosmos): add pure ephemeris calculator for astronomical events`

---

### T5: `AstronomicalEvent` — cobertura de tipos (eclipse/estação/cometa/conjunção)

**What**: Garantir que `EphemerisCalculator` cobre os 4 tipos declarados no domínio.
**Where**: `src/LivingWorld.Domain/Cosmos/EphemerisCalculator.cs` (modificado/completado)
**Depends on**: T4
**Reuses**: mesma função pura de T4
**Requirement**: COS-20

**Done when**:
- [ ] Solstício/equinócio, eclipse, cometa e conjunção têm pelo menos 1 caso de teste cada

**Tests**: unit
**Gate**: quick
**Commit**: `feat(cosmos): cover all astronomical event kinds in ephemeris calculator`

---

### T6: `AstronomicalProductionModifier`

**What**: Consultado pela produção agrícola existente (Fase 5) — multiplicador objetivo quando
evento ativo na janela de colheita.
**Where**: `src/LivingWorld.Simulation/Cosmos/AstronomicalProductionModifier.cs` (novo), ponto de
consumo em `src/LivingWorld.Simulation/Economy/**` (modificado, aditivo — local exato depende do
sistema de produção agrícola já existente da Fase 5)
**Depends on**: T5
**Reuses**: sistema de produção já existente (Fase 5), sem reescrita
**Requirement**: COS-21, COS-23

**Done when**:
- [ ] Modificador aplica independente de qualquer cultura "saber" do fenômeno
- [ ] Par base/tratamento (eclipse na colheita) — produção menor no tratado, 10/10 seeds, margem > spread do baseline
- [ ] Par separado com estação adversa — mesmo critério

**Tests**: unit + par base/tratamento
**Gate**: quick
**Commit**: `feat(cosmos): apply astronomical events as objective agricultural production modifier`

---

### T7: `AstronomicalBeliefFilter`

**What**: Consultado pela camada de crença (Fase 10/17) — presságio vs. efeméride prevista,
filtrado pelo conhecimento astronômico da cultura (interface assumida da Fase 13).
**Where**: `src/LivingWorld.Simulation/Cosmos/AstronomicalBeliefFilter.cs` (novo)
**Depends on**: T6
**Reuses**: mesmo evento de T4/T5, nunca recalculado
**Requirement**: COS-22

**Done when**:
- [ ] Cultura sem conhecimento astronômico recebe presságio; cultura com conhecimento recebe efeméride prevista — mesmo `AstronomicalEvent` de entrada nos dois casos
- [ ] Interface de conhecimento cultural usa o campo assumido documentado no design (reconciliação futura com Fase 13 anotada no código)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(cosmos): filter astronomical events into omen vs. prediction by cultural knowledge`

---

### T8: `CosmosMaterializationBridge` — promoção por contato

**What**: Evento de contato promove região correspondente do agregado, herdando
`SystemAggregatePool` proporcionalmente.
**Where**: `src/LivingWorld.Simulation/Cosmos/CosmosMaterializationBridge.cs` (novo)
**Depends on**: T3
**Reuses**: `MaterializationSystem`/`EnsureMaterialized` (Fase 8), sem modificação da classe
**Requirement**: COS-01, COS-04, COS-30, COS-31

**Done when**:
- [ ] Conservação: soma do agregado + `COUNT(*)` promovido bate com o total, sem tocar propriedade derivada (mesmo teste `LodConservationScenarioTests.cs`, extensão pro degrau `sistema`)
- [ ] Civilização distante declarada existe agregada desde tick 0, sem contato
- [ ] Contato promove região com cultura/liderança/economia coerentes com o agregado de origem

**Tests**: unit + conservação
**Gate**: quick
**Commit**: `feat(cosmos): implement contact-triggered materialization bridge with conservation`

---

### T9: Round-trip de contato

**What**: Promover→desmaterializar região de contato preserva `Hash(world)`.
**Where**: `tests/LivingWorld.Tests/Cosmos/ContactRoundTripTests.cs` (novo), possíveis ajustes em
`CosmosMaterializationBridge` (T8) se o round-trip revelar gap
**Depends on**: T8
**Reuses**: mesmo padrão de `MaterializationRoundTripTests.cs` (Fase 8)
**Requirement**: COS-32

**Done when**:
- [ ] Round-trip preserva hash byte-idêntico, totais de população/recurso/produção inclusos

**Tests**: unit + round-trip
**Gate**: quick
**Commit**: `test(cosmos): prove contact promotion round-trip preserves canonical hash`

---

### T10: Isolamento sem contato (hash byte-idêntico)

**What**: Degrau `sistema` habilitado sem nenhum contato agendado não altera o hash canônico vs.
mundo sem o degrau; com contato, diverge.
**Where**: `tests/LivingWorld.Tests/Cosmos/HashIsolationTests.cs` (novo)
**Depends on**: T9
**Reuses**: `WorldSnapshot.CanonicalHash` (sem modificação)
**Requirement**: COS-02, COS-03

**Done when**:
- [ ] Hash idêntico em 10 anos simulados sem contato (gate); 100 anos nightly
- [ ] Braço com contato agendado diverge do braço sem contato, mesma seed

**Tests**: unit (gate) + nightly (100 anos)
**Gate**: quick (unit) + nightly job separado
**Commit**: `test(cosmos): prove system-tier LOD is hash-neutral without contact`

---

### T11: Guard "alien não é tipo novo"

**What**: Enumeração por reflexão dos sistemas alcançados por civilização contatante vs. cultura
nativa — cobertura nos dois sentidos.
**Where**: `tests/LivingWorld.Tests/Cosmos/AlienSurfaceGuardTests.cs` (novo)
**Depends on**: T9
**Reuses**: mesmo padrão de guard de separação já usado na Fase 10/17
**Requirement**: COS-40, COS-41, COS-42

**Done when**:
- [ ] Enumeração reprova se civilização contatante tocar handler/tabela/campo exclusivo
- [ ] Enumeração reprova também no sentido oposto (sistema alcançado por cultura nativa sem par testado do lado alien)
- [ ] Degrau tecnológico declarado vem do vocabulário de módulos de conteúdo (interface assumida da Fase 13, documentada)

**Tests**: unit + enumeração por reflexão
**Gate**: quick
**Commit**: `test(cosmos): guard that alien civilizations reuse the exact native system surface`

---

### T12: `ContactOutcomeResolver`

**What**: Desfecho de encontro assimétrico calculado via `Resolver.Resolve` a partir de valores
culturais + coesão política (interface assumida) + intenção declarada; mortalidade por doença
parametrizada.
**Where**: `src/LivingWorld.Simulation/Cosmos/ContactOutcomeResolver.cs` (novo)
**Depends on**: T9
**Reuses**: `Resolver.Resolve`/`VarianceProfileCatalog` (ADR-0011), `MortalityPlanner` existente
(Fase 3/4) pra aplicar `ContactMortalityRate`
**Requirement**: COS-50, COS-51, COS-52

**Done when**:
- [ ] Desfecho nunca vem de tabela fixa de sorteio — sempre função de parâmetros declarados
- [ ] `ContactMortalityRate=0` nunca aplica mortalidade adicional; taxa > 0 aplica via `MortalityPlanner` existente, sem novo pipeline de morte
- [ ] Parâmetros culturais/políticos diferentes produzem desfechos observavelmente diferentes (ex.: coesão alta reduz taxa de colapso cultural)

**Tests**: unit + parâmetros variados
**Gate**: quick
**Commit**: `feat(cosmos): implement contact outcome resolver over cultural and political parameters`

---

### T13: `DelayedOrderQueue`

**What**: Fila de `DelayedOrder` — ordem só visível à colônia quando `currentTick >=
DeliveryTick`, calculado por distância orbital.
**Where**: `src/LivingWorld.Simulation/Cosmos/DelayedOrderQueue.cs` (novo)
**Depends on**: T3
**Reuses**: mesma disciplina conceitual de evento anexado com tick alvo (Fase 18, spec paralela — implementação própria aqui)
**Requirement**: COS-60, COS-61

**Done when**:
- [ ] Ordem antes do tick de entrega retorna "nenhuma pendente visível", nunca expõe payload antecipado
- [ ] Par com ordem plantada (entrega futura) vs. braço sem ordem nenhuma: decisão da colônia byte-idêntica até o tick de entrega (mesma família do teste de conhecimento limitado da Fase 11)

**Tests**: unit + par com/sem ordem
**Gate**: quick
**Commit**: `feat(cosmos): implement delayed order queue with orbital-distance delivery tick`

---

### T14: `ColonyDivergenceTracker` — independência sem entidade nova

**What**: Acumula divergência cultural; ao ultrapassar limiar, marca `City.IsIndependent=true` —
sem criar entidade política/cultural nova.
**Where**: `src/LivingWorld.Simulation/Cosmos/ColonyDivergenceTracker.cs` (novo),
`src/LivingWorld.Domain/Cities/City.cs` (modificado, campo aditivo `IsIndependent`)
**Depends on**: T13
**Reuses**: `City` existente (Fase 8), sem nova classe de entidade política
**Requirement**: COS-62

**Done when**:
- [ ] Divergência acima do limiar marca a `City` existente, sem alterar sua contagem/identidade
- [ ] Gate final: `bash scripts/test.sh` verde (backend completo)

**Tests**: unit
**Gate**: build
**Commit**: `feat(cities): mark colony independence via accumulated divergence, no new entity`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2 ──→ T3

Phase 2 (Sequential, depends on Phase 1):
  T3 ──→ T4 ──→ T5

Phase 3 (Sequential, depends on Phase 2):
  T5 ──→ T6 ──→ T7

Phase 4 (Sequential, depends on Phase 1, parallel with Phase 2/3):
  T3 ──→ T8 ──→ T9

Phase 5 (depends on Phase 4):
  T9 ──→ T10

Phase 6 (depends on Phase 4, parallel with Phase 5):
  T9 ──→ T11

Phase 7 (depends on Phase 4, parallel with Phase 5/6):
  T9 ──→ T12

Phase 8 (Sequential, depends on Phase 1, parallel with everything else):
  T3 ──→ T13 ──→ T14
```

8 fases > 3 — Execute vai oferecer delegação por sub-agent por fase (offer-then-confirm). Fases
2/3 (efeméride), 4 (materialização) e 8 (colônia) são ramos independentes que podem correr em
paralelo assim que a Fase 1 termina.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2, T3 | 1 conjunto de modelos/enum cada | ✅ Granular |
| T4, T5 | 1 função pura + 1 task de cobertura de tipos | ✅ Granular |
| T6, T7 | 1 modificador de produção + 1 filtro de crença | ✅ Granular |
| T8, T9 | 1 bridge de materialização + 1 suíte de round-trip dedicada | ✅ Granular |
| T10, T11, T12 | 1 suíte de isolamento + 1 guard de reflexão + 1 resolver | ✅ Granular |
| T13, T14 | 1 fila de entrega + 1 tracker de divergência | ✅ Granular |

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
| T8 | T3 | T3→T8 | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | T9 | T9→T10 | ✅ Match |
| T11 | T9 | T9→T11 | ✅ Match |
| T12 | T9 | T9→T12 | ✅ Match |
| T13 | T3 | T3→T13 | ✅ Match |
| T14 | T13 | T13→T14 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T8 | Domain/business-logic | unit (+ conservação em T8) | unit / unit + conservação | ✅ OK |
| T9, T10 | Suítes de prova dedicadas | round-trip / hash isolation + nightly | mesmo | ✅ OK |
| T11 | Guard de reflexão | unit + enumeração | mesmo | ✅ OK |
| T12, T13, T14 | Sistema/fila/tracker | unit (+ par em T13), build gate final em T14 | mesmo | ✅ OK |

No task defers its own tests to a later task.
