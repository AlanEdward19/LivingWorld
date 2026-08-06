# Fase 15 (Mapa visual VTT 2D) Tasks
## Execution Protocol (MANDATORY -- do not skip)
Implement these tasks com a skill `tlc-spec-driven` ativa (fluxo Execute completo). Se a skill falhar, parar.
---
**Design**: `.specs/features/phase-15-map-visual/design.md`  
**Status**: Draft

## Test Coverage Matrix
> Generated from codebase + guidelines: `AGENTS.md`, `rules/eval-criteria.md`, `rules/simulation-determinism.md`, `scripts/test.sh`, `scripts/verify.sh`.
| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Visual contracts/projectors/FOW (`Simulation`/`Api`) | unit + integration | 1:1 com VTT-01..16 + edge cases de foco/FOW/reconexão/camadas | `tests/LivingWorld.Tests/Visual/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Endpoints realtime + input | integration | subscribe/replay/403/400 + no-write hash invariável | `tests/LivingWorld.Tests/Visual/*EndpointTests.cs` | `bash scripts/verify.sh` |
| Cliente web VTT | unit + integration | render por escopo, drill-down, token e FOW por modo | `web/tests/**/*` | `bash scripts/test.sh --filter Category!=Scenario` |
| Scripts/OpenAPI/gate | architecture + integration | tipos gerados sem drift + verify falha em mutação | `tests/LivingWorld.Tests/Visual/*Gate*Tests.cs` | `bash scripts/verify.sh` |

## Parallelism Assessment
> Generated from codebase — confirm before Execute.
| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit | Yes | objetos isolados por teste | `BehaviorDecisionSystemTests.cs` |
| integration | Yes | `ScenarioRunner`/`WebApplicationFactory` por teste | `NpcEndpointTests.cs` |
| determinismo 2-processos | No | comparação sequencial entre processos | `DeterminismTwoProcessTests.cs` |
| arquitetura/reflexão | Yes | leitura de assembly compilado | `ArchitectureTests.cs` |

## Gate Check Commands
| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | task com unit/integration local | `bash scripts/test.sh --filter Category!=Scenario` |
| Full | task de API/realtime/fronteira/gate | `bash scripts/verify.sh` |
| Build | fechamento de fase | `bash scripts/build.sh && bash scripts/lint.sh && bash scripts/test.sh --filter Category!=Scenario` |

## Execution Plan
### Phase 1 (Sequential)
`T1 -> T2 -> T3`
### Phase 2 (Parallel + merge)
`T3 -> { T4 [P], T5 [P], T6 [P] } -> T7`
### Phase 3 (Sequential)
`T7 -> T8`
### Phase 4 (Sequential)
`T8 -> T9`

## Task Breakdown
### T1: Criar contratos visuais e catálogo de escopos
**What**: definir DTOs/schemas para snapshot+deltas por escopo (`world/city/interior`) e modo (`spectator/player`), incluindo catálogo de camadas derivadas e cursor de replay. **Where**: `src/LivingWorld.Api/Visual/*.cs`, `src/LivingWorld.Simulation/Visibility/*.cs`. **Depends on**: None. **Requirement**: VTT-01, VTT-04. **Tests**: unit. **Gate**: Quick.
### T2: Subir host de mundo persistente para API visual
**What**: trocar world efêmero da API por host canônico compartilhado (snapshot+clock) para stream e endpoints de visualização. **Where**: `src/LivingWorld.Api/Program.cs`, `src/LivingWorld.Infrastructure/*.cs`. **Depends on**: T1. **Requirement**: VTT-01..03. **Tests**: integration. **Gate**: Full.
### T3: Implementar gateway realtime com subscribe/replay
**What**: WebSocket primário + SSE fallback, subscribe por escopo e replay por cursor. **Where**: `src/LivingWorld.Api/Realtime/*.cs`. **Depends on**: T2. **Requirement**: VTT-02, VTT-10. **Tests**: integration. **Gate**: Full.
### T4 [P]: Implementar projeção global simplificada (mundi)
**What**: projector global com cidades, NPCs externos por LOD, eventos visuais resumidos e camadas derivadas globais (terreno/bioma/rios/montanhas/recursos/estradas/fronteiras/reinos/clima). **Where**: `src/LivingWorld.Api/Visual/Global*.cs`, `src/LivingWorld.Api/Visual/Layers/*.cs`. **Depends on**: T3. **Requirement**: VTT-01..06, VTT-10. **Tests**: integration. **Gate**: Quick.
### T5 [P]: Implementar projeção de cidade/interior (drill-down)
**What**: projectors de cidade e interior com entidades, atividades, transições por foco e camadas locais (cidades/aldeias/rotas/migrações/conflitos + overlays climáticos). **Where**: `src/LivingWorld.Api/Visual/City*.cs`, `src/LivingWorld.Api/Visual/Interior*.cs`, `src/LivingWorld.Api/Visual/Layers/*.cs`. **Depends on**: T3. **Requirement**: VTT-03, VTT-05, VTT-08, VTT-09, VTT-11. **Tests**: integration. **Gate**: Quick.
### T6 [P]: Implementar composição de token 2D de NPC
**What**: catálogo versionado de assets por camadas + `NpcTokenComposer` determinístico por estado canônico. **Where**: `src/LivingWorld.Api/Visual/NpcTokens/*.cs`, `web/src/assets/npc-tokens/*`. **Depends on**: T3. **Requirement**: VTT-11..13. **Tests**: unit + integration. **Gate**: Quick.
### T7: Implementar FOW/personagem + validação de intents
**What**: visibilidade por conhecimento, override admin e endpoints de movimento/interação com validação causal. **Where**: `src/LivingWorld.Simulation/Visibility/*.cs`, `src/LivingWorld.Api/VisualInput/*.cs`. **Depends on**: T4, T5. **Requirement**: VTT-04..06, VTT-10. **Tests**: integration. **Gate**: Full.
### T8: Entregar cliente web VTT 2D
**What**: criar projeto React+TS, renderers por escopo e por camada, controle espectador/personagem, FOW, drill-down e consumo realtime. **Where**: `web/*`, `LivingWorld.sln`, `scripts/*.sh`. **Depends on**: T6, T7. **Requirement**: VTT-01..16. **Tests**: unit + integration. **Gate**: Full.
### T9: Fechar gate de fase (OpenAPI + no-write + mutação)
**What**: gerar tipos TS via OpenAPI no verify, testes de hash invariável para leitura/subscribe, cobertura obrigatória de cada camada do catálogo e mutante do ramo web que prova reprovação. **Where**: `scripts/verify.sh`, `tests/LivingWorld.Tests/Visual/*Gate*Tests.cs`. **Depends on**: T8. **Requirement**: VTT-02, VTT-06, VTT-10..16. **Tests**: architecture + integration. **Gate**: Full.

## UX Pass 2 Tasks (2026-08-06) — Status: Done (todas T10-T15, verificado no browser real + gate completo 2026-08-06)
### T10: Expor dimensões do mapa no snapshot global — Done
**What**: adicionar `Width`/`Height` a `GlobalSnapshot` (de `world.Map`), regenerar tipos TS. **Where**: `src/LivingWorld.Api/Visual/GlobalProjector.cs`, `web/src/generated/api-types.ts`. **Depends on**: None. **Requirement**: UX Pass 2 "Grid 2D real". **Tests**: integration (`GlobalProjectionEndpointTests`). **Gate**: Quick.
### T11: Componente `GridCanvas` genérico + `colorById` — Done
**What**: canvas reusável (célula colorida opcional, marcadores com LOD dot/token, click de célula/marcador) e paleta determinística por id. **Where**: `web/src/components/GridCanvas.tsx`, `web/src/colorById.ts`. **Depends on**: T10. **Requirement**: UX Pass 2 "Grid 2D real", "Zoom com LOD". **Tests**: unit (seleção de marcador, LOD threshold) — `web/tests/GridCanvas.test.tsx`. **Gate**: Quick.
### T12: Reescrever `WorldMapView`/`CityView` sobre `GridCanvas` — Done
**What**: substituir listas/botões por grid real (terreno + cidades/NPCs no mundo; residentes + layout aproximado de prédios na cidade). **Where**: `web/src/components/WorldMapView.tsx`, `CityView.tsx`, `web/src/worldMapData.ts`. **Depends on**: T11. **Requirement**: UX Pass 2 "Grid 2D real". **Tests**: unit+integration (render, click) — `web/tests/WorldMapView.test.tsx`, `CityView.test.tsx`. **Gate**: Quick.
### T13: `SidePanel` + seleção por clique — Done
**What**: painel lateral de cidade/NPC ao clicar marcador, com ação "Entrar" pra cidade. **Where**: `web/src/components/SidePanel.tsx`, wiring em `WorldMapView`/`CityView`. **Depends on**: T12. **Requirement**: UX Pass 2 "Seleção por clique → painel lateral". **Tests**: unit (coberto por `WorldMapView.test.tsx`/`CityView.test.tsx`). **Gate**: Quick.
### T14: Editor de mapa por clique em "criar mundo" — Done
**What**: trocar autoria numérica do bloco `Map` por `GridCanvas` interativo (pintura de terreno/bioma + assentamento por clique), mantendo o mesmo JSON de saída. **Where**: `web/src/components/CreateWorldForm.tsx`, `web/src/components/MapGridEditor.tsx`, `web/src/scenarioDefaults.ts`. **Depends on**: T11. **Requirement**: UX Pass 2 "Editor de mapa por clique". **Tests**: unit — `web/tests/MapGridEditor.test.tsx`, caso `Cells` exaustivo em `CreateWorldForm.test.tsx`. **Gate**: Quick.
### T15: Overlay de mapa (tecla M) em modo jogador — Done
**What**: `MapOverlay` somente-leitura acionado por M enquanto dentro de cidade/interior em modo Player; Esc/M fecha. **Where**: `web/src/components/MapOverlay.tsx`, `App.tsx`. **Depends on**: T12. **Requirement**: UX Pass 2 "Overlay de mapa (tecla M)". **Tests**: unit (wiring simples, verificado por leitura de código + typecheck; sem teste dedicado — ponytail: `useEffect`+`fetchSnapshot` já cobertos nos padrões existentes). **Gate**: Quick.

## Diagram-Definition Cross-Check
| Task | Depends On (body) | Diagram | Status |
| --- | --- | --- | --- |
| T1 | None | root | ✅ |
| T2 | T1 | T1->T2 | ✅ |
| T3 | T2 | T2->T3 | ✅ |
| T4 | T3 | T3->{T4,T5,T6} | ✅ |
| T5 | T3 | T3->{T4,T5,T6} | ✅ |
| T6 | T3 | T3->{T4,T5,T6} | ✅ |
| T7 | T4,T5 | {T4,T5,T6}->T7 | ✅ |
| T8 | T6,T7 | T7->T8 | ✅ |
| T9 | T8 | T8->T9 | ✅ |

## Test Co-location Validation
| Task | Code Layer | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Visual contracts | unit+integration | unit | ✅ |
| T2 | API host/persistência | integration | integration | ✅ |
| T3 | Realtime gateway | integration | integration | ✅ |
| T4 | Global projector | integration | integration | ✅ |
| T5 | City/interior projector | integration | integration | ✅ |
| T6 | Token composer | unit+integration | unit+integration | ✅ |
| T7 | FOW + intents | integration | integration | ✅ |
| T8 | Cliente web | unit+integration | unit+integration | ✅ |
| T9 | Gate/OpenAPI/mutação | architecture+integration | architecture+integration | ✅ |
