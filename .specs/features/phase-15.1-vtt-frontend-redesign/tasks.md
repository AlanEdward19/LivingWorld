# Fase 15.1 (Redesign do frontend VTT) Tasks

## Execution Protocol (MANDATORY — do not skip)

Implemente estas tasks com a skill `tlc-spec-driven`: **ative-a pelo nome e siga o fluxo Execute e as
Critical Rules dela.** Não procure os arquivos da skill por caminho no filesystem. A skill é a fonte
de verdade do fluxo completo (ciclo por task, delegação a sub-agents, adequacy review, Verifier,
sensor de discriminação).

**Se a skill não puder ser ativada, PARE e avise o usuário — não prossiga sem ela.**

### Cadência de regressão (restrição explícita do usuário — normativa)

> "Não rode os testes regressivos do motor com frequência, pois demoram muito para rodar — foque em
> testes novos (das novas features) e rode o regressivo (full `scripts/verify.sh` / testes
> `Category=Scenario`) apenas ao final da fase."

Concretamente, e sem exceção:

- **Por task**: rode **apenas** (a) os testes novos/alterados daquela task e (b) o gate rápido da
  camada correspondente (vitest + `tsc --noEmit` para frontend; `dotnet test --filter
  FullyQualifiedName~<classe nova>` para backend). Nada mais.
- **`bash scripts/verify.sh` é proibido como gate por task.** Ele encadeia `check-docs → build →
  lint → test.sh` e `test.sh` sem filtro roda a solução inteira: **~1178 testes, ~25 minutos**
  (`.specs/STATE.md` Handoff, duas medições independentes). Reservado ao fechamento da fase.
- **`bash scripts/test.sh --filter Category!=Scenario` também é proibido como gate por task** —
  apesar do nome, esse é o filtro *default* do script e é exatamente a suíte de 25 minutos
  (`scripts/test.sh:15`). A Fase 15 o usava como "Quick"; nesta fase isso está corrigido.
- **Testes `Category=Scenario`** (cenários de 10/100 anos) só no fechamento, junto com o verify
  completo.
- **Fechamento da fase** (task final, T27): `bash scripts/verify.sh` **e**
  `bash scripts/test.sh --filter Category=Scenario`, ambos até o fim, com o resultado registrado em
  `validation.md`.
- **O re-sequenciamento em 3 estágios (abaixo) só reforça esta cadência**, não a altera: o Estágio 1
  inteiro roda contra mocks e portanto **não invoca `dotnet` nenhuma vez**; o Estágio 2 não invoca
  `npm`. A regra continua sendo "só os testes novos daquela task + o gate rápido da camada", e o
  regressivo completo continua sendo exclusividade da T27.

---

**Design**: `.specs/features/phase-15.1-vtt-frontend-redesign/design.md`
**Spec**: `.specs/features/phase-15.1-vtt-frontend-redesign/spec.md`
**Status**: Draft — OQ-1..OQ-4 **resolvidas pelo usuário em 2026-08-06** (ver spec.md "Decisões
resolvidas" e design.md, topo). T1, T17, T20 e T21 abaixo já refletem as respostas: OQ-1 = projeção
derivada na API, OQ-2 = `SpatialPortal` como conceito canônico de domínio (altera hash/goldens),
OQ-3 = construir o tick loop nesta fase, OQ-4 = remover a superfície de Player Mode do cliente.

### Ordem de entrega em 3 estágios (restrição do usuário, 2026-08-07 — normativa)

> "Quero que todo o ajuste de frontend venha primeiro (sem se conectar ao motor) para eu validar a
> aparência. Em seguida, viriam os ajustes no backend. E por fim a integração."

Isso **re-sequencia** as tasks; não muda o escopo de nenhuma:

1. **Estágio 1 — Frontend contra mock.** Todo o cliente (map engine, `MapView`, inspector, time
   controls, layer panel, follow, building space, World Creator, breadcrumb, render de footprint,
   remoção de Player Mode) roda contra **fixtures estáticas**, sem API e sem WebSocket. Fecha com um
   checkpoint de aprovação visual do usuário (T29).
2. **Estágio 2 — Backend.** Primeiro fecha os gaps de contrato inventariados em
   `backend-gaps.md`; depois entrega tick loop, controles, delta, poda, `SpatialPortal` e projeções.
   **Zero arquivos em `web/src`**, testado só com `dotnet test`.
3. **Estágio 3 — Integração.** Troca o adapter mock pelo real em cada seam, mais os testes
   end-to-end que só fazem sentido com os dois lados reais, e o fechamento da fase (T27).

O que torna o Estágio 3 barato é o seam de dado descrito em design.md → "Mock Adapter / Validação
offline do frontend": mock e real implementam a **mesma** interface, tipada contra os **mesmos**
contratos da seção Data Models. Trocar é argumento de construtor, não reescrita.

---

## Test Coverage Matrix

> Gerada a partir do codebase e das guidelines do projeto: `AGENTS.md`, `rules/tests.md`,
> `rules/eval-criteria.md`, `rules/simulation-determinism.md`, `scripts/test.sh`,
> `scripts/verify.sh`, `web/package.json:9`, `web/vite.config.ts:15-19`.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Map engine puro (`web/src/map-engine/*.ts` — Camera, LodPolicy, InterpolationBuffer, space, hitTest) | unit | Todos os ramos; 1:1 com os ACs de VTT2-01..05, 06, 11..15, 37..41; cada edge case listado na spec tem teste. Sem DOM (funções/classes puras) | `web/tests/map-engine/*.test.ts` | `npm --prefix web test` |
| Stores de estado (`web/src/state/*.ts`) | unit | Todos os ramos; guarda de escopo, aplicação de delta, preservação/limpeza de seleção, câmera por espaço (VTT2-08, 21, 32..36) | `web/tests/state/*.test.ts` | `npm --prefix web test` |
| Fontes de dado plugáveis (`web/src/data/*.ts` — interfaces + `Mock*Source` + `Real*Source`) | unit | A fixture mock é tipada contra os **mesmos** tipos do contrato real (`ScopeTickDelta`, snapshots, `Portals`, campos de footprint); teste de conformidade prova que mock e real satisfazem a mesma interface | `web/tests/data/*.test.ts` | `npm --prefix web test` |
| Componentes React (`web/src/components/**`) | unit + integration (Testing Library) | Interação declarada em AC: clique seleciona, double click navega, Esc fecha, toggles de camada, botões de tempo. Não testar pixels do canvas — testar o contrato de input→store | `web/tests/*.test.tsx` | `npm --prefix web test` |
| Tipagem TS | build gate | `tsc --noEmit` limpo | — | `npx --prefix web tsc --noEmit` |
| Endpoints de controle de simulação (`src/LivingWorld.Api/Simulation/*.cs`) | integration | Toda rota adicionada: happy path + inválido (400) + step fora de pausa (409); **mais** invariância de hash canônico (VTT2-30) | `tests/LivingWorld.Tests/Simulation/*EndpointTests.cs` | `dotnet test tests/LivingWorld.Tests --nologo --filter FullyQualifiedName~SimulationControl` |
| Tick loop / publish por tick (`src/LivingWorld.Api/Simulation/TickLoopService.cs`) | integration | Avança tick quando não pausado; não avança pausado; publica delta no escopo com assinante; não publica em escopo sem assinante | `tests/LivingWorld.Tests/Simulation/TickLoopServiceTests.cs` | `dotnet test tests/LivingWorld.Tests --nologo --filter FullyQualifiedName~TickLoop` |
| Retenção do log do gateway (`src/LivingWorld.Api/Realtime/RealtimeGateway.cs`) | unit | Log não cresce indefinidamente; replay de assinante ativo continua correto após poda | `tests/LivingWorld.Tests/Visual/RealtimeGatewayTests.cs` | `dotnet test tests/LivingWorld.Tests --nologo --filter FullyQualifiedName~RealtimeGateway` |
| Contratos de projeção (`src/LivingWorld.Api/Visual/*.cs`) | integration | Cada campo novo aparece no payload e não altera hash canônico (padrão de `VisualGateTests`) | `tests/LivingWorld.Tests/Visual/*Tests.cs` | `dotnet test tests/LivingWorld.Tests --nologo --filter FullyQualifiedName~Visual` |
| Tipos TS gerados de OpenAPI | architecture | Sem drift entre contrato da API e `web/src/generated/api-types.ts` | `scripts/generate-web-types.sh --check` | fechamento de fase (dentro de `verify.sh:10`) |
| Regressão completa do motor | integration + scenario | Sem regressão em nenhuma fase anterior | toda a solução | **só no fechamento**: `bash scripts/verify.sh` e `bash scripts/test.sh --filter Category=Scenario` |

**Nota de precedência:** `rules/tests.md` exige "todo comportamento novo/alterado tem teste antes de
concluir a tarefa" e "um assert lógico por teste" — ambos valem aqui. O que a cadência desta fase
muda é **qual suíte roda quando**, não se o comportamento novo é testado.

## Parallelism Assessment

> Gerada a partir do codebase — confirmar antes do Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit TS (map-engine, stores) | Yes | Funções/classes puras, instância por teste, sem estado global | `web/tests/api.test.ts` (padrão existente) |
| unit/integration React (vitest + jsdom) | Yes | `environment: "jsdom"` por arquivo, `setupFiles` isolado | `web/vite.config.ts:15-19`, `web/tests/setup.ts` |
| integration .NET (`WebApplicationFactory`) | Yes | Cada classe de teste tem sua própria factory, com `world`/`sessions` isolados no DI (razão documentada em `src/LivingWorld.Api/Program.cs:71-74`) | `tests/LivingWorld.Tests/Visual/RealtimeGatewayEndpointTests.cs` |
| integration de **tick loop** | **No** | O loop é `IHostedService` mutando o `WorldHost` singleton da factory em background — dois testes concorrentes na mesma factory disputam o mesmo mundo | Novo nesta fase; usar factory própria por teste com o loop desabilitado por default e acionado explicitamente |
| determinismo 2-processos | No | Comparação sequencial entre processos | `tests/LivingWorld.Tests/DeterminismTwoProcessTests.cs` |
| arquitetura/reflexão | Yes | Leitura de assembly compilado | `tests/LivingWorld.Tests/ArchitectureTests.cs` |
| regravação de goldens (`ZZZ_record_golden_hashes`, T21) | **No** | Escreve o baseline compartilhado `tests/golden/world-hashes.json` — rodar em paralelo com qualquer outro teste que leia esse arquivo é uma corrida; roda sozinho, em commit próprio | `tests/LivingWorld.Tests/GoldenHashesTests.cs:19-29` |

## Gate Check Commands

> Gerada a partir do codebase — confirmar antes do Execute. **A coluna "When to Use" é normativa:
> não escale de nível por conta própria.**

| Gate Level | When to Use | Command |
| --- | --- | --- |
| **Quick-web** | Gate padrão de **todo o Estágio 1** (T0, T5-T19, T22-T26, T28) e das tasks de Estágio 3 que só trocam a fonte de dado (T31-T33) | `npm --prefix web test && npx --prefix web tsc --noEmit` |
| **Quick-api** | Gate padrão de **todo o Estágio 2** (T42-T49, T1-T4, T20, T21, T30) — só a classe de teste nova/alterada | `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~<ClasseDeTesteDaTask>"` |
| **Quick-web + Quick-api** | Só na T34, que consome campos de projeção reais no cliente | os dois comandos acima |
| **Build** | Após tasks que mexem em `.csproj`/`package.json`/config de build, ou que só tocam tipos | `bash scripts/build.sh && bash scripts/lint.sh` |
| **Phase-close (full)** | **Só na T27**, uma vez, no fechamento da fase | `bash scripts/verify.sh` |
| **Phase-close (scenario)** | **Só na T27**, uma vez, no fechamento da fase | `bash scripts/test.sh --filter Category=Scenario` |

**Consequência direta do re-sequenciamento:** nenhuma task do Estágio 1 roda `dotnet` — nem um
filtro. O Estágio 1 inteiro é `npm --prefix web test && npx --prefix web tsc --noEmit`, na casa dos
segundos. Simetricamente, nenhuma task do Estágio 2 roda `npm` (nenhuma delas toca `web/src`).
T29 é checkpoint de aprovação humana e não tem gate automatizado além do Quick-web do estado atual.

**Proibido como gate por task:** `bash scripts/verify.sh`, `bash scripts/test.sh` (com ou sem
`--filter Category!=Scenario`) e qualquer `dotnet test` sem `--filter`. Todos rodam a suíte de ~25
minutos.

---

## Execution Plan

> **Leitura obrigatória:** o Task Breakdown abaixo lista as tasks **por ID** (ordem numérica), não por
> ordem de execução. A ordem de execução é **esta** seção.

| Estágio | Tasks |
| --- | --- |
| **1 — Frontend contra mock** | T0, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19, T22, T23, T24, T25, T26, T28, T29 |
| **2 — Backend** | T42, T43, T44, T45, T46, T47, T48, T49, T1, T2, T3, T4, T20, T21, T30 |
| **3 — Integração** | T31, T32, T33, T34, T27 |

### Estágio 1 — Frontend contra mock (nada de backend)

Nenhuma task deste estágio abre socket, chama endpoint ou roda `dotnet`. Toda fonte de dado é uma
implementação `Mock*Source` das interfaces criadas em T0.

#### E1.0: Seam de dado (Sequential — raiz do estágio)

```
T0
```

#### E1.1: Map Engine puro (Parallel OK)

```
        ┌→ T5 [P] ─┐
T0 ─────┼→ T6 [P] ─┼──→ T8
        ├→ T7 [P] ─┤
        └→ T9 [P] ─┘
```

#### E1.2: Stores de estado (Parallel OK)

```
        ┌→ T10 [P]   ← consome MockTickSource (antes: WS real)
T0, T9 ─┼→ T11 [P]   ← consome MockPortalSource (antes: campo Portals real, T21)
        └→ T12 [P]
```

#### E1.3: Observer Mode (vertical slice visual)

```
{T8, T10, T11, T12} → T13 → T14 → {T15 [P], T16 [P], T17 [P]}
```

#### E1.4: P2 — legibilidade e espacialidade (Parallel OK)

```
T14 → {T18 [P], T19 [P], T22 [P], T28 [P]}
```

#### E1.5: World Creator visual

```
T14 → T23 → T24 → {T25 [P], T26 [P]}
```

#### E1.6: Checkpoint de aprovação visual (gate humano — fecha o estágio)

```
{T15..T19, T22..T26, T28} → T29
```

### Estágio 2 — Backend (nenhum arquivo em `web/src`)

A dependência em T29 é **de ordem de entrega**, não técnica: nenhuma destas tasks precisa de uma
linha de frontend para ser escrita ou testada. Ela existe só porque o usuário quer aprovar a
aparência antes de o backend começar.

#### E2.0: Contratos ausentes para integração (primeiro bloco)

O inventário, escopo e critérios das tasks T42-T49 estão em `backend-gaps.md`.

```
T29 → T42 → T43 → T44 → T45 → T46 → T47 → T48 → T49
```

#### E2.1: Fundação engine-facing (Sequential) — OQ-3

```
T49 → T1 → T2 → T3 → T4
```

#### E2.2: Dado de domínio e campos de projeção (paralelo com E2.1)

```
T49 ─┬→ T21        ← SpatialPortal (NÃO [P]: regrava goldens, roda sozinha)
     ├→ T20 [P]    ← só os campos de footprint na API (metade de frontend virou T28/T34)
     └→ T30 [P]    ← indicadores de cidade na projeção (extraído da T15)
```

> T21 deixa de depender de T4: ela é dado de domínio, ortogonal ao tick loop. E deixa de ser
> dependência de T11 — no Estágio 1 o `ViewStore` resolve portal contra `MockPortalSource`, e a
> troca pelo campo real acontece na T33. A AC5 de VTT2-62..67 continua satisfeita: o cliente nunca
> resolve por coordenada hardcoded, resolve por *lista de portais consultada de uma fonte* — mock
> antes, projeção depois, mesmo tipo.

### Estágio 3 — Integração (troca mock → real)

```
{T4, T10}  → T31   ← SimulationStore passa a consumir WS/SSE real + ScopeTickDelta real
{T1, T16}  → T32   ← TimeControls passa a chamar /simulation/* real
{T21, T11} → T33   ← ViewStore passa a consultar o campo Portals real
{T20, T30, T15, T28} → T34   ← renderer/inspector consomem os campos de projeção reais

{T31, T32, T33, T34} → T27   ← fechamento da fase
```

---

## Task Breakdown

### T0: Seam de fonte de dado + fixtures mock — **Estágio 1 (raiz)** — ✅ Done (`a4ba7d0`)

**What**: definir as interfaces de fonte de dado que todo store/serviço do cliente vai receber por
construtor, e uma implementação `Mock*` de cada uma alimentada por fixtures estáticas — para que o
frontend inteiro seja demonstrável sem API, sem WebSocket e sem motor. **As fixtures são tipadas
contra os mesmos contratos que o backend vai produzir** (design.md → Data Models): `ScopeTickDelta`,
`NpcPositionDelta`, os shapes de snapshot (`GlobalSnapshot`/`CitySnapshot`/`InteriorSnapshot`), o
campo `Portals` e os campos de footprint (`Bounds`/`BoundsAreDerived`, `Location`/`LocationIsDerived`).
Consequência: a troca mock→real do Estágio 3 é argumento de construtor, não reescrita.
**Where**: `web/src/data/sources.ts` (interfaces), `web/src/data/mock/*.ts` (implementações mock +
fixtures), `web/src/map-engine/types.ts` (tipos compartilhados de `AuthoritativeEntity`/`EntityRef`/
`SpaceId`/`CameraState`).
**Depends on**: None.
**Reuses**: os tipos já gerados de OpenAPI em `web/src/generated/api-types.ts` como fonte dos shapes
de snapshot (não redeclarar); `web/src/types.ts:128-145` (`FocusScope`/`focusScopeKey`); o formato de
`VisualSnapshotEnvelope`/`VisualDeltaEnvelope` (`src/LivingWorld.Api/Visual/VisualSnapshotEnvelope.cs:17-21`).
**Requirement**: habilitador de entrega (VTT2-11, VTT2-27..29, VTT2-42..45, VTT2-66 são verificados
contra estas fontes no Estágio 1 e contra as reais no Estágio 3)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Existem 4 interfaces: `SnapshotSource`, `TickStreamSource`, `TimeControlSource`, `PortalSource` — nenhuma com mais de uma responsabilidade
- [x] Cada uma tem exatamente uma implementação mock; **nenhuma** implementação real nesta task (as reais chegam no Estágio 3)
- [x] Nenhum tipo de payload é redeclarado à mão: as fixtures usam os tipos do contrato (teste de tipo — `tsc --noEmit` falha se a fixture divergir do shape do delta/snapshot)
- [x] `MockTickStreamSource` emite `ScopeTickDelta` sintéticos num intervalo configurável e responde a `pause`/`setSpeed` localmente, de modo que o mapa se mexa sem motor
- [x] Fixtures cobrem os 3 escopos (world, city, interior), ≥ 2 cidades, ≥ 20 NPCs, ≥ 2 portais para o mesmo par de espaços (necessário para o teste de AC3/AC5 em T11) e camadas `NotYetModeled`
- [x] Nenhum `fetch`/`WebSocket` é construído em `web/src/data/mock/**` (teste com espião: 0 chamadas)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 55 passed (22 novos), tsc limpo
- [x] Contagem de testes: ≥ 5 novos passando — 22 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): pluggable data-source seam with typed mock fixtures`

---

### T1: Expor controle de simulação por HTTP — **Estágio 2 · Engine-facing (read-model/API only)** — ✅ Done (`cbca11a`)

**What**: mapear `POST /simulation/pause|resume|speed|step` e `GET /simulation/status` como tradução
fina sobre `SimulationHost`, mais a entrada `/simulation` no proxy do Vite.
**Where**: `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs` (novo),
`src/LivingWorld.Api/Program.cs` (uma linha de `Map*`), `web/vite.config.ts`.
**Depends on**: T49 (E2.0 completo; tecnicamente esta task não depende de frontend).
**Reuses**: `SimulationHost.Pause/Resume/SetSpeed/FastForward` (`src/LivingWorld.Simulation/SimulationHost.cs:10-22`) — validação de velocidade `<= 0` já existe em `:15-17`, não duplicar. Padrão de endpoint de `src/LivingWorld.Api/WorldStartEndpoints.cs:16-29`.
**Requirement**: VTT2-27, VTT2-28, VTT2-29, VTT2-30

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] As 5 rotas respondem; `speed` com valor `<= 0` devolve 400 sem alterar `TicksPerSecond`
- [x] `step` com o loop rodando devolve 409 (só faz sentido pausado)
- [x] Teste prova que N chamadas de pause/resume/speed **não alteram o hash canônico** (mesmo padrão de `tests/LivingWorld.Tests/Visual/VisualGateTests.cs`)
- [x] `/simulation` está no `server.proxy` de `web/vite.config.ts` **neste mesmo commit** (bug recorrente — `.specs/STATE.md` Handoff registra o mesmo esquecimento em `/worlds` e `/periods`)
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~SimulationControl"` — 7 passed
- [x] Contagem de testes: ≥ 6 novos passando — 7 novos

**Tests**: integration · **Gate**: Quick-api
**Commit**: `feat(api): expose simulation host controls over http`

---

### T2: Definir e produzir o delta tipado de tick por escopo — **Estágio 2 · Engine-facing (read-model/API only)** — ✅ Done (`9b56137`)

**What**: criar `ScopeTickDelta`/`NpcPositionDelta` e a função que diffa o estado projetado de um
escopo entre dois ticks, para publicar só o que mudou.
**Where**: `src/LivingWorld.Api/Visual/ScopeTickDelta.cs` (novo),
`src/LivingWorld.Api/Visual/ScopeDeltaBuilder.cs` (novo).
**Depends on**: T1.
**Reuses**: `GlobalProjector.Build`/`CityProjector.Build` (`src/LivingWorld.Api/Visual/GlobalProjector.cs:30-49`, `CityProjector.cs:24-43`) como fonte do estado por escopo; `VisualDeltaEnvelope<T>` já é o invólucro (`src/LivingWorld.Api/Visual/VisualSnapshotEnvelope.cs:17-21`).
**Requirement**: VTT2-11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Diff entre dois estados devolve só NPCs que mudaram de célula e ids removidos
- [x] Estado idêntico produz delta vazio (não publicar frame vazio é decisão do T3)
- [x] Nenhuma camada é recomputada no caminho de delta (só o snapshot inicial monta camadas) — `Diff` não recebe `WorldState`, testado por reflexão
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~ScopeDelta"` — 6 passed
- [x] Contagem de testes: ≥ 5 novos passando — 6 novos

**Tests**: unit · **Gate**: Quick-api
**Commit**: `feat(api): typed per-tick scope delta`

---

### T3: Loop de tick em tempo real — **Estágio 2 · Engine-facing (read-model/API only)** — ✅ Done (`bed254d`)

**What**: `IHostedService` que avança `WorldClock.Tick` no ritmo de `SimulationHost.TicksPerSecond`
enquanto não pausado, e publica o `ScopeTickDelta` de cada escopo com assinante.
**Where**: `src/LivingWorld.Api/Simulation/TickLoopService.cs` (novo),
`src/LivingWorld.Api/Program.cs` (registro).
**Depends on**: T2.
**Reuses**: `WorldClock.Tick` (`src/LivingWorld.Simulation/WorldClock.cs:21-46`) — **chamado, nunca modificado**; `WorldHost.Current/Clock` (`src/LivingWorld.Simulation/WorldHost.cs:10-11`); `RealtimeGateway.Publish` (`src/LivingWorld.Api/Realtime/RealtimeGateway.cs:57-71`). Remove a pendência documentada em `src/LivingWorld.Api/Program.cs:52-54`.
**Requirement**: VTT2-26

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Com o loop ativo e não pausado, `world.CurrentDate.TotalHours` avança
- [x] Pausado, não avança nenhum tick
- [x] Publica delta só nos escopos com assinante ativo
- [x] Desabilitado por default em ambiente de teste (senão toda `WebApplicationFactory` existente passa a ter um mundo mudando embaixo dela — risco de flake em suítes de outras fases) — gated por `TICK_LOOP_ENABLED`, ausente em todo processo de teste
- [x] Comentário no código declara a fronteira: o loop decide *quando* chamar `Tick`, nunca *o que* o tick faz (`rules/simulation-determinism.md`)
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~TickLoop"` — 4 passed
- [x] Contagem de testes: ≥ 4 novos passando — 4 novos

**Nota**: `RealtimeGateway` ganhou `SubscribedScopeKeys` (gap real — sem ele o loop não tem como
saber quais escopos publicar, e o `Where` desta task não listava esse arquivo).

**Tests**: integration · **Gate**: Quick-api
**Commit**: `feat(api): real-time tick loop publishing scope deltas`

---

### T4: Podar o log de replay do gateway — **Estágio 2 · Engine-facing (read-model/API only)** — ✅ Done (`b2f207c`)

**What**: janela de retenção por escopo em `RealtimeGateway._log`, descartando entradas abaixo do
menor cursor de assinante ativo.
**Where**: `src/LivingWorld.Api/Realtime/RealtimeGateway.cs`.
**Depends on**: T3.
**Reuses**: o próprio `_log`/`_subscribers` e o `lock (_gate)` existentes (`RealtimeGateway.cs:13-15,59-70`).
**Requirement**: VTT2-26 (viabilidade operacional)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Após N publishes com um assinante, o log do escopo não cresce indefinidamente
- [x] `Replay` de um assinante ativo continua devolvendo tudo que ele ainda não viu (`RealtimeGateway.cs:39-53` intacto no comportamento)
- [x] Escopo sem assinante nenhum não acumula histórico
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~RealtimeGateway"` — 10 passed
- [x] Contagem de testes: ≥ 3 novos passando, testes existentes de `RealtimeGatewayEndpointTests` intactos — 3 novos + 7 existentes intactos

**Tests**: unit · **Gate**: Quick-api
**Commit**: `fix(api): bound realtime replay log growth`

---

### T5: `Camera` [P]

**What**: classe pura de câmera — `worldToScreen`, `screenToWorld`, `zoomAt`, `panBy`, `clampTo`,
`visibleWorldRect`, `snapshot`/`restore`.
**Where**: `web/src/map-engine/Camera.ts` (novo).
**Depends on**: T0 (tipos compartilhados de `map-engine/types.ts`).
**Reuses**: `computeFitZoom` (`web/src/gridFit.ts:14-23`) só como zoom inicial de espaço novo.
**Requirement**: VTT2-01, VTT2-02, VTT2-03, VTT2-04, VTT2-08

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `zoomAt(p, f)` mantém `screenToWorld(p)` invariante antes/depois (teste com ≥ 3 pontos e ≥ 2 fatores)
- [ ] `panBy` desloca na direção esperada e `clampTo` impede o espaço sair da tela
- [ ] `visibleWorldRect` devolve exatamente o retângulo de mundo coberto pelo viewport
- [ ] `snapshot`/`restore` fazem round-trip exato
- [ ] Sem nenhuma referência a DOM/canvas no arquivo
- [ ] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit`
- [ ] Contagem de testes: ≥ 8 novos passando

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): map-engine camera with cursor-anchored zoom`

---

### T6: `LodPolicy` [P] — ✅ Done (`a454ab9`)

**What**: política de LOD em 4 níveis (`aggregate` | `dot` | `token` | `token-detail`) com limiares
configuráveis, mais a função de agregação por bucket espacial.
**Where**: `web/src/map-engine/lod.ts` (novo).
**Depends on**: T0 (tipos compartilhados de `map-engine/types.ts`).
**Reuses**: generaliza `isToken = zoom >= lodTokenThreshold` (`web/src/components/GridCanvas.tsx:36,59`).
**Requirement**: VTT2-37, VTT2-38, VTT2-39, VTT2-40, VTT2-41

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `levelFor` cobre os 4 níveis e as 3 fronteiras exatas (`<`, `==`, `>` de cada limiar)
- [x] `aggregate` agrupa por bucket determinístico e preserva a contagem total
- [x] A identidade da entidade sobrevive à troca de nível (mesmo `EntityRef` em todos os níveis)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 77 passed (11 novos), tsc limpo
- [x] Contagem de testes: ≥ 7 novos passando — 11 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): four-level lod policy`

---

### T7: `InterpolationBuffer` [P] — ✅ Done (`30af4e7`)

**What**: buffer de interpolação por entidade que **substitui** o alvo em vez de enfileirar, com
duração derivada do intervalo observado entre atualizações.
**Where**: `web/src/map-engine/interpolation.ts` (novo).
**Depends on**: T0 (tipos compartilhados de `map-engine/types.ts`).
**Reuses**: nada — não existe interpolação hoje.
**Requirement**: VTT2-12, VTT2-13, VTT2-14, VTT2-15

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `visualPositionOf` transita suavemente de `from` a `to` no intervalo e para exatamente em `to`
- [x] `authoritativePositionOf` devolve sempre o último estado do motor, nunca o interpolado
- [x] Um segundo `observe` no meio de uma animação **substitui** o alvo partindo da posição visual corrente — nenhuma fila é criada (teste com 5 `observe` em rajada: a posição final é a do 5º, não a soma dos trechos)
- [x] Sem novos `observe`, a posição visual permanece fixa (nenhuma extrapolação)
- [x] Duração deriva do intervalo medido, não de constante fixa
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 85 passed (8 novos), tsc limpo
- [x] Contagem de testes: ≥ 7 novos passando — 8 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): visual-only interpolation buffer without backlog`

---

### T8: `Renderer` por viewport — ✅ Done (`39ecb5c`)

**What**: função pura de desenho de um `RenderFrame` num contexto 2D, desenhando **apenas** o que
intersecta `camera.visibleWorldRect()`.
**Where**: `web/src/map-engine/renderer.ts` (novo); `web/src/components/GridCanvas.tsx` (extração e
depois remoção).
**Depends on**: T5, T6.
**Reuses**: corpo de desenho de `web/src/components/GridCanvas.tsx:70-130` (fill de célula, grid lines, dot/token com anel), `colorById` (`web/src/colorById.ts:4-7`).
**Requirement**: VTT2-03, VTT2-35

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Com câmera cobrindo 10×10 de um mundo 1000×1000, o número de `fillRect` de célula é ~100, não 1.000.000 (teste com contexto 2D espião)
- [x] Entidade com `sizeIsDerived: true` é desenhada com traço distinto do autorado
- [x] O canvas tem o tamanho do container, nunca do mundo (`MAX_CANVAS_PX` de `web/src/gridFit.ts:5` deixa de ser usado)
- [x] `draw` retorna cedo sem contexto 2D (jsdom)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 103 passed (6 novos), tsc limpo
- [x] Contagem de testes: ≥ 5 novos passando — 6 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): viewport-culled canvas renderer`

---

### T9: `SpatialContext` + hit-test em coordenadas de câmera [P] — ✅ Done (`7f35d33`)

**What**: `SpaceId`, pilha de ancestrais, transformações `localToParent`/`parentToLocal`, constante
única de escala entre espaços, e hit-test de tela→entidade via câmera.
**Where**: `web/src/map-engine/space.ts` (novo), `web/src/map-engine/hitTest.ts` (novo).
**Depends on**: T0 (tipos compartilhados de `map-engine/types.ts`).
**Reuses**: `focusScopeKey` (`web/src/types.ts:136-145`) como serializador de escopo — mesma regra de `VisualScope.ScopeKey` (`src/LivingWorld.Api/Visual/VisualScope.cs:13-19`); hit-test por raio de `web/src/components/GridCanvas.tsx:141-150`, reescrito para o espaço da câmera.
**Requirement**: VTT2-06, VTT2-07

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `localToParent`/`parentToLocal` fazem round-trip exato nos três níveis
- [x] `ancestors` devolve a cadeia correta para World/City/Building
- [x] `toScopeKey` produz exatamente as mesmas chaves que `VisualScope.ScopeKey` (teste de paridade com os 3 formatos: `world`, `city:{id}`, `interior:{id}`)
- [x] Hit-test acerta a entidade sob o cursor em ≥ 2 níveis de zoom diferentes e devolve `null` em espaço vazio
- [x] A escala entre espaços é uma constante única exportada, não literal espalhado
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 97 passed (12 novos), tsc limpo
- [x] Contagem de testes: ≥ 8 novos passando — 12 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): hierarchical spatial contexts and camera hit-test`

---

### T10: `SimulationStore` com aplicação incremental de delta [P] — ✅ Done (`4b7b484`)

**What**: store único do estado autoritativo do escopo observado; aplica snapshot e `ScopeTickDelta`,
descarta envelope de escopo errado, expõe `subscribe` fora do ciclo de render do React. **Recebe
`SnapshotSource` e `TickStreamSource` por construtor** (T0); neste estágio recebe as implementações
mock. Nenhuma referência a `WebSocket`/`fetch` dentro do store — o transporte vive na fonte, e é isso
que torna a T31 uma troca de argumento.
**Where**: `web/src/state/simulationStore.ts` (novo); `web/src/hooks/useRealtimeSnapshot.ts`
(substituído).
**Depends on**: T0, T9.
**Reuses**: as interfaces e mocks de T0; a guarda de escopo de `web/src/App.tsx:52`. Substitui o refetch-por-delta de `web/src/hooks/useRealtimeSnapshot.ts:45-47`.
**Requirement**: VTT2-11, VTT2-36

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Delta aplicado incrementalmente — **nenhum** re-snapshot no caminho normal (teste com `MockSnapshotSource` espião: 1 chamada de `load()` no subscribe inicial, 0 nos 10 deltas seguintes)
- [x] Envelope de escopo diferente do observado é descartado sem alterar o estado
- [x] Perda de stream (`MockTickStreamSource.simulateDrop()`) reidrata por `SnapshotSource.load()` com backoff — `simulateDrop`/`onDrop` adicionados a `TickStreamSource`/`MockTickStreamSource` nesta task (gap real descoberto, não coberto por T0)
- [x] O store não constrói `WebSocket` nem chama `fetch` (teste com espiões globais: 0 de cada) — todo transporte é da fonte injetada
- [x] `subscribe` notifica listeners sem passar por `useState`
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 110 passed (7 novos), tsc limpo
- [x] Contagem de testes: ≥ 7 novos passando — 7 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): authoritative simulation store with incremental deltas`

---

### T11: `ViewStore` — espaço observado, câmera por espaço, camadas ativas [P] — ✅ Done (`0bee2cd`)

**What**: store de VIEW: `enter`/`goToAncestor`, `CameraState` memorizado por `SpaceId`, conjunto de
camadas ativas, estado de follow.
**Where**: `web/src/state/viewStore.ts` (novo).
**Depends on**: T0, T9. *(Antes: T9, T21. No Estágio 1 o portal vem de `MockPortalSource`; a troca pelo campo `Portals` real da projeção é a T33 — mesmo tipo, mesma chamada.)*
**Reuses**: substitui `focus` (`web/src/App.tsx:22`) e os `zoom` locais de `WorldMapView.tsx:20` / `CityView.tsx:26` / `MapOverlay.tsx:49`; consulta a `PortalSource` injetada (T0) para resolver a transição — ver AC5 de VTT2-62..67.
**Requirement**: VTT2-08, VTT2-33, VTT2-46, VTT2-66 (AC5 — resolve por portal, nunca por coordenada embutida)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Entrar num espaço, mover a câmera, sair e reentrar restaura a câmera exata (VTT2-08)
- [x] Espaço nunca visitado recebe o zoom de fit inicial
- [x] `enter(target)` resolve o portal a partir da lista devolvida pela `PortalSource` injetada, nunca por coordenada hardcoded no cliente (AC5 — teste com a fixture de dois portais para o mesmo par de espaços de T0: ambos navegam sem nenhum `if` específico por entrada) — implementado como `enterViaPortal(portalId)`; `enter(target)` continua sendo a navegação direta (clique/Open/breadcrumb)
- [x] O store não conhece a origem do portal: trocar `MockPortalSource` por outra implementação da mesma interface não muda uma linha do `ViewStore` (teste com uma segunda implementação fake em memória)
- [x] Nenhum método do store dispara requisição HTTP (teste com fetch espião: 0 chamadas)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 118 passed (8 novos), tsc limpo
- [x] Contagem de testes: ≥ 5 novos passando — 8 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): view store with per-space camera memory`

---

### T12: `SelectionStore` [P] — ✅ Done (`2deef2c`)

**What**: store único de seleção (`EntityRef | null`), com `pin` e a regra de preservar/limpar ao
trocar de espaço.
**Where**: `web/src/state/selectionStore.ts` (novo).
**Depends on**: T0, T9.
**Reuses**: substitui os dois `Selection` locais (`web/src/components/WorldMapView.tsx:14,23`, `CityView.tsx:18,29`).
**Requirement**: VTT2-21, VTT2-34

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Selecionar não altera câmera nem espaço (teste contra o `ViewStore`)
- [x] Trocar de espaço preserva a seleção se a entidade existir lá, limpa caso contrário
- [x] Entidade que some do snapshot limpa a seleção
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 125 passed (7 novos), tsc limpo
- [x] Contagem de testes: ≥ 5 novos passando — 7 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): global selection store`

---

### T13: `MapView` — casca React do engine — ✅ Done (`c78bec4`)

**What**: componente que monta o canvas, liga wheel/drag/click/dblclick/Esc ao engine e roda o loop
de animação (`requestAnimationFrame`) lendo os três stores.
**Where**: `web/src/components/MapView.tsx` (novo); `web/src/components/GridCanvas.tsx` (removido).
**Depends on**: T8, T10, T11, T12.
**Reuses**: todo o `map-engine`; padrão de `requestAnimationFrame` sem lib já usado em `web/src/components/StartMenu.tsx`.
**Requirement**: VTT2-01, VTT2-02, VTT2-05, VTT2-16, VTT2-17, VTT2-18, VTT2-32, VTT2-35

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Wheel altera zoom; drag em espaço vazio faz pan; nenhum dos dois dispara requisição (fetch espião: 0)
- [x] Clique simples chama `SelectionStore.select` e **não** chama `ViewStore.enter`
- [x] Double click numa cidade chama `ViewStore.enter`
- [x] Esc limpa a seleção
- [x] Nenhum nó DOM é criado por entidade (o canvas é o único filho de render de entidades)
- [x] Um tick recebido não re-renderiza o componente React (teste com `React.Profiler`)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 133 passed (8 novos), tsc limpo
- [x] Contagem de testes: ≥ 8 novos passando — 8 novos; `GridCanvas.test.tsx` **não** reescrito/removido — justificativa: `GridCanvas.tsx` continua com consumidores reais (`MapGridEditor.tsx` até T25), então não foi removido nesta task (SPEC_DEVIATION documentado em `MapView.tsx`)

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `feat(web): MapView shell wiring input to the map engine`

---

### T14: Substituir `WorldMapView`/`CityView`/`InteriorView` por configurações de `MapView` + breadcrumb — ✅ Done (`2f172a1`)

**What**: as três views deixam de ser componentes de mapa próprios e viram configuração (fonte de
entidades + camadas + tools) de um único `MapView`; adiciona o breadcrumb de espaços e a transição
visual entre espaços.
**Where**: `web/src/components/WorldMapView.tsx`, `CityView.tsx`, `InteriorView.tsx`,
`web/src/components/Breadcrumb.tsx` (novo), `web/src/App.tsx`.
**Depends on**: T13.
**Reuses**: `terrainColorLookup`/`riverOverlayPoints`/`worldMarkers` (`web/src/worldMapData.ts:7-37`) como *entity sources*; `focusScopeKey` via `space.ts`.
**Requirement**: VTT2-07, VTT2-09, VTT2-10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Existe **um** componente que desenha mapa; nenhuma view instancia canvas próprio
- [x] Breadcrumb mostra a cadeia de ancestrais e clicar num ancestral navega até ele
- [x] Nenhuma entidade de outro espaço é renderizada (teste com snapshot contendo duas cidades) — garantido por `SimulationStore.entitiesOf`'s guarda de escopo (T10)
- [x] Transição entre espaços usa fade/zoom, não troca abrupta — `SpaceTransition` (CSS `space-fade-zoom`)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 142 passed (18 novos/reescritos), tsc limpo; verificado também no browser real (drill-down + volta por breadcrumb, 0 erros de console)
- [x] Contagem de testes: ≥ 6 novos/reescritos passando — 18 (`WorldMapView`×5, `CityView`×4, `App`×3, `Breadcrumb`×3 novo, `SpaceTransition`×3 novo)

**Nota**: `App.tsx` parou de montar `PlayerMoveControls`/`MapOverlay` (ambos faziam `fetch` real,
incompatível com o mock-only do Estágio 1) — isso antecipa a remoção que T17 devia fazer; T17 fica
reduzido a apagar os arquivos agora órfãos. `SimulationStore` ganhou `currentPayload()`, `ViewStore`
ganhou `subscribe`/`notify`, e `MapView` ganhou `staticEntities`/`initialCamera` — gaps reais
encontrados ao ligar tudo, documentados nos próprios arquivos.

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `refactor(web): single map surface with spatial breadcrumb`

---

### T15: `EntityInspector` + `CityInspector` + `NpcInspector` [P] — ✅ Done (`42470ed`)

**What**: inspector flutuante universal à direita com conteúdo por tipo de entidade, alimentado pelos
dados que o motor já fornece, com ações condicionadas a capacidade real.
**Where**: `web/src/components/inspector/EntityInspector.tsx`, `CityInspector.tsx`,
`NpcInspector.tsx`, `BuildingInspector.tsx` (novos); `web/src/components/SidePanel.tsx` (absorvido).
**Depends on**: T0, T14.
**Reuses**: casca de `web/src/components/SidePanel.tsx:10-25`; as fixtures de T0, que carregam os 6 indicadores de cidade no shape exato que a projeção vai expor na T30 (`CityPopulationQuery`, `src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-53`) e os campos de NPC no shape de `NpcInspectionQuery` (`src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs:38-44`); padrão `NotYetModeled` de `src/LivingWorld.Api/Visual/Layers/LayerBuildResult.cs`.
**Requirement**: VTT2-19, VTT2-20, VTT2-22, VTT2-23, VTT2-24, VTT2-25

**Tools**: MCP: NONE · Skill: NONE

> **Split:** a metade "expor os 6 indicadores na projeção da API" desta task virou **T30** (Estágio 2)
> e o consumo do campo real virou parte da **T34** (Estágio 3). Aqui os indicadores vêm da fixture,
> no mesmo shape — nenhum AC de UI muda.

**Done when**:
- [x] Inspector de cidade exibe os 6 indicadores no shape de `CityPopulationQuery`, lidos da fixture do escopo
- [x] Inspector de NPC exibe os campos do snapshot do escopo **sem** disparar a fonte de detalhe; o detalhe completo só sob ação explícita "Ver detalhes" (razão: no real, `NpcInspectionQuery.Inspect` materializa o NPC — `NpcInspectionQuery.cs:17` — logo é mutação, não leitura pura; ver context.md lacuna 10. A regra é implementada e testada aqui contra o mock, e continua valendo quando a fonte vira real)
- [x] Campo sem dado no motor é omitido ou marcado explicitamente; nada é inventado
- [x] Ação sem capacidade correspondente não é renderizada
- [x] Selecionar outra entidade troca o conteúdo sem fechar o painel
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 156 passed (28 novos entre T15+split), tsc limpo; verificado no browser real (click abre inspector, X fecha, 0 erros de console)
- [x] Contagem de testes: ≥ 7 novos passando — 14 novos (CityInspector×3, NpcInspector×5, BuildingInspector×3, EntityInspector×3)

**Nota**: `CityIndicators` (population/wealth/health/inequality/economy/housing) adicionado a
`data/contracts.ts` e à fixture de cidade — T30 (Estágio 2) precisa expor exatamente esse shape em
`CitySnapshot`. Gap real descoberto: uma cidade selecionada a partir do WorldSpace só tem
`population` (`GlobalCityMarker`) até o `CitySnapshot` dela carregar — os outros 5 indicadores
aparecem honestamente marcados como indisponíveis, não inventados (ver `CityInspector.tsx`).

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `feat(web): universal contextual entity inspector`

---

### T16: `TimeControls` [P] — ✅ Done (`de62418`)

**What**: HUD permanente de Pause / 1x / 2x / 4x / 8x / +1 tick com indicação da velocidade corrente,
ligado à `TimeControlSource` injetada (T0). Neste estágio a fonte é `MockTimeControlSource`, que
pausa/acelera o `MockTickStreamSource` localmente — o mapa realmente para e acelera, sem motor. A
troca pelas chamadas HTTP reais é a T32.
**Where**: `web/src/components/TimeControls.tsx` (novo).
**Depends on**: T0, T14.
**Reuses**: `TimeControlSource`/`MockTimeControlSource` de T0.
**Requirement**: VTT2-27, VTT2-28, VTT2-29, VTT2-31

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Cada botão chama o método correspondente da `TimeControlSource` exatamente uma vez (`pause()`, `resume()`, `setSpeed(n)`, `step()`) — teste com fonte espiã
- [x] A velocidade corrente é visível e reflete o `status()` da fonte
- [x] O componente não conhece HTTP: nenhum `fetch` é construído nele (teste com espião: 0 chamadas)
- [x] `+1 tick` só está habilitado quando pausado
- [x] Trocar de velocidade **não** reassina a fonte de stream nem limpa seleção/câmera (teste: contagem de `TickStreamSource.subscribe` inalterada; `SelectionStore.current()` preservado)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 162 passed (6 novos), tsc limpo; verificado no browser real (Pause/+1 tick respondem, 0 chamada de rede do app)
- [x] Contagem de testes: ≥ 6 novos passando — 6 novos

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): simulation time controls hud`

---

### T17: Retirar a superfície de Player Mode do cliente [P] — OQ-4 resolvida (remover agora) — ✅ Done (`6e96297`)

**What**: remover seletor de modo, input de NPC do jogador, `PlayerMoveControls` e o overlay de tecla
M, mantendo o backend intacto.
**Where**: `web/src/App.tsx:28-39,85-105,139-141`, `web/src/components/PlayerMoveControls.tsx`,
`web/src/components/MapOverlay.tsx`, `web/tests/PlayerMoveControls.test.tsx`.
**Depends on**: T14.
**Reuses**: nada — é remoção.
**Requirement**: escopo da spec (Out of Scope: Player Mode)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Nenhum caminho de UI aciona `moveNpc` nem assina em `ViewerMode.Player` (grep confirma zero referências fora de `api.ts`)
- [x] `src/LivingWorld.Api/VisualInput/*`, `src/LivingWorld.Simulation/Visibility/*` e `src/LivingWorld.Api/Visual/CityVisibilityFilter.cs` **não são tocados** (o backend permanece testado para a Fase 25)
- [x] Testes de servidor de FOW/movimento continuam passando sem alteração (nenhum arquivo de backend tocado)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 159 passed, tsc limpo
- [x] Contagem de testes: suíte web sem os testes removidos, todos os restantes passando — 162→159 (-3, exatamente os de `PlayerMoveControls.test.tsx`)

**Nota**: a maior parte deste trabalho já tinha acontecido em T14 (App.tsx parou de montar
`PlayerMoveControls`/`MapOverlay` porque ambos faziam `fetch` real, incompatível com o mock-only do
Estágio 1) — T17 ficou reduzida a apagar os arquivos órfãos + limpar 2 comentários obsoletos que
ainda citavam `MapOverlay`, e remover `worldMarkers()` de `worldMapData.ts` (morta desde T14).

**Tests**: unit · **Gate**: Quick-web
**Commit**: `refactor(web): drop player-mode surface (out of scope until phase 25)`

---

### T18: `LayerPanel` com toggles reais [P] — ✅ Done

**What**: transformar a legenda em painel de camadas com liga/desliga, z-order determinística e
camadas `NotYetModeled` desabilitadas com o motivo.
**Where**: `web/src/components/LayerPanel.tsx` (evolui `LayerLegend.tsx`),
`web/src/map-engine/layers.ts` (generaliza `worldMapData.ts`).
**Depends on**: T14.
**Reuses**: `web/src/components/LayerLegend.tsx:18-24` (já distingue `isModeled`); `terrainColorLookup`/`riverOverlayPoints` (`web/src/worldMapData.ts:7-20`); o payload já traz todas as camadas suportadas (`src/LivingWorld.Api/Visual/GlobalProjector.cs:45-46`).
**Requirement**: VTT2-46, VTT2-47, VTT2-48

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Ligar/desligar uma camada muda o render sem nenhuma requisição (fetch espião: 0 — `WorldMapView.test.tsx`)
- [x] As camadas `NotYetModeled` do escopo global (11 das 14 na fixture; 3 modeladas — Terrain/Biome/Rivers) aparecem desabilitadas com o motivo
- [x] Ordem de composição é determinística e declarada — `LAYER_Z_ORDER` (`map-engine/layers.ts`), `sortActiveLayers` aplicado ao array de `ActiveLayer`
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 186 passed, tsc limpo
- [x] Contagem de testes: `tests/LayerPanel.test.tsx` (5) + `tests/map-engine/layers.test.ts` (4) + 1 novo em `WorldMapView.test.tsx` — 10 novos

**Nota**: só Terrain/Rivers têm efeito real no renderer hoje (Biome é `isModeled: true` na
fixture mas nenhum consumidor lê o payload — mesmo gap de `worldMapData.ts` de sempre); o toggle
de Biome fica no painel por paridade de dado, sem fingir efeito visual que não existe. `LayerLegend.tsx` foi removido (substituído por `LayerPanel.tsx`, nenhum outro consumidor).

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): toggleable layer panel`

---

### T19: Follow [P] — ✅ Done (adiantado em `237595f`)

**What**: seguir uma entidade com a câmera, com cancelamento explícito e cancelamento por pan manual.
**Where**: `web/src/state/viewStore.ts` (extensão), `web/src/components/inspector/EntityInspector.tsx`
(botão), `web/src/components/MapView.tsx` (cancelamento por input).
**Depends on**: T14.
**Reuses**: `Camera` (T5), `SelectionStore` (T12).
**Requirement**: VTT2-49, VTT2-50, VTT2-51, VTT2-52

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Com follow ativo, a câmera acompanha a posição **autoritativa** (não a interpolada) a cada atualização
- [x] Pan manual cancela o follow e a UI indica
- [x] Botão explícito de parar existe e funciona (`FollowButton`, nos 3 inspectors)
- [x] Entidade que sai do espaço cancela o follow sem trocar de espaço automaticamente
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 176 passed, tsc limpo
- [x] Contagem de testes: `tests/inspector/FollowButton.test.tsx` (4)

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): follow entity camera mode`

---

### T20: Campos de footprint na projeção [P] — **Estágio 2 · Engine-facing (read-model/API only)** · OQ-1 resolvida — ✅ Done (`444e177`)

**What**: expor em `GlobalCityMarker`/`CityBuildingMarker` os bounds e posições canônicos entregues
por T45, mantendo fallback derivado e explicitamente marcado para mundos legados. **Só a API.**
**Where**: `src/LivingWorld.Api/Visual/GlobalProjector.cs`, `src/LivingWorld.Api/Visual/CityProjector.cs`.
**Depends on**: T49 (E2.0 completo; T45 fornece a geometria canônica).
**Reuses**: geometria de T45 e precedente de `GlobalSnapshot.Width/Height` para projeção sem mutação.
**Requirement**: VTT2-42, VTT2-43, VTT2-44, VTT2-45 (habilitador de dado — os ACs de renderização/hit-area são verificados em T28 contra a fixture e em T34 contra este campo)

**Tools**: MCP: NONE · Skill: NONE

> **Split:** a metade "consumir no renderer e remover o anel client-side" saiu daqui. O render de
> footprint contra fixture é **T28** (Estágio 1, onde o usuário valida a aparência); a troca da
> fixture por este campo é **T34** (Estágio 3).

**Done when**:
- [x] `GlobalCityMarker` traz `Bounds`/`BoundsAreDerived`; `CityBuildingMarker` traz `Location`/`LocationIsDerived`
- [x] Autoria canônica tem precedência; fallback legado é estável por `BuildingId` e nunca move um prédio ao reordenar a coleção
- [x] Os campos batem, campo a campo, com o shape que a fixture de T0 já usa (se divergirem, a T34 deixa de ser mecânica — verificar antes de fechar)
- [x] Teste prova que projetar os campos não altera o hash; a mudança canônica pertence exclusivamente à T45
- [x] `git diff --name-only` não lista nenhum arquivo sob `web/`
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~Visual"` — 78 passed (5 novos), tsc n/a (task não toca web/)
- [x] Contagem de testes: ≥ 4 novos passando — 5 novos

**Tests**: integration · **Gate**: Quick-api
**Commit**: `feat(api): derived city footprint and stable building placement fields`

---

### T21: `SpatialPortal` como conceito canônico de domínio — **Estágio 2 · Engine-facing (DOMAIN — altera hash/goldens)** · OQ-2 resolvida — ✅ Done (`8539125` + regravação de goldens em `1da3d5b`)

**What**: modelar entradas/saídas nomeadas de um espaço ("portão norte", "docas", "porta da frente")
como dado canônico novo em `LivingWorld.Domain`/`WorldState`, expor esse dado na projeção da API, e
fazer a navegação do cliente resolver transições consultando-o — nunca por coordenada hardcoded.
Fronteira estrita: **só dado descritivo**. Nenhuma regra nova de quem pode usar qual portal, nenhum
efeito econômico/social, nenhuma mudança de posição de NPC. `MigrationSystem` não é alterado
(continua trocando só `Npc.City`/`Household.City` via `JoinCity` —
`src/LivingWorld.Simulation/Cities/MigrationSystem.cs:58,60`).
**Where**:
- `src/LivingWorld.Domain/Geography/SpatialPortal.cs` (novo) — `PortalId`, `SpatialPortal(Id, Label, From, To)`, `PortalEndpoint(Space, RefId, Cell)`, `PortalSpaceKind { World, City, Building }`.
- `src/LivingWorld.Simulation/WorldState.cs` — coleção `[Canonical] Portals` nova.
- `src/LivingWorld.Simulation/ScenarioLoaderV2.cs` — autoria de portais por cenário, mesmo caminho declarativo de `SettlementAnchor` (`src/LivingWorld.Domain/Geography/MapCell.cs:16-18`).
- `src/LivingWorld.Api/Visual/GlobalProjector.cs`, `CityProjector.cs` — campo `Portals` em `GlobalSnapshot`/`CitySnapshot`, listando os portais cuja origem pertence ao escopo.
- `tests/LivingWorld.Tests/GoldenHashesTests.cs` — regravação do baseline em commit separado.
**Depends on**: T49 (E2.0 completo; usa o endereço espacial/andares definido em T46).
**Reuses**: molde de value object declarativo de `SettlementAnchor` (`MapCell.cs:16-18`); padrão de coleção canônica de `WorldState.Cities`/`Buildings` (`WorldState.cs:238-241`); padrão de campo de projeção de `CityFootprintProjection` (T20).
**Requirement**: VTT2-62, VTT2-63, VTT2-64, VTT2-65, VTT2-66, VTT2-67 (spec.md AC1-6 da story "SpatialPortal como conceito canônico de domínio")

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Cada portal é dado canônico com identidade, rótulo, espaço/posição de origem e de destino, marcado `[Canonical]` (AC1)
- [x] Round-trip de serialização preserva todos os portais com hash canônico idêntico (AC2)
- [x] N portais para o mesmo par de espaços funcionam distinguíveis só por rótulo, sem nenhum ramo de código por entrada (AC3)
- [x] Cenário sem portais declarados continua válido; cenário com portais os carrega pelo caminho declarativo (AC4)
- [x] O campo `Portals` aparece em `GlobalSnapshot`/`CitySnapshot`, no mesmo shape que a fixture de T0 já usa (a *consulta* pelo cliente é de T11 contra o mock e de T33 contra este campo; nenhum arquivo de `web/` é tocado aqui)
- [x] Teste prova que um mundo **sem** nenhum portal declarado produz hash idêntico ao baseline atual (isola a mudança de hash à coleção nova, não a um efeito colateral) — testado como isolamento (duas cargas idênticas hasheiam igual; só adicionar um portal diverge), não como igualdade ao hash pré-feature — ver nota abaixo
- [x] Goldens regravados em commit **separado e explícito** via `dotnet test --filter ZZZ_record_golden_hashes` (`GoldenHashesTests.cs:19-29`), nunca como efeito colateral do gate (AC6) — `1da3d5b`
- [x] Nenhum sistema de simulação lê `world.Portals` nesta task (fronteira estrita — grep pós-implementação confirma zero leituras fora de domínio/projeção/cliente)
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~SpatialPortal"` **e** o teste de regravação de goldens isolado — 101 passed (13 novos + regen), goldens regravados e revalidados
- [x] Contagem de testes: ≥ 8 novos passando — 13 novos (+1 boundary test adicionado após o Verifier)

**Tests**: unit + integration · **Gate**: Quick-api (+ regravação de goldens em commit próprio, não no mesmo commit da task)
**Commit**: `feat(domain): spatial portals as canonical named space entrances/exits`

**Nota (SPEC_DEVIATION)**: `SpatialPortal.Id`/`PortalEndpoint` usam `string Id`/`string Label`, não o
`readonly record struct PortalId(long Value)` do design.md. Razão: `PortalId(long)` serializaria
como `{"value": N}` (mesmo padrão de `CityId`/`BuildingId` nesta base), divergindo do shape que a
fixture mock do T0 já usa e que o cliente (T11/ViewStore) já consome
(`web/src/data/contracts.ts` `SpatialPortalDto.id: string`, ex. `"portal-city-a-north"`). Como a
task explicitamente exige bater "no mesmo shape que a fixture de T0 já usa", `Id` ficou string
plana — mesmo molde de `SettlementAnchor.Id` (`MapCell.cs:16-18`), que também é string autorada de
cenário, não um contador de `WorldState`. Validado independentemente pelo Verifier (`validation.md`).

---

### T22: `BuildingSpace` [P] — ✅ Done (`237595f` + rodadas de fixes não commitadas)

**What**: espaço de prédio como `MapView` configurado, com breadcrumb de 3 níveis e declaração
explícita do que o motor não modela.
**Where**: `web/src/components/InteriorView.tsx` (reescrito como configuração de `MapView`).
**Depends on**: T14.
**Reuses**: `InteriorProjector`/`InteriorSnapshot.OccupancyModeled` (`src/LivingWorld.Api/Visual/InteriorProjector.cs:11,20`); nota atual de `web/src/components/InteriorView.tsx:19`.
**Requirement**: VTT2-60, VTT2-61

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Breadcrumb mostra World / Cidade / Prédio e permite voltar a qualquer nível
- [x] Com `occupancyModeled: false`, o espaço declara isso e **não** desenha cômodos/móveis fictícios — planta sólida removida por pedido do usuário, sobra só contorno decorativo (`decorative: true`, ver context.md "Terceira rodada")
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 176 passed, tsc limpo
- [x] Contagem de testes: `tests/InteriorView.test.tsx` (5)

**Nota**: escopo cresceu ao vivo com feedback do usuário (não estava no Done-when original):
seletor de andar (`FloorSelector.tsx`, estado local, sem dado real de Z no motor — reseed de
footprint por andar), escala CityTile→BuildingTile (`SCALE.cityTilesPerBuildingTile`), e o bug de
`BuildingInspector` assumindo `entityRef.space` sempre = cidade (quebrava ao abrir um prédio
selecionado). Ver context.md "Segunda rodada"/"Terceira rodada".

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): building space with honest unmodeled declaration`

---

### T23: Tela de presets do World Creator — ✅ Done

**What**: primeira tela curta do creator — nome, seed, tamanho aproximado, preset, botão criar —
substituindo a entrada direta no wizard de 6 abas.
**Where**: `web/src/components/creator/PresetStart.tsx` (novo), `web/src/App.tsx`.
**Depends on**: T14.
**Reuses**: `listPeriodTemplates`/`fetchPeriodTemplate` (`web/src/api.ts:90-101`), `jsonToScenarioForm` (`web/src/scenarioDefaults.ts`), `DefaultPeriodSeeder` (`src/LivingWorld.Api/DefaultPeriodSeeder.cs`), `createWorld` (`web/src/api.ts:72-78`).
**Requirement**: VTT2-53, VTT2-59

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] A tela inicial exige no máximo 4 campos e nenhum parâmetro avançado — nome/seed/tamanho/ponto-de-partida, sem `<details>`
- [x] Escolher um preset pré-popula o `ScenarioFormState` completo (`jsonToScenarioForm`, mesmo caminho do wizard)
- [x] O JSON submetido é o mesmo shape que `POST /worlds/create` aceita hoje — `PresetStart` só produz `ScenarioFormState`, quem serializa continua sendo `CreateWorldForm`→`scenarioFormToJson` (nenhum novo caminho de submit)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 190 passed, tsc limpo
- [x] Contagem de testes: `tests/creator/PresetStart.test.tsx` (4 novos)

**Nota**: `nome` não tem campo correspondente no domínio (`WorldCreateEndpoints.cs` não recebe
nome de mundo) — fica só como rótulo de sessão no cliente (`App.tsx` exibe no header), nunca
entra no `ScenarioJson`. `CreateWorldForm` ganhou `initialForm?` opcional (default inalterado:
`defaultScenarioForm()`) pra receber o que `PresetStart` decidiu. `App.tsx`: `creatingWorld`
continua controlando a entrada no fluxo do creator; `creatorForm` (null = ainda em `PresetStart`)
decide qual dos dois passos renderizar.

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): world creator preset entry screen`

---

### T24: Casca do editor visual — toolbar + mapa + inspector — ✅ Done

**What**: layout do editor (toolbar no topo, `MapView` ocupando o centro, `EntityInspector` à
direita), com o inspector mostrando config geral do mundo quando nada está selecionado.
**Where**: `web/src/components/creator/WorldEditor.tsx` (novo).
**Depends on**: T23.
**Reuses**: `MapView` (T13), `EntityInspector` (T15), `ScenarioFormState` (`web/src/scenarioDefaults.ts`).
**Requirement**: VTT2-54, VTT2-55

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] O mapa ocupa a maior parte da tela; toolbar e inspector são HUD, não empurram o layout
- [x] Sem seleção, o inspector mostra a configuração geral do mundo; com terreno/cidade/prédio selecionado, mostra as propriedades daquela entidade — assentamento (`kind: "city"`) selecionável hoje via o hit-test padrão do `MapView`; terreno/prédio ficam pra T25 (nenhuma ferramenta de clique ainda)
- [x] O editor e o Observer usam a **mesma** instância de `MapView` (nenhuma segunda implementação de mapa)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 195 passed, tsc limpo
- [x] Contagem de testes: `tests/creator/WorldEditor.test.tsx` (5 novos)

**Desvio explícito**: `MapGridEditor.tsx` (`GridCanvas`-based) **não** foi removido aqui —
`CreateWorldForm.tsx` (ainda o caminho vivo em `App.tsx`, o `WorldEditor` não está montado nele
ainda) continua dependendo dele pra pintura de terreno. A remoção é o próprio Where-clause da
T25 (`MapGridEditor.tsx (removido)`); marcá-la aqui como feita seria falso — o consumidor real
só migra quando a T25 substitui a pintura por ferramenta+clique no `MapView`. `WorldEditor` hoje
é construído e testado isoladamente (sem stores do `App`: monta `SimulationStore`/`ViewStore`/
`SelectionStore` próprios com fontes nulas, já que nenhum mundo existe antes do submit) — o
swap em `App.tsx` (`PresetStart` → `WorldEditor` no lugar de `CreateWorldForm`) fica pra T26,
que já lista `CreateWorldForm.tsx (desmontado)` no seu próprio Where-clause.

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `feat(web): visual world editor shell reusing the map engine`

---

### T25: Ferramentas espaciais por clique no mapa [P] — ✅ Done

**What**: ferramentas de escala WORLD (pintar terreno/bioma, adicionar assentamento) operando por
seleção de ferramenta + clique no `MapView`, com as coordenadas visíveis no inspector como leitura.
**Where**: `web/src/components/creator/tools/*.ts` (novos); `web/src/components/MapGridEditor.tsx`
(removido).
**Depends on**: T24.
**Reuses**: lógica de pintura de `web/src/components/MapGridEditor.tsx:43-69`, `PaintedCell`/`SettlementRow`/`buildCells` (`web/src/scenarioDefaults.ts`).
**Requirement**: VTT2-56

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Posicionar um assentamento é feito por ferramenta + clique; o campo x/y aparece no inspector como leitura, não como forma primária de entrada — `WorldEditor` toolbar (`tool-select`) + `MapView.onPaintClick` (novo hook, aditivo — nenhuma view existente passa essa prop, comportamento delas inalterado) + `tool-last-cell` read-only
- [x] O `Cells` emitido é idêntico ao que o `MapGridEditor` atual produziria para a mesma sequência de cliques — `paintTerrainCell`/`paintWaterCell`/`eraseCell`/`addSettlement` (`creator/tools/paint.ts`) são a MESMA lógica de `MapGridEditor.paintCell` portada pura, com os 3 casos de `MapGridEditor.test.tsx` recriados 1:1 em `tests/creator/tools/paint.test.ts` (+ 2 casos de água, não cobertos antes) — mesma entrada, mesma saída
- [x] Sem nenhuma célula pintada, o mapa continua 100% procedural por seed — `buildCells` (agora exportado) inalterado; teste dedicado em `WorldEditor.test.tsx` prova `Cells` ausente do JSON submetido sem nenhum clique de pintura
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 204 passed, tsc limpo
- [x] Contagem de testes: `tests/creator/tools/paint.test.ts` (6) + 3 novos em `tests/creator/WorldEditor.test.tsx` — 9 novos

**Desvio explícito (mesma razão da T24)**: `MapGridEditor.tsx`/`GridCanvas`-based **não** foi
removido — `CreateWorldForm.tsx` continua sendo o caminho vivo em `App.tsx` (o `WorldEditor`
ainda não está montado nele) e depende dele. `web/tests/MapGridEditor.test.tsx` continua existindo
e passando (não migrado/removido ainda) pela mesma razão — remover o componente agora quebraria
a build de `CreateWorldForm`. A migração e a remoção de ambos ficam pra T26, que dismonta
`CreateWorldForm.tsx` e é quem de fato libera `MapGridEditor` do seu último consumidor.

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): spatial-first world editing tools`

---

### T26: Progressive disclosure e resolução de ids para nomes [P] — ✅ Done

**What**: mover os blocos densos de parâmetros para accordions/drawers por área dentro do inspector,
e trocar linhas repetidas de "id: [] valor: [] remover" por tabelas/chips/selectors com rótulos
legíveis; id cru só em modo avançado.
**Where**: `web/src/components/creator/panels/*.tsx` (novos), `web/src/components/formFields.tsx`
(evolução), `web/src/components/CreateWorldForm.tsx` (desmontado).
**Depends on**: T24.
**Reuses**: `KeyNumberListEditor`/`ObjectListEditor` (`web/src/components/formFields.tsx`) como base; **todo** o `ScenarioFormState` de `web/src/scenarioDefaults.ts` (AD-001 proíbe perder campos).
**Requirement**: VTT2-25, VTT2-57, VTT2-58

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Nenhum campo de `ScenarioFormState` foi perdido (o JSON submetido é byte-idêntico a `scenarioFormToJson(initialForm)`)
- [x] Nenhuma tela do editor mostra dezenas de inputs simultâneos; parâmetros avançados estão atrás de disclosure
- [x] Ids resolvem para rótulos legíveis quando há catálogo; id cru só sob toggle "avançado"
- [x] Gate: suíte web completa + `tsc --noEmit` — **208 passed**, TypeScript limpo
- [x] Contagem de testes: ≥ 5 novos passando; paridade PascalCase migrou para `WorldEditor.test.tsx`

**Retomada concluída em 2026-08-07.** O handoff abaixo registra o ponto de partida preservado.

**Pesquisa que embasa o design (já concluída, não refazer)**: só existe catálogo real de id→nome
pra **profession/skill**, via `GET /periods/{id}/catalog` (`PeriodCatalog.cs`), e mesmo esse é
condicional por período (só entra no dict o id que aquele período declarou com `Name` num bias).
Terreno/bioma/recurso/cultura/tipo-de-local/prédio **não têm catálogo em lugar nenhum do domínio**
(`GeographyIds.cs`/`PopulationIds.cs` documentam "o motor nunca conhece o nome") — esses ids
continuam crus sempre, em qualquer modo. Não inventar catálogo pra eles.

**Feito e funcionando**:
1. `web/src/components/formFields.tsx` — `FieldSpec`/`KeyNumberListEditor` ganharam `labels?:
   Record<number,string>` opcional. Quando presente, o campo numérico (`"number"` e
   `"nullable-number"`) renderiza `<select>` com nome+id em vez de input numérico cru; toggle
   "IDs crus" por editor força input numérico mesmo com catálogo. Sem `labels`, comportamento
   idêntico a antes (aditivo, não quebra nenhum consumidor existente). **Testado**:
   `web/tests/formFields.test.tsx` (7 testes, todos passando na última rodada).
2. `web/src/scenarioDefaults.ts` — `buildCells` exportado (já feito na T25).
3. `web/src/api.ts` — `fetchPeriodCatalog(id)` novo, chama `GET /periods/{id}/catalog`. **Ainda
   não testado** (sem teste próprio, sem uso em lugar nenhum ainda).
4. `web/src/components/creator/panels/*.tsx` (6 arquivos novos, todos criados, `tsc --noEmit`
   limpo, **nenhum ainda importado/renderizado em lugar nenhum** — código morto por enquanto):
   - `types.ts` — `PanelProps` compartilhada (`form`, `set`, `professionNames?`, `skillNames?`).
   - `MapPanel.tsx` — largura/altura/seed/região/ids csv + avançado (custo, peso por terreno,
     assentamentos por número). Porta 1:1 a aba "Mapa" do `CreateWorldForm` atual.
   - `PopulationPanel.tsx` — porta 1:1 a aba "População".
   - `BehaviorPanel.tsx` — porta 1:1 a aba "Comportamento"; `routineSlots.professionId` já
     recebe `labels={professionNames}`.
   - `EconomyPanel.tsx` — porta 1:1 a aba "Economia"; `wageByProfession`/`locationTypeByProfession`
     já recebem `labels={professionNames}`.
   - `CitiesPanel.tsx` — porta 1:1 a aba "Cidades".
   - `DynamicsPanel.tsx` — porta 1:1 a aba "Dinâmica"; `professionBiases`/`skillBiases` já
     recebem `labels={professionNames}`/`labels={skillNames}`.

**Plano executado na retomada**:
1. **Wire dos painéis no `WorldEditor.tsx`**: hoje o painel "sem seleção" é só um resumo
   read-only (`world-general-config`, de T24) — trocar pelos 6 `<details>` (um por painel
   acima), cada painel recebendo `form`/`set` (o `WorldEditor` já tem `setForm`, só falta o
   helper `set(key,value)` — o `CreateWorldForm` atual tem o padrão exato em
   `CreateWorldForm.tsx:64-66`, copiar) e manter o botão "Criar mundo" + resumo compacto no topo.
2. **Buscar o catálogo**: `WorldEditor` precisa saber o `periodId` de origem (só existe quando
   `PresetStart` carregou um template, não no caminho "em branco") — `PresetStart.tsx` (T23)
   precisa passar esse id pra cima (`onStart(form, name, periodId?)`), `App.tsx` guarda e passa
   pro `WorldEditor` como prop nova (`catalogPeriodId?`), que faz `fetchPeriodCatalog` num
   `useEffect` e guarda `professionNames`/`skillNames` em estado (vazio = sem catálogo = tudo
   cru, comportamento honesto de fallback).
3. **Teste de paridade byte-idêntica** (o item mais importante do Done-when): montar
   `WorldEditor`, preencher os mesmos valores que `CreateWorldForm.test.tsx` usa no caso "posts
   the default scenario as a full PascalCase JSON body", chamar `scenarioFormToJson` nos dois
   caminhos e comparar string a string. Como os painéis usam o MESMO `form`/`scenarioFormToJson`
   que o wizard antigo, isso deve só funcionar — mas precisa ser escrito e rodado.
4. **Testes dos painéis novos** (≥5 exigido pelo Done-when — os 7 de `formFields.test.tsx` já
   escritos contam, mas não substituem testar os painéis renderizando de fato): pelo menos um
   teste por painel confirmando que o `<details>` esconde o bloco avançado por padrão e que um
   campo simples chama `set` corretamente.
5. **Swap final em `App.tsx`**: trocar a montagem de `CreateWorldForm` por `PresetStart` →
   `WorldEditor` (hoje `App.tsx` ainda monta `CreateWorldForm`, ver `creatorForm`/`setCreatorForm`
   de T23 — só falta apontar pro `WorldEditor` em vez do form antigo).
6. **Remoção**: só DEPOIS do passo 5 funcionar de ponta a ponta —
   `web/src/components/CreateWorldForm.tsx`, `web/src/components/MapGridEditor.tsx`,
   `web/tests/CreateWorldForm.test.tsx`, `web/tests/MapGridEditor.test.tsx` somem (o caso de JSON
   PascalCase completo migra pro teste de paridade do passo 3 antes de apagar o arquivo antigo).
7. **Gate completo** (`npm --prefix web test && npx --prefix web tsc --noEmit`) — **a suíte
   inteira não foi rodada depois do último fix** (só `tsc --noEmit`, que está limpo). Rodar tudo
   antes de marcar qualquer checkbox como `[x]`.

**Resultado**: `App.tsx` usa `PresetStart → WorldEditor`; os componentes antigos e o `GridCanvas`
sem consumidores foram removidos depois da migração dos testes.

**Tests**: unit · **Gate**: Quick-web
**Commit**: `refactor(web): progressive disclosure and readable ids in the world editor`

---

### T27: Fechamento de fase — regressivo completo — **Estágio 3 (última task da fase)**

**What**: única execução do gate completo da fase, mais os testes de cenário, mais o teste de
invariância de hash com a UI conectada, mais `validation.md`.
**Where**: `scripts/verify.sh` (execução), `tests/LivingWorld.Tests/Visual/VisualGateTests.cs`
(extensão), `.specs/features/phase-15.1-vtt-frontend-redesign/validation.md` (novo).
**Depends on**: T31, T32, T33, T34 (que por transitividade fecham todo o Estágio 1 e todo o Estágio 2).
**Reuses**: padrão de invariância de hash de `tests/LivingWorld.Tests/Visual/VisualGateTests.cs:57-70`; `scripts/generate-web-types.sh --check` já encadeado em `scripts/verify.sh:10`.
**Requirement**: VTT2-05, VTT2-30, VTT2-33 (verificação final) + todos

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Teste novo: N ticks com sessão de observação ativa navegando produzem o mesmo hash canônico que N ticks sem sessão
- [ ] Teste novo: nenhum endpoint de `/simulation` altera o hash canônico
- [ ] `bash scripts/verify.sh` passa até o fim (primeira e única execução completa da fase)
- [ ] `bash scripts/test.sh --filter Category=Scenario` passa até o fim
- [ ] Sensor de discriminação: ≥ 3 mutações injetadas e mortas (sugestões: `Camera.zoomAt` sem ancoragem no cursor; `InterpolationBuffer.observe` enfileirando em vez de substituir; `TickLoopService` ignorando `IsPaused`)
- [ ] Nenhuma `Mock*Source` é referenciada no caminho de produção (grep em `web/src/main.tsx`/`App.tsx`: zero); os mocks continuam existindo e usados **só** por testes e pelo modo de demo offline
- [ ] `validation.md` escrito por um verificador que não implementou as tasks
- [ ] Contagem de testes registrada (dotnet + vitest) sem deleções silenciosas

**Tests**: integration + architecture + scenario · **Gate**: Phase-close (full) + Phase-close (scenario)
**Commit**: `chore: phase 15.1 close — full regression and validation report`

---

### T28: Render de footprint de cidade contra fixture [P] — **Estágio 1** — ✅ Done (`237595f` + rodadas de fixes não commitadas)

**What**: desenhar a cidade como **área** (não ponto) no `WorldSpace`, com hit-area de footprint
inteiro e marcação visual de derivado, lendo `Bounds`/`BoundsAreDerived` e `Location`/`LocationIsDerived`
da fixture de T0; e remover o anel de prédios client-side.
**Where**: `web/src/map-engine/renderer.ts`, `web/src/map-engine/hitTest.ts`,
`web/src/components/CityView.tsx` (remoção do anel e da nota).
**Depends on**: T0, T14. *(Metade de frontend da antiga T20.)*
**Reuses**: `Renderer` (T8), `hitTest` (T9), `AuthoritativeEntity.size`/`sizeIsDerived` (T0).
**Requirement**: VTT2-42, VTT2-43, VTT2-44, VTT2-45

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Cidade é desenhada como área; clicar em qualquer ponto interno seleciona (VTT2-45) — muralha com portão (`generateCityWallFootprint`), hit-test corrigido pra mirar o mesmo centro do renderer (rodada 3)
- [x] Footprint derivado é visualmente distinguível de autorado (`sizeIsDerived`) — VTT2-44
- [x] Duas cidades de populações diferentes na fixture produzem áreas diferentes (VTT2-42)
- [x] O zoom revela detalhe progressivo conforme dado disponível, sem inventar estrutura (VTT2-43)
- [x] O anel de `web/src/components/CityView.tsx:38-50` e a nota de `:104` foram removidos
- [x] Nenhum `dotnet` no gate desta task
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 176 passed, tsc limpo
- [x] Contagem de testes: `tests/map-engine/buildingFootprint.test.ts` (5), `tests/CityView.test.tsx` (4)

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): city footprint rendering and full-area hit test`

---

### T35: Pawn visual determinístico dos NPCs — **Estágio 1 · ajuste após review** — ✅ Done

**What**: substituir o token circular genérico por um pawn top-down original, composto em SVG e
derivado deterministicamente do ID; compartilhar a mesma aparência entre mapa e inspector.
**Where**: `web/src/npcAppearance.ts`, `web/src/components/NpcTokenSvg.tsx`,
`web/src/map-engine/renderer.ts`, `web/src/components/inspector/NpcInspector.tsx`.
**Depends on**: T15, T18.
**Requirement**: VTT2-68..72

**Done when**:
- [x] Mesmo ID + mesmo estado produz SVG idêntico em mapa e inspector
- [x] Mudança de ação preserva as camadas de identidade e muda só o indicador de estado
- [x] Token LOD usa o pawn; dot/agregado e o canvas único permanecem inalterados
- [x] Gate: 39 arquivos, 212 testes; `tsc --noEmit` limpo

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `feat(web): deterministic layered npc pawns`

---

### T36: World Builder visual como configuração de jogo — **Estágio 1 · ajuste após review** — ✅ Done

**What**: reduzir a carga de botões/formulários e tornar escolhas, mapa e efeitos dos parâmetros
visuais e dinâmicos. O detalhamento começa depois da aprovação dos personagens.
**Depends on**: T35.
**Requirement**: VTT2-53..59

**Done when**:
- [x] Entrada usa cartões de escala/origem e preview vivo no lugar de quatro campos soltos
- [x] Editor usa dock visual contextual sem criar outro mapa ou alterar o contrato do cenário
- [x] Resumo do cenário mostra mapa, população e assentamentos de forma escaneável
- [x] Layout visual validado no navegador; dock não invade o painel lateral
- [x] Gate Quick-web: 39 arquivos, 214 testes; `tsc --noEmit` limpo

---

### T37: Paisagem top-down viva — **Estágio 1 · ajuste após review** — ✅ Done

**What**: renderizar variação determinística de grama/solo, rios em tile e nuvens cosméticas no
mapa-múndi e na cidade, sem adicionar estado ao motor.
**Depends on**: T14, T18.
**Requirement**: VTT2-73, VTT2-74, VTT2-76, VTT2-78
**Tests**: unit + integration · **Gate**: Quick-web

**Done when**:
- [x] Mundo e cidade usam solo/grama determinísticos; rios ocupam tiles visuais
- [x] Nuvens são cosméticas, estáveis por espaço e não entram no estado do motor
- [x] Microtextura aparece só no zoom próximo; mapa-múndi mantém leitura ampla

---

### T38: Arquitetura top-down e estabilidade em Z — **Estágio 1 · ajuste após review** — ✅ Done

**What**: detalhar telhado/parede/porta e tornar footprint/portão independentes do Z observado.
**Depends on**: T37.
**Requirement**: VTT2-75, VTT2-76, VTT2-77
**Tests**: unit + integration · **Gate**: Quick-web

**Done when**:
- [x] Renderer distingue telhado, pedra, madeira e porta com detalhes top-down
- [x] Footprint/porta de prédio são idênticos entre níveis Z
- [x] Muralha/portão de cidade são idênticos entre níveis Z
- [x] Gate: 40 arquivos, 220 testes; `tsc --noEmit` limpo

---

### T39: Correções de escala e leitura arquitetônica — **Estágio 1 · ajuste após review** — ✅ Done

**What**: substituir plantas ocas por massas de telhado, dar quarteirões/ruas à cidade no mapa,
reduzir o zoom inicial da cidade e restaurar piso/contorno do interior.
**Depends on**: T38.
**Requirement**: VTT2-75, VTT2-76

**Done when**:
- [x] Cidade no mapa tem silhueta com cantos cortados, muralha, quarteirões e ruas
- [x] Prédios na cidade exibem cobertura contínua e porta, não a planta interna
- [x] Zoom inicial da cidade passa de 16 para 8 px/tile
- [x] Interior usa piso terroso e contorno de parede opaco, sem fundo de céu azul
- [x] Gate Quick-web: 40 arquivos, 221 testes; `tsc --noEmit` limpo

**Tests**: unit + integration · **Gate**: Quick-web

---

### T41: World Builder espacial e cinematográfico — **Estágio 1 · ajuste após review** — ✅ Review 2 implementado

**What**: tornar preset e editor uma experiência espacial contínua: preview por seed/escala,
mapa full-screen, pintura por arraste, assentamento editável/móvel e editor local de cidade.
**Depends on**: T36, T39.
**Requirement**: VTT2-53..59, World Creator AC8..21
**Tests**: unit + integration · **Gate**: Quick-web

**Done when**:
- [x] Preview anima escala e compartilha paisagem determinística por seed com o editor
- [x] Editor preenche o viewport e pintura cobre a trajetória do arraste
- [x] Assentamento pode ser selecionado, renomeado, movido e aberto
- [x] Editor local de cidade permite posicionar e mover construções
- [x] Configuração revela uma seção temática por vez e usa ações "Começar"/"Dar vida ao mundo"
- [x] Gate web: 42 arquivos, 233 testes; TypeScript e build Vite limpos
- [x] Cidade/prédio podem ser apagados por ferramenta, botão e Delete/Backspace
- [x] Preview e editor compartilham dimensões, proporção e paleta de seed/pintura
- [x] Ctrl+Z/Ctrl+Y desfazem/refazem autoria no mundo e dentro da cidade
- [x] Capítulos explicam efeitos e recomendação antes de revelar os valores técnicos
- [x] Gate web da review 2: 42 arquivos, 239 testes; TypeScript e build Vite limpos
- [x] Assentamentos e construções selecionados rotacionam 90° por botão ou tecla R
- [x] Gate web da rotação: 42 arquivos, 241 testes; TypeScript e build Vite limpos

---

### T29: Checkpoint de validação visual — **Estágio 1 (fecha o estágio; gate humano)** — ✅ aprovado

**What**: build de demo do cliente rodando **inteiramente** contra os mocks, apresentado ao usuário
para aprovação da aparência antes de qualquer linha de backend. Não é uma task de código: é o gate de
entrega que motivou o re-sequenciamento.
**Where**: `web/src/main.tsx` (composition root escolhendo as fontes mock),
`.specs/features/phase-15.1-vtt-frontend-redesign/visual-review.md` (novo — o que foi mostrado e o
que o usuário aprovou/pediu).
**Depends on**: T15, T16, T17, T18, T19, T22, T23, T24, T25, T26, T28, T35, T36, T37, T38, T39, T41.
**Reuses**: tudo do Estágio 1; `npm --prefix web run dev`.
**Requirement**: nenhum AC novo — é verificação de aceitação visual das stories já entregues

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `npm --prefix web run dev` sobe a aplicação **sem nenhum backend rodando** e o mapa se move (tick mock), navega entre os 3 espaços, seleciona, inspeciona, troca camadas, segue entidade e abre o World Creator
- [x] O composition root é o **único** lugar que nomeia `Mock*Source` (grep: nenhum store/componente importa mock diretamente)
- [x] Suíte web inteira verde + `tsc --noEmit` limpo — **241 passed**
- [x] `visual-review.md` registra a validação técnica, as iterações visuais e a decisão final de 2026-08-07
- [x] **Aprovação explícita do usuário registrada** em 2026-08-07 após rotação por botão/tecla R
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit`

**Tests**: none (gate de aceitação humana; toda a cobertura automatizada já foi entregue nas tasks T0-T28)
**Gate**: Quick-web
**Commit**: `chore(web): offline demo build for visual approval`

---

### T30: Indicadores de cidade na projeção — **Estágio 2 · Engine-facing (read-model/API only)** [P] — ✅ Done (`841a00d`)

**What**: expor os 6 indicadores que `CityPopulationQuery` já calcula (população, riqueza, saúde,
desigualdade/Gini, economia, habitação) no snapshot de cidade. **Só a API.**
**Where**: `src/LivingWorld.Api/Visual/CityProjector.cs` (campo novo em `CitySnapshot`).
**Depends on**: T49 (E2.0 completo). *(Metade de backend da antiga T15.)*
**Reuses**: `CityPopulationQuery` (`src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-53`) — chamado, não reimplementado; mesmo padrão de campo de projeção de T20.
**Requirement**: VTT2-22 (habilitador de dado — o AC de exibição é verificado em T15 contra a fixture e em T34 contra este campo)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] Os 6 indicadores aparecem no payload de `CitySnapshot`, no mesmo shape que a fixture de T0 usa
- [x] Teste prova que o campo novo **não** altera o hash canônico (padrão de `VisualGateTests`)
- [x] Nenhum indicador é recalculado no projector — `CityPopulationQuery` é a única fonte
- [x] `git diff --name-only` não lista nenhum arquivo sob `web/`
- [x] Gate: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~Visual"` — 31 passed (2 novos)
- [x] Contagem de testes: ≥ 3 novos passando — 3 novos (indicadores batem com `CityPopulationQuery`, hash-invariância, cidade vazia = todos os indicadores zerados)

**Tests**: integration · **Gate**: Quick-api
**Commit**: `feat(api): expose city population indicators in the city snapshot`

---

### T31: `SimulationStore` contra o transporte real — **Estágio 3** — ✅ Done

**What**: implementar `RealSnapshotSource` (`GET /visual/subscribe` + `/visual/replay`) e
`RealTickStreamSource` (`GET /visual/ws` com `ScopeTickDelta` tipado), e trocar a fonte injetada no
composition root. Nenhuma linha do `SimulationStore` muda.
**Where**: `web/src/data/real/snapshotSource.ts`, `web/src/data/real/tickStreamSource.ts` (novos),
`web/src/main.tsx` (troca de argumento), `web/src/api.ts` (reuso).
**Depends on**: T4 (gateway podado, delta publicado), T10.
**Reuses**: `buildWebSocketUrl`/`fetchSnapshot` (`web/src/api.ts:32-54`); as interfaces de T0; os tipos de `ScopeTickDelta` gerados de OpenAPI.
**Requirement**: VTT2-11, VTT2-36

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [x] `RealTickStreamSource` implementa a mesma interface de `MockTickStreamSource` — o diff no `SimulationStore` é **zero linhas** (verificado no diff da task)
- [x] Os testes de T10 rodam inalterados contra ambas as implementações (suite parametrizada pela fonte) — `simulationStore.ts` não foi tocado; os testes de T10 continuam passando sem alteração
- [x] Reconexão real por `onclose` reidrata por `GET /visual/subscribe` com backoff — `onDrop` aciona `SimulationStore.scheduleReconnect` (T10), reutilizado sem mudança
- [x] Delta real aplicado incrementalmente: 10 frames = 0 refetches de snapshot — mesma lógica de `SimulationStore.applyDelta` (T10), `RealTickStreamSource` nunca chama `snapshotSource.load`
- [x] `main.tsx` ganhou `VITE_DEMO_MODE` — real por padrão, mock só em demo offline (T27)
- [x] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` — 249 passed (8 novos), tsc limpo
- [x] Contagem de testes: ≥ 4 novos passando — 8 novos (`snapshotSource.test.ts`×3, `tickStreamSource.test.ts`×5)

**Tests**: unit + integration · **Gate**: Quick-web
**Commit**: `feat(web): swap mock tick stream for the real realtime transport`

---

### T32: `TimeControls` contra `/simulation/*` real — **Estágio 3** [P]

**What**: implementar `RealTimeControlSource` chamando `POST /simulation/{pause,resume,speed,step}` e
`GET /simulation/status`, e trocar a fonte no composition root.
**Where**: `web/src/data/real/timeControlSource.ts` (novo), `web/src/api.ts` (funções novas),
`web/src/main.tsx`.
**Depends on**: T1 (endpoints + proxy do Vite), T16.
**Reuses**: padrão de chamada HTTP de `moveNpc`/`createWorld` (`web/src/api.ts:62-78`); endpoints da T1.
**Requirement**: VTT2-27, VTT2-28, VTT2-29, VTT2-30, VTT2-31

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Cada botão dispara exatamente um `POST` para a rota correspondente (teste com fetch espião)
- [ ] `+1 tick` fora de pausa não é clicável; se for forçado, o 409 do endpoint é tratado sem quebrar a UI
- [ ] `speed` inválido devolve 400 e a UI mantém a velocidade anterior
- [ ] Zero linhas alteradas em `TimeControls.tsx` além do tipo da prop (verificado no diff)
- [ ] `/simulation` está no `server.proxy` de `web/vite.config.ts` (já entregue na T1 — reconferir aqui, é o bug recorrente do STATE.md)
- [ ] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit`
- [ ] Contagem de testes: ≥ 4 novos passando

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): wire time controls to the real simulation endpoints`

---

### T33: `ViewStore` contra o campo `Portals` real — **Estágio 3** [P]

**What**: implementar `RealPortalSource` lendo o campo `Portals` de `GlobalSnapshot`/`CitySnapshot`
(T21) e trocar a fonte no composition root.
**Where**: `web/src/data/real/portalSource.ts` (novo), `web/src/main.tsx`.
**Depends on**: T21 (campo de projeção), T11.
**Reuses**: interface `PortalSource` de T0; o snapshot já carregado pelo `SimulationStore` (não fazer request própria).
**Requirement**: VTT2-66 (AC5 — a navegação resolve por portal da projeção, nunca por coordenada embutida)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `enter(target)` resolve pela lista real de portais; dois portais reais para o mesmo par de espaços navegam sem nenhum ramo por entrada (AC3/AC5, agora contra dado canônico)
- [ ] Zero linhas alteradas em `viewStore.ts` (verificado no diff)
- [ ] `RealPortalSource` não dispara request própria — lê do snapshot corrente (fetch espião: 0)
- [ ] Escopo sem portais declarados não quebra a navegação (fallback declarado, não silencioso)
- [ ] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit`
- [ ] Contagem de testes: ≥ 4 novos passando

**Tests**: unit · **Gate**: Quick-web
**Commit**: `feat(web): resolve space transitions through the real portal projection`

---

### T34: Footprint e indicadores de cidade contra os campos reais — **Estágio 3**

**What**: trocar as fixtures de footprint e de indicadores de cidade pelos campos reais de projeção
(T20, T30) no renderer e no inspector.
**Where**: `web/src/data/real/snapshotSource.ts` (mapeamento dos campos), `web/src/map-engine/renderer.ts`
(nenhuma mudança esperada além de tipos), `web/src/components/inspector/CityInspector.tsx`.
**Depends on**: T20, T30, T15, T28.
**Reuses**: o mapeamento já escrito em T31; os tipos gerados de OpenAPI.
**Requirement**: VTT2-22, VTT2-42, VTT2-43, VTT2-44, VTT2-45 (verificação final contra dado real)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] O footprint desenhado vem de `Bounds` da API; `BoundsAreDerived` alimenta `sizeIsDerived` sem tradução ad-hoc
- [ ] Os 6 indicadores do inspector vêm do campo de `CitySnapshot`, não da fixture
- [ ] Nenhuma fixture mock permanece no caminho de produção destes dois consumidores (grep)
- [ ] Os testes de T15 e T28 continuam passando sem alteração de assert (só a fonte muda)
- [ ] `scripts/generate-web-types.sh --check` limpo — sem drift entre a projeção e `web/src/generated/api-types.ts`
- [ ] Gate: `npm --prefix web test && npx --prefix web tsc --noEmit` **e** `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~Visual"`
- [ ] Contagem de testes: ≥ 3 novos passando

**Tests**: unit (web) + integration (api) · **Gate**: Quick-web + Quick-api
**Commit**: `feat: consume real footprint and city indicator projections`

---

## Parallel Execution Map

```
ESTÁGIO 1 — FRONTEND CONTRA MOCK (gate Quick-web puro; zero dotnet)

E1.0:  T0                                  ← seam de fonte de dado + fixtures tipadas

E1.1 (após T0):
    ├── T5 [P]
    ├── T6 [P]   } sem dependência mútua
    ├── T7 [P]
    └── T9 [P]
  T5, T6 completos ──→ T8

E1.2 (após T0 e T9):
    ├── T10 [P]  ← MockSnapshotSource + MockTickStreamSource
    ├── T11 [P]  ← MockPortalSource (antes dependia de T21)
    └── T12 [P]

E1.3 (após T8, T10, T11, T12):
  T13 ──→ T14 ──→ ├── T15 [P]  ← indicadores vindos da fixture
                  ├── T16 [P]  ← MockTimeControlSource
                  └── T17 [P]

E1.4 (após T14):
    ├── T18 [P]
    ├── T19 [P]  } sem dependência mútua
    ├── T22 [P]
    └── T28 [P]  ← render de footprint contra fixture (metade FE da antiga T20)

E1.5 (após T14):
  T23 ──→ T24 ──→ ├── T25 [P]
                  └── T26 [P]

E1.6:
  T15..T19, T22..T26, T28 completos ──→ T29   ← APROVAÇÃO VISUAL DO USUÁRIO (gate humano)

ESTÁGIO 2 — BACKEND (gate Quick-api puro; nenhum arquivo em web/src)

E2.0 (primeiro bloco, após T29; detalhes em backend-gaps.md):
  T42 ──→ T43 ──→ T44 ──→ T45 ──→ T46 ──→ T47 ──→ T48 ──→ T49

E2.1 (Sequential, após E2.0):
  T1 ──→ T2 ──→ T3 ──→ T4

E2.2 (após E2.0, em paralelo com E2.1):
    ├── T21       ← NÃO [P]: regrava tests/golden/world-hashes.json, roda sozinha
    ├── T20 [P]   ← só os campos de footprint na API
    └── T30 [P]   ← indicadores de cidade na projeção (metade BE da antiga T15)

ESTÁGIO 3 — INTEGRAÇÃO (troca mock → real)

  T4, T10          ──→ T31
  T1, T16          ──→ T32 [P]
  T21, T11         ──→ T33 [P]
  T20, T30, T15, T28 ──→ T34

  T31, T32, T33, T34 completos ──→ T27   ← fechamento da fase
```

**Restrição de paralelismo:** toda task marcada `[P]` tem (a) dependências resolvidas, (b) tipo de
teste marcado parallel-safe na Parallelism Assessment, e (c) nenhum estado mutável compartilhado com
outra `[P]` do mesmo bloco. Nenhuma task de E2.1 é `[P]` — o tipo de teste de tick loop é **não**
parallel-safe. T21 não é `[P]` mesmo estando em E2.2: ela escreve o baseline de goldens.
T31 e T34 não são `[P]` entre si nem com T32/T33 no que toca `web/src/main.tsx` — as quatro editam o
composition root; se rodarem em paralelo, sequenciar a edição desse arquivo.

`[P]` é informação de ordenação (as tasks podem ser feitas em qualquer ordem dentro do bloco), **não**
uma diretiva de spawnar um sub-agent por task.

**Fronteiras de estágio são bloqueantes:** o Estágio 2 não começa antes da aprovação registrada em
T29, e o Estágio 3 não começa antes de os Estágios 1 e 2 estarem completos. Isso é ordem de entrega
pedida pelo usuário, não dependência técnica — dentro de cada estágio o paralelismo acima vale
integralmente.

**Execução por fase:** são 3 estágios com 9 blocos internos (> 3), então o agente orquestrador deve
**oferecer** um sub-agent por bloco (sequencial) antes de executar — ver `sub-agents.md` da skill.
Confirmação do usuário é obrigatória antes de dispatch.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T0 | 4 interfaces de 1 método-ish + 4 mocks + fixtures | ⚠️ OK — são 4 seams, mas é um único conceito (a fronteira de dado do cliente) e dividi-los deixaria metade do Estágio 1 sem fonte; nenhuma interface tem mais de uma implementação nesta fase por acidente: a segunda chega no Estágio 3, que é a razão de a interface existir |
| T1 | 1 arquivo de endpoints + 2 linhas de wiring | ✅ Granular |
| T2 | 2 tipos + 1 função de diff, mesmo conceito | ✅ Granular |
| T3 | 1 hosted service | ✅ Granular |
| T4 | 1 método de poda numa classe existente | ✅ Granular |
| T5 | 1 classe pura | ✅ Granular |
| T6 | 1 módulo de política pura | ✅ Granular |
| T7 | 1 classe pura | ✅ Granular |
| T8 | 1 função de render (extração) | ✅ Granular |
| T9 | 2 módulos coesos (espaço + hit-test no mesmo sistema de coordenadas) | ⚠️ OK — coesos, separá-los criaria dependência circular de tipos |
| T10 | 1 store | ✅ Granular |
| T11 | 1 store | ✅ Granular |
| T12 | 1 store | ✅ Granular |
| T13 | 1 componente | ✅ Granular |
| T14 | 3 views convertidas + breadcrumb | ⚠️ OK — é uma única conversão mecânica ao mesmo alvo; dividir deixaria o app quebrado entre tasks |
| T15 | 1 componente + 3 variantes de conteúdo | ⚠️ OK — variantes são o mesmo contrato de campos/ações |
| T16 | 1 componente + funções de api | ✅ Granular |
| T17 | remoção | ✅ Granular |
| T18 | 1 componente + 1 módulo de camadas | ✅ Granular |
| T19 | 1 feature em stores existentes | ✅ Granular |
| T20 | 2 campos de projeção (o consumo no renderer virou T28/T34) | ✅ Granular |
| T21 | 1 tipo de domínio + 1 coleção canônica + autoria de cenário + 1 campo de projeção (2 projectors) + regravação de goldens | ⚠️ OK — maior que o padrão da fase por decisão explícita do usuário (OQ-2, opção maior que a recomendação); é um único conceito coeso (o dado do portal) atravessando as camadas que ele naturalmente toca — separar dado de domínio e projeção em tasks distintas deixaria a projeção sem o que expor |
| T22 | 1 view convertida | ✅ Granular |
| T23 | 1 componente | ✅ Granular |
| T24 | 1 componente de layout | ✅ Granular |
| T25 | 1 conjunto de ferramentas coeso | ✅ Granular |
| T26 | reorganização de apresentação de um modelo existente | ⚠️ OK — é grande em linhas, mas é um único refactor sem mudança de modelo |
| T27 | fechamento | ✅ Granular |
| T28 | render de footprint + hit area (metade FE da antiga T20) | ✅ Granular |
| T29 | checkpoint de aprovação visual | ✅ Granular — sem código de produção além do composition root |
| T42-T49 | 1 contrato ausente por task; ver `backend-gaps.md` | ✅ Granulares; sequência evita contratos provisórios |
| T30 | 1 campo de projeção (metade BE da antiga T15) | ✅ Granular |
| T31 | 2 implementações reais de fonte + troca no root | ✅ Granular — mesmo transporte, duas interfaces irmãs |
| T32 | 1 implementação real de fonte + funções de api | ✅ Granular |
| T33 | 1 implementação real de fonte | ✅ Granular |
| T34 | mapeamento de 2 campos reais | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Estágio | Depends On (corpo) | Diagrama mostra | Status |
| --- | --- | --- | --- | --- |
| T0 | 1 | None | raiz de E1.0 | ✅ |
| T5 | 1 | T0 | T0→{T5,T6,T7,T9} | ✅ |
| T6 | 1 | T0 | T0→{T5,T6,T7,T9} | ✅ |
| T7 | 1 | T0 | T0→{T5,T6,T7,T9} | ✅ |
| T9 | 1 | T0 | T0→{T5,T6,T7,T9} | ✅ |
| T8 | 1 | T5, T6 | {T5,T6}→T8 | ✅ |
| T10 | 1 | T0, T9 | {T0,T9}→{T10,T11,T12} | ✅ |
| T11 | 1 | T0, T9 | {T0,T9}→{T10,T11,T12} | ✅ |
| T12 | 1 | T0, T9 | {T0,T9}→{T10,T11,T12} | ✅ |
| T13 | 1 | T8, T10, T11, T12 | {T8,T10,T11,T12}→T13 | ✅ |
| T14 | 1 | T13 | T13→T14 | ✅ |
| T15 | 1 | T0, T14 | T14→{T15,T16,T17} | ✅ |
| T16 | 1 | T0, T14 | T14→{T15,T16,T17} | ✅ |
| T17 | 1 | T14 | T14→{T15,T16,T17} | ✅ |
| T18 | 1 | T14 | T14→{T18,T19,T22,T28} | ✅ |
| T19 | 1 | T14 | T14→{T18,T19,T22,T28} | ✅ |
| T22 | 1 | T14 | T14→{T18,T19,T22,T28} | ✅ |
| T28 | 1 | T0, T14 | T14→{T18,T19,T22,T28} | ✅ |
| T23 | 1 | T14 | T14→T23 | ✅ |
| T24 | 1 | T23 | T23→T24 | ✅ |
| T25 | 1 | T24 | T24→{T25,T26} | ✅ |
| T26 | 1 | T24 | T24→{T25,T26} | ✅ |
| T29 | 1 | T15..T19, T22..T26, T28 | {…}→T29 (E1.6) | ✅ |
| T42-T49 | 2 | T29, em sequência | T29→T42→…→T49 (E2.0) | ✅ |
| T1 | 2 | T49 (E2.0 completo) | T49→T1, raiz de E2.1 | ✅ |
| T2 | 2 | T1 | T1→T2 | ✅ |
| T3 | 2 | T2 | T2→T3 | ✅ |
| T4 | 2 | T3 | T3→T4 | ✅ |
| T20 | 2 | T49 (E2.0 completo) | T49→T20 (E2.2) | ✅ |
| T21 | 2 | T49 (E2.0 completo) | T49→T21 (E2.2) | ✅ |
| T30 | 2 | T49 (E2.0 completo) | T49→T30 (E2.2) | ✅ |
| T31 | 3 | T4, T10 | {T4,T10}→T31 | ✅ |
| T32 | 3 | T1, T16 | {T1,T16}→T32 | ✅ |
| T33 | 3 | T21, T11 | {T21,T11}→T33 | ✅ |
| T34 | 3 | T20, T30, T15, T28 | {T20,T30,T15,T28}→T34 | ✅ |
| T27 | 3 | T31, T32, T33, T34 | {T31..T34}→T27 | ✅ |

---

## Test Co-location Validation

| Task | Code Layer criada/modificada | Matriz exige | Task diz | Status |
| --- | --- | --- | --- | --- |
| T0 | Fontes de dado plugáveis (interfaces + mocks) | unit | unit | ✅ |
| T1 | Endpoints de controle de simulação | integration | integration | ✅ |
| T2 | Contrato de projeção / delta | unit (lógica pura de diff) | unit | ✅ |
| T3 | Tick loop | integration | integration | ✅ |
| T4 | Gateway realtime | unit | unit | ✅ |
| T5 | Map engine puro | unit | unit | ✅ |
| T6 | Map engine puro | unit | unit | ✅ |
| T7 | Map engine puro | unit | unit | ✅ |
| T8 | Map engine puro | unit | unit | ✅ |
| T9 | Map engine puro | unit | unit | ✅ |
| T21 | Domínio (`SpatialPortal`) + contrato de projeção + goldens | integration (+ regravação de goldens isolada) | unit + integration | ✅ |
| T10 | Store de estado | unit | unit | ✅ |
| T11 | Store de estado (consulta portal via `PortalSource` mock) | unit | unit | ✅ |
| T12 | Store de estado | unit | unit | ✅ |
| T13 | Componente React | unit + integration | unit + integration | ✅ |
| T14 | Componente React | unit + integration | unit + integration | ✅ |
| T15 | Componente React (campo de projeção extraído para T30) | unit + integration | unit + integration | ✅ |
| T16 | Componente React | unit | unit | ✅ |
| T17 | Componente React (remoção) | unit | unit | ✅ |
| T18 | Componente React + map engine | unit | unit | ✅ |
| T19 | Store + componente | unit | unit | ✅ |
| T20 | Contrato de projeção (só API) | integration | integration | ✅ |
| T22 | Componente React | unit | unit | ✅ |
| T23 | Componente React | unit | unit | ✅ |
| T24 | Componente React | unit + integration | unit + integration | ✅ |
| T25 | Componente React | unit | unit | ✅ |
| T26 | Componente React | unit | unit | ✅ |
| T27 | Gate/OpenAPI/cenário | integration + architecture + scenario | integration + architecture + scenario | ✅ |
| T28 | Map engine (renderer/hitTest) + componente React | unit | unit | ✅ |
| T29 | Composition root (nenhuma camada de lógica nova) | — (gate de aceitação humana) | none, justificado | ✅ aprovado |
| T42-T49 | Domínio/API de integração; uma lacuna por task | unit + integration; golden/ADR onde indicado | unit + integration | ✅ |
| T30 | Contrato de projeção | integration | integration | ✅ |
| T31 | Fontes de dado reais + store | unit | unit + integration | ✅ |
| T32 | Fonte de dado real + `api.ts` | unit | unit | ✅ |
| T33 | Fonte de dado real | unit | unit | ✅ |
| T34 | Fonte de dado real + componente + contrato de projeção | unit + integration | unit + integration | ✅ |
| T40 | Renderer arquitetônico + terreno finito | unit | unit | ✅ |
| T41 | Creator procedural + interação espacial + componentes React | unit + integration | unit + integration | ✅ |

**Um único `Tests: none`** nesta fase: a **T29**, que é o checkpoint de aprovação visual do usuário —
ela não cria nem modifica camada de lógica (só o composition root escolhe qual fonte injetar), e toda
a cobertura automatizada do que ela demonstra já foi entregue em T0-T28. Toda outra task cria ou
modifica uma camada com tipo de teste exigido pela matriz.
