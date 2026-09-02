# Fase 28 — Cognição e LOD observacional — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implementar estas tasks com a skill `tlc-spec-driven`: ative por nome e siga o fluxo de
Execute e as Critical Rules dela. Não procure arquivos da skill por caminho de sistema.

**Se a skill não puder ser ativada, PARE e avise — não prossiga sem ela.**

---

**Design**: `.specs/features/phase-28-cognition/design.md`
**Status**: Draft

**Decisão de sequenciamento (não estava no spec/design, decidida aqui pra destravar P1 sem
depender de P2)**: as tasks P1 (Cognição) gravam rastro condicionado a `Npc` já estar
**materializado/detalhado** (sinal que já existe hoje, Fase 8) em vez de esperar o
`ObservationRegistry` completo (P2). P2 refina esse gate para os 3 escopos reais sem mudar o
contrato público de `NpcCognitionLog`. Isso cumpre a prioridade confirmada (Cognição P1 não
bloqueia em LOD P2) sem violar a AC "não grava fora de escopo observado" — "detalhado" já é
condição necessária de "observável" hoje.

---

## Test Coverage Matrix

> Gerado a partir de `AGENTS.md`, `rules/tests.md`, `scripts/test.sh` — confirmar antes de
> Execute.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain (`NpcCognitionLog`, `LazyPosition`, `StringInternPool`) | Unit | Todos os branches; 1:1 com ACs (COG-01..04, CMP-03) | `tests/LivingWorld.Tests/**/*Tests.cs` | `bash scripts/test.sh --filter "FullyQualifiedName~Cognition\|FullyQualifiedName~Interning"` |
| Simulation (`ObservationRegistry`, `CosmeticDetailSystem`, `BehaviorDecisionSystem` wiring) | Unit + Determinismo | 1:1 com ACs (LOD-01..05, COG-01..04); determinismo obrigatório por sistema novo (`rules/tests.md`) | `tests/LivingWorld.Tests/Behavior/**`, `tests/LivingWorld.Tests/Observation/**` | `bash scripts/test.sh --filter "FullyQualifiedName~Behavior\|FullyQualifiedName~Observation"` |
| Infrastructure (`BinarySnapshotWriter` diff, `ColdTierPersistence`, compressão de `EventLogRecord`) | Unit + round-trip | Round-trip byte-idêntico (CMP-01,02,04); todo path de erro de (de)serialização | `tests/LivingWorld.Tests/Snapshot/**`, `tests/LivingWorld.Tests/Infrastructure/**` | `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot\|FullyQualifiedName~ColdTier"` |
| API (`POST /observation/scope`, `GET /npcs/{id}` estendido) | Integração | Happy path + erro de borda (escopo inválido) por rota nova/alterada | `tests/LivingWorld.Tests/Api/**` | `bash scripts/test.sh --filter "FullyQualifiedName~Api"` |
| Sensor de escala (custo por escopo) | Cenário (property-based) | Propriedades agregadas, não valor exato (`rules/tests.md`) — fora do gate de rotina | `tests/LivingWorld.Tests/Scenario/**`, trait `Category=Scenario` | `bash scripts/test.sh --filter Category=Scenario` (manual/nightly) |
| Web (`viewStore.ts`, `NpcInspector.tsx`) | Unit/component (Vitest) | Toda seção nova do painel: estado vazio, dados presentes, erro de rede | `web/src/**/*.test.tsx` | `npm --prefix web test` (já acionado por `scripts/test.sh`) |
| Sandbox de decisão (P3) | Unit + isolamento | Hash do mundo principal idêntico antes/depois do uso (SBX-02) | `tests/LivingWorld.Tests/Sandbox/**` | `bash scripts/test.sh --filter "FullyQualifiedName~Sandbox"` |

## Parallelism Assessment

> Baseado no padrão já em uso no repo (cada teste cria seu próprio `WorldState`/
> `ScenarioRunner.Create`, sem fixture de banco compartilhado — ver AD-047).

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Unit (.NET) | Sim | `WorldState`/`ScenarioRunner.Create` novo por teste, sem estático mutável compartilhado | Padrão observado em `ScenarioRunner.Create` (AD-047) |
| Determinismo | Sim | Mesmo isolamento acima; roda dois processos/mundos independentes | `rules/simulation-determinism.md` |
| Infraestrutura (Snapshot/EventLog) | Não | `SqliteWorldRepository` usa banco de teste — risco de tabela compartilhada entre testes paralelos | Nenhuma fixture de schema-por-teste encontrada; default conservador |
| Cenário | Não | Caro, fora do gate de rotina, sempre sequencial (nightly) | `scripts/test.sh` já isola via `Category=Scenario` |
| Web (Vitest) | Sim | Vitest isola módulo por arquivo de teste por padrão; nenhum store global sem reset encontrado em `viewStore.ts` | Padrão Vitest default |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Depois de task só com unit tests .NET ou Vitest | `bash scripts/test.sh --filter "FullyQualifiedName~<área>"` |
| Full | Depois de task com Infrastructure/API/round-trip | `bash scripts/test.sh` (todo `Category!=Scenario`, inclui Vitest) |
| Build | Fim de fase ou task só de config/entidade | `bash scripts/verify.sh` |

---

## Execution Plan

### Phase 1: Fundação de domínio (Sequential)
```
T1 ──→ T3 ──→ T5
T2 ──┘
```

### Phase 2: Cognição observável — P1 (Parallel OK após Phase 1)
```
        ┌→ T6 ─┐
T1,T5 ──┼→ T7 ─┼──→ T10 ──→ T11 ──→ T12
        └──────┘
```

### Phase 3: LOD observacional — P2 (Sequential, depende de Phase 2 pra sensor)
```
T2 ──→ T4 ──→ T8 ──→ T13 ──→ T9 ──→ T14
```

### Phase 4: Compressão — P2 (Parallel OK, independente das fases 2-3)
```
T15 ──┬→ T16 ─┐
      └→ T17 ─┼──→ T18 ──→ T19 ──→ T20
```

### Phase 5: Sandbox — P3 (Sequential, depende só de T1)
```
T1 ──→ T21 ──→ T22
```

### Phase 6: Fechamento (Sequential)
```
T23
```

---

## Task Breakdown

### T1: Criar `NpcCognitionLog` (side-store, ring buffer + watchlist)

**What**: novo tipo em Domain com `Record`, `RecentEntries`, `MarkWatchlisted`, `Unmark` — retenção janela curta (default 50, FIFO) ou completa comprimida se watchlisted, não retroativa.
**Where**: `src/LivingWorld.Domain/Cognition/NpcCognitionLog.cs`
**Depends on**: None
**Reuses**: `DecisionTrace` (existente, `Behavior/DecisionTrace.cs`)
**Requirement**: COG-01, COG-04, COG-20, COG-21, COG-22, COG-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Record` grava FIFO respeitando janela; watchlist retém tudo desde a marca, não retroativo
- [ ] `Unmark` preserva histórico já acumulado
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Cognition"`
- [ ] Teste count: ≥8 (janela FIFO, watchlist não-retroativo, unmark preserva, custo proporcional a marcados)

**Tests**: unit · **Gate**: quick

---

### T2: Criar `ObservationRegistry` + `SpaceScope`

**What**: novo tipo não-canônico em Simulation com `SetScope`, `ClearScope`, `IsObserved` — união dos escopos de toda fonte ativa.
**Where**: `src/LivingWorld.Simulation/Observation/ObservationRegistry.cs`
**Depends on**: None
**Reuses**: vocabulário de `SpaceId` do cliente (`World`/`City`/`Building`)
**Requirement**: LOD-01, LOD-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `IsObserved` retorna true se qualquer fonte enquadra o lugar do NPC (união, LOD-04)
- [ ] Registro não entra no hash canônico (teste: alterar scope não muda `CanonicalHash`)
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Observation"`
- [ ] Teste count: ≥6

**Tests**: unit · **Gate**: quick

---

### T3: Criar `LazyPosition` (mesmo molde de `LazyNeed`)

**What**: struct com `LastKnown`, `TickOfLastEvent`, `PendingRoute`, método `ValueAt(tick, world)` — fórmula fechada, exata.
**Where**: `src/LivingWorld.Domain/Population/LazyPosition.cs`
**Depends on**: None
**Reuses**: `LazyNeed.cs` (mesmo padrão de campo)
**Requirement**: LOD-10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `ValueAt` reproduz posição exata dado tick e rota — sem tolerância, fórmula fechada
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~LazyPosition"`
- [ ] Teste count: ≥5

**Tests**: unit · **Gate**: quick

---

### T4: Ligar `ObservationRegistry` a `CityPopulationQuery`/`MaterializationSystem`

**What**: NPCs em prédio não enquadrado por nenhuma fonte caem para camada cosmética aproximada; eventos de vida (Fase 9 task 4) não são tocados.
**Where**: `src/LivingWorld.Simulation/Cities/MaterializationSystem.cs` (modificar), `CityPopulationQuery.cs` (modificar)
**Depends on**: T2
**Reuses**: `MaterializationSystem`/`CityPopulationQuery` (AD-068, on-demand sem cache)
**Requirement**: LOD-01, LOD-02, LOD-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC fora de qualquer escopo permanece na resolução agregada de `simulation-lod.md` (comportamento já existente, não alterado)
- [ ] NPC dentro de cidade observada, fora de prédio enquadrado, cai para camada cosmética aproximada
- [ ] `NpcWakeScheduler`/eventos de vida seguem rodando idênticos — teste explícito: taxa de morte/nascimento/casamento igual com e sem observação, 10/10 seeds
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Materialization"`
- [ ] Teste count: ≥10

**Tests**: unit + determinismo · **Gate**: quick

---

### T5: Criar `CosmeticDetailSystem`

**What**: `ResolvePosition` (lê `LazyPosition.ValueAt` se não observado, posição exata se observado), `OnPromoted` (dispara `WorldRngRegistry.StreamFor` para micro-ação pendente).
**Where**: `src/LivingWorld.Simulation/Behavior/CosmeticDetailSystem.cs`
**Depends on**: T2, T3
**Reuses**: `LazyNeed.ValueAt` como padrão; `WorldRngRegistry.StreamFor("cosmetic", npc.Id.Value)` (primeiro consumidor real, `WorldRngRegistry.cs:33-34`)
**Requirement**: LOD-10, LOD-11, LOD-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Promoção recalcula posição por fórmula fechada — exata, não aproximada
- [ ] Micro-ação dependente de RNG usa `StreamFor`, sequência idêntica entre braço sempre-observado e braço promovido tardiamente (mesma seed)
- [ ] Nunca mantém as duas camadas (aproximada e exata) ativas ao mesmo tempo pro mesmo NPC
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~CosmeticDetail"`
- [ ] Teste count: ≥8

**Tests**: unit + determinismo · **Gate**: quick

---

### T6: Gravar `DecisionTrace` em `NpcCognitionLog` a partir de `BehaviorDecisionSystem.Tick`

**What**: no ponto onde `UtilityDecision.Trace` é hoje descartado, gravar em `NpcCognitionLog.Record` condicionado a `Npc` estar materializado/detalhado (ver decisão de sequenciamento no topo).
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (modificar linha ~92-118)
**Depends on**: T1
**Reuses**: `DecisionTrace` já montado em `SelectByUtility`
**Requirement**: COG-01, COG-02, COG-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC materializado grava rastro a cada decisão relevante (necessidade dominante, traço, memória, opção descartada)
- [ ] NPC agregado/não-materializado não gera custo de gravação algum
- [ ] Mesma seed produz rastro byte-idêntico entre execuções
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystem"`
- [ ] Teste count: ≥10

**Tests**: unit + determinismo · **Gate**: quick

---

### T7: API de marcação watchlist

**What**: endpoint/método de aplicação que chama `NpcCognitionLog.MarkWatchlisted`/`Unmark`, rejeitando NPC morto/arquivado.
**Where**: `src/LivingWorld.Api/Program.cs` (novo `POST /npcs/{id}/watchlist`, `DELETE /npcs/{id}/watchlist`)
**Depends on**: T1
**Reuses**: padrão de validação de borda já usado em `POW-03` (Failure nomeando o campo)
**Requirement**: COG-20, COG-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Marcar NPC vivo materializado funciona; marcar morto/arquivado retorna `Failure` explícito
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Api"`
- [ ] Teste count: ≥6

**Tests**: integração · **Gate**: full

---

### T8: `POST /observation/scope`

**What**: endpoint que recebe `SpaceScope` de uma fonte e chama `ObservationRegistry.SetScope`/`ClearScope`.
**Where**: `src/LivingWorld.Api/Program.cs` (novo)
**Depends on**: T2
**Reuses**: mesmo formato de `SpaceId` do cliente — sem tradução
**Requirement**: LOD-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Escopo inválido (prédio inexistente) é rejeitado nomeando o campo, fonte mantém escopo anterior
- [ ] Timeout de fonte sem heartbeat remove a fonte do registro (config declarada)
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Api"`
- [ ] Teste count: ≥8

**Tests**: integração · **Gate**: full

---

### T9: Teste de equivalência determinística (braço observado vs. promovido)

**What**: teste dedicado rodando dois braços da mesma seed — um sempre observado, outro aproximado por N ticks e depois promovido — confirmando estado final byte-idêntico.
**Where**: `tests/LivingWorld.Tests/Observation/CosmeticEquivalenceTests.cs`
**Depends on**: T5, T8
**Reuses**: padrão de teste par-de-braços já usado em Fase 7 (AD-059)
**Requirement**: LOD-10, LOD-11, LOD-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Dois braços convergem byte-idêntico no tick de comparação, 10/10 seeds
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~CosmeticEquivalence"`
- [ ] Teste count: ≥3 (uma por cenário: promoção única, promoção/rebaixamento repetido, múltiplas fontes)

**Tests**: determinismo · **Gate**: quick

---

### T10: Estender `GET /npcs/{id}` com campo de rastro

**What**: `NpcInspectionQuery.Inspect` passa a incluir `RecentEntries` de `NpcCognitionLog` no DTO de resposta.
**Where**: `src/LivingWorld.Api/NpcInspectionQuery.cs` (modificar)
**Depends on**: T6
**Reuses**: endpoint existente `GET /npcs/{id}` (`Program.cs:124-127`)
**Requirement**: COG-10, COG-12, COG-13

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC com rastro retorna lista de decisões; NPC sem rastro retorna lista vazia explícita
- [ ] Duas consultas no mesmo tick retornam exatamente o mesmo resultado (idempotente)
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Api"`
- [ ] Teste count: ≥6

**Tests**: integração · **Gate**: full

---

### T11: `NpcInspector.tsx` — seção "ver o cérebro" (dados)

**What**: nova seção no painel existente exibindo o rastro em tabela/timeline, consumindo o campo de T10 sem recalcular nada no cliente.
**Where**: `web/src/components/inspector/NpcInspector.tsx` (modificar), novo `web/src/components/inspector/CognitionTrace.tsx`
**Depends on**: T10
**Reuses**: `NpcInspector.tsx` existente (identidade/biografia/relações)
**Requirement**: COG-10, COG-12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Estado vazio explícito quando não há rastro; tabela populada quando há
- [ ] Gate check: `npm --prefix web test`
- [ ] Teste count: ≥5 (componente Vitest)

**Tests**: unit/component (Vitest) · **Gate**: quick

---

### T12: `CognitionTrace.tsx` — visão visual (fluxo estímulo→decisão) [P]

**What**: renderização visual navegável do fluxo estímulo→ponderação→decisão, lendo o mesmo dado de T11 (nenhum novo fetch).
**Where**: `web/src/components/inspector/CognitionTrace.tsx` (estender)
**Depends on**: T11
**Reuses**: mesmo dado já buscado em T11
**Requirement**: COG-11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Navegação entre decisões anteriores dentro da janela retida funciona
- [ ] Gate check: `npm --prefix web test`
- [ ] Teste count: ≥4

**Tests**: unit/component (Vitest) · **Gate**: quick

---

### T13: `viewStore.ts` emite `POST /observation/scope` na troca de `SpaceId`

**What**: `ViewStore.enter(target)`/`enterViaPortal` passam a chamar o endpoint de T8 com o novo escopo.
**Where**: `web/src/state/viewStore.ts` (modificar)
**Depends on**: T8
**Reuses**: `SpaceId`/`ViewStore.enter` existentes
**Requirement**: LOD-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Mudança de escopo no cliente reflete no `ObservationRegistry` do servidor (teste de integração ponta a ponta)
- [ ] Gate check: `npm --prefix web test`
- [ ] Teste count: ≥4

**Tests**: unit/component (Vitest) · **Gate**: quick

---

### T14: Sensor de custo por escopo (estende sensor da Fase 9)

**What**: sensor de escala reporta µs/NPC-tick da camada cosmética separadamente para observado vs. não-observado; reprova acima da fração declarada no cenário.
**Where**: sensor existente da Fase 9 (localizar em `tests/LivingWorld.Tests/Scenario/` — extender, não duplicar)
**Depends on**: T4, T5, T6
**Reuses**: sensor de escala já existente (Fase 9 task 1)
**Requirement**: LOD-20, LOD-21, LOD-22

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Sensor reporta os dois custos separadamente, nunca uma média
- [ ] Reprova se a fração ultrapassar o teto declarado
- [ ] Confirma custo zero de gravação de rastro fora de escopo observado
- [ ] Gate check: `bash scripts/test.sh --filter Category=Scenario` (manual, não é gate de rotina)
- [ ] Teste count: ≥4

**Tests**: cenário · **Gate**: manual/nightly

---

### T15: Criar `StringInternPool` [P]

**What**: pool genérico `Intern(string): int` / `Resolve(int): string`.
**Where**: `src/LivingWorld.Domain/Interning/StringInternPool.cs`
**Depends on**: None
**Reuses**: nenhum precedente — componente novo isolado
**Requirement**: CMP-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Mesma string produz mesmo id; ids diferentes nunca colidem em strings diferentes
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Interning"`
- [ ] Teste count: ≥5

**Tests**: unit · **Gate**: quick

---

### T16: Aplicar interning em `WorldSnapshot.Serialize` [P]

**What**: profissão/traço/tag de evento passam a referenciar `StringInternPool` em vez de string literal repetida.
**Where**: `src/LivingWorld.Simulation/WorldSnapshot.cs` (modificar)
**Depends on**: T15
**Reuses**: `StringInternPool` de T15
**Requirement**: CMP-03, CMP-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Round-trip (serializar → desserializar) produz mundo byte-idêntico ao original
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot"`
- [ ] Teste count: ≥6

**Tests**: unit + round-trip · **Gate**: quick

---

### T17: Interning de `EventLogRecord.Kind` na fronteira de persistência [P]

**What**: `Kind` passa a referenciar `StringInternPool` no momento da escrita/leitura em `SqliteWorldRepository`, sem mudar `EventLogRecord` publicamente (mapeamento fica em Infrastructure).
**Where**: `src/LivingWorld.Infrastructure/SqliteWorldRepository.cs` (modificar)
**Depends on**: T15
**Reuses**: `StringInternPool`; `EventLogRecord.cs` existente
**Requirement**: CMP-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Leitura de evento antigo (pré-interning) continua funcionando (aditivo, COH-04-style)
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Infrastructure"`
- [ ] Teste count: ≥5

**Tests**: unit + integração · **Gate**: full

---

### T18: `BinarySnapshotWriter` — diff real campo-a-campo

**What**: substituir o filtro de inclusão atual (`BuildPartialJson`) por diff real contra a última versão conhecida de cada NPC sujo, mantendo o envelope binário (`Magic`, marker) existente.
**Where**: `src/LivingWorld.Simulation/Snapshot/BinarySnapshotWriter.cs` (modificar `WriteDelta`, `BuildPartialJson`, `ReadAndApply`)
**Depends on**: T16
**Reuses**: envelope binário existente; `StringInternPool`
**Requirement**: CMP-01, CMP-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Diff contém só campos alterados por NPC, não a entidade completa
- [ ] `ReadAndApply` reconstrói o mundo aplicando diffs sobre o baseline corretamente
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Snapshot"`
- [ ] Teste count: ≥10

**Tests**: unit + round-trip · **Gate**: quick

---

### T19: Round-trip byte-idêntico do diff real

**What**: teste dedicado comprimir→descomprimir→mundo idêntico ao original, e hash incremental igual ao hash recomputado do zero.
**Where**: `tests/LivingWorld.Tests/Snapshot/BinaryDiffRoundTripTests.cs`
**Depends on**: T18
**Reuses**: `IncrementalHasher.cs` existente
**Requirement**: CMP-04, CMP-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Round-trip produz hash byte-idêntico em 10 anos simulados
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~BinaryDiffRoundTrip"`
- [ ] Teste count: ≥3

**Tests**: unit + round-trip · **Gate**: quick

---

### T20: `ColdTierPersistence` — arquivo frio persistido e comprimido

**What**: componente novo (não extensão) que persiste `NpcSummary` de `ColdTierArchive` comprimido em disco (gzip/Brotli), substituindo o dict só-em-memória.
**Where**: `src/LivingWorld.Infrastructure/ColdTierPersistence.cs` (novo)
**Depends on**: T18
**Reuses**: `ColdTierArchive.NpcSummary` (shape existente); `StringInternPool`
**Requirement**: CMP-01, CMP-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] NPC morto há mais de N anos sai do estado quente e vira registro comprimido em disco
- [ ] Bytes/NPC/ano pós-compactação dentro do teto declarado na Fase 9 (ou mais apertado)
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~ColdTier"`
- [ ] Teste count: ≥8

**Tests**: unit + integração · **Gate**: full

---

### T21: Sandbox de decisão isolado

**What**: ambiente que roda o mesmo pipeline de `BehaviorDecisionSystem.SelectByUtility` com `DecisionContext` sintético, sem tocar `WorldState`/tick/RNG de mundo.
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionSandbox.cs` (novo)
**Depends on**: T1
**Reuses**: `SelectByUtility`, `DecisionContext`, `DecisionTrace` (existentes)
**Requirement**: SBX-01, SBX-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Estímulo sintético produz decisão pelo mesmo pipeline, sem efeito em `WorldState`
- [ ] Mesmo estímulo sintético repetido produz decisão idêntica
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~Sandbox"`
- [ ] Teste count: ≥6

**Tests**: unit + determinismo · **Gate**: quick

---

### T22: Teste de isolamento do sandbox

**What**: prova que o hash do mundo principal é idêntico antes/depois de qualquer uso do sandbox, em 5 combinações de estímulo sintético.
**Where**: `tests/LivingWorld.Tests/Sandbox/DecisionSandboxIsolationTests.cs`
**Depends on**: T21
**Reuses**: `IncrementalHasher`
**Requirement**: SBX-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Hash do mundo principal inalterado antes/depois em todas as 5 combinações
- [ ] Gate check: `bash scripts/test.sh --filter "FullyQualifiedName~SandboxIsolation"`
- [ ] Teste count: ≥5

**Tests**: unit + determinismo · **Gate**: quick

---

### T23: Fechamento — gate completo + registro no ROADMAP/STATE

**What**: rodar `bash scripts/verify.sh` verde, atualizar `ROADMAP.md` (status da Fase 28 de `spec` para o status real alcançado), registrar em `STATE.md`.
**Where**: `ROADMAP.md`, `STATE.md`
**Depends on**: T9, T12, T13, T14, T19, T20, T22
**Reuses**: política de commit de `AGENTS.md` (`feat(phase-28): ...`)
**Requirement**: todos

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `bash scripts/verify.sh` sai 0
- [ ] `ROADMAP.md`/`STATE.md` refletem o estado real (não fechar a fase se algum critério P1/P2 não fechou — registrar o que ficou pendente como P2 residual, nunca silenciosamente)

**Tests**: — · **Gate**: build

**Commit**: `feat(phase-28): cognição inspecionável, LOD observacional e compressão de estado frio`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1, T2, T3 [P entre si — sem dependência mútua]

Phase 2 (Sequential com paralelismo interno):
  T1 ──→ T6 ──┐
  T2,T3 ──→ T5 ──→ (T6 depende de T1 só) ──┼──→ T10 ──→ T11 ──→ T12
  T1 ──→ T7 ──┘

Phase 3 (Sequential):
  T2 ──→ T4 ──→ T8 ──→ T13 ──→ T9 ──→ T14

Phase 4 (Parallel OK):
  T15 ──┬→ T16 [P] ─┐
        └→ T17 [P] ─┼──→ T18 ──→ T19 ──→ T20

Phase 5 (Sequential):
  T1 ──→ T21 ──→ T22

Phase 6 (Sequential, fecha tudo):
  T9, T12, T13, T14, T19, T20, T22 ──→ T23
```

`[P]` = ordem livre, sem dependência entre si dentro da mesma fase. Fases 2, 3, 4 e 5 podem
rodar em paralelo entre si (não dependem umas das outras) — só a Fase 6 espera todas.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1–T3, T15 | 1 tipo/componente cada | ✅ Granular |
| T4, T5, T6 | 1 sistema modificado/criado cada, 2-3 métodos coesos | ✅ Granular |
| T7, T8, T10 | 1 endpoint/DTO cada | ✅ Granular |
| T9, T19, T22 | 1 arquivo de teste dedicado cada | ✅ Granular |
| T11, T12, T13 | 1 componente/store React cada | ✅ Granular |
| T14 | Extensão de 1 sensor existente | ✅ Granular |
| T16, T17 | 1 arquivo modificado cada | ✅ Granular |
| T18 | 1 componente, 3 métodos relacionados (`WriteDelta`/`BuildPartialJson`/`ReadAndApply`) | ⚠️ OK — coesos, mesmo arquivo, mesma responsabilidade |
| T20, T21 | 1 componente novo cada | ✅ Granular |
| T23 | Fechamento — sem código, só gate+docs | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | Nenhuma seta de entrada | ✅ Match |
| T2 | None | Nenhuma seta de entrada | ✅ Match |
| T3 | None | Nenhuma seta de entrada | ✅ Match |
| T4 | T2 | T2 → T4 | ✅ Match |
| T5 | T2, T3 | T2,T3 → T5 | ✅ Match |
| T6 | T1 | T1 → T6 | ✅ Match |
| T7 | T1 | T1 → T7 | ✅ Match |
| T8 | T2 | T2 → T8 | ✅ Match |
| T9 | T5, T8 | T5,T8 → T9 | ✅ Match |
| T10 | T6 | T6 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T8 | T8 → T13 | ✅ Match |
| T14 | T4, T5, T6 | T4,T5,T6 → T14 | ✅ Match |
| T15 | None | Nenhuma seta de entrada | ✅ Match |
| T16 | T15 | T15 → T16 | ✅ Match |
| T17 | T15 | T15 → T17 | ✅ Match |
| T18 | T16 | T16 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T18 | T18 → T20 | ✅ Match |
| T21 | T1 | T1 → T21 | ✅ Match |
| T22 | T21 | T21 → T22 | ✅ Match |
| T23 | T9,T12,T13,T14,T19,T20,T22 | Todas convergem em T23 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain | unit | unit | ✅ OK |
| T2 | Simulation | unit | unit | ✅ OK |
| T3 | Domain | unit | unit | ✅ OK |
| T4 | Simulation | unit + determinismo | unit + determinismo | ✅ OK |
| T5 | Simulation | unit + determinismo | unit + determinismo | ✅ OK |
| T6 | Simulation | unit + determinismo | unit + determinismo | ✅ OK |
| T7 | API | integração | integração | ✅ OK |
| T8 | API | integração | integração | ✅ OK |
| T9 | Simulation (teste) | determinismo | determinismo | ✅ OK |
| T10 | API | integração | integração | ✅ OK |
| T11 | Web | unit/component | unit/component | ✅ OK |
| T12 | Web | unit/component | unit/component | ✅ OK |
| T13 | Web | unit/component | unit/component | ✅ OK |
| T14 | Cenário | cenário | cenário | ✅ OK |
| T15 | Domain | unit | unit | ✅ OK |
| T16 | Simulation | unit + round-trip | unit + round-trip | ✅ OK |
| T17 | Infrastructure | unit + integração | unit + integração | ✅ OK |
| T18 | Infrastructure/Simulation | unit + round-trip | unit + round-trip | ✅ OK |
| T19 | Infrastructure (teste) | round-trip | round-trip | ✅ OK |
| T20 | Infrastructure | unit + integração | unit + integração | ✅ OK |
| T21 | Simulation | unit + determinismo | unit + determinismo | ✅ OK |
| T22 | Simulation (teste) | determinismo | determinismo | ✅ OK |
| T23 | — | — (build gate) | build | ✅ OK |

Nenhuma violação — todas as tasks que criam/alteram camada com teste exigido incluem o teste
na própria task (nenhum "testado depois").

---

## Tips
(ver `references/tasks.md` da skill — não duplicado aqui)
