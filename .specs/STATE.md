# STATE

## Decisions

### AD-001
- **Decision**: A tela de "criar mundo" do cliente web vai expor o body de cenário (`ScenarioLoaderV2`) como formulário campo a campo (não um textarea de JSON cru).
- **Reason**: Usuário pediu explicitamente formulário campo a campo, mesmo sendo mais trabalho de UI — prioriza usabilidade sobre velocidade de entrega.
- **Trade-off**: Formulário precisa acompanhar manualmente qualquer campo novo que `ScenarioLoaderV2`/`MapScenarioLoader`/`PopulationScenarioLoader`/etc. passem a exigir (um editor de JSON cru não teria esse risco de drift, mas foi descartado).
- **Scope**: Feature ad-hoc "criar mundo" (ainda sem `.specs/features/` própria) — cliente web (`web/src/**`) e o novo endpoint de criação de mundo na API.
- **Date**: 2026-08-06
- **Status**: active

## Handoff

- **Feature**: "Criar mundo" (ad-hoc, sem spec.md formal ainda — pedido direto do usuário em cima da fase 15 já fechada). Sem pasta em `.specs/features/`; decisões ficam aqui até (se) formalizar.
- **Phase / Task**: Backend + formulário web fechados e verificados ponta a ponta no browser real. Feature completa (exceto grade 2D, adiada).
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
- **In-progress**: nenhum.
- **Next step**: Prioridade menor, adiada explicitamente pelo usuário: grade visual 2D de verdade em `WorldMapView` (hoje é lista/texto, não canvas/SVG). Regenerar `web/src/generated/api-types.ts` já foi feito nesta sessão (inclui `/worlds/create`).
- **Blockers**: nenhum.
- **Uncommitted files**: nenhum pendente de decisão — tudo pronto pra commit (backend WorldHost, fix do upsert, formulário web completo, proxy do vite, testes).
- **Branch**: main
