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

## Handoff

- **Feature**: "Criar mundo" (ad-hoc, AD-001) — completa. "UX/tema visual" (ad-hoc, AD-002) — completa (start menu + tema global + settings placeholder).
- **Phase / Task**: Ambas as features desta sessão fechadas e verificadas ponta a ponta no browser real. Falta só a grade visual 2D (adiada explicitamente pelo usuário, ver abaixo).
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
- **In-progress**: nenhum.
- **Next step**: Único item restante do escopo desta fase: grade visual 2D de verdade em `WorldMapView` (hoje é lista/texto, não canvas/SVG) — adiada explicitamente pelo usuário, prioridade menor.
- **Blockers**: nenhum.
- **Uncommitted files**: nenhum pendente de decisão — tudo pronto pra commit (tema global, start menu, settings, navegação, testes).
- **Branch**: main
