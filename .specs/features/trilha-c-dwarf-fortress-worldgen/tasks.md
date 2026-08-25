# Trilha C — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path.

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Spec**: `.specs/features/trilha-c-dwarf-fortress-worldgen/spec.md`
**Status**: Draft
**Nota**: Fase 16/motor de poderes excluído desta trilha por decisão explícita do usuário —
ver `.specs/features/phase-16-1-power-engine/` (Design+Tasks já feitos, separadamente).

---

## Test Coverage Matrix

> Guidelines found: none dedicated — segue o mesmo padrão do repo (xUnit backend, Vitest
> frontend) como floor e target.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| API endpoints (avanço/navegação) | unit + integration | Happy path + years inválido + branch efêmero vs. raiz | `tests/LivingWorld.Tests/Api/**` | `dotnet test --filter "FullyQualifiedName~Simulation\|FullyQualifiedName~Worlds"` |
| Domain (`WorldEventKind` novos + geração) | unit | 1:1 a cada AC de WGN-20..23 | `tests/LivingWorld.Tests/History/**` | `dotnet test --filter "FullyQualifiedName~History"` |
| Frontend (`HistoryGeneration.tsx` novo) | unit (component) | Fluxo completo: avançar/retroceder/iniciar, feed renderiza eventos | `web/tests/HistoryGeneration.test.tsx` | `npx vitest run` |
| Frontend (integração com `MapView`/`map-engine`) | unit | Câmera livre renderiza mundo em geração sem duplicar engine | `web/tests/HistoryGeneration.test.tsx` (mesma suíte) | `npx vitest run` |
| Full regression | build gate | Backend+frontend verdes, sem regressão fora do escopo tocado | `tests/LivingWorld.Tests/**`, `web/tests/**` | `bash scripts/test.sh` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit (Domain/History) | Yes | Cada teste constrói seu próprio `WorldState` | Padrão já usado em `tests/LivingWorld.Tests/History/**` |
| integration (API endpoints) | Yes | `WebApplicationFactory`/cliente HTTP isolado por teste (já o padrão de `tests/LivingWorld.Tests/Api/**`) | Testes existentes de endpoint no repo |
| unit (Vitest component) | Yes | Render isolado por teste, sem estado global compartilhado | Padrão já usado em `web/tests/**` |
| build gate (`scripts/test.sh`) | No | Suíte completa sequencial, inclui `Category=Scenario` | Já documentado em `.specs/STATE.md` |

## Gate Check Commands

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick (backend) | Após task de domínio/endpoint | `dotnet test --filter "Category!=Scenario&FullyQualifiedName~History"` |
| Quick (frontend) | Após task de UI | `npx vitest run` |
| Full | Após fase que toca endpoint+branch (risco de regressão em "Continuar"/save) | `dotnet test --filter "Category!=Scenario"` |
| Build | Última task da feature | `bash scripts/test.sh` |

---

## Execution Plan

### Phase 1: Backend — avanço parametrizado (Sequential)

```
T1 → T2
```

### Phase 2: Backend — navegação escopada a branch efêmero (Sequential, depende de Phase 1)

```
T2 → T3 → T4
```

### Phase 3: Backend — eventos de civilização (Parallel OK, independente de Phase 1/2)

```
T5 [P], T6 [P], T7 [P]
```

### Phase 4: Frontend — tela "Gerar História" (Sequential, depende de Phase 1-3)

```
T8 → T9 → T10
```

---

## Task Breakdown

### T1: Endpoint de avanço parametrizado de anos

**What**: `POST /simulation/advance-years?count=N` (ou `/worlds/{id}/simulate-history?years=N`)
reusando `SimulationHost.FastForward` direto, validando `years>0`.
**Where**: `src/LivingWorld.Api/Simulation/SimulationEndpoints.cs` (novo endpoint ou modificado)
**Depends on**: None
**Reuses**: `SimulationHost.FastForward` (já roda N ticks headless)
**Requirement**: WGN-01, WGN-02

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] `years=N` avança exatamente N anos de tick, retorna novo ano/tick
- [ ] `years` ausente ou ≤0 rejeita com erro claro, não avança nada
- [ ] Gate: `dotnet test --filter "FullyQualifiedName~Simulation"` verde

**Tests**: integration
**Gate**: quick
**Commit**: `feat(api): add parametrized advance-years endpoint`

---

### T2: Chronicle reflete o avanço sem flush extra

**What**: Confirmar/ajustar `GET /narratives/chronicles` pra já refletir eventos do período
avançado por T1 sem chamada adicional.
**Where**: `src/LivingWorld.Api/Narratives/ChronicleEndpoints.cs` (verificado/ajustado se
necessário)
**Depends on**: T1
**Reuses**: `ChronicleGenerationSystem` (já existente)
**Requirement**: WGN-03

**Done when**:
- [ ] Avançar N anos e chamar o chronicle na sequência mostra eventos dentro da janela sem
      chamada de "flush"/refresh adicional

**Tests**: integration
**Gate**: quick
**Commit**: `test(api): confirm chronicle reflects advanced years without extra flush`

---

### T3: Endpoint de navegação (ir pro ano Y) sobre branch efêmero

**What**: `POST /worlds/{id}/history/goto-year` liga `PersistentWorldRunner.LoadAt(tick)` a um
`BranchId` efêmero dedicado — nunca o `BranchId.Root`.
**Where**: `src/LivingWorld.Api/Worlds/WorldHistoryEndpoints.cs` (novo)
**Depends on**: T1
**Reuses**: `PersistentWorldRunner.LoadAt`, `BranchId` (conceito já existente, nunca usado)
**Requirement**: WGN-10

**Done when**:
- [ ] "Ir pro ano Y" usa `LoadAt` sobre um branch efêmero, nunca o branch raiz
- [ ] `BranchId.Root` (o que "Continuar" lê) permanece intocado durante a navegação

**Tests**: integration
**Gate**: quick
**Commit**: `feat(api): add pre-game history navigation scoped to an ephemeral branch`

---

### T4: "Iniciar simulação" promove branch efêmero a save real

**What**: Endpoint/ação que torna o estado corrente do branch efêmero o save real
(`BranchId.Root`), descartando o restante do branch efêmero.
**Where**: `src/LivingWorld.Api/Worlds/WorldHistoryEndpoints.cs` (modificado)
**Depends on**: T3
**Reuses**: mesma infra de persistência (`SqliteWorldRepository`)
**Requirement**: WGN-11, WGN-12

**Done when**:
- [ ] "Iniciar simulação" no ano corrente vira o save real a partir dali
- [ ] Fluxo linear (nunca navegou pro passado) continua idêntico a hoje — nenhuma regressão
- [ ] Gate: `dotnet test --filter "Category!=Scenario"` verde (risco de regressão em "Continuar")

**Tests**: integration
**Gate**: full
**Commit**: `feat(api): promote ephemeral history branch to the real save on simulation start`

---

### T5: `WorldEventKind.CivilizationFounded` [P]

**What**: Novo kind + gatilho reusando o limiar já existente de fundação/assentamento.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (novo valor),
sistema que já dispara fundação (modificado pra também logar o novo kind quando aplicável)
**Depends on**: None
**Reuses**: `SettlementFoundingSystem`/limiar de fundação já existente
**Requirement**: WGN-20

**Done when**:
- [ ] Cidade que atinge o critério já existente de "vira civilização" loga o novo kind
- [ ] `ChronicleGenerationSystem` narra o evento com a mesma qualidade dos existentes

**Tests**: unit
**Gate**: quick
**Commit**: `feat(history): add CivilizationFounded world event kind`

---

### T6: `WorldEventKind.War` [P]

**What**: Novo kind pra conflito sustentado entre cidades/civilizações, reusando qualquer
sinal de conflito já existente no motor (não reinventa combate).
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (novo valor), sistema que já
detecta conflito econômico/território (modificado)
**Depends on**: None
**Reuses**: sinal de conflito já existente (ou `WorldEventKind.CombatResolved` se a spec do
motor de poderes já tiver entregue essa peça no momento da implementação)
**Requirement**: WGN-21

**Done when**:
- [ ] Conflito sustentado entre duas cidades/civilizações loga o novo kind
- [ ] Chronicle narra o evento

**Tests**: unit
**Gate**: quick
**Commit**: `feat(history): add War world event kind`

---

### T7: `WorldEventKind.DynastyRise` [P]

**What**: Novo kind pra mudança de linhagem/família governante.
**Where**: `src/LivingWorld.Domain/History/WorldEventKind.cs` (novo valor), sistema de
governança/liderança já existente (modificado)
**Depends on**: None
**Reuses**: qualquer conceito de liderança/família já rastreado (household/relacionamento)
**Requirement**: WGN-22, WGN-23

**Done when**:
- [ ] Mudança de linhagem governante loga o novo kind
- [ ] Chronicle narra os 3 kinds novos (T5/T6/T7) com qualidade determinística consistente

**Tests**: unit
**Gate**: quick
**Commit**: `feat(history): add DynastyRise world event kind`

---

### T8: Tela "Gerar História" — esqueleto e navegação de fluxo

**What**: Novo componente `HistoryGeneration.tsx` inserido entre `PresetStart.tsx` e
`WorldEditor.tsx`; contador de ano, botões avançar/retroceder N anos (chama T1/T3).
**Where**: `web/src/components/HistoryGeneration.tsx` (novo), `web/src/App.tsx` (roteamento,
modificado)
**Depends on**: T1, T3
**Reuses**: padrão de fluxo já usado entre `PresetStart`/`WorldEditor`
**Requirement**: WGN-30, WGN-31, WGN-32

**Done when**:
- [ ] Tela aparece entre preset e editor
- [ ] Avançar N anos atualiza contador e feed
- [ ] Retroceder pro ano Y reflete o estado daquele ano

**Tests**: unit (component)
**Gate**: quick (frontend)
**Commit**: `feat(web): add HistoryGeneration screen with year advance/rewind`

---

### T9: Feed de texto + câmera livre sobre o mapa

**What**: Feed scrollável (chronicles via `/narratives/chronicles`, linha por evento,
timestamp em anos) + `MapView`/`map-engine` reusado pra câmera livre sobre o mundo em geração.
**Where**: `web/src/components/HistoryGeneration.tsx` (modificado, integra `MapView`)
**Depends on**: T8, T2, T5, T6, T7 (feed só tem substância real depois dos kinds novos)
**Reuses**: `MapView`/`map-engine` (nenhuma engine de render duplicada)
**Requirement**: WGN-33

**Done when**:
- [ ] Feed mostra eventos novos (incluindo os 3 kinds de civilização) em ordem cronológica
- [ ] Câmera livre sobre o mapa reusa o mesmo `MapView` do jogo, sem código de render duplicado

**Tests**: unit (component)
**Gate**: quick (frontend)
**Commit**: `feat(web): wire chronicle feed and free camera into HistoryGeneration`

---

### T10: Botão "Iniciar simulação"

**What**: Fecha a tela de história e entra no jogo real a partir do ano corrente (chama T4).
**Where**: `web/src/components/HistoryGeneration.tsx` (modificado), `web/src/App.tsx`
(transição pro editor/jogo, modificado)
**Depends on**: T9, T4
**Reuses**: transição já existente `WorldEditor`→jogo
**Requirement**: WGN-34

**Done when**:
- [ ] Clicar "Iniciar simulação" fecha a tela e o jogo começa no ano exibido
- [ ] Gate final: `bash scripts/test.sh` verde (backend+frontend)

**Tests**: unit (component)
**Gate**: build
**Commit**: `feat(web): add Start Simulation action promoting history branch to real save`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2

Phase 2 (Sequential, depends on Phase 1):
  T2 ──→ T3 ──→ T4

Phase 3 (Parallel, independent of Phase 1/2):
  T5 [P], T6 [P], T7 [P]

Phase 4 (Sequential, depends on Phase 1-3):
  T8 ──→ T9 ──→ T10
    (T9 also depends on T2, T5, T6, T7)
    (T10 also depends on T4)
```

4 phases — at or below the skill's >3-phase sub-agent trigger threshold is borderline (4 > 3);
Execute will offer per-phase delegation, but given the tight sequential coupling (Phase 4 needs
all of 1-3 done), inline execution in one continuous pass is also reasonable to propose.

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1-T4 | 1 endpoint/behavior each | ✅ Granular |
| T5-T7 | 1 `WorldEventKind` + its trigger each | ✅ Granular |
| T8-T10 | 1 component concern each (skeleton, feed+camera, start action) | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T1 | T2→T3 (Phase 2 chain) | ✅ Match |
| T4 | T3 | T3→T4 | ✅ Match |
| T5, T6, T7 | None | [P], independent | ✅ Match |
| T8 | T1, T3 | Phase 4 depends on Phase 1-3 | ✅ Match |
| T9 | T8, T2, T5, T6, T7 | noted under Phase 4 diagram | ✅ Match |
| T10 | T9, T4 | noted under Phase 4 diagram | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1-T4 | API endpoints | integration | integration | ✅ OK |
| T5-T7 | Domain (History) | unit | unit | ✅ OK |
| T8-T10 | Frontend component | unit (component) | unit | ✅ OK |
| T10 | Final task | build gate | build | ✅ OK |
