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
- **Phase / Task**: `WorldHost` implementado e endpoint `POST /worlds/create` funcionando; falta o formulário no cliente web.
- **Completed**:
  - Bugfix StrictMode (WS realtime) + `run.cmd`: já commitados em `50ff7f6` (STATE anterior estava desatualizado nesse ponto — não havia mais nada pendente de commit desses dois itens).
  - `WorldHost` (`src/LivingWorld.Simulation/WorldHost.cs`): wrapper mutável `{ WorldState Current; WorldClock Clock; void Replace(world, clock) }`, registrado como singleton único em `Program.cs` no lugar do antigo `AddSingleton(world)`.
  - `SimulationHost` (`src/LivingWorld.Simulation/SimulationHost.cs`) passou a receber `WorldHost` em vez de `WorldClock`+`WorldState` fixos — `FastForward` lê `host.Clock`/`host.Current` a cada chamada.
  - `RealtimeGateway`, `GET /npcs/{id}`, `ConversationEndpoints.MapConversationEndpoints`, `NarrativeEndpoints.MapNarrativeEndpoints` (todos em `src/LivingWorld.Api/`) pararam de capturar `world` por closure fixa — leem `host.Current`/`worldHost.Current` a cada request.
  - Endpoint novo `POST /worlds/create` (`src/LivingWorld.Api/WorldCreateEndpoints.cs`): recebe `{ ScenarioJson }`, chama `ScenarioLoaderV2.LoadWorld`, troca `host.Replace(world, clock)` e persiste na hora via `PersistentWorldRunner.Snapshot` (senão o host fica sem lastro no repositório entre o create e o próximo snapshot automático de 24 ticks).
  - `dotnet build`: 0 erros. `dotnet test --filter "Category!=Scenario"`: **1178 passed, 0 failed, 11 skipped** (baselines `ZZZ_record_*`), ~27 min. Duas rodadas anteriores pegaram regressão de DI (`WorldState` registrado `Scoped` quebrava 2 testes que resolvem `factory.Services.GetRequiredService<WorldState>()` direto da root provider — `CityProjectionEndpointTests`/`GlobalProjectionEndpointTests` na 1ª rodada por falta do registro, `NarrativeEndpointTests`/`ConversationEndpointTests` na 2ª por ele ser `Scoped`); resolvido trocando para `AddTransient` (resolve tanto de root quanto de scope, sempre lê `WorldHost.Current` na hora).
- **In-progress**: nenhum — feature "criar mundo" (backend) fechada e verificada.
- **Next step**: 1) Regerar tipos TS (`scripts/generate-web-types.sh`) pro OpenAPI incluir `POST /worlds/create`. 2) Menu de start no cliente web (criar / continuar / configurações) com formulário campo a campo (AD-001) pro scenario body, chamando o novo endpoint. 3) Prioridade menor, adiada explicitamente pelo usuário: grade visual 2D de verdade em `WorldMapView` (hoje é lista/texto, não canvas/SVG).
- **Blockers**: nenhum.
- **Uncommitted files** (build+teste verdes, prontos pra commit): `src/LivingWorld.Simulation/WorldHost.cs` (novo), `src/LivingWorld.Api/WorldCreateEndpoints.cs` (novo), `src/LivingWorld.Simulation/SimulationHost.cs`, `src/LivingWorld.Api/Program.cs`, `src/LivingWorld.Api/ConversationEndpoints.cs`, `src/LivingWorld.Api/NarrativeEndpoints.cs`, `tests/LivingWorld.Tests/WorldSnapshotTests.cs` (ctor do `SimulationHost` mudou).
- **Branch**: main
