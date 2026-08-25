# Fase 16.1 — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill
is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy
review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-16-1-power-engine/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/LivingWorld.Tests/Extraordinary/ExtraordinaryInvocationEngineTests.cs`,
> `ExtraordinaryLocomotionTests.cs`, `tests/LivingWorld.Tests/Population/PairedScenarioTests.cs`).
> Guidelines found: none dedicated (`AGENTS.md`/`CLAUDE.md` absent) — this repo's own
> established pattern (paired control/treated world, `dotnet test` xUnit) is used as both floor
> and target, matching the strong default for domain logic.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| `IExtraordinaryMechanic` implementations (Domain/Simulation) | unit | 1:1 to spec ACs per mechanic; paired control/treated world per Independent Test | `tests/LivingWorld.Tests/Extraordinary/**` | `dotnet test --filter "FullyQualifiedName~Extraordinary"` |
| New domain types (`Animal`, `Plant`, `MapCell.Temperature`, `Npc.IsGhost`) | unit | Construction/invariants + consumer behavior (no dedicated test for bare data fields) | `tests/LivingWorld.Tests/**` (colocated with consuming mechanic's tests) | same as above |
| Consumer-system changes (`MortalityPlanner`, `BehaviorDecisionSystem`, `ProductionSystem`/`ConstructionSystem`, `NatalitySystem`, `NpcDeath.Apply`) | unit | Existing suite for that system stays green + new paired test for the power-driven branch | `tests/LivingWorld.Tests/{Behavior,Population,Economy}/**` | `dotnet test --filter "FullyQualifiedName~<System>"` |
| Full regression | build gate | Whole backend suite green, no regression outside `Extraordinary`/touched systems | `tests/LivingWorld.Tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

> Generated from codebase — confirm before Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Extraordinary mechanics) | Yes | Each test builds its own `World`/`WorldState` via `WorldWithPower(...)`-style fixture, no shared static state | Existing `ExtraordinaryInvocationEngineTests.cs`/`ExtraordinaryLocomotionTests.cs` pattern |
| unit (consumer-system paired control/treated) | Yes | Same per-test world construction pattern (`PairedScenarioTests.cs`) | Existing paired-world tests already run in the default xUnit parallel runner |
| build gate (`scripts/test.sh` full run) | No | Runs the whole suite sequentially including `Category=Scenario` long-run tests | Existing STATE.md notes (e.g. `Ten_k_population_ten_years...` run times) |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After each task (mechanic added/modified) | `dotnet test --filter "FullyQualifiedName~Extraordinary"` |
| Full | After a phase touches a consumer system (Mortality/Behavior/Economy/Population) | `dotnet test --filter "Category!=Scenario&FullyQualifiedName~<TouchedArea>"` |
| Build | After the last task of the feature (before Verifier) | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Foundation (Sequential)

```
T1 → T2, T3
```

### Phase 2: Cheap attribute/value mechanics (Parallel OK, depends on Phase 1)

```
T1 ──┬→ T4 [P]
     ├→ T5 [P]
     ├→ T6 [P]
     ├→ T7 [P]
     ├→ T8 [P]
     ├→ T9 [P]  (T9 also depends on T3)
     └→ T10 [P]
```

### Phase 3: Força → Percepção → Reação → Combate (Sequential — user-combined build order)

```
T11 → T12 → T13 → T14 → T15
```

### Phase 4: Gravidade (recreates flight/speed)

```
T1 → T16
```

### Phase 5: Environmental/ecological concepts (Parallel OK)

```
T1 ──┬→ T17 [P]
     ├→ T18 [P]
     └→ T19 [P]
```

### Phase 6: Memory

```
T1 → T20
```

### Phase 7: Passive cycle (foundation for Bond)

```
T1 → T21
```

### Phase 8: Vulnerability/resistance (independent, cheap)

```
T1 → T22
```

### Phase 9: Higher-risk mechanics (Sequential by risk, per Design's discretion ordering)

```
T21 → T25
T1 → T23
T1 → T24
```

### Phase 10: Niche mechanics (Parallel OK)

```
T1 → T26 [P]
T1 → T27 [P]
```

### Phase 11: Precognition (last — unconfirmed assumption item, safest to drop late if wrong)

```
T1 → T28
```

---

## Task Breakdown

### T1: Registro de mecânicas substitui o switch fechado

**What**: `IExtraordinaryMechanic`/`IExtraordinaryMechanicRegistry`, migração de `npc.*`/
`movement.*`/`construct.create`/`npc.teleport`/`npc.force-action` pro registro; loop de
`PrepareEffects`/`PrepareCosts` despacha por prefixo, nunca `switch`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/` (novo `IExtraordinaryMechanic.cs`,
`ExtraordinaryMechanicRegistry.cs`; `ExtraordinaryInvocationEngine.cs` modificado)
**Depends on**: None
**Reuses**: Todo o corpo de `PrepareEffects`/`PrepareCosts`/`PrepareTeleport`/`PrepareForceAction` existente — vira o conteúdo das primeiras classes registradas, não reescrito
**Requirement**: PWR-01..05

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Toda mecânica hoje aceita (stats, movimento, constructo, teleporte, force-action) resolve via registro
- [ ] Token com prefixo não registrado falha com a mesma mensagem de contrato de hoje (PWR-03)
- [ ] Nenhuma edição no laço de `Invoke`/`Prepare` além do despacho por prefixo (PWR-02)
- [ ] Suíte `ExtraordinaryInvocationEngineTests`/`ExtraordinaryLocomotionTests` passa sem alterar nenhuma asserção existente (migração invisível)

**Tests**: unit
**Gate**: quick
**Commit**: `refactor(extraordinary): replace closed effect/cost switch with mechanic registry`

---

### T2: Primitiva de seletor de área/região

**What**: `AreaTargetResolver` — resolve `area:radius:<n>`/`area:region:<id>` num conjunto
determinístico de `NpcId` (ordem por Id), custo cobrado uma vez do portador.
**Where**: `src/LivingWorld.Simulation/Extraordinary/AreaTargetResolver.cs`
**Depends on**: T1
**Reuses**: Footprint/colisão de mapa já usado em `npc.teleport`
**Requirement**: PWR-06..09

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] `area:radius:<n>` afeta todos dentro do raio a partir da posição atual do portador, recalculado por invocação
- [ ] Zero alvos na área não é erro (sucesso sem efeito)
- [ ] Custo cobrado uma única vez, não por alvo

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add area/region multi-target selector primitive`

---

### T3: Primitiva de transferência (dois alvos)

**What**: `TransferMechanic` genérico (`transfer.<atributo>:<magnitude>`) — débito/crédito
atômico entre portador e alvo (ou o inverso), clamp no teto, ordem custo→transfer determinística.
**Where**: `src/LivingWorld.Simulation/Extraordinary/TransferMechanic.cs`
**Depends on**: T1
**Reuses**: `ClampNeed` já usado em custos
**Requirement**: PWR-10..13

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Débito da parte doadora e crédito da receptora na mesma transação atômica
- [ ] Saldo insuficiente falha por completo (nenhum crédito parcial)
- [ ] Excedente acima do teto é descartado (clamp), nunca falha por isso
- [ ] Par controle/tratado confirma conservação do total transferido

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add two-party transfer effect primitive`

---

### T4: Senescência controlável [P]

**What**: `MortalityPlanner.RollDeathAge` passa a ler `SenescenceRateMultiplier` (campo já
existente, hoje nunca lido).
**Where**: `src/LivingWorld.Simulation/Mortality/MortalityPlanner.cs` (modificado)
**Depends on**: T1
**Reuses**: `PowerDescriptor.SenescenceRateMultiplier`/`ExtraordinaryCarrierState` (já existem)
**Requirement**: PWR-20..23

**Done when**:
- [ ] Multiplicador < 1 adia idade de morte proporcionalmente
- [ ] Multiplicador == 0 nunca agenda morte por idade enquanto manifestado (mas continua sujeito a fome/dano)
- [ ] Rolagem usa o multiplicador vigente no momento da rolagem, nunca retroativo
- [ ] Dois poderes ativos agregam pelo mínimo (mesma regra já usada em outros eixos)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(mortality): consume power-driven senescence rate multiplier`

---

### T5: Sorte / probabilidade determinística [P]

**What**: `LuckMechanic` (`luck.capacity-bonus:<n>`, `luck.curse:<n>`) alimenta `capacity` de
`Resolver.Resolve`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/LuckMechanic.cs`
**Depends on**: T1
**Reuses**: `Resolver.Resolve` (já seedado)
**Requirement**: PWR-24..27

**Done when**:
- [ ] Bônus soma à capacidade antes de resolver, nunca troca o stream de RNG
- [ ] Maldição subtrai capacidade do alvo por janela declarada
- [ ] Mesma seed produz resultado byte-idêntico entre execuções
- [ ] Capacidade negativa clampa em zero

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add luck mechanic feeding Resolver capacity`

---

### T6: Leitura/alteração de mente [P]

**What**: `MindMechanic` (`mind.read`, `mind.alter-trait:<traço>:<delta>`) via
`WorldAuthoringCommands.RewritePersonality`; valor pré-alteração em
`ExtraordinaryCarrierState.PreAlterationTraits`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/MindMechanic.cs`
**Depends on**: T1
**Reuses**: `WorldAuthoringCommands.RewritePersonality`
**Requirement**: PWR-28..31

**Done when**:
- [ ] `mind.read` expõe só campos já públicos do domínio, nunca inventa dado
- [ ] `mind.alter-trait` aplica delta via `RewritePersonality` (nunca escreve `Personality` direto)
- [ ] Reverte ao valor original quando manifestação cessa
- [ ] Conflito entre dois poderes no mesmo traço resolve deterministicamente (última invocação, ordem por `InvocationId`)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add mind read/alter mechanic`

---

### T7: Transferência de anos de vida [P]

**What**: `transfer.lifespan-years:<n>` reagenda o evento de morte por idade já agendado das
duas partes.
**Where**: `src/LivingWorld.Simulation/Extraordinary/LifespanTransferMechanic.cs`
**Depends on**: T1
**Reuses**: `MortalitySystem.SchedulePlannedDeath`
**Requirement**: PWR-32..34

**Done when**:
- [ ] Doador morre `n` anos mais cedo, receptor `n` anos mais tarde (nunca no passado)
- [ ] Doador sem `n` anos de sobra falha a invocação
- [ ] Falha explícita se a morte de qualquer parte já foi processada

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add lifespan-years transfer mechanic`

---

### T8: Transmutação de matéria [P]

**What**: `MatterTransmuteMechanic` (`matter.transmute:<origem>:<destino>:<taxa>`) via
`WorldEventKind.Destroyed`+`Minted`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/MatterTransmuteMechanic.cs`
**Depends on**: T1
**Reuses**: canal já auditado `WorldEventKind.Minted`/`Destroyed`
**Requirement**: PWR-35..38

**Done when**:
- [ ] Débito de origem + crédito de destino na mesma invocação, ambos eventos logados
- [ ] Estoque insuficiente falha por completo
- [ ] Sensor de conservação da Fase 16 original continua verde com este poder ativo
- [ ] Taxa aplicada exatamente como declarada no cenário (sem teto embutido no motor)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add matter transmutation mechanic via audited mint/destroy channel`

---

### T9: Skill como efeito de poder [P]

**What**: `SkillMechanic` (`skill.copy:<skillId>`, `skill.learn-rate:<multiplicador>`).
**Where**: `src/LivingWorld.Simulation/Extraordinary/SkillMechanic.cs`
**Depends on**: T1, T3 (roubo de skill via transferência já existente)
**Reuses**: `Npc.Skills`/`SkillPracticeSystem`
**Requirement**: PWR-96..98

**Done when**:
- [x] `skill.copy` copia valor exato do alvo (nunca inventa id que o alvo não tem)
- [x] `skill.learn-rate` escala a progressão enquanto manifestado
- [x] Cessar remove o multiplicador sem resíduo; cópia já aplicada permanece

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add skill copy/learn-rate mechanic`

---

### T10: Fertilidade modificável [P]

**What**: `attribute.fertility:<multiplicador>` multiplica a taxa de `NatalitySystem`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/AttributeMechanic.cs` (fertility branch)
**Depends on**: T1
**Reuses**: `NatalitySystem`
**Requirement**: PWR-99..100

**Done when**:
- [ ] Multiplicador escala taxa de concepção do NPC
- [ ] `attribute.fertility:0` nunca concebe enquanto ativo
- [ ] Cessar volta ao valor base sem resíduo

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add fertility multiplier mechanic`

---

### T11: Limite de carga (Força, base)

**What**: `CarryCapacity` real em `Npc` (hoje binário); `attribute.strength:<multiplicador>`
escala a capacidade.
**Where**: `src/LivingWorld.Domain/Npc.cs` (novo campo), `src/LivingWorld.Simulation/Extraordinary/AttributeMechanic.cs` (strength branch)
**Depends on**: T1
**Reuses**: `Npc.CarriedResourceId/CarriedQuantity`
**Requirement**: PWR-50..52

**Done when**:
- [ ] `PickUp` acima da capacidade rejeita o excedente
- [ ] `attribute.strength` escala a capacidade enquanto manifestado
- [ ] Cessar volta ao valor base imediatamente

**Tests**: unit
**Gate**: quick
**Commit**: `feat(economy): add real carry-capacity limit driven by strength power`

---

### T12: Velocidade de coleta/construção por força

**What**: `attribute.strength` vira segundo multiplicador (combinado com `Skill`/`RateGene`) em
`ProductionSystem`/`SkillPracticeSystem`/`ConstructionSystem`.
**Where**: `src/LivingWorld.Simulation/Economy/ProductionSystem.cs`,
`src/LivingWorld.Simulation/Construction/ConstructionSystem.cs` (modificados)
**Depends on**: T11
**Reuses**: multiplicador de `Skill`/`RateGene` já existente
**Requirement**: PWR-53..55

**Done when**:
- [x] Taxa de produção/coleta/consumo de construção escala multiplicativamente por `attribute.strength`
- [x] Cessar volta a refletir só skill/RateGene, sem resíduo
- [x] Combinação respeita qualquer teto já validado do sistema de produção

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Economy`)
**Commit**: `feat(economy): apply strength power as second multiplier on gather/build rate`

---

### T13: Percepção / alcance de detecção

**What**: `attribute.perception:<raio>` — `BehaviorDecisionSystem` passa a considerar NPCs/
perigo dentro do raio (não só adjacência) pra fuga/abordagem social.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modificado),
`src/LivingWorld.Simulation/Extraordinary/AttributeMechanic.cs` (perception branch)
**Depends on**: T2 (reusa a mesma noção de raio da área)
**Reuses**: `BehaviorDecisionSystem` (decisão de fuga/abordagem já existe)
**Requirement**: PWR-56..58

**Done when**:
- [x] NPC com o poder reage a perigo/NPC dentro do raio declarado
- [x] Sem o poder, comportamento idêntico ao de hoje (adjacência) — nenhuma regressão
- [x] Raio é por-portador, nunca global

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Behavior`)
**Commit**: `feat(behavior): add perception-radius power affecting threat/social detection`

---

### T14: Reação / decisão mais rápida

**What**: `attribute.reaction-speed:<multiplicador>` — `BehaviorDecisionSystem` reavalia o
portador mais vezes por hora simulada.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modificado)
**Depends on**: T13
**Reuses**: scheduler de decisão já existente
**Requirement**: PWR-59..61

**Done when**:
- [x] Reavaliação multiplicada por `multiplicador`× por hora simulada
- [x] Combinado com percepção, reage antes de um NPC controle na mesma ameaça
- [x] Cessar volta à cadência normal sem resíduo

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Behavior`)
**Commit**: `feat(behavior): add reaction-speed power multiplying decision cadence`

---

### T15: Combate NPC-vs-NPC

**What**: `combat.strike:<magnitude-base>` resolve via `Resolver.Resolve` (capacidade incluindo
`attribute.strength`), aplica dano, loga `WorldEventKind.CombatResolved` novo.
**Where**: `src/LivingWorld.Simulation/Extraordinary/CombatMechanic.cs`,
`src/LivingWorld.Domain/History/WorldEventKind.cs` (novo valor)
**Depends on**: T11 (Força), T5 estilo de resolução (não bloqueante, reusa `Resolver` já usado por Sorte)
**Reuses**: `Resolver.Resolve`/`ResolveDeclaredOutcome`, `npc.health` negativo (caminho existente)
**Requirement**: PWR-62..65

**Done when**:
- [x] Confronto resolve via `Resolver.Resolve`, dano proporcional ao resultado
- [x] Evento `CombatResolved` dedicado logado (nunca `ExtraordinaryEffectApplied` genérico)
- [x] Determinístico pra mesma seed
- [x] Inatingível com `Extraordinary.Enabled == false`

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Extraordinary`)
**Commit**: `feat(extraordinary): add NPC-vs-NPC combat mechanic`

---

### T16: Gravidade pessoal (recria voo/velocidade)

**What**: `GravityMechanic` (`gravity.self:<mult>`, `gravity.target:<mult>`) —
`ExtraordinaryLocomotion.Resolve` deriva `CanFly`/`SpeedMultiplier` de gravidade;
`movement.flight`/`movement.speed-multiplier` viram sinônimos.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ExtraordinaryLocomotion.cs` (modificado),
`GravityMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: `ExtraordinaryLocomotionProfile`/`Resolve` (padrão de referência já existente)
**Requirement**: PWR-70..73

**Done when**:
- [x] `gravity.self` deriva voo/velocidade (mesma interface pública)
- [x] `gravity.target` afeta orçamento de movimento de um alvo externo
- [x] `movement.flight`/`movement.speed-multiplier` aceitos como sinônimo (retrocompatibilidade)
- [x] `gravity.self`+`gravity.target` simultâneos compõem deterministicamente
- [x] Suíte `ExtraordinaryLocomotionTests` existente passa sem alterar asserção

**Tests**: unit
**Gate**: quick
**Commit**: `refactor(extraordinary): derive flight/speed from a real personal-gravity concept`

---

### T17: Temperatura / clima local [P]

**What**: `MapCell.Temperature` (base determinístico, derivado de bioma/altitude);
`environment.temperature:<região>:<delta>:<duração>`.
**Where**: `src/LivingWorld.Domain/Map/MapCell.cs` (novo campo),
`src/LivingWorld.Simulation/MapGenerator.cs` (gera base),
`src/LivingWorld.Simulation/Extraordinary/EnvironmentTemperatureMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: bioma/altitude já gerados
**Requirement**: PWR-74..76

**Done when**:
- [x] Toda célula tem temperatura base determinística no gen
- [x] Poder ajusta região por duração, revertendo ao expirar
- [x] `CropSystem` tem gancho pra consultar temperatura (sem reformular fórmula agrícola)
- [x] Sem poder ativo, temperatura permanece no valor base (nenhuma variação RNG não semeada)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(map): add per-cell temperature concept and environment power`

---

### T18: Fauna (entidade animal mínima) [P]

**What**: Tipo `Animal` mínimo + `fauna.dominate`/`fauna.infect-vector`.
**Where**: `src/LivingWorld.Domain/Fauna/Animal.cs` (novo), `WorldState.cs` (nova coleção),
`src/LivingWorld.Simulation/Extraordinary/FaunaMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: infra de posição/movimento já validada pra `Npc`
**Requirement**: PWR-77..79

**Done when**:
- [x] `Animal` (id/espécie/posição/vivo) existe e é simulável no mundo
- [x] `fauna.dominate` faz animais no raio seguirem o portador
- [x] `fauna.infect-vector` marca animais no raio como vetor (gancho causal, sem epidemiologia completa)
- [x] Inatingível com `Extraordinary.Enabled == false`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add minimal Animal entity and fauna power mechanic`

---

### T19: Flora (par de Fauna) [P]

**What**: Tipo `Plant` mínimo + `flora.growth-rate:<multiplicador>` numa área.
**Where**: `src/LivingWorld.Domain/Flora/Plant.cs` (novo), `WorldState.cs` (nova coleção),
`src/LivingWorld.Simulation/Extraordinary/FloraMechanic.cs` (novo)
**Depends on**: T1, T2 (reusa seletor de área)
**Reuses**: mesma infra espacial de Fauna, nunca duplica `CropSystem`
**Requirement**: PWR-101..103

**Done when**:
- [x] `Plant` (id/espécie/posição/estágio) existe e é simulável
- [x] `flora.growth-rate` acelera estágio de crescimento numa área
- [x] Inatingível com `Extraordinary.Enabled == false`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add minimal Plant entity and flora growth-rate mechanic`

---

### T20: Memória / cognição privada

**What**: `mind.read-memory`/`mind.erase-memory:<factId>`/`mind.implant-memory:<factId>` sobre
consulta filtrada ao log de `Fact`, com lista de "esquecidos" por NPC.
**Where**: `src/LivingWorld.Simulation/Extraordinary/MemoryMechanic.cs` (novo),
`ExtraordinaryCarrierState.cs` (novo campo `ForgottenFactIds`)
**Depends on**: T1
**Reuses**: `Fact`/`WorldEventKind` (log causal imutável já existente)
**Requirement**: PWR-80..83

**Done when**:
- [x] `mind.read-memory` expõe `Fact`s reais do alvo, filtrados por esquecidos, nunca inventa
- [x] `mind.erase-memory` só adiciona a "esquecidos" — `Fact` original nunca muta
- [x] `mind.implant-memory` sempre referencia um `Fact` real existente em outro lugar do mundo
- [x] Inatingível com `Extraordinary.Enabled == false`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add private memory query/erase/implant mechanic`

---

### T21: Ciclo de poder passivo/contínuo

**What**: `ExtraordinaryPassiveTickSystem` reinvoca poderes `Mode="Passive"` a cada tick elegível.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ExtraordinaryPassiveTickSystem.cs` (novo)
**Depends on**: T1
**Reuses**: cadência `Hourly` já usada por `ExtraordinaryStateSystem`
**Requirement**: PWR-90..92

**Done when**:
- [x] Poder passivo manifestado reinvoca automaticamente sem chamada manual
- [x] Custo cobrado a cada reinvocação; sem saldo, pula o tick sem revogar o poder
- [x] Para no mesmo tick em que a manifestação cai
- [x] Sem trabalho algum com `Extraordinary.Enabled == false`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add passive-power auto-reinvocation system`

---

### T22: Vulnerabilidade/resistência mecânica

**What**: Casamento tipo-a-tipo entre `tipo` declarado no efeito e `intrinsicVulnerabilities`
do alvo, multiplicando magnitude.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ExtraordinaryInvocationEngine.cs` (aplicação
de magnitude, modificado)
**Depends on**: T1
**Reuses**: `carrier.health:` (único caso já mecânico hoje)
**Requirement**: PWR-93..95

**Done when**:
- [x] Tipo casando vulnerabilidade multiplica magnitude pelo fator declarado no cenário
- [x] Sem casamento, magnitude aplicada normalmente
- [x] Sem `tipo` declarado, comportamento idêntico a hoje (nenhuma regressão)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): make declared vulnerabilities mechanically consulted, not just narrative`

---

### T23: Instanciação de NPC via poder

**What**: `npc.clone`/`npc.split-on-death`/`npc.reincarnate` + `WorldEventKind.NpcInstantiated`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/NpcInstantiationMechanic.cs` (novo),
`src/LivingWorld.Domain/Death/NpcDeath.cs` (modificado, hook de split-on-death)
**Depends on**: T1
**Reuses**: `NatalitySystem` (ponto de nascimento pra reincarnate), `NpcDeath.Apply`
**Requirement**: PWR-104..107

**Done when**:
- [x] `npc.clone` instancia `Npc` com `NpcId` novo, cópia (não referência) de personalidade/aparência
- [x] `npc.split-on-death` instancia N novos NPCs antes de marcar o portador morto
- [x] `npc.reincarnate` transfere fração declarada de skills/traços pro próximo nascimento real
- [x] Toda mutação loga `WorldEventKind` dedicado

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Population`)
**Commit**: `feat(extraordinary): add NPC instantiation mechanic (clone/split/reincarnate)`

---

### T24: Identidade/controle prolongado

**What**: `control.possess`/`control.body-swap`/`appearance.impersonate:<npcId>`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ControlMechanic.cs` (novo),
`src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (delegação, modificado)
**Depends on**: T1
**Reuses**: `BehaviorDecisionSystem` (decisão já existe, delega em vez de substituir)
**Requirement**: PWR-108..111

**Done when**:
- [x] `control.possess` delega decisões do alvo ao portador, log causal atribui ao possuído
- [x] `control.body-swap` troca `Personality`/identidade observável, reversível
- [x] `appearance.impersonate` é cosmético/social, nunca troca `NpcId`
- [x] Estado original restaurado ao cessar (qualquer uma das três)

**Tests**: unit
**Gate**: full (`FullyQualifiedName~Behavior`)
**Commit**: `feat(extraordinary): add possession/body-swap/impersonation mechanic`

---

### T25: Vínculo/pacto duradouro

**What**: `bond.share:<atributo>`/`bond.oath:<consequência>` entre duas partes, reavaliado a
cada tick via ciclo passivo.
**Where**: `src/LivingWorld.Simulation/Extraordinary/BondMechanic.cs` (novo)
**Depends on**: T21 (ciclo passivo)
**Reuses**: `ManifestationCondition` (já genérico, pra `bond.oath`)
**Requirement**: PWR-112..114

**Done when**:
- [x] `bond.share` iguala/proporciona o atributo entre as partes a cada tick
- [x] `bond.oath` aplica consequência automática quando a condição é violada
- [x] Vínculo desfeito automaticamente se qualquer parte morre

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add persistent bond/oath mechanic between two NPCs`

---

### T26: Estado de alma/fantasma pós-morte [P]

**What**: `soul.persist-as-ghost` — `Npc.IsGhost` opt-in, consultável (posição última,
personalidade, skills) sem participar de sistemas que exigem `IsAlive`.
**Where**: `src/LivingWorld.Domain/Npc.cs` (novo campo `IsGhost`),
`src/LivingWorld.Simulation/Extraordinary/SoulMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: `mind.read-memory` (T20) pra `mind.commune`
**Requirement**: PWR-115..116

**Done when**:
- [x] NPC com o poder permanece consultável após `IsAlive=false`, `IsGhost=true`
- [x] `mind.commune` sobre um fantasma reusa a leitura de memória já existente
- [x] Sem o poder, comportamento terminal idêntico a hoje (opt-in, nenhuma regressão)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add opt-in ghost persistence after death`

---

### T27: Espaço dimensional (bolso/portal) [P]

**What**: `dimension.pocket-store`/`dimension.portal:<célulaA>:<célulaB>`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/DimensionMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: mecânica de teleporte já existente (`npc.teleport`)
**Requirement**: PWR-117..119

**Done when**:
- [x] Item guardado no bolso sai do estoque/mapa normal, não conta como perda econômica
- [x] Portal teleporta nos dois sentidos entre as duas células declaradas enquanto ativo
- [x] Portal desativa junto com o poder que o criou

**Tests**: unit
**Gate**: quick
**Commit**: `feat(extraordinary): add dimensional pocket-store and bidirectional portal mechanic`

---

### T28: Precognição probabilística (sem viagem no tempo)

**What**: `foresight.preview:<evento>` roda a resolução real em modo leitura, nunca muta
`WorldState`/loga `Fact` como se tivesse ocorrido.
**Where**: `src/LivingWorld.Simulation/Extraordinary/ForesightMechanic.cs` (novo)
**Depends on**: T1
**Reuses**: `Resolver.Resolve`/sistemas existentes (mesmo cálculo, escopo de leitura)
**Requirement**: PWR-120..122

**Done when**:
- [x] Prévia reporta exatamente o resultado que `Resolver.Resolve` produziria naquele tick/seed
- [x] Nenhum `Fact` novo no log causal após a prévia
- [x] Resultado real (se o evento ocorrer depois) pode divergir sem que a prévia tenha sido uma garantia forçada

**Tests**: unit
**Gate**: full (`bash scripts/test.sh`, gate final da fase — última task)
**Commit**: `feat(extraordinary): add read-only precognition mechanic`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2, T3

Phase 2 (Parallel, all depend on T1; T9 also on T3):
  T4 [P], T5 [P], T6 [P], T7 [P], T8 [P], T9 [P], T10 [P]

Phase 3 (Sequential — Força→Percepção→Reação→Combate, combined build order):
  T11 ──→ T12 ──→ T13 ──→ T14 ──→ T15

Phase 4 (Sequential, depends on T1):
  T16

Phase 5 (Parallel, depend on T1; T19 also on T2):
  T17 [P], T18 [P], T19 [P]

Phase 6:
  T20

Phase 7:
  T21

Phase 8:
  T22

Phase 9 (T25 depends on T21; T23/T24 depend on T1):
  T23, T24, T25

Phase 10 (Parallel):
  T26 [P], T27 [P]

Phase 11 (last — unconfirmed assumption item):
  T28
```

**11 phases > 3** — per the skill's Sub-Agent Delegation trigger, Execute will offer one
worker per phase (sequential) before starting. Given the build order already combined with the
user (Força→Percepção→Reação→Combate strictly sequential; risk-ordered mechanics — instantiation,
possession, bond, ghost, dimensional, precognition — deliberately last), phases must still run
in the order listed even if workers are dispatched per-phase.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 1 registry + migration of existing mechanics | ✅ Granular (foundation, inherently touches all existing dispatch sites once) |
| T2, T3 | 1 primitive each | ✅ Granular |
| T4-T10 | 1 mechanic each (1 new class + 1 consumer hook) | ✅ Granular |
| T11-T15 | 1 mechanic/system-change each, explicitly sequenced | ✅ Granular |
| T16 | 1 mechanic + migration of 2 existing keys | ✅ Granular |
| T17-T22 | 1 concept/mechanic each | ✅ Granular |
| T23-T28 | 1 mechanic each (higher risk, still single-concern) | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T1 | T1→T3 | ✅ Match |
| T4-T8, T10 | T1 | T1→[P] | ✅ Match |
| T9 | T1, T3 | T1→T9, T3→T9 | ✅ Match |
| T11 | T1 | T1→T11 (Phase 4 arrow into Phase 3 chain) | ✅ Match |
| T12 | T11 | T11→T12 | ✅ Match |
| T13 | T2 | T2→T13 | ✅ Match |
| T14 | T13 | T13→T14 | ✅ Match |
| T15 | T11 | T11→T15 | ✅ Match |
| T16 | T1 | T1→T16 | ✅ Match |
| T17, T18 | T1 | T1→[P] | ✅ Match |
| T19 | T1, T2 | T1→T19, T2→T19 | ✅ Match |
| T20-T22 | T1 | T1→T20/21/22 | ✅ Match |
| T23, T24 | T1 | T1→T23/24 | ✅ Match |
| T25 | T21 | T21→T25 | ✅ Match |
| T26, T27 | T1 | T1→[P] | ✅ Match |
| T28 | T1 | T1→T28 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T28 | Domain/business-logic (`IExtraordinaryMechanic` impls + consumer systems) | unit | unit | ✅ OK |
| T12-T15, T23, T24 | Consumer-system change (Economy/Behavior/Population) | unit + full gate on touched area | unit, full | ✅ OK |
| T28 | Final task of feature | build gate before Verifier | full (`bash scripts/test.sh`) | ✅ OK |

No task defers its own tests to a later task — every task is self-testable per the coverage
matrix above.
