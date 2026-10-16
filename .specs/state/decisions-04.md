# Decisões 4/4

### AD-029
- **Decision**: Closeout Scenario da Fase 16.4 usa horizonte de **10 anos**, não 100.
  100 anos permanece no objetivo #1 do roadmap (`LifeTable*` / Scenario de população),
  não é pré-requisito de fechamento desta fase.
- **Reason**: Usuário cancelou a rodagem de ~864k ticks (~2h+) por custo; 10 anos cobrem
  várias estações, cold-archive e variação ecológica sem duplicar o gate do objetivo #1.
- **Trade-off**: Closeout 16.4 não prova sozinho multi-geração de século; isso continua
  nos testes de população de longo prazo já existentes.
- **Scope**: `.specs/features/phase-16-4-world-realism/` + `WorldRealismCloseoutTests`.
- **Date**: 2026-08-27
- **Status**: active

### AD-030
- **Decision**: Scenario de closeout da 16.4 (`Reference_scenario_ten_years_*`) usa regras de
  espécie **hunger-only** (ReproduceProbability/PredationProbability = 0), mesmo padrão do
  `ScaleScenarioFixture`. Repro/predação continuam cobertas pelos Independent Tests unitários.
- **Reason**: Com o default (repro ~0.12–0.18 + predação), a fauna sobe ao teto
  (`MaxAliveFauna=400`) e `TryPredate` fica O(n²)/hora — 10 anos travou ~6k+ s. Usuário
  cancelou; closeout precisa provar horizonte longo sem duplicar o custo O(n²).
- **Trade-off**: O Scenario longo não exercita dinâmica populacional predatória; isso já
  está nos testes Ecology focados (T5/T6).
- **Scope**: `WorldRealismCloseoutTests` + early-out em `FaunaLifecycleSystem` quando
  probabilidades são zero.
- **Date**: 2026-08-27
- **Status**: active

### AD-031
- **Decision**: Height/Weight/MuscleMass passam a consumir combate: ofensa via
  `BodyMechanic.CombatOffenseMultiplier` (= curva de MuscleMass/`WorkCapacityMultiplier`)
  no capacity do strike/round; dano recebido via `CombatDamageTakenMultiplier`
  (Weight+Height). Equipment compatibility permanece FUTURE_DEPENDENCY.
- **Reason**: Fechar FUTURE_DEPENDENCY do audit 16.3 (COH-25) agora que combate multi-round
  existe (16.4); reusa `BodyMechanic` em vez de fórmula paralela.
- **Trade-off**: Sem equipamento ainda — só sizing corporal no Resolve/Damage.
- **Scope**: `BodyMechanic`, `CombatMechanic`, `CombatEncounterSystem`.
- **Date**: 2026-08-27
- **Status**: active

### AD-032
- **Decision**: `phase-16-3-web` — World Map redesign (novo doc "World Map Visual & Spatial Design"), Fases 1+2+4 do doc (terreno procedural/rios/estradas/footprints reais + agents como pontos vivos + continuidade de câmera ao entrar num settlement/agent). Trocado o World View de SVG declarativo (`map/SemanticZoomMap.tsx`, `IsoProjection.ts`, `usePatrolPosition.ts` — todos REMOVIDOS, sem consumidor sobrando) por `render/WorldStage.tsx`, um segundo renderer Pixi.js dedicado (mesma linguagem do `SettlementStage`, AD-020) na escala do mundo inteiro. Continuidade World↔Settlement/Agent implementada como DOIS renderers separados (decisão explícita do usuário, unificar os dois numa Pixi App/câmera só é escopo maior que fica pra outra rodada) + uma animação de ponte: `WorldStage` dá zoom na entidade clicada (câmera lerp até ~3.2x, `TRANSITION_MIN_MS`) ANTES de chamar `onSelectSettlement`/`onSelectNpc` (troca de rota), em vez de cortar direto pra `SettlementStage`. Follow/Inspector têm paridade completa com o Settlement (mesmo `followStore.activeFollowId()`/`detachCamera()`, mesmo anel de "seguindo", clique em settlement/agent abre Inspector do jeito que já abria).
- **Decisão de dado — posição absoluta**: usuário revelou que o BACKEND real usa X/Y absoluto único pro mundo inteiro (um NPC dentro de casa ocupa uma posição específica do mapa mundi, sem separação mundo/cidade/casa como o frontend tem hoje). Fixture desta demo continua hierárquica (settlement.gridPosition em unidades de mundo; building/patrol/indoorLocation em unidades LOCAIS de settlement, precisa pro `SettlementStage` desenhar) — mas `map/worldPosition.ts` (novo) é agora o ÚNICO lugar que resolve as duas escalas pra uma posição absoluta única (`agentWorldPosition(fixture, agent, now)`), a fronteira exata onde plugar uma API real que já mande X/Y absoluto sem precisar tocar em quem consome (`WorldStage` já chama só essa função, nunca lê `gridPosition`/`patrolPoints` direto).
- **Reason**: pedido explícito do usuário ("vamos implementar agora o mapa mundi"), com dois requisitos centrais: (1) mesma animação de continuidade que já existe pra entrar num prédio, aplicada a entrar numa cidade a partir do mapa mundi; (2) toda funcionalidade de NPC (seguir, inspecionar) precisa funcionar igual no mapa mundi. Confirmado via pergunta direta: dois renderers + animação de ponte (não unificação total, mais rápido de entregar) e Fases 1+2+4 do doc (não clima/estações/histórico, doc já diz "só depois").
- **Trade-off**: continuidade não é uma câmera matematicamente contínua de verdade (dois renderers/coordenadas diferentes) — é uma animação de zoom convincente seguida de uma troca de componente, não um único frame ininterrupto. Terreno/rio/florestas são procedurais determinísticos (mesma técnica de `settlementLayout.ts`), não dado canônico de nenhum sistema. `agentWorldPosition` usa a posição do PRÉDIO (não o offset exato do cômodo) pra agents indoors — no LOD do mapa mundi essa diferença é imperceptível, não duplica a transform de interior do `SettlementStage`.
- **Scope**: `web-demo/src/render/{WorldStage,worldLayout}.tsx`, `web-demo/src/map/worldPosition.ts` (novos); `web-demo/src/components/CenterStage.tsx` (troca de renderer na rota "world"); `web-demo/src/map/isoPalette.ts` (remove `TERRAIN_PALETTE` morto, `SETTLEMENT_PALETTE` agora reaproveitada); REMOVIDOS: `web-demo/src/map/{SemanticZoomMap,IsoProjection,usePatrolPosition}.tsx/ts` + testes correspondentes; testes novos/atualizados em `web-demo/tests/{render,map,components}/`.
- **Date**: 2026-08-27
- **Status**: active

### AD-033
- **Decision**: `phase-16-3-web` — três correções no World Map, achadas ao vivo em cima do AD-032: (1) agents ganham LOD de verdade (doc §29-32/§50) — ponto discreto de longe, sprite real (mesma textura do `SettlementStage`) a partir de `SPRITE_REVEAL_ZOOM` (2.2x), decidido uma vez por frame no ticker; (2) clicar um agent no mapa mundi vira seleção INSTANTÂNEA (sem zoom de transição) — só settlements continuam com a animação de "entrar"; `CenterStage` ganha `useSpatialScope` (mesma família de fix do `useFocusBuildingId`, memória via `useRef`) pra decidir se o mapa espacial continua no World ou pula pro Settlement do agent — só pula se a gente JÁ estava dentro de algum settlement quando selecionou, preservando o deep-link direto `/agent/:id` (mostra o settlement, comportamento antigo intacto); (3) `components/MapHoverCard.tsx` (novo) — card de hover LOD (menos informação que a sidebar cheia) ancorado perto do cursor (`event.clientX/Y` + offset), sem bloquear clique (`pointer-events:none`); ligado em settlement/agent do `WorldStage` E building/agent do `SettlementStage` (mesmo componente reusado nos dois renderers).
- **Reason**: usuário reportou 3 bugs testando o AD-032 ao vivo: agents deveriam ter LOD igual o doc pede (círculo de longe, sprite de perto — "idem pra carros/carruagens", mas a demo não tem fixture de veículo nenhum, então não aplicável ainda); clicar um NPC estava "abrindo a cidade" dele em vez de só selecionar (bug real de rota, mesma classe do bug do AD-025); pediu hover-popup estilo LOD pra cidade, e explicitamente "o mesmo comportamento para casas e NPCs" — estendido pro Settlement View também, não só o mapa mundi.
- **Trade-off**: hover no mouse real (pointerover/pointerout no Pixi) foi verificado só por teste (mock do Pixi + wiring), não pixel-a-pixel ao vivo num mouse de verdade — reaproveita a MESMA infra de eventos (`eventMode:"static"`) já validada ao vivo pro clique, risco residual considerado baixo. Sem veículos no fixture desta demo — o pedido "idem para carros, carruagens" fica sem o que renderizar até existir esse tipo de entidade no core.
- **Scope**: `web-demo/src/render/{WorldStage,SettlementStage}.tsx`, `web-demo/src/components/{CenterStage,MapHoverCard}.tsx` (novo), `web-demo/src/styles/tokens.css`, `web-demo/tests/{render,components}/`.
- **Date**: 2026-08-27
- **Status**: active

### AD-034
- **Decision**: `ScenarioLoaderV2` é o único loader de cenário. `ScenarioLoader` (v1, só mapa+população) foi removido; `scenarios/default.json` (mapa 20x20) e `scenarios/test-scifi.json` (mapa 40x40) migraram pro schema completo (blocos Economy/Cities/Dynamics). `PersistentWorldRunner` ganhou `ColdTierPersistence?` opcional (arquivo `<db>.coldtier`, só NPCs): salva em `Snapshot`, reidrata em `LoadLatest/LoadAt`; banco `:memory:` não persiste cold tier. `DecisionMetrics` e `DecisionSandbox` ficam (consumidor real só em teste, por desenho da Fase 16.3/28).
- **Reason**: auditoria `.specs/features/dead-code-audit/` — v1 sem consumidor de produção; cold tier da Fase 28 (T20) existia e era testado mas nenhum caminho real de save/resume o chamava, então restart perdia o arquivo frio.
- **Trade-off**: mapas maiores nos 2 cenários (10x10 não comportava o footprint dos workplaces autorados). Fauna/flora arquivados continuam só em memória (formato de disco cobre NPC). Golden/scale-sensor não foram regravados nesta rodada — o usuário confirma no gate.
- **Scope**: `src/LivingWorld.Simulation/Scenarios/`, `src/LivingWorld.Infrastructure/PersistentWorldRunner.cs`, Api/Workers `Program.cs`, `scenarios/*.json`.
- **Date**: 2026-10-10
- **Status**: active
