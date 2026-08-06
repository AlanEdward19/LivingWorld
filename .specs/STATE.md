# STATE

## Decisions

### AD-001
- **Decision**: A tela de "criar mundo" do cliente web vai expor o body de cenário (`ScenarioLoaderV2`) como formulário campo a campo (não um textarea de JSON cru).
- **Reason**: Usuário pediu explicitamente formulário campo a campo, mesmo sendo mais trabalho de UI — prioriza usabilidade sobre velocidade de entrega.
- **Trade-off**: Formulário precisa acompanhar manualmente qualquer campo novo que `ScenarioLoaderV2`/`MapScenarioLoader`/`PopulationScenarioLoader`/etc. passem a exigir (um editor de JSON cru não teria esse risco de drift, mas foi descartado).
- **Scope**: Feature ad-hoc "criar mundo" (ainda sem `.specs/features/` própria) — cliente web (`web/src/**`) e o novo endpoint de criação de mundo na API.
- **Date**: 2026-08-06
- **Status**: active

### AD-002
- **Decision**: Tela inicial (start menu) estilo jogo — botões centrais (Continuar/Criar mundo/Configurações) sobre fundo animado — com motivo visual deliberadamente atemporal (campo de partículas à deriva), não medieval nem preso a nenhuma época.
- **Reason**: Usuário pediu estilo "Minecraft" de menu inicial, mas corrigiu que o projeto simula qualquer período de tempo (não só medieval) — iconografia de época específica ficaria errada.
- **Trade-off**: Sem CSS/design system prévio no cliente (era HTML puro sem estilo); criado `web/src/styles/global.css` com estilos por seletor de elemento (não por classe) pra herdar em todos os componentes existentes sem reescrevê-los.
- **Scope**: UX geral do cliente web (fase 15) — tema visual, menu inicial, tela de configurações placeholder.
- **Date**: 2026-08-06
- **Status**: active

### AD-003
- **Decision**: Grid 2D real (canvas) substitui listas/botões no mapa-múndi/cidade; NPCs viram token/dot por LOD de zoom; seleção por clique abre painel lateral; editor de mapa em "criar mundo" também vira grid clicável; overlay de mapa (tecla M) em modo jogador. Token/terreno usam cor procedural determinística por id — não há pipeline de arte (pixel-art/ilustrado) no projeto.
- **Reason**: Usuário rejeitou a entrega textual do T8 original ("nada batendo com o que eu esperava") e trouxe referências de VTT ilustrado — cor procedural é o teto realista sem um pipeline de assets.
- **Trade-off**: Prédios não têm `CellCoord` no domínio — layout em anel calculado no cliente (aproximado, marcado visualmente, não é posição real). Movimento "andar até a saída" pra trocar de escopo mundo↔cidade não foi construído (exigiria sistema de movimento em escala mapa-múndi que não existe) — mantido botão/painel de drill-down.
- **Scope**: Fase 15, UX Pass 2 — ver `.specs/features/phase-15-map-visual/spec.md` (seção "UX Pass 2"), `design.md` e `tasks.md` (T10-T15), atualizados antes da implementação a pedido do usuário.
- **Date**: 2026-08-06
- **Status**: active

## Handoff

- **Feature**: "Criar mundo" (AD-001) — completa. "UX/tema visual" (AD-002) — completa. "Grid 2D real / VTT" (AD-003) — completa (T10-T15).
- **Phase / Task**: As três features desta sessão fechadas e verificadas ponta a ponta no browser real. Fase 15 sem pendência conhecida além das limitações registradas em AD-003 (prédios sem posição real, sem movimento mundo-múndi).
- **Completed**:
  - Bugfix StrictMode (WS realtime) + `run.cmd`: já commitados em `50ff7f6`.
  - `WorldHost` (`src/LivingWorld.Simulation/WorldHost.cs`): wrapper mutável `{ WorldState Current; WorldClock Clock; void Replace(world, clock) }`, singleton único em `Program.cs`.
  - `SimulationHost`/`RealtimeGateway`/`GET /npcs/{id}`/`ConversationEndpoints`/`NarrativeEndpoints`: pararam de capturar `world` por closure fixa, leem `host.Current` por chamada.
  - `POST /worlds/create` (`src/LivingWorld.Api/WorldCreateEndpoints.cs`): `{ ScenarioJson }` → `ScenarioLoaderV2.LoadWorld` → `host.Replace` → `PersistentWorldRunner.Snapshot`.
  - **Bug real encontrado e corrigido durante verificação no browser**: `SqliteWorldRepository.SaveSnapshotWithEvents` (`src/LivingWorld.Infrastructure/SqliteWorldRepository.cs`) sempre fazia `context.Snapshots.Add(...)` — como `worldRepository` é um único `DbContext` de vida longa (`Program.cs`), criar um mundo novo (que começa no tick 0, igual ao snapshot de bootstrap já rastreado em memória pro mesmo `BranchId.Root`) colidia na identity map do EF (`InvalidOperationException` no `POST /worlds/create`, 500). Corrigido pra upsert via `context.Snapshots.Find(branch, tick)` antes de decidir Add vs. atualizar in-place.
  - **Formulário web exaustivo** (`web/src/components/CreateWorldForm.tsx` + `web/src/scenarioDefaults.ts` + `web/src/components/formFields.tsx`): cobre TODOS os campos de Map/Population/Behavior/Economy/City/Dynamics (inclusive dicts dinâmicos — recipes, wages, workplaces, cities, vieses, regras de transformação — via editores genéricos `KeyNumberListEditor`/`ObjectListEditor` com add/remove). Dicts aninhados dentro de uma linha (Inputs/Outputs/Stock/Prices de receita) usam texto compacto `"resId:qtd,..."` em vez de mais um nível de editor genérico — ponytail: nível de aninhamento a mais não valia o código extra, ainda é campo-a-campo no nível de linha. `web/src/api.ts` ganhou `createWorld()`. `App.tsx`: botão "Criar mundo" no header alterna pro formulário; `onCreated` fecha o formulário e volta pro mapa.
  - `web/vite.config.ts`: proxy `/worlds` adicionado (só tinha `/visual`) — sem isso o dev server nunca alcançava a API real.
  - Verificado no browser real (Vite+API rodando lado a lado): formulário renderiza todas as seções, submit faz `POST /worlds/create` → `200 OK`, mundo troca e a UI volta pro mapa.
  - Testes: `web/tests/CreateWorldForm.test.tsx` (3 casos: JSON PascalCase completo, edição de campo refletida, callback `onCreated`) — `npx vitest run`: **20/20 passed** (7 arquivos). Backend: `dotnet test --filter "Category!=Scenario"` após o fix do upsert: **1178 passed, 0 failed, 11 skipped**, ~24 min.
  - **Tema visual global** (`web/src/styles/global.css`, importado em `main.tsx`): paleta escura + dourado/âmbar, tipografia serifada pra títulos, painéis translúcidos com borda sutil, botões com glow no hover. Estilizado por seletor de elemento (`button`, `input`, `fieldset`, `[data-testid="world-map-view"]` etc.) — nenhum componente existente precisou de className novo.
  - **`StartMenu`** (`web/src/components/StartMenu.tsx`): tela inicial com canvas de partículas à deriva (drift + twinkle, `requestAnimationFrame`, sem lib) atrás de título/subtítulo/3 botões com fade-in escalonado (animation-delay por botão).
  - **`SettingsView`** (`web/src/components/SettingsView.tsx`): placeholder simples (decisão do usuário) — só mostra a URL da API atual + "mais opções em breve".
  - **`App.tsx`**: estado `screen: "start" | "world" | "settings"`, default `"start"`. "Continuar"/"Criar mundo" vão pra `"world"` (Criar mundo já abre o formulário); "Configurações" vai pra `"settings"`; header ganhou botão "☰ menu" pra voltar ao início.
  - **`useRealtimeSnapshot`** ganhou parâmetro `enabled` (default `true`) — `App.tsx` só conecta o WebSocket quando `screen === "world"`, não mais assim que o componente monta (evita abrir socket parado na tela inicial).
  - `web/tests/setup.ts`: stub de `HTMLCanvasElement.prototype.getContext` (jsdom não implementa canvas — sem o stub, todo teste logava "Not implemented" no stderr).
  - Testes novos: `web/tests/StartMenu.test.tsx` (3 handlers de botão), `web/tests/App.test.tsx` (navegação start→settings→start). `npx tsc --noEmit`: limpo. `npx vitest run`: **22/22 passed** (8 arquivos).
  - Verificado no browser real: menu inicial renderiza com canvas/animação, paleta/fontes aplicadas (checado via `getComputedStyle`), navegação Continuar→mapa→menu→Configurações funciona, nenhum WS abre antes de "Continuar".
  - **Grid 2D real (AD-003)**: `GlobalSnapshot` ganhou `Width`/`Height` (`src/LivingWorld.Api/Visual/GlobalProjector.cs`, de `world.Map`) — sem impacto em hash/goldens (campo de projeção API, não canônico). `web/src/components/GridCanvas.tsx`: canvas genérico (célula colorida opcional, marcadores com LOD dot↔token por zoom, hit-test de clique) reusado por mapa-múndi, cidade e editor de mapa. `web/src/colorById.ts`: cor determinística por id (ângulo áureo em HSL) — sem semântica "grama"/"deserto", o domínio só tem ids. `web/src/components/SidePanel.tsx`: painel lateral de info+ação ao clicar marcador.
  - `WorldMapView.tsx`/`CityView.tsx` reescritos sobre `GridCanvas`: mundo mostra terreno real (camada Terrain) + rios (overlay) + cidades/NPCs plotados na posição real; cidade mostra moradores na posição real (relativa ao centro) + prédios num layout de anel client-side (aproximado, sem posição real no domínio — `Building` não tem `CellCoord`, nota visual "posição no mapa é layout aproximado").
  - `web/src/components/MapGridEditor.tsx`: substitui a autoria numérica do bloco `Map` em `CreateWorldForm` — clique pinta terreno/água ou adiciona/remove assentamento. `scenarioDefaults.ts` ganhou `cells: Record<string,PaintedCell>` e `buildCells()`: só emite `Cells` (exaustivo sobre Width×Height, células não pintadas usam terreno/bioma default) quando o usuário pintou algo — sem pintura nenhuma, mapa continua 100% procedural como antes.
  - `web/src/components/MapOverlay.tsx`: tecla M abre mapa-múndi somente-leitura (`fetchSnapshot` uma vez, não realtime) enquanto em modo Jogador dentro de cidade/interior; M/Esc fecha. Wiring em `App.tsx` (`canOpenMapOverlay`, listener de keydown).
  - Testes novos: `GridCanvas.test.tsx` (5), `MapGridEditor.test.tsx` (3) — `WorldMapView.test.tsx`/`CityView.test.tsx`/`App.test.tsx`/`CreateWorldForm.test.tsx` reescritos pra clique-em-canvas (helper `getBoundingClientRect` stub) em vez de clique em texto/botão de lista. `npx tsc --noEmit`: limpo. `npx vitest run`: **32/32 passed** (10 arquivos).
  - Verificado no browser real: grid do mundo renderiza cores de terreno reais (confirmado via `getImageData`, cores variam por célula), zoom in/out funciona, editor de mapa em "criar mundo" pinta célula visualmente e o `POST /worlds/create` com `Cells` autorado retorna 200 e o novo terreno aparece no mapa-múndi.
  - Docs: `.specs/features/phase-15-map-visual/spec.md`/`design.md`/`tasks.md` atualizados com a seção "UX Pass 2" (T10-T15) **antes** da implementação, a pedido do usuário.
- **In-progress**: nenhum.
- **Next step**: Nenhum item obrigatório pendente. Melhorias futuras registradas em AD-003/spec.md "Open Questions": posição real de prédio no domínio (exigiria campo canônico novo + reserva de golden hashes), movimento validado em escala mundo-múndi (andar até a saída da cidade).
- **Blockers**: nenhum.
- **Uncommitted files**: nenhum pendente de decisão — tudo pronto pra commit (backend Width/Height, GridCanvas/colorById/SidePanel/MapOverlay/MapGridEditor, WorldMapView/CityView reescritos, testes, docs de fase 15).
- **Branch**: main
