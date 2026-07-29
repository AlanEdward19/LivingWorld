# Fase 10 (História degradável) Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is
the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review,
Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-10-history/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase (`scripts/verify.sh`, `scripts/test.sh`, `tests/LivingWorld.Tests/*`,
> `rules/simulation-determinism.md`, `rules/eval-criteria.md`, `rules/database-entities.md`).
> Guidelines found: `rules/simulation-determinism.md` (determinismo em dois processos + golden
> hash), `rules/eval-criteria.md` (R1..R5, teste de mutação para gate de segurança — aplica
> diretamente à separação Verdade/Crença), `rules/database-entities.md` (append-only real,
> nenhum recurso exclusivo de SQLite).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain rules (`HistoryRules`) | unit | Todos os branches de validação (`Result<T>` sucesso/falha por campo, mesmo padrão de `EconomyRulesTests`/`PerfRulesTests`) | `tests/LivingWorld.Tests/History/HistoryRulesTests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Domain types puros (`Fact`, `ReportState`, `Book`, `TransmissionMediumType`/`MediumFidelity`, `DistortionOperator`, `CompensatingCorrection`) | none | Build gate só — são `record`/enum sem lógica própria, cobertos indiretamente pelos testes dos sistemas que os consomem | — | build gate (`bash scripts/build.sh`, parte de `verify.sh`) |
| Sistemas de domínio (`SignificanceCalculator`, `DistortionEngine`, `CanonSlotManager`, `LineageQuery`) | unit + determinismo | 1:1 com o HIST-NN correspondente; todo sistema com RNG ou índice novo roda o teste de **dois processos** obrigatório (mesmo risco nomeado no design da Fase 9) | `tests/LivingWorld.Tests/History/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Sistemas estruturais (`FactToReportConversionScheduler`, `LivingMemoryWindow`, `BookRediscoverySystem`) | integration (roda `ScenarioRunner` real) | Cada AC de HIST-01..HIST-14 com cenário real: morte de testemunha, agendamento, conversão observada; golden hash regenerado + registrado no mesmo commit (todo `Fact`/`Kind` novo muda o hash) | `tests/LivingWorld.Tests/History/*.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Persistência (append-only da tabela de fatos) | integration | `UPDATE`/`DELETE` reais tentados contra o armazenamento, ambos falham (HIST-02 AC2) — mesmo padrão de teste real usado hoje pelo append-only do event log | `tests/LivingWorld.Tests/History/FactAppendOnlyTests.cs` (novo, mesmo espírito de testes de `EventLogRecord`) | `bash scripts/test.sh --filter Category!=Scenario` |
| Consultas (`HistoryTruthQuery`, `HistoryBeliefQuery`) | unit + arquitetura | 1:1 com HIST-15/16 (dois handlers físicos distintos); teste de arquitetura via `NetArchTest` enumerando por reflexão **todos** os handlers de `LivingWorld.Api`/`LivingWorld.Workers` — falha se algum ficar sem cobertura | `tests/LivingWorld.Tests/History/HistoryQuerySeparationTests.cs` (novo) | `bash scripts/test.sh --filter Category!=Scenario` |
| Teste de mutação da fronteira Verdade/Crença | unit (par obrigatório, `rules/eval-criteria.md`) | Desligar a checagem por flag de teste **tem de** fazer o critério de HIST-17/18 falhar — se não falhar, a checagem não mede nada | `tests/LivingWorld.Tests/History/HistoryQuerySeparationMutationTests.cs` (novo, mesmo padrão de `Fitness_symbol_scanner_flags_a_banned_member_name_in_scratch_source` em `ArchitectureTests.cs`) | `bash scripts/test.sh --filter Category!=Scenario` |
| Índices (`HistoryIndex`) | unit + determinismo + prova de complexidade | 1:1 com HIST-20/21; contagem de linhas lidas via `CountingCommandInterceptor` (mesmo padrão da Fase 3/9), reprova se ler mais que `k × tamanho do resultado` (baseline) | `tests/LivingWorld.Tests/History/HistoryIndexTests.cs` (novo) | `bash scripts/test.sh --filter Category!=Scenario` |
| Cenário de cânone limitado (50/100/200 anos) | scenario (curta o bastante para o gate; três horizontes, não 100 anos "nightly" — usar `N` pequeno no cenário de teste para tornar despejo observável rápido) | HIST-10/HIST-11: contagem de relatos vivos por comunidade estável nos 3 horizontes; bytes por relato retido dentro do baseline de 20 seeds | `tests/LivingWorld.Tests/History/CanonBoundedOverTimeTests.cs` (novo) | `bash scripts/test.sh --filter Category!=Scenario` (roda no gate padrão — cenário pequeno, não `Category=Scenario`) |
| Round-trip snapshot + replay (`Hash(world)`) | integration | HIST-26: reidratar snapshot e reaplicar log reproduz o mesmo `Hash(world)` da execução contínua | `tests/LivingWorld.Tests/History/HistorySnapshotReplayTests.cs` (novo, mesmo padrão de `PersistenceCrossProcessTests.cs`) | `bash scripts/test.sh --filter Category!=Scenario` |

**Coverage Expectation values** seguem o forte-padrão-existente do repo: toda AC de `HIST-NN`
mapeia 1:1 para um teste nomeado; nenhum teste de camada de domínio aceita cobertura parcial;
todo sistema que muda `WorldEventKind`/o esqueleto exige golden hash regenerado no mesmo commit
(mesma disciplina de AD-047/AD-059/Fase 9 Bloco C).

## Parallelism Assessment

> Generated from codebase — confirm before Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (domain rules, tipos puros, sistemas de domínio) | Yes | Cada teste cria seu próprio `WorldState`/objeto via `ScenarioRunner.Create` — sem estado global compartilhado | `ScenarioRunner.Create` retorna instância nova por chamada (mesmo padrão já usado em toda a suíte) |
| integration (sistemas estruturais, persistência, snapshot/replay) | Yes | `WorldState`/banco de teste por teste, sem fixture compartilhada entre casos (zero round-trip no tick é invariante da Fase 3) | `src/LivingWorld.Simulation/ScenarioRunner.cs`, `tests/LivingWorld.Tests/PersistenceCrossProcessTests.cs` |
| arquitetura/mutação (separação Verdade/Crença) | Yes | `NetArchTest` roda sobre assemblies já compilados, sem estado mutável entre testes | `ArchitectureTests.cs` já roda em paralelo com o resto da suíte hoje |
| scenario curto (cânone 50/100/200 anos) | Yes | Cenário isolado por teste, `N` pequeno mantém o custo baixo o bastante para não precisar do isolamento de `Category=Scenario` | Mesmo padrão do sensor de escala da Fase 9 (`ScaleScenarioSensorTests.cs`) |
| determinismo em dois processos | No | Por definição roda dois processos do mesmo teste em sequência comparando saída | `rules/simulation-determinism.md`, `DeterminismTwoProcessTests.cs` |

## Gate Check Commands

> Generated from codebase — confirm before Execute.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Depois de tasks só com unit tests (domain rules, tipos puros) | `bash scripts/test.sh --filter Category!=Scenario` |
| Full | Depois de tasks com integration (sistemas estruturais, persistência, snapshot/replay, arquitetura) | `bash scripts/verify.sh` |
| Mutação (manual, caro) | Se a task mexer no próprio gate/sensor de separação Verdade/Crença | `bash scripts/verify-mutation.sh` |

---

## Execution Plan

### Phase 1: Esqueleto do fato (Sequential)

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Relato, distorção e meios (Sequential, com paralelismo interno)

```
        ┌→ T6 ─┐
T5 ──T4→┤       ├→ T8 → T9 → T10 → T11
        └→ T7 ─┘
```

### Phase 3: Livros (Sequential)

```
T11 → T12 → T13
```

### Phase 4: Consultas Verdade/Crença (Sequential)

```
T2 → T14
T11, T9 → T15
T14, T15 → T16
```

### Phase 5: Índices, linhagem e correção (Parallel após esqueleto)

```
     ┌→ T17
T2 ──┼→ T18 → T19
     └(T7 também alimenta T17)
```

### Phase 6: Fechamento — invariantes de escala e round-trip (Sequential)

```
T10, T11 → T20 → T21
```

---

## Task Breakdown

### T1: Criar `HistoryRules` (record + factory)

**What**: Novo tipo `HistoryRules` com `SkeletonSignificanceThreshold`, `CanonSizePerCommunity`,
tabela `MediumFidelity` por `TransmissionMediumType`, probabilidade por `DistortionOperator`,
pesos de despejo (`ImportanceWeight`/`TransmissibilityWeight`/`RecencyWeight`) — seguindo o
padrão `record` + `static Result<HistoryRules> Create(...)`.
**Where**: `src/LivingWorld.Domain/History/HistoryRules.cs`
**Depends on**: None
**Reuses**: `src/LivingWorld.Domain/Economy/EconomyRules.cs` (padrão record+factory+`Result<T>`)
**Requirement**: HIST-08

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `HistoryRules.Create` valida `CanonSizePerCommunity > 0`, `SkeletonSignificanceThreshold` em `[0,1]`, taxas de distorção em `[0,1]`, pesos de despejo `>= 0`
- [ ] `ScenarioRunner.Create` ganha parâmetro opcional `historyRules` (default `HistoryRules.Disabled`), mesmo padrão de `economyRules`/`perfRules`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: sucesso + 1 caso de falha por campo inválido (mínimo 5 casos de falha)

**Tests**: unit
**Gate**: quick

---

### T2: `Fact` (esqueleto imutável) + `FactId` + `WorldEventKind` novos

**What**: Novo tipo `Fact` (quem/o quê/onde/quando/significância) e `FactId` (`readonly record
struct`, id monotônico — mesmo molde de `NpcId`); `WorldEventKind` ganha `FactRecorded` (os
demais kinds desta fase entram nas tasks que os produzem, evitando um enum inflado cedo demais).
**Where**: `src/LivingWorld.Domain/History/Fact.cs`, `src/LivingWorld.Domain/Ids.cs`
(modificação — novo `FactId`), `src/LivingWorld.Simulation/WorldEvent.cs` (modificação)
**Depends on**: None
**Reuses**: Molde de `NpcId`/`HouseholdId` (`Ids.cs`); `WorldEvent`/`WorldEventKind` existentes
**Requirement**: HIST-01

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Fact` é `[Canonical]`, `sealed record`, sem método de mutação
- [ ] `FactId` monotônico nasce de contador do `WorldState` (nunca `Guid`), mesmo padrão de `NpcId`
- [ ] `WorldState.Facts: IReadOnlyList<Fact>` existe como coleção canônica
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: teste de criação + teste de que `Fact` não expõe nenhum setter/método de mutação (reflexão simples)

**Tests**: unit
**Gate**: quick

---

### T3: Append-only real da tabela de fatos

**What**: `FactLogRecord` em `Infrastructure` (mesmo molde de `EventLogRecord`) com constraint de
banco/trigger que rejeita `UPDATE`/`DELETE` diretos — teste que **executa** ambas as operações
contra o armazenamento e exige que as duas falhem (não é checagem de tipo, é tentativa real).
**Where**: `src/LivingWorld.Infrastructure/FactLogRecord.cs` (novo), `WorldDbContext.cs`
(modificação), migração EF nova
**Depends on**: T2
**Reuses**: `EventLogRecord.cs` (molde), `rules/database-entities.md` (regra de migração
versionada, nenhum recurso exclusivo de SQLite)
**Requirement**: HIST-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Migração nova (não edita migração aplicada) cria a tabela com constraint de imutabilidade
- [ ] Teste executa `UPDATE` direto contra a tabela — falha
- [ ] Teste executa `DELETE` direto contra a tabela — falha
- [ ] Nenhum recurso exclusivo de SQLite usado (mesma regra do event log)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: 1 teste de `UPDATE` + 1 teste de `DELETE`, ambos exigindo falha

**Tests**: integration
**Gate**: full

---

### T4: `SignificanceCalculator`

**What**: Calcula significância (0–1) na escrita a partir de escopo do impacto, entidades
afetadas e papel dos envolvidos; evento ≥ `HistoryRules.SkeletonSignificanceThreshold` vira
`Fact`, abaixo colapsa (omissão na escrita, nunca deleção).
**Where**: `src/LivingWorld.Simulation/History/SignificanceCalculator.cs`
**Depends on**: T1, T2
**Reuses**: `WorldEventKind` existente como base do "o quê"
**Requirement**: HIST-01, HIST-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Compute` retorna valor determinístico e reproduzível para o mesmo `WorldEvent`/`WorldState`
- [ ] 100% dos eventos de teste com significância ≥ limiar geram `Fact` íntegro no esqueleto
- [ ] ≥ `X%` (baseline, `tests/baselines/history-collapse.json`, 20 seeds) dos eventos abaixo do limiar não geram linha (colapso, não deleção)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de cálculo + teste par acima/abaixo do limiar + baseline registrado

**Tests**: integration
**Gate**: full

---

### T5: `LivingMemoryWindow`

**What**: Enquanto há testemunha viva (participante do `Fact` ainda vivo), consulta resolve com
fidelidade alta e enviesada, sem converter para relato.
**Where**: `src/LivingWorld.Simulation/History/LivingMemoryWindow.cs`
**Depends on**: T2
**Reuses**: `AliveNpcIndex` (Fase 9) para checagem de vivo/morto, sem reimplementar
**Requirement**: HIST-01 (AC3)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `HasLivingWitness` retorna `true` enquanto ao menos um participante do `Fact` está vivo
- [ ] `Recall` nunca aplica operador de distorção (distorção só começa no relato, T8)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de janela aberta (testemunha viva) + teste de janela fechada (última testemunha morta)

**Tests**: integration
**Gate**: full

---

### T6: `TransmissionMediumType` + `MediumFidelity` [P]

**What**: Enum fechado dos 5 meios (memória viva, tradição oral familiar, livro/crônica,
monumento/inscrição, canção/ditado) e o record de parâmetros (`DistortionRatePerHop`,
`ReachHops`, `DeathConditionType`), consumidos por `HistoryRules`.
**Where**: `src/LivingWorld.Domain/History/TransmissionMediumType.cs` (novo)
**Depends on**: T4
**Reuses**: Molde de catálogo simples (`PopulationCatalog`/`EconomyCatalog`)
**Requirement**: HIST-08

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] 5 meios declarados, cada um com `MediumFidelity` própria
- [ ] Ordem relativa de distorção/alcance bate com `docs/domain/historical-memory.md` (oral > canção > livro > monumento em distorção por salto; monumento > livro em alcance)
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: 1 teste por meio validando os parâmetros declarados + 1 teste de ordem relativa

**Tests**: unit
**Gate**: quick

---

### T7: `ReportState` + `ReportId` [P]

**What**: Novo tipo `ReportState` (metadata compacta canônica do relato — Opção C do design):
`OriginFactId`, `CommunityId`, `Medium`, `HopCount`, `Weight`, `CreatedAtTick`, `LastHopTick`.
**Where**: `src/LivingWorld.Domain/History/ReportState.cs`, `Ids.cs` (novo `ReportId`)
**Depends on**: T4
**Reuses**: Molde de `NpcId`/`FactId` para `ReportId`
**Requirement**: HIST-01 (AC4)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `ReportState` é `[Canonical]`, `sealed record`
- [ ] `ReportId` monotônico, mesmo molde de `FactId`
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: teste de criação + teste de imutabilidade (sem setter)

**Tests**: unit
**Gate**: quick

---

### T8: `DistortionOperator` (8 operadores) + `DistortionEngine.Apply`

**What**: Enum dos 8 operadores (troca de atribuição, inflação de magnitude, compressão
temporal, perda de causa, moralização, anacronismo, omissão conveniente, fusão de personagens)
com dispatch explícito (não reflexão) sobre um payload estruturado; escolha e parametrização via
RNG derivado de `(ReportId, hop)`.
**Where**: `src/LivingWorld.Domain/History/DistortionOperator.cs` (enum),
`src/LivingWorld.Simulation/History/DistortionEngine.cs` (dispatch)
**Depends on**: T6, T7
**Reuses**: `WorldRngRegistry.StreamFor("history-distortion", reportId, hop)` (padrão on-demand
da Fase 9, PERF-13)
**Requirement**: HIST-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Os 8 operadores implementados como transformação determinística sobre campos estruturados
- [ ] `Apply` com mesma seed produz o mesmo resultado byte-idêntico em dois processos
- [ ] Nenhum provider de LLM (fake injetado em teste) é chamado durante `Apply` — teste falha se for
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: 1 teste por operador (8) + teste de determinismo em dois processos + teste de "LLM nunca chamada"

**Tests**: integration
**Gate**: full

---

### T9: `ReportState.AdvanceHop` + invariante de distância não decrescente

**What**: `AdvanceHop` escolhe um subconjunto de operadores (via `HistoryRules`/RNG), aplica via
`DistortionEngine`, retorna `ReportState` com `HopCount+1`; expõe `DistanceFromFact` para a
invariante `d(hop n+1) >= d(hop n)`.
**Where**: `src/LivingWorld.Simulation/History/DistortionEngine.cs` (extensão de T8)
**Depends on**: T8
**Reuses**: `DistortionOperator`/`ReportState` de T7/T8
**Requirement**: HIST-06

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Cadeia de 5+ hops mantém `d` não decrescente em todo hop, sem redescoberta
- [ ] Determinismo em dois processos verde na cadeia inteira
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de cadeia de 5 hops (assert de não-decrescência) + determinismo

**Tests**: integration
**Gate**: full

---

### T10: `FactToReportConversionScheduler` (conversão fato→relato via `EventScheduler`)

**What**: Detecta a morte da última testemunha (gancho em `MortalitySystem`) e agenda no
`EventScheduler` a conversão do `Fact` em `ReportState` hop-0 por comunidade testemunhada —
nunca varredura por tick.
**Where**: `src/LivingWorld.Simulation/History/FactToReportConversionScheduler.cs`
**Depends on**: T5, T9
**Reuses**: `EventScheduler.Schedule`, mesmo padrão de `MortalitySystem.SchedulePlannedDeath`
**Requirement**: HIST-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Morte da última testemunha agenda a conversão no tick correto (não varre `Facts` por tick)
- [ ] Duas testemunhas morrendo no mesmo tick desempatam por `NpcId` (nunca ordem de coleção)
- [ ] `ReportState` hop-0 criado com `CommunityId` correto (comunidade onde os participantes residiam/estavam)
- [ ] Golden hash regenerado (novo `WorldEventKind.ReportConverted`)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de agendamento + teste de empate no mesmo tick + golden regenerado

**Tests**: integration
**Gate**: full

**Commit**: `feat(phase-10-history): esqueleto imutavel, significancia, janela de memoria e conversao fato-relato (HIST-01..06)`

---

### T11: `CanonSlotManager` + `City.CanonSlots`

**What**: Mantém no máximo `HistoryRules.CanonSizePerCommunity` `ReportState`s vivos por `City`;
relato novo desloca o de menor peso (`importância × transmissibilidade × recência`).
**Where**: `src/LivingWorld.Simulation/History/CanonSlotManager.cs`,
`src/LivingWorld.Domain/Cities/City.cs` (modificação — nova coleção `CanonSlots`)
**Depends on**: T9
**Reuses**: `Result<T>` (mesmo padrão de `City.Materialize`/`Workplace.Hire`)
**Requirement**: HIST-08 (AC2)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Admit` nunca ultrapassa o teto declarado
- [ ] Despejo escolhe sempre o menor peso; empate desempata por `ReportId`
- [ ] Golden hash regenerado (`City.CanonSlots` novo campo canônico)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de admissão sem despejo + teste de admissão com despejo + teste de empate

**Tests**: integration
**Gate**: full

---

### T12: `Book` (objeto do mundo — cópia com erro, perda)

**What**: `Book` como registro canônico: `CarriesReportId`, `CopyOfBookId` (encadeamento de
cópias), `Lost`/`LostAtTick`. Copiar aplica erro de copista via `DistortionEngine` com meio fixo
`Book`.
**Where**: `src/LivingWorld.Domain/History/Book.cs`
**Depends on**: T11
**Reuses**: `DistortionEngine.Apply` (T8) — cópia com erro é só mais um hop
**Requirement**: HIST-09 (AC1, AC2)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Copy` gera novo `Book` com `CopyOfBookId` apontando para o original e erro de copista aplicado
- [ ] `MarkLost` marca `Lost=true`/`LostAtTick`, nunca apaga a linha
- [ ] Golden hash regenerado (`Book` novo tipo canônico)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de cópia com erro + teste de perda (linha continua legível)

**Tests**: integration
**Gate**: full

---

### T13: `BookRediscoverySystem` (evento declarado)

**What**: Redescoberta de livro perdido é sempre um `BookRediscoveryEvent` agendado
explicitamente no `EventScheduler` — nunca sorteio implícito por tick; permite conteúdo
redescoberto contradizer o cânone vigente.
**Where**: `src/LivingWorld.Simulation/History/BookRediscoverySystem.cs`
**Depends on**: T12
**Reuses**: `EventScheduler.Schedule`, mesmo padrão de `FactToReportConversionScheduler`
**Requirement**: HIST-09 (AC3)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Livro perdido sem evento agendado permanece perdido indefinidamente (nenhuma checagem por tick sorteia redescoberta)
- [ ] `BookRediscoveryEvent` agendado dispara no tick alvo, desmarca `Lost`
- [ ] Conteúdo redescoberto pode divergir do cânone vigente da comunidade (teste planta divergência)
- [ ] Golden hash regenerado (`WorldEventKind.BookRediscovered`)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de "sem evento, permanece perdido" + teste de redescoberta agendada + teste de divergência com o cânone

**Tests**: integration
**Gate**: full

**Commit**: `feat(phase-10-history): canone limitado por comunidade e livros como objetos do mundo (HIST-07..09)`

---

### T14: `HistoryTruthQuery`

**What**: Único ponto de acesso ao `Fact` bruto — motor/debug/autor. Handler físico separado,
nunca reaproveitado por consulta de jogo.
**Where**: `src/LivingWorld.Simulation/History/HistoryTruthQuery.cs`
**Depends on**: T2
**Reuses**: Padrão `Result<T>` de consulta única (`NpcInspectionQuery`), mas em tipo próprio
**Requirement**: HIST-10 (AC1)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `GetFact` retorna o `Fact` completo, sem distorção
- [ ] Nenhum outro tipo do projeto referencia `HistoryTruthQuery` além de uma ferramenta de autor/debug explicitamente marcada como tal (verificado por T16)
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: teste de consulta de fato existente + teste de falha para `FactId` inexistente

**Tests**: unit
**Gate**: quick

---

### T15: `HistoryBeliefQuery` + `DistortionEngine.Materialize`

**What**: Único ponto de acesso à crença de um NPC/família/cultura. Resolve o `ReportState`
vigente na comunidade do crente e materializa o `DistortedReport` sob demanda (nunca
serializado). NPC do pool agregado (AD-068) resolve direto para o cânone da comunidade, sem
estado de crença individual.
**Where**: `src/LivingWorld.Simulation/History/HistoryBeliefQuery.cs`,
`src/LivingWorld.Simulation/History/DistortionEngine.cs` (extensão — `Materialize`)
**Depends on**: T11, T9
**Reuses**: AD-020 (consulta única API+CLI), `CanonSlotManager` (T11)
**Requirement**: HIST-10 (AC2, AC5)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `BeliefOf(NpcId, ...)` e `BeliefOf(CityId, ...)` resolvem para o `ReportState` vigente, nunca para `Fact`
- [ ] NPC de comunidade que nunca recebeu o relato: `Result.Fail` explícito ("nunca ouviu falar"), não erro silencioso
- [ ] Duas comunidades diferentes consultando o mesmo `FactId` podem divergir (teste planta o caso)
- [ ] NPC do pool agregado resolve direto para o cânone da comunidade, sem estado individual criado
- [ ] `Materialize` nunca é serializado no snapshot canônico (`[Volatile]`, teste de reflexão sobre atributos confirma)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de resolução normal + teste de "nunca ouviu falar" + teste de divergência entre comunidades + teste de NPC agregado + teste de não-serialização

**Tests**: integration
**Gate**: full

---

### T16: Separação estrutural Verdade/Crença — teste de arquitetura + par de mutação

**What**: Enumera por reflexão (`NetArchTest`) todos os handlers de `LivingWorld.Api`/
`LivingWorld.Workers` e garante que nenhum referencia `HistoryTruthQuery`; inclui o par de
mutação obrigatório (desligar a checagem por flag de teste tem de fazer o critério falhar).
**Where**: `tests/LivingWorld.Tests/History/HistoryQuerySeparationTests.cs`,
`tests/LivingWorld.Tests/History/HistoryQuerySeparationMutationTests.cs`
**Depends on**: T14, T15
**Reuses**: `ArchitectureTests.cs` (`Types.InAssembly(...).Should().NotHaveDependencyOn(...)`)
**Requirement**: HIST-10 (AC3, AC4)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Enumeração cobre 100% dos handlers de `LivingWorld.Api`/`LivingWorld.Workers` (falha se algum tipo novo escapar da varredura, não só dos handlers conhecidos hoje)
- [ ] Nenhum handler resolve para `HistoryTruthQuery`
- [ ] Par de mutação: desligar a checagem por flag de teste faz o critério falhar (prova que a checagem mede algo)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de enumeração + teste de mutação (par obrigatório)

**Tests**: unit
**Gate**: full

**Commit**: `feat(phase-10-history): consultas Verdade e Crenca separadas com prova estrutural (HIST-10)`

---

### T17: `HistoryIndex` (por ano, entidade, tipo) [P]

**What**: Índice derivado (`[Volatile]`, reconstruído na rehidratação) sobre `Fact`s/
`ReportState`s por ano, entidade e tipo; consulta de linha do tempo resolve por lookup, nunca
varrendo a base.
**Where**: `src/LivingWorld.Simulation/History/HistoryIndex.cs`
**Depends on**: T2, T7
**Reuses**: Mesmo princípio de `AliveNpcIndex`/`MarketIndex` (Fase 9) — derivado, reconstruído,
nunca serializado
**Requirement**: HIST-11

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `ByYear`/`ByEntity`/`ByKind` resolvem sem varrer `WorldState.Facts`/`City.CanonSlots` inteiros
- [ ] Contagem de linhas lidas (via `CountingCommandInterceptor`) fica ≤ `k × tamanho do resultado`, `k` vindo de `tests/baselines/history-index.json` (20 seeds)
- [ ] `RebuildFrom` reconstrói o índice na rehidratação, nunca serializado (`[Volatile]`)
- [ ] Determinismo em dois processos verde (índice novo é o risco nomeado no design)
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste por tipo de consulta (3) + teste de contagem de linhas lidas + determinismo + baseline registrado

**Tests**: integration
**Gate**: full

---

### T18: `LineageQuery` (dinastias/linhagens derivadas) [P]

**What**: Reconstrói linhagem a partir do esqueleto (`Fact`s de nascimento/morte +
`Npc.MotherId`/`FatherId` já existentes) — nunca tabela paralela; falha explícita em ciclo/buraco.
**Where**: `src/LivingWorld.Simulation/History/LineageQuery.cs`
**Depends on**: T2
**Reuses**: `Npc.MotherId`/`FatherId` (Fase 7), nenhum campo novo de parentesco
**Requirement**: HIST-12 (AC1, AC2)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Linhagem de 4+ gerações reconstruída chega ao fundador sem buraco e sem ciclo
- [ ] Toda morte no esqueleto tem nascimento do mesmo `NpcId` em tick anterior (teste planta violação e assert falha)
- [ ] Zero eventos do esqueleto para um `NpcId` em tick posterior à sua morte
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de reconstrução de 4 gerações + teste de detecção de ciclo + teste de detecção de buraco + teste de "zero eventos pós-morte"

**Tests**: integration
**Gate**: full

---

### T19: `CompensatingCorrection`

**What**: Corrigir o passado é sempre um evento novo anexado (`CompensatingCorrection`
referenciando o `Fact` original) — a linha original nunca é reescrita, só marcada.
**Where**: `src/LivingWorld.Domain/History/CompensatingCorrection.cs`
**Depends on**: T2, T18
**Reuses**: Mesmo espírito de "branch é evento anexado, nunca `UPDATE`"
(`rules/simulation-determinism.md`)
**Requirement**: HIST-12 (AC3, AC4)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Emitir uma correção não muta o `Fact` original (bytes idênticos antes/depois)
- [ ] Consulta expõe as duas linhas (original + correção), marcadas explicitamente
- [ ] Golden hash regenerado (`WorldEventKind.CompensatingCorrection`)
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: teste de imutabilidade do original + teste de consulta expondo as duas linhas marcadas

**Tests**: integration
**Gate**: full

**Commit**: `feat(phase-10-history): indices de consulta, linhagem derivada e correcao compensatoria (HIST-11..12)`

---

### T20: Cânone limitado no tempo (50/100/200 anos) + orçamento por relato

**What**: Cenário de teste com `N` pequeno (para tornar despejo observável sem esperar 100
anos reais); roda a 50, 100 e 200 anos e assertar contagem de relatos vivos por comunidade
estável nos três horizontes; mede bytes por relato retido em 10 anos contra baseline de 20 seeds.
**Where**: `tests/LivingWorld.Tests/History/CanonBoundedOverTimeTests.cs`
**Depends on**: T10, T11
**Reuses**: `ScenarioRunner.Create(..., historyRules: ...)`, `tests/baselines/`
(`BaselineFixture`, mesmo mecanismo da Fase 9)
**Requirement**: HIST-08 (AC3, AC4)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Contagem de relatos vivos por comunidade fica no teto declarado nos 3 horizontes (50/100/200 anos), sem tendência de crescimento
- [ ] Bytes por relato retido (10 anos) dentro do baseline de 20 seeds
- [ ] Nenhum literal de teto/orçamento no texto do teste — tudo vem de `HistoryRules`/baseline
- [ ] Gate check passa: `bash scripts/test.sh --filter Category!=Scenario`
- [ ] Test count: 1 teste de estabilidade nos 3 horizontes + 1 teste de orçamento por relato + baseline registrado

**Tests**: unit (cenário pequeno, roda no gate padrão, mesmo espírito do sensor de escala da Fase 9)
**Gate**: quick

---

### T21: Round-trip snapshot + replay reproduz `Hash(world)`

**What**: Rodar um cenário até o tick T (com fatos, relatos, cânone e livros já em jogo), tirar
snapshot, reidratar, reaplicar o log até T, comparar `Hash(world)` com o hash da execução
contínua até T.
**Where**: `tests/LivingWorld.Tests/History/HistorySnapshotReplayTests.cs`
**Depends on**: T10, T11, T12
**Reuses**: `PersistenceCrossProcessTests.cs` (mesmo padrão de round-trip já usado desde a Fase 1/3)
**Requirement**: HIST-26

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Hash(world)` da execução reidratada+replay bate byte a byte com o da execução contínua
- [ ] Cenário de teste inclui ao menos um `Fact`, um `ReportState` com hop > 0 e um `Book`
- [ ] Determinismo em dois processos verde
- [ ] Gate check passa: `bash scripts/verify.sh`
- [ ] Test count: 1 teste de round-trip completo

**Tests**: integration
**Gate**: full

**Commit**: `test(phase-10-history): canone limitado no tempo provado e round-trip snapshot-replay (HIST-08, HIST-26)`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2 ──→ T3 ──→ T4 ──→ T5

Phase 2 (Parallel apos T4, depois sequencial):
  T4 concluida, entao:
    ├── T6 [P]
    └── T7 [P]  } podem rodar em qualquer ordem entre si (arquivos diferentes)
  T6, T7 completos → T8 → T9 → T10 → T11

Phase 3 (Sequential, Livros):
  T11 → T12 → T13

Phase 4 (Sequential, Consultas):
  T2 → T14
  T11, T9 completos → T15
  T14, T15 completos → T16

Phase 5 (Parallel apos esqueleto):
  T2, T7 completos, entao:
    ├── T17 [P]
    └── T18 [P]  } indices e linhagem sao consultas independentes sobre o mesmo esqueleto
  T18 completo → T19

Phase 6 (Sequential, fechamento):
  T10, T11, T12 completos → T20 → T21
```

**Parallelism constraint:** confirmado — `T6` (`TransmissionMediumType`) e `T7` (`ReportState`)
não compartilham arquivo nem dependem entre si. `T17` (`HistoryIndex`) e `T18` (`LineageQuery`)
são consultas independentes sobre o mesmo esqueleto imutável, sem escrita compartilhada.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: `HistoryRules` | 1 arquivo, 1 tipo | ✅ Granular |
| T2: `Fact` + `FactId` + `WorldEventKind` | 1 arquivo novo + 2 modificações pontuais (mesmo conceito: o esqueleto) | ⚠️ OK — coeso (um único conceito, três arquivos tocados) |
| T3: Append-only real | 1 arquivo novo + migração + config | ⚠️ OK — coeso (uma tabela, uma garantia) |
| T4: `SignificanceCalculator` | 1 arquivo, 1 tipo | ✅ Granular |
| T5: `LivingMemoryWindow` | 1 arquivo, 1 tipo | ✅ Granular |
| T6: `TransmissionMediumType`/`MediumFidelity` | 1 arquivo, 1 enum + 1 record | ✅ Granular |
| T7: `ReportState` + `ReportId` | 1 arquivo novo + 1 id novo (mesmo conceito) | ✅ Granular |
| T8: `DistortionOperator` + `DistortionEngine.Apply` | 1 enum + 1 dispatch (8 operadores, mesma responsabilidade) | ⚠️ OK — 8 operadores são cohesivos (uma engine, um enum) |
| T9: `AdvanceHop` + invariante de distância | 1 método, extensão de T8 | ✅ Granular |
| T10: `FactToReportConversionScheduler` | 1 arquivo, 1 tipo | ✅ Granular |
| T11: `CanonSlotManager` + `City.CanonSlots` | 1 arquivo novo + 1 modificação pontual | ✅ Granular |
| T12: `Book` | 1 arquivo, 1 tipo | ✅ Granular |
| T13: `BookRediscoverySystem` | 1 arquivo, 1 tipo | ✅ Granular |
| T14: `HistoryTruthQuery` | 1 arquivo, 1 tipo | ✅ Granular |
| T15: `HistoryBeliefQuery` + `Materialize` | 1 arquivo novo + 1 extensão de T8 | ✅ Granular |
| T16: Separação Verdade/Crença (teste + mutação) | 2 arquivos de teste, 1 conceito (fronteira) | ⚠️ OK — par obrigatório é sempre um conceito |
| T17: `HistoryIndex` | 1 arquivo, 1 tipo | ✅ Granular |
| T18: `LineageQuery` | 1 arquivo, 1 tipo | ✅ Granular |
| T19: `CompensatingCorrection` | 1 arquivo, 1 tipo | ✅ Granular |
| T20: Cânone limitado no tempo | 1 arquivo de teste | ✅ Granular |
| T21: Round-trip snapshot+replay | 1 arquivo de teste | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | None | T1 → T2 (sequencial na Phase 1) | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T1, T2 | T3 → T4 (sequencial) | ✅ Match |
| T5 | T2 | T4 → T5 | ✅ Match |
| T6 | T4 | T4 → T6 [P] | ✅ Match |
| T7 | T4 | T4 → T7 [P] | ✅ Match |
| T8 | T6, T7 | T6, T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T5, T9 | T9 → T10 (T5 já concluído na Phase 1) | ✅ Match |
| T11 | T9 | T9 → T11 (via T10 na Phase 2) | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T2 | T2 → T14 (Phase 4) | ✅ Match |
| T15 | T11, T9 | T11, T9 → T15 | ✅ Match |
| T16 | T14, T15 | T14, T15 → T16 | ✅ Match |
| T17 | T2, T7 | T2, T7 → T17 [P] | ✅ Match |
| T18 | T2 | T2 → T18 [P] | ✅ Match |
| T19 | T2, T18 | T18 → T19 | ✅ Match |
| T20 | T10, T11 | T10, T11 → T20 | ✅ Match |
| T21 | T10, T11, T12 | T10, T11, T12 → T21 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain rules (`HistoryRules`) | unit | unit | ✅ OK |
| T2 | Domain types puros (`Fact`) | none (build gate) — mas task inclui testes de criação/imutabilidade além do mínimo | unit (excede o mínimo, não viola) | ✅ OK |
| T3 | Persistência (append-only) | integration | integration | ✅ OK |
| T4 | Sistema de domínio (`SignificanceCalculator`) | unit + determinismo | integration (cenário real necessário para medir colapso) | ✅ OK — excede o mínimo do layer, coerente com o sistema tocar `WorldState` |
| T5 | Sistema estrutural (`LivingMemoryWindow`) | integration | integration | ✅ OK |
| T6 | Domain types puros (`TransmissionMediumType`) | none | unit (dado validável, excede o mínimo) | ✅ OK |
| T7 | Domain types puros (`ReportState`) | none | unit (excede o mínimo) | ✅ OK |
| T8 | Sistema de domínio (`DistortionEngine`) | unit + determinismo | integration (excede — cenário com RNG real) | ✅ OK |
| T9 | Sistema de domínio (extensão) | unit + determinismo | integration | ✅ OK |
| T10 | Sistema estrutural (`FactToReportConversionScheduler`) | integration | integration | ✅ OK |
| T11 | Sistema estrutural (`CanonSlotManager`) | integration | integration | ✅ OK |
| T12 | Domain types puros (`Book`) + distorção | none / integration (via distorção) | integration | ✅ OK |
| T13 | Sistema estrutural (`BookRediscoverySystem`) | integration | integration | ✅ OK |
| T14 | Consulta (`HistoryTruthQuery`) | unit + arquitetura | unit | ✅ OK |
| T15 | Consulta (`HistoryBeliefQuery`) | unit + arquitetura | integration (excede — cenário real de comunidades) | ✅ OK |
| T16 | Teste de arquitetura + mutação | unit + mutação (par obrigatório) | unit | ✅ OK |
| T17 | Índice (`HistoryIndex`) | unit + determinismo + prova de complexidade | integration | ✅ OK |
| T18 | Consulta (`LineageQuery`) | integration | integration | ✅ OK |
| T19 | Domain types puros + sistema (`CompensatingCorrection`) | none / integration | integration | ✅ OK |
| T20 | Cenário (cânone limitado no tempo) | scenario (curta, gate) | unit (roda no gate padrão) | ✅ OK |
| T21 | Round-trip snapshot+replay | integration | integration | ✅ OK |

Nenhuma violação — todo task com camada "none" no layer (`Fact`, `TransmissionMediumType`,
`ReportState`, `Book`) inclui testes que excedem o mínimo do build-gate porque o tipo tem
invariante própria (imutabilidade, validação de parâmetro) testável sem custo — nenhum "Tests:
none" foi usado para adiar cobertura.

---

## Tips

Ver `## Tips` do template do skill — aplicado. Nota específica desta fase: **toda task que
adiciona um `WorldEventKind` novo ou um campo `[Canonical]` novo tem "golden hash regenerado"
como parte do Done when** — mesma disciplina do Bloco C da Fase 9. Toda task de índice/sistema
com RNG novo tem "determinismo em dois processos verde" como parte do Done when, nunca como
task separada.
