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
- **Phase / Task**: Investigação concluída (ver achados abaixo), nenhuma implementação de "criar mundo" começou ainda.
- **Completed**:
  - Bugfix: WS realtime quebrava em dev por causa do `<StrictMode>` (double-mount abria 2 WebSockets, o segundo derrubava o proxy de WS do Vite com `write ECONNABORTED`). Removido `StrictMode` de `web/src/main.tsx`, testado no browser (reconecta limpo, sem "conexão realtime falhou"). **Não commitado ainda.**
  - `run.cmd` criado na raiz (sobe API + web cada um em janela própria). **Não commitado ainda** (untracked).
  - Investigação completa do que falta pra "criar mundo": `ScenarioLoaderV2.LoadWorld(json)` (`src/LivingWorld.Simulation/ScenarioLoaderV2.cs:16`) já existe e monta `(WorldState, WorldClock)` a partir de um JSON de cenário completo (mapa+população+comportamento+economia+cidades+dynamics — ver `tests/LivingWorld.Tests/Periods/ScenarioLoaderV2Tests.cs:9-64` pro exemplo `FullValidRoot()`). Mas **nada hoje troca a instância de `WorldState` do processo em runtime** — `Program.cs` registra `world` como singleton de DI fixo no `app.Build()`, e vários lugares capturam `world` por closure direta (`RealtimeGateway`'s `Func<long> currentTick`, `MapConversationEndpoints`, `MapNarrativeEndpoints`, o handler de `GET /npcs/{id}`). Pra "criar mundo" funcionar, precisa de um wrapper mutável (`WorldHost { WorldState Current; }` como singleton + `AddScoped<WorldState>(sp => host.Current)`), e `ConversationEndpoints.cs`/`NarrativeEndpoints.cs` precisam parar de receber `WorldState` fixo por parâmetro do método de extensão e passar a resolver via DI por request. `POST /worlds/start` já existe (`src/LivingWorld.Api/WorldStartEndpoints.cs:18`) mas só aceita `{PeriodId, Seed}` de um template já cadastrado — não aceita scenario JSON cru, e nem troca o `world` do processo (só devolve `NpcCount`).
- **In-progress**: nenhum código de "criar mundo" escrito ainda — só a investigação acima.
- **Next step**: implementar o `WorldHost` (wrapper mutável) primeiro — sem ele, nenhum endpoint de criar mundo tem efeito visível. Depois: endpoint `POST` que aceita o scenario JSON, chama `ScenarioLoaderV2.LoadWorld`, e troca `host.Current` (+ persiste via `PersistentWorldRunner.Snapshot` pra "continuar" funcionar depois de reiniciar o processo). Depois: menu de start no cliente (criar / continuar / configurações) com formulário campo a campo (AD-001) pro scenario body. Depois (prioridade menor, adiada explicitamente pelo usuário): grade visual 2D de verdade em `WorldMapView` (hoje é lista/texto, não canvas/SVG).
- **Blockers**: nenhum técnico — só pausado a pedido do usuário (vai desligar o computador).
- **Uncommitted files**: `web/src/main.tsx` (fix do StrictMode, testado e funcionando), `run.cmd` (novo, untracked).
- **Branch**: main
