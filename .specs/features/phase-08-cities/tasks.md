# Fase 8 — Cidades Tasks

## Execution Protocol (MANDATORY — não pular)

Implemente estas tasks com a skill `tlc-spec-driven`: **ative-a pelo nome e siga o fluxo de
Execute e as Critical Rules dela.** Não procure arquivos da skill por caminho de filesystem —
a skill é a fonte de verdade pro fluxo completo (ciclo por task, delegação a sub-agent,
Verifier, sensor de discriminação).

**Se a skill não puder ser ativada, PARE e avise — não prossiga sem ela.**

---

**Design**: `.specs/features/phase-08-cities/design.md`
**Status**: Approved — sub-agent por fase, sem MCP extra

---

## Test Coverage Matrix

> Gerado por amostragem do repo (`rules/tests.md`, `AGENTS.md`, `tests/LivingWorld.Tests/*`) —
> confirmar antes de Execute. Diretrizes encontradas: `rules/tests.md` (camadas, nomeação,
> um assert por teste, sem `Thread.Sleep`/rede/disco real), `rules/eval-criteria.md` (R1-R5,
> critério de fase é gate executável).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
|---|---|---|---|---|
| Entidade de domínio (`City`, `Building`, `ConstructionProject`, `AggregatePopulationPool`) | unit | Invariante de construtor + toda transição (`Materialize`/`Dematerialize`/`Advance`) — 1:1 com AC da spec | `tests/LivingWorld.Tests/Cities/*Tests.cs` | `bash scripts/test.sh` |
| Regras cenário-driven (`CityRules`, `CityCatalog`) | unit | `Create` valida cada bound; `Disabled`/default cobertos | `tests/LivingWorld.Tests/Cities/*RulesTests.cs` | `bash scripts/test.sh` |
| Sistema de simulação (`ConstructionSystem`, `CityGrowthSystem`, `MigrationSystem`, `MaterializationSystem`, `SettlementFoundingSystem`) | unit + determinismo | Todo AC da spec + branch de falha; par mesma-seed/dois-processos onde a task introduz estado novo no hash | `tests/LivingWorld.Tests/Cities/*SystemTests.cs` | `bash scripts/test.sh` |
| Cenário/propriedade (conservação LOD, fundação, fome base/tratamento, inspeção exaustiva, flag LOD) | cenário (property-based) | Critério do roadmap exato — ver `rules/eval-criteria.md` R1-R4; controle par base/tratamento onde causal | `tests/LivingWorld.Tests/Cities/*ScenarioTests.cs`, `[Trait("Category","Scenario")]` quando horizonte > 10 anos | `bash scripts/test.sh` (10 anos) / `bash scripts/test.sh --filter Category=Scenario` (100 anos, nightly) |
| Arquitetura/cobertura por reflexão (`ReferentialIntegritySweep`, DTO exaustivo) | unit (reflexão) | Falha se algum tipo/campo novo ficar sem entrada — mesmo padrão de `ReferentialIntegritySweepTests`/`MonotonicFieldsTests` | `tests/LivingWorld.Tests/ReferentialIntegritySweepTests.cs` (estendido), `tests/LivingWorld.Tests/Cities/NpcInspectionDtoCoverageTests.cs` | `bash scripts/test.sh` |
| API (`GET /npcs/{id}`) | integration | Happy path + 404 (id morto/inexistente) — `WebApplicationFactory` | `tests/LivingWorld.Tests/Cities/NpcEndpointTests.cs` | `bash scripts/test.sh` |
| CLI (`inspect-npc`) | integration | Happy path (stdout) + falha (exit code 1) — spawn de processo real, mesmo padrão de `DeterminismTwoProcessTests` | `tests/LivingWorld.Tests/Cities/InspectNpcCliTests.cs` | `bash scripts/test.sh` |
| Entidade/schema pura (`BuildingId`, `AggregatePopulationPool` como record) | none | — só build gate | — | `bash scripts/build.sh` |

## Parallelism Assessment

> Baseado em: xUnit sem config de paralelismo customizado (nenhum `xunit.runner.json` no
> repo) — paralelismo default é por classe de teste; testes que spawnam processo (`Workers`)
> ou tocam arquivo compartilhado (`tests/golden/world-hashes.json`, `tests/baselines/*.json`)
> não são seguros em paralelo entre si.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
|---|---|---|---|
| Unit (entidade/regra) | Yes | Cada teste cria seu próprio `WorldState`/objeto, sem estado compartilhado | Padrão de `MoneyTests.cs`, `WorldStateTests.cs` |
| Determinismo (mesmo processo) | Yes | Duas instâncias de `WorldState` independentes no mesmo teste | `GoldenHashesTests.cs` |
| Determinismo (dois processos / CLI) | No | Spawna processo `Workers` real, custo de I/O e possível contenção de porta/arquivo temporário | `DeterminismTwoProcessTests.cs` (já sequencial) |
| Cenário (10-100 anos) | No | Caro (CPU), e testes de golden hash compartilham `tests/golden/world-hashes.json` | `GoldenHashesTests.cs`, `rules/tests.md` ("Cenário: caro — poucos") |
| API integration (`WebApplicationFactory`) | Yes | Host isolado por teste, sem estado global compartilhado | Padrão .NET `WebApplicationFactory` |

## Gate Check Commands

> **Cadência por task (ajuste do usuário):** cada task roda só o que prova ela mesma — nunca
> `bash scripts/verify.sh` completo nem `Category=Scenario` por task. Regressão completa
> (build+lint+suíte inteira) e cenário de 100 anos ficam pro **fim da fase/feature**, papel
> do Verifier (step 10 do Execute), não de cada task individual.

| Gate Level | When to Use | Command |
|---|---|---|
| Quick | Task isolada (entidade/regra/sistema só em `Cities`) | `bash scripts/test.sh --filter "FullyQualifiedName~Cities"` |
| Full | Task que toca tipo compartilhado (`Npc`/`Household`/`WorldState`) ou tem determinismo/integration cruzando módulo | `bash scripts/test.sh` (sem `--filter` — já exclui `Category=Scenario` por padrão, ver `scripts/test.sh`) |
| Nightly | Critério de 100 anos — **nunca por task**, só quando toda a fase estiver implementada | `bash scripts/test.sh --filter Category=Scenario` |
| Build | **Só no fim da fase** (Verifier/fechamento), nunca por task individual | `bash scripts/verify.sh` |

---

## Execution Plan

### Phase 1: Foundation (Sequential)

```
T1 ──→ T2 ──→ T3 ──→ T4 ──→ T5 ──→ T6 ──→ T7 ──→ T8
```

### Phase 2: Core Systems (Parallel OK após T8)

```
        ┌→ T9  ─┐
T8 ─────┼→ T10 ─┼──→ T12 ──→ T13
        └→ T11 ─┘
```

### Phase 3: Inspection (Sequential após T9)

```
T9 ──→ T14 ──→ T15
           └──→ T16
```

### Phase 4: Verification (cenário/propriedade — Sequential, depende de tudo acima)

```
T13, T15, T16 completos ──→ T17 ──→ T18 ──→ T19 ──→ T20 ──→ T21 ──→ T22
```

---

## Task Breakdown

### T1: `AggregatePopulationPool` + `City` entity

**What**: Novo tipo valor `AggregatePopulationPool` (Count/WealthSum/HealthSum) + `City`
(Id, Location, FoundedAtTick, FoundedFromCityId, AggregatePool, BuildingIds,
ConstructionQueue) com `Materialize`/`Dematerialize`.
**Where**: `src/LivingWorld.Domain/Cities/{AggregatePopulationPool,City}.cs`
**Depends on**: None
**Reuses**: molde de `Household`/`Workplace` (lista+dict, construtor único de reidratação)
**Requirement**: CITY-01, CITY-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `City` construtível só por construtor único (round-trip de snapshot)
- [ ] `Materialize`/`Dematerialize` decrementam/incrementam `AggregatePool` de forma simétrica
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T2: `CityRules`

**What**: Record cenário-driven (limiares de emigração, pesos de migração, limiares de
fundação + `OrganizationTicks`, `MaterializationIdleTicksBeforeEligible`) com `Create` +
`Disabled`.
**Where**: `src/LivingWorld.Domain/Cities/CityRules.cs`
**Depends on**: T1
**Reuses**: `EconomyRules.Create` como template de validação
**Requirement**: CITY-02, CITY-05, CITY-07, CITY-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Create` rejeita todo limiar negativo/inconsistente (mesmo padrão de `EconomyRules.Create`)
- [ ] `Disabled` nunca usado por cenário real (mesma disciplina de `EconomyRules.Disabled`)
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T3: `CityCatalog` + `Building` + `ConstructionProject`

**What**: `BuildingRecipe(Inputs, TicksToBuild, HousingCapacityProvided)`,
`CityCatalog.BuildingRecipes` (id-only, AD-023), `Building`, `ConstructionProject.Advance()`.
**Where**: `src/LivingWorld.Domain/Cities/{CityCatalog,Building,ConstructionProject}.cs`
**Depends on**: T1
**Reuses**: `ProductionRecipe.Create`/`ResourceStock` como template
**Requirement**: CITY-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `BuildingRecipe` rejeita input negativo/`TicksToBuild <= 0`
- [ ] `ConstructionProject.Advance()` decrementa `TicksRemaining`, nunca abaixo de 0
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T4: `Npc.CityId` / `Household.CityId`

**What**: Novo campo `CityId City` em `Npc` e `Household` (mutável só por
`JoinCity`/`LeaveCity`, mesmo padrão de `JoinHousehold`/`LeaveHousehold`); atualiza os
construtores únicos de reidratação dos dois tipos.
**Where**: `src/LivingWorld.Domain/Population/{Npc,Household}.cs` (modifica)
**Depends on**: T1
**Reuses**: padrão `JoinHousehold`/`LeaveHousehold`
**Requirement**: CITY-01 (pré-requisito de todas as demais)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Npc`/`Household` aceitam `CityId` no construtor único; snapshot round-trip preserva o campo
- [ ] `JoinCity`/`LeaveCity` espelham `JoinHousehold`/`LeaveHousehold`
- [ ] Gate: `bash scripts/test.sh`

**Tests**: unit
**Gate**: full (toca tipo consumido por todo o resto do mundo — roda a suíte inteira, não só `Cities`)

---

### T5: `City`/`Building` em `WorldState`

**What**: Listas+dicts canônicos de `City`/`Building`, contadores `NextCityId`/`NextBuildingId`
(via RNG semeado de stream dedicado pra `CityId`/`LocationId`, nunca `Guid.NewGuid()`),
`AddCity`/`AddBuilding`/`FindCity`/`FindBuilding`, atualiza os dois construtores de
`WorldState`.
**Where**: `src/LivingWorld.Simulation/WorldState.cs` (modifica)
**Depends on**: T1, T3, T4
**Reuses**: molde de `Households`/`Workplaces` em `WorldState`
**Requirement**: CITY-01, CITY-03, CITY-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `[Canonical]` em `Cities`/`Buildings`; snapshot round-trip preserva os dois
- [ ] Geração de `CityId`/`LocationId` só via `ctx.Rng("city-founding")` (nunca `Guid.NewGuid()` — `rules/simulation-determinism.md`)
- [ ] Gate: `bash scripts/test.sh`

**Tests**: unit
**Gate**: full

---

### T6: `ReferentialIntegritySweep` — resolvers reais de `CityId`/`LocationId`

**What**: Troca as duas entradas vazias (`_ => []`) por resolvers reais a partir de
`world.Cities`/`world.Buildings`.
**Where**: `src/LivingWorld.Simulation/ReferentialIntegritySweep.cs:24-25` (modifica)
**Depends on**: T5
**Reuses**: entradas já existentes de `NpcId`/`HouseholdId` como template
**Requirement**: CITY-01 (integridade referencial, task 12 herdada da Fase 3)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Npc.CityId`/`Household.CityId` órfão (aponta pra cidade inexistente) é pego pelo sweep
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~ReferentialIntegritySweep"`

**Tests**: unit
**Gate**: quick

---

### T7: `CityScenarioLoader`

**What**: Parse manual + `Result<T>` de `CityRules`/`CityCatalog`/cidades iniciais a partir de
JSON de cenário — nenhum parâmetro hardcoded em C# (R3).
**Where**: `src/LivingWorld.Simulation/Cities/CityScenarioLoader.cs`
**Depends on**: T2, T3
**Reuses**: `EconomyScenarioLoader` como template exato (mesmos helpers `TryGetInt`/`TryGetIntLongMap`/etc.)
**Requirement**: CITY-02, CITY-03, CITY-07, CITY-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Campo obrigatório ausente nomeia o campo no erro (mesmo contrato de `EconomyScenarioLoader`)
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T8: `CityPopulationQuery`

**What**: `Population`/`Wealth`/`Health`/`Inequality` sempre recomputados on-demand
(approach A) a partir de `world.Npcs` (filtrado por `CityId`, vivo) + `AggregatePool`;
`Inequality` = Gini sobre `Wallet` dos materializados.
**Where**: `src/LivingWorld.Domain/Cities/CityPopulationQuery.cs`
**Depends on**: T5
**Reuses**: nada — componente novo central da fase
**Requirement**: CITY-01, CITY-09

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Population` bate com `COUNT` manual + `AggregatePool.Count` em cenário de teste
- [ ] Nenhum campo é cacheado (grep confirma ausência de campo mutável privado pra esses agregados)
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T9: `MaterializationSystem` [P]

**What**: Materializa por papel formal (líder/mestre/chefe de household) ou alvo de
inspeção ativa; desmaterializa por ociosidade (`CityRules.MaterializationIdleTicksBeforeEligible`);
`EnsureMaterialized(NpcId)` chamável sob demanda pela inspeção.
**Where**: `src/LivingWorld.Simulation/Cities/MaterializationSystem.cs`
**Depends on**: T8
**Reuses**: `ISimulationSystem` (Daily), `WorldRngRegistry` pra amostragem de atributos na materialização
**Requirement**: CITY-04, CITY-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Materializar decrementa `AggregatePool` em exatamente 1 e cria `Npc`
- [ ] Desmaterializar devolve exatamente os atributos do NPC ao pool e remove a linha
- [ ] NPC com papel formal nunca é elegível a desmaterialização enquanto ocupar o papel
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit + determinismo (round-trip de hash materializar→desmaterializar)
**Gate**: quick

---

### T10: `ConstructionSystem` [P]

**What**: Avança `City.ConstructionQueue` (Daily, FIFO); consome do estoque da cidade
conforme `BuildingRecipe`; falha (`Result.Fail`, sem mutar) se insumo insuficiente; conclui →
`City.AddBuilding`.
**Where**: `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`
**Depends on**: T3, T5
**Reuses**: `ResourceStock.Withdraw` (falha sem mutar já é a garantia existente)
**Requirement**: CITY-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Iniciar obra sem insumo retorna `Failure` e `Hash(world)` inalterado
- [ ] Obra concluída tem consumo total == receita do cenário
- [ ] Fila processada em ordem determinística (FIFO), nunca por hash de dicionário
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit + determinismo
**Gate**: quick

---

### T11: `CityGrowthSystem` [P]

**What**: Emigração agregada do pool quando comida/moradia/segurança < limiar do cenário
(`CityRules`), taxa proporcional ao déficit (nunca fixa).
**Where**: `src/LivingWorld.Simulation/Cities/CityGrowthSystem.cs`
**Depends on**: T2, T5, T8
**Reuses**: molde de sistema Daily (`WagePaymentSystem`)
**Requirement**: CITY-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Déficit de comida/moradia/segurança reduz `AggregatePool.Count` (nunca NPC materializado, que segue por `MigrationSystem`)
- [ ] Taxa vem só de `CityRules`, nenhum literal em C#
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit + determinismo
**Gate**: quick

---

### T12: `MigrationSystem`

**What**: NPC/household materializado decide migrar pesando emprego/comida/segurança/laços
familiares (`CityRules`); move `CityId` do NPC e de todo o household no mesmo tick.
**Where**: `src/LivingWorld.Simulation/Cities/MigrationSystem.cs`
**Depends on**: T9, T11
**Reuses**: `BehaviorDecisionSystem` (mesmo padrão de decisão utility-driven), `JoinCity`/`LeaveCity` (T4)
**Requirement**: CITY-07

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC sai de A e entra em B no mesmo tick — nunca um tick sem cidade
- [ ] Household migra em conjunto, preserva `HouseholdId`
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit + cenário (par com/sem laço familiar — R4, contagem de acertos em seeds)
**Gate**: full

---

### T13: `SettlementFoundingSystem`

**What**: Checa limiares de fundação (concentração, recurso, rota, defensabilidade,
liderança) mensalmente; ao bater todos, agenda evento único em
`now + CityRules.OrganizationTicks` (mesmo padrão de `MortalitySystem.SchedulePlannedDeath`);
ao disparar, cria `City` novo e move o grupo qualificado (`AggregatePool` + NPCs
materializados do grupo) da cidade-mãe.
**Where**: `src/LivingWorld.Simulation/Cities/SettlementFoundingSystem.cs`
**Depends on**: T9, T11
**Reuses**: `ctx.ScheduleEvent`/`EventScheduler` (evento único, nunca varredura por tick)
**Requirement**: CITY-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Todos os limiares satisfeitos → evento agendado em exatamente `OrganizationTicks`
- [ ] Soma de populações antes/depois do split é idêntica
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit + determinismo
**Gate**: quick

---

### T14: `NpcInspectionQuery` + `NpcInspectionDto`

**What**: Único ponto de consulta (identidade, família, profissão, atributos, rotina,
memórias — lista vazia nesta fase) — materializa sob demanda antes de montar o DTO; `Fail`
se `id` não existe ou está morto.
**Where**: `src/LivingWorld.Simulation/Cities/{NpcInspectionQuery,NpcInspectionDto}.cs`
**Depends on**: T9
**Reuses**: `MaterializationSystem.EnsureMaterialized`, `Result<T>`
**Requirement**: CITY-06

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Inspect` materializa NPC agregado sob demanda antes de responder
- [ ] `Inspect` falha (nunca lança) para id morto/inexistente
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit
**Gate**: quick

---

### T15: `LivingWorld.Api` — `GET /npcs/{id}` [P]

**What**: `ProjectReference` de `Api` pra `Infrastructure`+`Simulation` (hoje não referencia
nenhum); endpoint carrega snapshot mais recente, roda `NpcInspectionQuery.Inspect`, 404 em
`Fail`.
**Where**: `src/LivingWorld.Api/Program.cs` (+ `LivingWorld.Api.csproj`)
**Depends on**: T14
**Reuses**: `NpcInspectionQuery`
**Requirement**: CITY-06

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `GET /npcs/{id}` com NPC vivo devolve o DTO completo
- [ ] `GET /npcs/{id}` com NPC morto/inexistente devolve 404, nunca 500
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~NpcEndpoint"`

**Tests**: integration
**Gate**: full

---

### T16: `LivingWorld.Workers inspect-npc <id>` [P]

**What**: Novo branch de `args[0]` (mesmo padrão de `hash <seed> <ticks>`, AD-020) que chama
o mesmo `NpcInspectionQuery.Inspect` e imprime o DTO; exit code 1 em `Fail`.
**Where**: `src/LivingWorld.Workers/Program.cs` (modifica)
**Depends on**: T14
**Reuses**: `NpcInspectionQuery` (mesma consulta da API — zero lógica duplicada)
**Requirement**: CITY-06

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `inspect-npc <id>` válido imprime o mesmo conteúdo que a API devolveria
- [ ] `inspect-npc <id>` inválido sai com código 1, sem stack trace não tratada
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~InspectNpcCli"`

**Tests**: integration
**Gate**: full

---

### T17: Conservação da LOD contra fonte independente

**What**: Cenário de 10 anos; a cada tick, `COUNT(*)` de NPCs materializados no store (lido
direto, sem `City.Population`) + `city.AggregatePool.Count` (lido cru) == população total.
**Where**: `tests/LivingWorld.Tests/Cities/LodConservationScenarioTests.cs`
**Depends on**: T9, T10, T11, T12, T13
**Reuses**: molde de cenário de `GoldenHashesTests`/`BytesPerNpcPerYearSensorTests`
**Requirement**: CITY-04, CITY-09

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Assert por tick, 10 anos, nunca diverge
- [ ] `[Trait("Category","Scenario")]` no teste equivalente de 100 anos (nightly)
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: cenário
**Gate**: full

---

### T18: Round-trip de materialização

**What**: Materializar e desmaterializar o mesmo NPC (sem nenhuma outra mudança) deixa
`Hash(world)` byte-idêntico.
**Where**: `tests/LivingWorld.Tests/Cities/MaterializationRoundTripTests.cs`
**Depends on**: T9
**Reuses**: `GoldenHashesTests` (comparação de hash)
**Requirement**: CITY-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Hash antes == hash depois do ciclo materializar→desmaterializar
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: determinismo
**Gate**: quick

---

### T19: Par base/tratamento — fome derruba população

**What**: Mesma seed, tratamento = produção de comida zerada; `popTrat < popBase`, diferença
maior que o spread entre duas seeds do baseline (≥10 seeds, R4).
**Where**: `tests/LivingWorld.Tests/Cities/FoodShortageMigrationScenarioTests.cs`
**Depends on**: T11
**Reuses**: `PairedScenarioTests` (Fase 7) como template do harness par base/tratamento
**Requirement**: CITY-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Contagem de acertos (ex.: 10/10) documentada no teste, não magnitude solta
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: cenário
**Gate**: full

---

### T20: Fundação com gatilho já satisfeito

**What**: Cenário com todos os limiares de fundação batidos no tick 0; assert de fundação em
`≤ OrganizationTicks`; soma de populações antes/depois do split idêntica.
**Where**: `tests/LivingWorld.Tests/Cities/SettlementFoundingScenarioTests.cs`
**Depends on**: T13
**Reuses**: nada novo — cenário dedicado
**Requirement**: CITY-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Fundação ocorre em `≤ K` ticks, nunca antes do limiar bater
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: cenário
**Gate**: full

---

### T21: Inspeção exaustiva por reflexão, 100 NPCs

**What**: Mundo de 100 NPCs; itera **todos** os vivos (sem sorteio); compara `NpcInspectionDto`
campo a campo (por reflexão) com o estado do motor no mesmo tick — falha se algum campo do
DTO ficar sem comparação.
**Where**: `tests/LivingWorld.Tests/Cities/NpcInspectionDtoCoverageTests.cs`
**Depends on**: T15, T16
**Reuses**: molde de `ReferentialIntegritySweepTests`/`MonotonicFieldsTests` (cobertura por reflexão)
**Requirement**: CITY-06

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 100/100 NPCs comparados, nenhum sorteado
- [ ] Campo novo no DTO sem comparação reprova o próprio teste de cobertura
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Tests**: unit (reflexão)
**Gate**: full

---

### T22: Flag LOD/migração desligada muda o hash

**What**: Desligar LOD e migração por flag de teste faz `Hash(world)` após 10 anos divergir
do mundo com LOD ligado — prova que o sistema entra na conta (mesmo padrão do inverso de
determinismo em `rules/eval-criteria.md`).
**Where**: `tests/LivingWorld.Tests/Cities/LodEntersHashTests.cs`
**Depends on**: T9, T11, T12
**Reuses**: molde de teste de mutação (desligar por flag) já usado em Fase 7 (`NeutralDriftEnabled`)
**Requirement**: CITY-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Hash com LOD/migração ligados ≠ hash com ambos desligados, mesma seed, 10 anos
- [ ] Gate: `bash scripts/test.sh --filter "FullyQualifiedName~Cities"`

**Commit**: `feat(phase-08-cities): fecha Fase 8 (Cidades)` (task final — commit de fase, precedido pelos commits atômicos de T1-T21)

**Tests**: cenário
**Gate**: full

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8

Phase 2 (Parallel após T8):
    ├── T9  [P]
    ├── T10 [P]  } podem rodar em qualquer ordem
    └── T11 [P]
  T9, T11 completos → T12 → T13

Phase 3 (após T9; T15/T16 paralelos entre si):
  T14 → ├── T15 [P]
        └── T16 [P]

Phase 4 (Sequential, depende de tudo):
  T13, T15, T16 completos → T17 → T18 → T19 → T20 → T21 → T22
```

---

## Task Granularity Check

| Task | Scope | Status |
|---|---|---|
| T1-T3 | 1 entidade/tipo por task | ✅ Granular |
| T4 | 1 mudança de schema (campo) em 2 arquivos coesos | ✅ Granular (2-3 relacionados) |
| T5-T8 | 1 componente/serviço por task | ✅ Granular |
| T9-T13 | 1 sistema por task | ✅ Granular |
| T14-T16 | 1 componente (query/endpoint/CLI) por task | ✅ Granular |
| T17-T22 | 1 critério de verificação do roadmap por task | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T1 | None | — | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T1 | T1→T3 (via T2→T3 sequencial) | ✅ Match |
| T4 | T1 | T3→T4 sequencial | ✅ Match |
| T5 | T1, T3, T4 | T4→T5 | ✅ Match |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | T2, T3 | T6→T7 sequencial | ✅ Match |
| T8 | T5 | T7→T8 sequencial | ✅ Match |
| T9 | T8 | T8→T9 [P] | ✅ Match |
| T10 | T3, T5 | T8→T10 [P] | ✅ Match |
| T11 | T2, T5, T8 | T8→T11 [P] | ✅ Match |
| T12 | T9, T11 | T9,T11→T12 | ✅ Match |
| T13 | T9, T11 | T12→T13 sequencial | ✅ Match |
| T14 | T9 | T9→T14 | ✅ Match |
| T15 | T14 | T14→T15 [P] | ✅ Match |
| T16 | T14 | T14→T16 [P] | ✅ Match |
| T17 | T9, T10, T11, T12, T13 | Phase 4 depende de T13,T15,T16 | ✅ Match (T15/T16 não afetam LOD, dependência do diagrama é superset seguro) |
| T18 | T9 | T17→T18 sequencial | ✅ Match |
| T19 | T11 | T18→T19 sequencial | ✅ Match |
| T20 | T13 | T19→T20 sequencial | ✅ Match |
| T21 | T15, T16 | T20→T21 sequencial | ✅ Match |
| T22 | T9, T11, T12 | T21→T22 sequencial | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T1 | Entidade de domínio | unit | unit | ✅ OK |
| T2 | Regra cenário-driven | unit | unit | ✅ OK |
| T3 | Entidade de domínio | unit | unit | ✅ OK |
| T4 | Entidade de domínio (modifica `Npc`/`Household`) | unit | unit | ✅ OK |
| T5 | Sistema/estado (`WorldState`) | unit + determinismo | unit | ⚠️ ver nota |
| T6 | Arquitetura/reflexão | unit | unit | ✅ OK |
| T7 | Regra cenário-driven (loader) | unit | unit | ✅ OK |
| T8 | Entidade de domínio (query) | unit | unit | ✅ OK |
| T9 | Sistema de simulação | unit + determinismo | unit + determinismo | ✅ OK |
| T10 | Sistema de simulação | unit + determinismo | unit + determinismo | ✅ OK |
| T11 | Sistema de simulação | unit + determinismo | unit + determinismo | ✅ OK |
| T12 | Sistema de simulação | unit + determinismo | unit + cenário | ✅ OK (cenário é o AC causal, superset de determinismo) |
| T13 | Sistema de simulação | unit + determinismo | unit + determinismo | ✅ OK |
| T14 | Entidade de domínio (query) | unit | unit | ✅ OK |
| T15 | API | integration | integration | ✅ OK |
| T16 | CLI | integration | integration | ✅ OK |
| T17-T22 | Cenário/propriedade | cenário | cenário | ✅ OK |

**Nota T5**: `WorldState` em si é estado/fiação, não um sistema com comportamento próprio —
o determinismo de `Cities`/`Buildings` no hash é coberto pelos testes de T9-T13 (que exercitam
o snapshot através dos sistemas); T5 isolado só precisa provar round-trip de snapshot (unit),
sem duplicar o teste de determinismo de sistema.

---

## Tips (herdado da skill — não repetir aqui)
Ver `references/tasks.md` da skill `tlc-spec-driven` pra convenção de `[P]`, commits e
verificação por task.
