# Fase 16.4 — World Realism Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill
is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy
review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-16-4-world-realism/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase (`AGENTS.md`, `scripts/test.sh`, `tests/LivingWorld.Tests/Extraordinary/*`,
> `tests/LivingWorld.Tests/Performance/ScaleScenarioSensorTests.cs`). Guideline found: `AGENTS.md`
> line 12-13 — "Rode tarefa repetível por `bash scripts/<x>.sh`... antes de dizer pronto,
> `bash scripts/verify.sh` deve sair 0." Single test project (xUnit), no separate unit/e2e split
> — the repo's own layering is domain/simulation vs. performance-sensor tests.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain model (`Animal`, `Plant`, `CombatEncounter`, species-rules records) | unit (xUnit) | All branches; 1:1 to spec ACs; every listed edge case | `tests/LivingWorld.Tests/Ecology/*.cs`, `tests/LivingWorld.Tests/Extraordinary/*.cs` | `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Extraordinary"` |
| Simulation system (`FaunaLifecycleSystem`, `FloraLifecycleSystem`, `TemperatureSeasonSystem`, `CombatEncounterSystem`, foresight hook, possession resistance) | unit (xUnit, deterministic-seed style already used by `CombatMechanicTests`/`ForesightMechanicTests`) | All branches; 1:1 to spec ACs; every listed edge case; determinism re-run (two-process style already used by the time engine) | `tests/LivingWorld.Tests/Ecology/*.cs`, `tests/LivingWorld.Tests/Extraordinary/*.cs` | same as above |
| Reflection/hasher classification (`Animal.Energy`, `CombatEncounter` new fields) | unit (xUnit, generated-by-reflection, same pattern as Fase 1 canônico/volátil test) | Every new field classified — unclassified field fails the generated test | `tests/LivingWorld.Tests/Snapshot/*` (existing file, extended) | `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot"` |
| Performance/scale sensor | integration (scenario-style, existing `ScaleScenarioSensorTests` pattern) | Confirms fauna/flora at declared population does not exceed `PerfRules.MaxMicrosPerAliveNpcTick`/`MaxBytesAllocPerTick` | `tests/LivingWorld.Tests/Performance/ScaleScenarioSensorTests.cs` (extended) | `bash scripts/test.sh --filter "FullyQualifiedName~Performance"` |
| Full solution build/lint/test gate | build | Whole-repo gate, 0 failures | — | `bash scripts/verify.sh` |

## Parallelism Assessment

> Generated from codebase — `scripts/test.sh` runs `dotnet test` over the whole solution with
> `[Collection]`/shared `WorldState` fixtures per existing test file (no shared DB/external
> store — each test builds its own in-memory `WorldState`).

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Domain model unit | Yes | Each test constructs its own `WorldState`/record instances, no shared static state | `tests/LivingWorld.Tests/Extraordinary/CombatMechanicTests.cs` pattern (per-test world) |
| Simulation system unit | Yes | Same — per-test `WorldState` + explicit seed | `tests/LivingWorld.Tests/Extraordinary/ForesightMechanicTests.cs` pattern |
| Reflection/hasher | No | Generated test enumerates ALL record types via reflection in one pass — adding a field changes a shared generated list | `tests/LivingWorld.Tests/Snapshot/*` (single generated test file) |
| Performance/scale sensor | No | Measures wall-clock/allocation — concurrent runs on shared CI hardware skew the measurement | `ScaleScenarioSensorTests.cs` (already run isolated, per repo convention) |

## Gate Check Commands

> Generated from codebase.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After a task touching only domain/simulation unit tests | `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Extraordinary\|FullyQualifiedName~Snapshot"` |
| Full | After a task touching performance/scale or cross-system integration | `bash scripts/test.sh` (whole suite, no filter) |
| Build | After the last task of the feature | `bash scripts/verify.sh` |

---

## Execution Plan

### Phase 1: Fundação — Temperatura sazonal (Sequential)

Pré-requisito de sentido pra Fauna e Flora (ambos leem temperatura da célula).

```
T1 → T2
```

### Phase 2: Fauna (Sequential, depende da Fase 1)

```
T3 → T4 → T5 → T6
```

### Phase 3: Flora (Sequential, depende da Fase 1; pode rodar em paralelo com a Fase 2 — não compartilha estado)

```
T7 → T8 → T9
```

### Phase 4: Combate multi-round (Sequential, depende de decisão de compatibilidade em T10)

```
T10 → T11 → T12 → T13
```

### Phase 5: Instanciação — herança real (Sequential, depende de T1-T9 só pra ordem declarada no spec, sem dependência de código real)

```
T14 → T15 → T16
```

### Phase 6: Foresight informa decisão (Sequential)

```
T17 → T18
```

### Phase 7: Possessão com resistência (Sequential)

```
T19 → T20
```

### Phase 8: Fechamento (Sequential, depende de todas as anteriores)

```
T21 → T22
```

---

## Task Breakdown

### T1: Classificar `EnvironmentTemperatureAdjustments` sazonal no hasher e criar `AnimalSpeciesRules`/`PlantSpeciesRules` de cenário

**What**: Adiciona ao cenário (`ScenarioRunner`/config já existente) as listas
`AnimalSpeciesRules`/`PlantSpeciesRules` (records definidos no Design) e garante que o teste
gerado por reflexão (Fase 1, canônico/volátil) classifica corretamente qualquer campo novo que
`TemperatureSeasonSystem` vier a introduzir em `WorldState` (nenhum campo novo em `MapCell`,
só overlay já existente — mas o teste de reflexão precisa rodar e confirmar isso antes de
qualquer código de sistema, pra travar a decisão do Design: "overlay único, sem campo novo").
**Where**: `src/LivingWorld.Domain/Ecology/AnimalSpeciesRules.cs` (novo),
`src/LivingWorld.Domain/Ecology/PlantSpeciesRules.cs` (novo), cenário de referência (arquivo já
usado por `ScenarioRunner`)
**Depends on**: None
**Reuses**: `EnvironmentTemperatureAdjustment` (shape existente, sem mudança)
**Requirement**: REALISM-01 (pré-requisito de dados), REALISM-07 (idem)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] `AnimalSpeciesRules`/`PlantSpeciesRules` existem com os campos do Design
- [x] Teste gerado por reflexão (Fase 1) passa sem exigir classificação nova (confirma que
      nenhum campo canônico/volátil ficou sem categoria)
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot"`

**Tests**: unit
**Gate**: quick

---

### T2: `TemperatureSeasonSystem` — temperatura varia por estação/bioma sem poder ativo

**What**: Implementa `TemperatureSeasonSystem.Apply`, rodando no `Daily` tick em que a estação
muda, escrevendo um `EnvironmentTemperatureAdjustment` perpétuo-por-estação por região/bioma
(substituído na próxima mudança de estação).
**Where**: `src/LivingWorld.Simulation/Geography/TemperatureSeasonSystem.cs`
**Depends on**: T1
**Reuses**: `EnvironmentTemperatureMechanic.EffectiveTemperature` (leitura combinada, sem
mudança), `EnvironmentTemperatureAdjustments` (overlay existente)
**Requirement**: REALISM-12, REALISM-13, REALISM-14, REALISM-15

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-12: célula sob 2 estações opostas lê temperatura diferente, sem nenhum poder ativo
- [x] AC REALISM-13: célula sem poder segue curva sazonal (não trava em valor único)
- [x] AC REALISM-14: delta de poder soma sobre o valor sazonal (não sobre base fixa)
- [x] AC REALISM-15: `CropSystem.ReadCellTemperature` lê o valor combinado sem mudança de assinatura
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): temperatura sazonal por bioma`

---

### T3: `Animal.Energy` (`LazyNeed`) + classificação no hasher

**What**: Estende o record `Animal` com o campo `Energy: LazyNeed` (Design), sem quebrar
`FaunaMechanic`/`FaunaDominateSystem` existentes (construtores atualizados). Classifica o
campo novo no teste de reflexão.
**Where**: `src/LivingWorld.Domain/Ecology/Animal.cs` (edição)
**Depends on**: T1
**Reuses**: `LazyNeed` (mesmo tipo de `Npc.HungerNeed`)
**Requirement**: REALISM-01 (pré-requisito de dado)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] `Animal.Energy` existe e é lido/escrito só via `LazyNeed.ValueAt`
- [x] Nenhum call-site existente de `Animal` quebra (compila)
- [x] Teste de reflexão classifica o campo sem exigir mudança manual em outro lugar
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot\|FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): animal energy as LazyNeed`

---

### T4: `FaunaLifecycleSystem.ApplyHunger` + morte por fome

**What**: Implementa decaimento de energia por espécie e morte quando a energia chega a
zero, gerando `Fact` (mesmo padrão de `NpcDeath`).
**Where**: `src/LivingWorld.Simulation/Ecology/FaunaLifecycleSystem.cs` (novo)
**Depends on**: T3
**Reuses**: `LazyNeed`, `Fact`/log causal
**Requirement**: REALISM-01, REALISM-02, REALISM-06

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-01: animal consome fome por tick conforme `AnimalSpeciesRules.HungerDecayPerTick`
- [x] AC REALISM-02: energia zero → morte + `Fact` de morte
- [x] AC REALISM-06: roda com `Extraordinary.Enabled == false`
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): fauna consome energia e morre de fome`

---

### T5: `FaunaLifecycleSystem.TryReproduce`

**What**: Reprodução determinística por seed entre animais da mesma espécie dentro do raio,
acima do limiar de energia.
**Where**: `src/LivingWorld.Simulation/Ecology/FaunaLifecycleSystem.cs` (edição)
**Depends on**: T4
**Reuses**: `world.Rng(stream)` (determinismo já garantido pelo motor)
**Requirement**: REALISM-03

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-03: par elegível gera novo animal próximo, determinístico por seed (dois
      processos com mesmo seed produzem o mesmo resultado — mesma garantia já usada no motor
      de tempo)
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): fauna reproduz por proximidade e energia`

---

### T6: `FaunaLifecycleSystem.TryPredate` + compatibilidade com `fauna.dominate`/`fauna.infect-vector`

**What**: Predação determinística por par espécie declarado; garante que `FaunaMechanic`
(poder) continua funcionando por baixo da simulação de base (não substitui).
**Where**: `src/LivingWorld.Simulation/Ecology/FaunaLifecycleSystem.cs` (edição),
`src/LivingWorld.Simulation/Extraordinary/FaunaMechanic.cs` (verificação de integração, sem
mudança de contrato esperada)
**Depends on**: T5
**Reuses**: `Resolver`/RNG determinística
**Requirement**: REALISM-04, REALISM-05

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-04: predador consome presa no raio, determinístico
- [x] AC REALISM-05: `fauna.dominate` ativo não interrompe fome/reprodução/predação de base
- [x] Edge case: espécie sem `PredatorOf` declarado não gera erro (no-op)
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Extraordinary"`
- [x] `Independent Test` do spec (P1 Fauna) reproduzido manualmente: mundo com 2 espécies, 0
      poderes, T ticks — população varia no log

**Tests**: unit
**Gate**: full

**Commit**: `feat(phase-16-4): fauna preda e mantém poderes existentes funcionando`

---

### T7: `Plant` sem campo novo — `PlantSpeciesRules` aplicado + classificação de hasher (se algum campo novo emergir em `WorldState`)

**What**: Confirma que `Plant` (estágio já existente) cobre o ciclo sem campo novo; qualquer
estado agregado novo (ex.: lista de plantas por espécie em `WorldState`, se necessário para
performance de busca por raio) é classificado no hasher.
**Where**: `src/LivingWorld.Domain/Ecology/Plant.cs` (revisão, provavelmente sem edição),
`src/LivingWorld.Simulation/WorldState.cs` (só se um índice novo for necessário)
**Depends on**: T1
**Reuses**: `Plant` existente
**Requirement**: REALISM-07 (pré-requisito de dado)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] Confirmado (comentário/teste) que `Plant` não precisa de campo novo
- [x] Se um índice novo foi adicionado a `WorldState`, está classificado no hasher
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): confirm plant model needs no new fields`

---

### T8: `FloraLifecycleSystem.AdvanceStage` — ciclo dirigido por temperatura/estação

**What**: Avança estágio de vida da planta conforme temperatura local/estação; taxa cai ou
reverte fora da faixa de tolerância da espécie.
**Where**: `src/LivingWorld.Simulation/Ecology/FloraLifecycleSystem.cs` (novo)
**Depends on**: T2, T7
**Reuses**: `EnvironmentTemperatureMechanic.EffectiveTemperature`, `FloraMechanic.GrowthIncrement`
(multiplicador de poder passa a atuar sobre a taxa de base, não substituí-la)
**Requirement**: REALISM-07, REALISM-08, REALISM-11

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-07: estágio avança sem poder ativo, taxa depende de temperatura/estação
- [x] AC REALISM-08: fora da faixa de tolerância, taxa cai/reverte (nunca avança normal)
- [x] AC REALISM-11: `flora.growth-rate` multiplica a taxa de base, não a substitui
- [x] Edge case: planta que nunca entra na faixa morre sem nunca produzir
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): flora cresce por temperatura e estação`

---

### T9: `FloraLifecycleSystem.TryReproduce` + produção alimenta `CropBatch`

**What**: Reprodução por proximidade/espaço livre (mesmo padrão de Fauna, sem predação);
planta madura deposita em `CropBatch`/`workplace.Deposit` (nunca cria segundo estoque).
**Where**: `src/LivingWorld.Simulation/Ecology/FloraLifecycleSystem.cs` (edição)
**Depends on**: T8
**Reuses**: `CropBatch`/`workplace.Deposit` (`CropSystem.cs`)
**Requirement**: REALISM-09, REALISM-10

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-09: planta madura gera recurso consumível via `CropBatch` existente (sem
      estoque duplicado)
- [x] AC REALISM-10: planta madura com espaço livre compatível brota nova planta, determinístico
- [x] `Independent Test` do spec (P1 Flora) reproduzido: 2 estações, taxa de avanço difere
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Ecology\|FullyQualifiedName~Economy"`

**Tests**: unit
**Gate**: full

**Commit**: `feat(phase-16-4): flora reproduz e alimenta o estoque de cultivo`

---

### T10: Decisão de compatibilidade — `combat.strike` vs `combat.engage` (resolve o Risk flagged no Design)

**What**: Decide e documenta (ADR curto no `STATE.md`/comentário no código) se `combat.strike`
passa a iniciar um `CombatEncounter` multi-round ou se um token novo (`combat.engage:`) é
introduzido preservando `combat.strike` como resolução imediata (compat com poderes já
declarados em mundos salvos). Nenhum código de resolução ainda — só a decisão registrada e um
teste que trava o contrato escolhido.
**Where**: `docs/decisions-log.md` (novo AD-NNN), `src/LivingWorld.Simulation/Extraordinary/CombatMechanic.cs`
(comentário apontando a decisão)
**Depends on**: None (pode rodar em paralelo com Fases 2-3)
**Reuses**: N/A — decisão, não código
**Requirement**: REALISM-16 (pré-requisito de contrato)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AD-NNN registrado com a escolha e o porquê (impacto em poderes já salvos)
- [x] Teste que documenta o contrato escolhido (ex.: `combat.strike` continua resolvendo
      imediato OU passa a iniciar encontro — o teste prova qual)
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): lock combat.strike vs combat.engage contract`

---

### T11: `CombatEncounter` record + `CombatEncounterSystem.StartEncounter`

**What**: Cria o record `CombatEncounter` em `WorldState` (classificado no hasher) e o método
que inicia um encontro conforme a decisão de T10.
**Where**: `src/LivingWorld.Simulation/Extraordinary/CombatEncounterSystem.cs` (novo)
**Depends on**: T10
**Reuses**: N/A (estado novo)
**Requirement**: REALISM-16

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-16: combate cria estado persistente entre ticks (não resolve tudo num
      cálculo único)
- [x] Campo novo classificado no hasher
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot\|FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): combat encounter persistent state`

---

### T12: `CombatEncounterSystem.ProcessRound` — dano acumulado, esquiva/bloqueio, fuga

**What**: Cada round chama `Resolver.Resolve`/`DamageOf` (reuso, `CombatMechanic.cs:39-54`),
acumula dano sobre a vida já reduzida, resolve morte imediata se a vida chegar a zero, e
avalia fuga se abaixo do limiar declarado.
**Where**: `src/LivingWorld.Simulation/Extraordinary/CombatEncounterSystem.cs` (edição)
**Depends on**: T11
**Reuses**: `Resolver.Resolve`, `DamageOf`, `target.SetHealth`
**Requirement**: REALISM-17, REALISM-18, REALISM-24, REALISM-25

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-17: round acumula dano sobre o round anterior, chance de esquiva/bloqueio
- [x] AC REALISM-18: vida zero em qualquer round resolve morte imediata
- [x] AC REALISM-24: abaixo do limiar, chance de fuga bem-sucedida encerra sem morte
- [x] AC REALISM-25: roda com `Extraordinary.Enabled == false`
- [x] Edge case: teto de rounds força resolução (empate/fuga automática), nunca trava
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): combate resolve em rounds com estado`

---

### T13: Teto de rounds anti-loop-infinito + `Independent Test` do spec

**What**: Aplica o teto declarado de rounds (mesmo padrão de teto de iterações por tick do
motor de tempo); valida o `Independent Test` completo (2 NPCs, log mostra múltiplos rounds
distintos antes da resolução).
**Where**: `src/LivingWorld.Simulation/Extraordinary/CombatEncounterSystem.cs` (edição)
**Depends on**: T12
**Reuses**: padrão de teto de iterações já existente no motor de tempo
**Requirement**: REALISM-17..25 (fecha a story)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] Combate nunca excede o teto de rounds declarado no cenário
- [x] `Independent Test` do spec (P2 Combate) reproduzido: log mostra rounds distintos
- [x] Gate (AD-009): `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary|FullyQualifiedName~Ecology|FullyQualifiedName~Snapshot"` — não suite full unfiltered

**Tests**: unit
**Gate**: full (AD-009: scoped broaden, not bare `scripts/test.sh`)

**Commit**: `feat(phase-16-4): teto de rounds fecha combate multi-round`

---

### T14: `InheritSkills` — reusa fórmula de `RateGene`/`HeredityService`

**What**: Função que aplica blend ponderado + mutação RNG + clamp (mesma fórmula de
`RateGene.Inherit`) sobre `SkillSet`, usada pelas 3 mecânicas de instanciação.
**Where**: `src/LivingWorld.Simulation/Extraordinary/NpcCloneSplitReincarnateStubs.cs` (edição —
ou renomeado se a task decidir que "stub" não descreve mais o arquivo)
**Depends on**: None (independente de Fauna/Flora/Combate)
**Reuses**: `RateGene.Inherit`, `HeredityService.InheritVitality` (mesmo padrão), `SkillSet.WithGain`
**Requirement**: REALISM-26, REALISM-27, REALISM-28

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-26: `npc.clone` herda skill completa do original (não zero)
- [x] AC REALISM-27: `npc.split-on-death` herda fração proporcional por novo NPC
- [x] AC REALISM-28: `npc.reincarnate` herda fração pelo mesmo peso `w_gene`-equivalente
      usado em atributos (não 1:1)
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): clone/split/reincarnate herdam skill real`

---

### T15: `TransferBonds` — vínculos sociais por mecânica (Copy/Preserve/None)

**What**: Implementa a transferência de vínculos declarada no Design: clone copia vínculos
(independente), split preserva (cada novo NPC mantém os originais), reincarnate não
transfere (NPC novo).
**Where**: `src/LivingWorld.Simulation/Extraordinary/NpcCloneSplitReincarnateStubs.cs` (edição)
**Depends on**: T14
**Reuses**: `world.Relationships` (Fase 7, existente)
**Requirement**: REALISM-29

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] AC REALISM-29: as 3 mecânicas produzem o comportamento de vínculo declarado (nunca
      vazio por omissão)
- [x] `Independent Test` do spec (P2 Instanciação) reproduzido: clone com skill N e F
      vínculos → clone nasce com skill N e F vínculos
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): clone/split/reincarnate transferem vínculos sociais`

---

### T16: Teto de população viva em `npc.split-on-death` (edge case do spec)

**What**: Limita split a N novos NPCs quando excederia o teto de população viva já usado em
reprodução normal; corte registrado em `Fact`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/NpcCloneSplitReincarnateStubs.cs` (edição)
**Depends on**: T15
**Reuses**: teto de população já existente (reprodução normal)
**Requirement**: Edge case do spec (teto de split)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [x] Split que excederia o teto é cortado a N, sem estourar memória
- [x] `Fact` registra o corte
- [x] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary\|FullyQualifiedName~Performance"`

**Tests**: unit
**Gate**: full

**Commit**: `feat(phase-16-4): split-on-death respeita teto de população viva`

---

### T17: Persistir preview de `foresight.preview` no tick (em vez de descartar)

**What**: `ForesightMechanic.PreviewResolve` passa a gravar o `ResolutionResult` num
dicionário por-portador-por-ação acessível no mesmo tick (sem `Fact`, mesma garantia já
documentada de não mutar `WorldState` além disso).
**Where**: `src/LivingWorld.Simulation/Extraordinary/ForesightMechanic.cs` (edição)
**Depends on**: None (independente de Fauna/Flora/Combate/Instanciação)
**Reuses**: `ForesightMechanic.PreviewResolve` (cálculo existente, só o destino do resultado muda)
**Requirement**: REALISM-30

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] AC REALISM-30: preview fica disponível como entrada pro tick corrente do portador
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

---

### T18: Hook em `BehaviorDecisionSystem.SelectByUtility` — foresight ajusta o score

**What**: Parâmetro opcional `foresightPreviews` em `SelectByUtility`; quando a ação avaliada
tem preview disponível, o score é multiplicado por um fator derivado do `ResolutionResult`.
Default `null`/dicionário vazio compartilhado quando não há foresight — sem alocação no
caminho comum (Risk do Design).
**Where**: `src/LivingWorld.Simulation/.../BehaviorDecisionSystem.cs` (edição)
**Depends on**: T17
**Reuses**: `UtilityBaseOf`, `PersonalityWeighting.WeightOf` (cálculo existente)
**Requirement**: REALISM-31, REALISM-32

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] AC REALISM-31: ação com preview de desfecho ruim tem utility reduzida, medida
      estatisticamente (NPC com foresight evita mais que NPC sem, mesmo seed/cenário)
- [ ] AC REALISM-32: sem preview no tick, decisão idêntica ao comportamento anterior
      (regressão zero pra NPC sem o poder)
- [ ] Sensor de performance confirma sem alocação extra no caminho comum (dicionário vazio
      compartilhado, não alocado por chamada)
- [ ] `Independent Test` do spec (P2 Foresight) reproduzido
- [ ] Gate: `bash scripts/test.sh` (full — hot path, cruza com Performance)

**Tests**: unit
**Gate**: full

**Commit**: `feat(phase-16-4): foresight informa a decisão real da utility AI`

---

### T19: Escolher/confirmar atributo de resistência do hospedeiro (resolve o Risk flagged no Design)

**What**: Decide qual atributo existente de `Npc` representa "vontade/resistência" pra
`PossessionResistance` (ou confirma com o usuário se precisa de um atributo novo — flagged no
Design como não decidido). Registra a escolha como comentário/AD curto.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ControlMechanic.cs` (comentário),
`docs/decisions-log.md` (se virar AD)
**Depends on**: None
**Reuses**: atributo existente de `Npc` (a ser escolhido)
**Requirement**: REALISM-33 (pré-requisito de contrato)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Atributo escolhido e justificado (ou confirmado com o usuário que precisa de um novo)
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

---

### T20: `PossessionResistance.TryResist` + `Fact` de retomada de controle

**What**: Roll determinístico por tick, modulado pelo atributo escolhido em T19, chamado
antes de `RevertIfCeased`; ao resistir, grava `Fact` com o NPC possuidor identificado (mesma
atribuição causal já garantida na 16.1).
**Where**: `src/LivingWorld.Simulation/Extraordinary/ControlMechanic.cs` (edição)
**Depends on**: T19
**Reuses**: `RevertIfCeased`, `world.Rng(stream)`
**Requirement**: REALISM-33, REALISM-34

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] AC REALISM-33: hospedeiro com resistência alta recupera controle mais que resistência
      baixa, mesmo seed/cenário
- [ ] AC REALISM-34: `Fact` registra o evento com o possuidor identificado
- [ ] `Independent Test` do spec (P3 Possessão) reproduzido
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Extraordinary"`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(phase-16-4): hospedeiro pode resistir e retomar controle da possessão`

---

### T21: Sensor de escala — fauna/flora em massa dentro do teto de custo por NPC-tick

**What**: Estende `ScaleScenarioSensorTests`/cenário de referência com N animais/plantas e
confirma que `PerfRules.MaxMicrosPerAliveNpcTick`/`MaxBytesAllocPerTick` não são excedidos.
**Where**: `tests/LivingWorld.Tests/Performance/ScaleScenarioSensorTests.cs` (edição)
**Depends on**: T6, T9, T13, T16, T18, T20 (todas as fases de sistema)
**Reuses**: `PerfRules`, padrão `LazyNeed` (já garante O(eventos), não O(ticks × entidades))
**Requirement**: REALISM-19 (dimensão Failure states)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Sensor confirma que fauna/flora em massa não fura o teto já fixado na Fase 9
- [ ] Se o teto for furado, sistema degrada por decaimento preguiçoso (não trava o tick) —
      testado explicitamente
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Performance"`

**Tests**: integration
**Gate**: full

**Commit**: `test(phase-16-4): sensor de escala cobre fauna/flora em massa`

---

### T22: Cenário de referência 100 anos + `bash scripts/verify.sh`

**What**: Roda o cenário de referência do objetivo #1 (100 NPCs, 100 anos) com
fauna/flora/temperatura habilitadas e 0 poderes ativos, confirma que não trava e que a
população/estágios variam de forma auditável no log. Roda o gate final.
**Where**: N/A (execução de cenário, não código novo — a menos que o cenário de referência
precise de flags novas pra habilitar fauna/flora, o que é edição de config)
**Depends on**: T21
**Reuses**: cenário de referência já existente (objetivo #1)
**Requirement**: Success Criteria do spec (fechamento)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Cenário de 100 anos com fauna/flora/temperatura habilitadas e 0 poderes ativos termina
      sem travar; log mostra população de fauna e estágios de flora variando
- [ ] `bash scripts/verify.sh` sai 0
- [ ] Nenhuma das 5 mecânicas antes "ocas" continua com o gap específico citado pelo revisor
      (checklist manual contra o Success Criteria do spec)

**Tests**: none (execução de cenário + gate)
**Gate**: build

**Commit**: `feat(phase-16-4): fecha fase — realismo autônomo de fauna/flora/clima e mecânicas aprofundadas`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 → T2

Phase 2 (Sequential, after T1; parallel with Phase 3):
  T3 → T4 → T5 → T6

Phase 3 (Sequential, after T1/T2; parallel with Phase 2):
  T7 → T8 → T9

Phase 4 (T10 can start anytime; T11+ after T10):
  T10 → T11 → T12 → T13

Phase 5 (independent of 2/3/4, can run anytime):
  T14 → T15 → T16

Phase 6 (independent of 2/3/4/5):
  T17 → T18

Phase 7 (independent of 2/3/4/5/6):
  T19 → T20

Phase 8 (after ALL of 2,3,4,5,6,7):
  T21 → T22
```

**Note**: dentro de cada fase as tasks são sequenciais (cada uma edita o mesmo arquivo/sistema
da anterior) — nenhuma task individual está marcada `[P]`. O paralelismo real é **entre fases**
(2↔3, e 4/5/6/7 são mutuamente independentes) — se sub-agents forem usados no Execute, um
worker por fase (2, 3, 4, 5, 6, 7) pode rodar concorrente, convergindo em T21.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 2 records novos + config | ✅ Granular |
| T2 | 1 sistema | ✅ Granular |
| T3 | 1 campo em 1 record | ✅ Granular |
| T4-T6 | 1 método por task no mesmo sistema | ✅ Granular (cohesive, mesmo arquivo) |
| T7 | 1 verificação/revisão | ✅ Granular |
| T8-T9 | 1 método por task no mesmo sistema | ✅ Granular |
| T10 | 1 decisão registrada | ✅ Granular |
| T11-T13 | 1 método/aspecto por task no mesmo sistema | ✅ Granular |
| T14-T16 | 1 função/aspecto por task no mesmo arquivo | ✅ Granular |
| T17-T18 | 1 mudança por task, 2 arquivos distintos | ✅ Granular |
| T19-T20 | 1 decisão + 1 método | ✅ Granular |
| T21-T22 | 1 sensor + 1 execução de fechamento | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T1 | Phase 2 after T1 | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5 | T4 | T4→T5 | ✅ Match |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | T1 | Phase 3 after T1 | ✅ Match |
| T8 | T2, T7 | Phase 3 after T2/T7 (T2 é de Phase 1, cross-phase dep explícita no texto) | ✅ Match |
| T9 | T8 | T8→T9 | ✅ Match |
| T10 | None | Phase 4 (T10 "pode começar a qualquer momento") | ✅ Match |
| T11 | T10 | T10→T11 | ✅ Match |
| T12 | T11 | T11→T12 | ✅ Match |
| T13 | T12 | T12→T13 | ✅ Match |
| T14 | None | Phase 5 independente | ✅ Match |
| T15 | T14 | T14→T15 | ✅ Match |
| T16 | T15 | T15→T16 | ✅ Match |
| T17 | None | Phase 6 independente | ✅ Match |
| T18 | T17 | T17→T18 | ✅ Match |
| T19 | None | Phase 7 independente | ✅ Match |
| T20 | T19 | T19→T20 | ✅ Match |
| T21 | T6, T9, T13, T16, T18, T20 | Phase 8 "after ALL of 2,3,4,5,6,7" | ✅ Match |
| T22 | T21 | T21→T22 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain model + reflection | unit / unit | unit | ✅ OK |
| T2 | Simulation system | unit | unit | ✅ OK |
| T3 | Domain model + reflection | unit | unit | ✅ OK |
| T4 | Simulation system | unit | unit | ✅ OK |
| T5 | Simulation system | unit | unit | ✅ OK |
| T6 | Simulation system | unit | unit | ✅ OK |
| T7 | Domain model | unit | unit | ✅ OK |
| T8 | Simulation system | unit | unit | ✅ OK |
| T9 | Simulation system | unit | unit | ✅ OK |
| T10 | Decision + contract test | unit | unit | ✅ OK |
| T11 | Domain model + reflection | unit | unit | ✅ OK |
| T12 | Simulation system | unit | unit | ✅ OK |
| T13 | Simulation system | unit | unit | ✅ OK |
| T14 | Simulation system (mechanic) | unit | unit | ✅ OK |
| T15 | Simulation system (mechanic) | unit | unit | ✅ OK |
| T16 | Simulation system (mechanic) | unit | unit | ✅ OK |
| T17 | Simulation system (mechanic) | unit | unit | ✅ OK |
| T18 | Simulation system (hot path) | unit | unit | ✅ OK |
| T19 | Decision | unit | unit | ✅ OK |
| T20 | Simulation system (mechanic) | unit | unit | ✅ OK |
| T21 | Performance/scale sensor | integration | integration | ✅ OK |
| T22 | Scenario execution + build gate | build | none (execução) / build gate | ✅ OK — matriz define "build" só pro gate final; T22 é exatamente esse gate, sem código novo próprio |

Nenhuma violação — todas as tasks que criam/editam camada de código testável incluem os testes
na mesma task (nenhum "testado em outra task").

---

## Tools & Skills per Task

Todas as tasks usam só o toolchain padrão do repo (`dotnet`/`bash scripts/*.sh`) — nenhum MCP
ou skill externo necessário além do próprio `tlc-spec-driven` (Execution Protocol acima).
