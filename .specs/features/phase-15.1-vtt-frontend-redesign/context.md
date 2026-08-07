# Fase 15.1 (Redesign do frontend VTT) Context

Decisões e lacunas verificadas que restringem o design. Tudo aqui foi conferido no código em
2026-08-06 — nenhuma afirmação é herdada da Fase 15 sem reverificação.

---

## Decisões travadas pelo usuário (2026-08-06)

As quatro perguntas em aberto da spec foram resolvidas antes do início da implementação. São
restrições, não sugestões:

| # | Decisão | Camada | Hash/goldens |
| --- | --- | --- | --- |
| OQ-1 | Footprint de cidade/prédio é **projeção derivada na API** (mesmo padrão de `GlobalSnapshot.Width/Height`). `LivingWorld.Domain` intocado | `LivingWorld.Api/Visual` | Nenhum impacto |
| OQ-2 | `SpatialPortal` vira **conceito canônico real em `LivingWorld.Domain`** — entradas/saídas nomeadas como dado descritivo. Escolha explícita da opção maior | **`LivingWorld.Domain` + `WorldState`** | **Goldens regravados** |
| OQ-3 | Construir o **tick loop** e os endpoints de controle de tempo sobre `SimulationHost`; a poda do log do `RealtimeGateway` é dependência obrigatória | `LivingWorld.Api` + hospedeiro | Nenhum impacto |
| OQ-4 | **Remover a superfície de Player Mode do cliente** (seletor, `PlayerMoveControls`, WASD, tecla M); backend intocado para a Fase 25 | `web/src` (só remoção) | Nenhum impacto |

Duas decisões de design não questionadas e mantidas: inspector lê os campos do snapshot por padrão
(detalhe completo só sob "Ver detalhes", para não disparar a materialização de
`NpcInspectionQuery.Inspect`), e Canvas 2D em vez de PixiJS.

---

## Baseline verificado — o que existe hoje

| Área | Estado real | Evidência |
| --- | --- | --- |
| Câmera | **Não existe.** Zoom só por botões `+`/`−`; sem wheel, sem drag, sem offset de viewport. O canvas é dimensionado pelo mundo inteiro (`width*zoom × height*zoom`) e redesenhado por completo a cada mudança | `web/src/components/GridCanvas.tsx:64-65,161-178`; `useEffect` de desenho em `:61-131` |
| LOD | Binário `dot ↔ token`, limiar default 18, passado como prop por view; sem nível agregado, sem gerência central | `web/src/components/GridCanvas.tsx:36,50,59,111-130` |
| Separação de estado | **Entrelaçada.** `focus` (view) e `mode` vivem no `App`; a câmera vive dentro de cada view e morre na navegação; a seleção é estado local duplicado em duas views; o estado de simulação entra direto no corpo do `App`, re-renderizando o subtree inteiro | `web/src/App.tsx:22-26,45-52`; `WorldMapView.tsx:20,23`; `CityView.tsx:26,29` |
| Transporte realtime | Primeiro frame = snapshot; **qualquer** frame seguinte dispara um refetch HTTP completo do snapshot do escopo | `web/src/hooks/useRealtimeSnapshot.ts:39-48` |
| Clique | Sempre seleciona **e** o painel oferece "Entrar" (navegação misturada à inspeção). Sem double click, sem Esc, sem highlight da entidade selecionada | `web/src/components/WorldMapView.tsx:52-62`; `CityView.tsx:79-101`; `SidePanel.tsx:10-25` |
| Inspector | Painel lateral genérico com título + conteúdo + 1 ação; conteúdo de cidade = 2 campos, de NPC = 2 campos | `web/src/components/SidePanel.tsx`; `WorldMapView.tsx:64-67`; `CityView.tsx:87-90` |
| Camadas na UI | Só legenda informativa (nome + "disponível"/"ainda não modelada"); nenhum toggle. Terrain e Rivers são hardcoded no render | `web/src/components/LayerLegend.tsx:18-24`; `WorldMapView.tsx:46-47` |
| Controle de tempo | `SimulationHost.Pause/Resume/SetSpeed/FastForward` **existem** e são explicitamente estado de hospedeiro fora de `WorldState`/snapshot/hash. Mas **nenhum endpoint HTTP os expõe** — o objeto só é registrado no DI e nunca mapeado | `src/LivingWorld.Simulation/SimulationHost.cs:3-4,10-22`; `src/LivingWorld.Api/Program.cs:55,85` (registro), sem `Map*` correspondente |
| Loop de tick | **Não existe.** Decisão documentada no próprio `Program.cs`: "este host ainda não ticka automaticamente ... tick em tempo real fica para uma task futura". O único `RealtimeGateway.Publish` do sistema é disparado pelo POST de movimento manual | `src/LivingWorld.Api/Program.cs:52-54`; grep `\.Publish(` → apenas `src/LivingWorld.Api/VisualInput/VisualInputEndpoints.cs:32` |
| Posição de cidade | Ponto único (`CellCoord`), sem largura/altura/bounds | `src/LivingWorld.Domain/Cities/City.cs:10-14` |
| Posição de prédio | **Inexistente** — `Building` tem só `Id`, `City`, `BuildingTypeId`, `CompletedAtTick` | `src/LivingWorld.Domain/Cities/Building.cs:6-12` |
| Eixo Z | **Não existe.** `CellCoord` é `readonly record struct CellCoord(int X, int Y)` | `src/LivingWorld.Domain/Geography/GeographyIds.cs:5` |
| Transição entre espaços | Nenhum conceito nomeado. Grep por `portal|entrance|gateway|doorway|transition` em `src/LivingWorld.Domain` e `src/LivingWorld.Simulation`: **zero ocorrências**. A "entrada" é o cliente trocando `focus` e reassinando outro escopo | `web/src/App.tsx:126,134`; `src/LivingWorld.Api/Visual/VisualScope.cs:13-19` |
| Dados disponíveis para inspector de cidade | `CityPopulationQuery` já calcula População, Riqueza, Saúde, Desigualdade (Gini), Economia e Habitação — nada disso chega ao cliente hoje | `src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-53` |
| Dados disponíveis para inspector de NPC | `NpcInspectionQuery.Inspect` monta identidade, idade, cultura, cidade, household, pais, cônjuge, profissão, empregador, saúde, 4 needs, personalidade, skills, posição e ação — o cliente mostra 2 campos | `src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs:38-44`; exposto em `src/LivingWorld.Api/Program.cs:103-107` |

---

## Lacunas herdadas da Fase 15 — reverificadas

Cada item de `.specs/features/phase-15-map-visual/context.md` foi reconferido contra o código atual
e classificado quanto ao que a Fase 15.1 faz com ele.

### 1. Camadas Roads / Borders / Kingdoms / Climate / Mountains sem dado canônico

**Ainda verdadeiro.** `GlobalLayerBuilder.Build` devolve `LayerBuildResult.NotYetModeled` para essas
5 (`src/LivingWorld.Api/Visual/Layers/GlobalLayerBuilder.cs:32-33`), e as 5 city-only
(Cities/Villages/Routes/Migrations/Conflicts) devolvem `NotYetModeled` incondicionalmente
(`src/LivingWorld.Api/Visual/Layers/CityLayerBuilder.cs:16-18`). Nenhuma classe `Kingdom`/`Border`/
`Road`/`Climate` existe no domínio.

**15.1 endereça?** **Parcialmente, e só na apresentação.** A story P2 "Layers/overlays selecionáveis"
(VTT2-47) exige que uma camada não modelada apareça **desabilitada com o motivo**, em vez de virar um
toggle que não faz nada — hoje a legenda já distingue `isModeled`
(`web/src/components/LayerLegend.tsx:20-21`), mas não há toggle algum. Modelar os dados continua fora
de escopo (fase dona da mecânica).

### 2. `GlobalSnapshot.ActiveEvents` sempre vazio

**Ainda verdadeiro.** `GlobalProjector.Build` passa `[]` literal
(`src/LivingWorld.Api/Visual/GlobalProjector.cs:48`), com a razão documentada em `:16-19`: o motor só
tem histórico ponto-a-ponto (`Facts`/event log), não "evento em andamento".

**15.1 endereça?** **Não.** Fora de escopo (Out of Scope da spec). Consequência de design: nenhuma
story desta fase depende de marcador de evento no mapa, e o `Renderer` não reserva um tipo de
entidade para isso.

### 3. FOW só por raio fixo — "áreas visitadas permanecem visíveis" não implementado

**Ainda verdadeiro.** `PlayerVisibilityService.CanSee` é distância de Chebyshev ≤ `SightRadius = 5`
(`src/LivingWorld.Simulation/Visibility/PlayerVisibilityService.cs:34-37`), com a limitação
documentada no próprio doc-comment (`:27-31`). Nenhum estado de "células descobertas" existe no
domínio — grep por `discovered|visited|knownCells|explored` em `src` não retorna nada de espacial
(só `Book.RediscoveredAtTick`, que é história, não geografia).

**15.1 endereça?** **Não, e a lacuna deixa de importar nesta fase.** FOW é superfície de Player Mode,
que o master prompt §12 exclui. O código de servidor permanece intacto e testado
(`tests/LivingWorld.Tests/Visual/CityVisibilityFilterTests.cs`); o cliente simplesmente não assina em
`ViewerMode.Player` — e, por OQ-4, a superfície de Player Mode sai do cliente nesta fase. A lacuna
volta a ser bloqueante na Fase 25.

### 4. `POST /visual/player/{id}/interact` nunca implementado

**Ainda verdadeiro.** Só `/move` existe (`src/LivingWorld.Api/VisualInput/VisualInputEndpoints.cs:20`),
com a razão registrada em `:9-13` (nenhum AC define o que "interagir" significa).

**15.1 endereça?** **Não.** É superfície de Player Mode → Out of Scope. Relacionado, porém distinto: o
master prompt §17 pede que o **inspector** só exiba ações que o motor realmente suporta — isso é
atendido pela regra "ação sem capacidade correspondente não é renderizada" (VTT2-20), sem precisar de
`/interact`.

### 5. `Building` sem `CellCoord`; layout em anel calculado no cliente

**Ainda verdadeiro e agora é bloqueante.** `Building` não tem posição
(`src/LivingWorld.Domain/Cities/Building.cs:6-12`); o cliente distribui os prédios num círculo de raio
4 ao redor do centro da cidade (`web/src/components/CityView.tsx:38-50`), com a nota textual "posição
no mapa é layout aproximado (sem dado real)" (`CityView.tsx:104`). `CityVisibilityFilter` até
documenta que não consegue aplicar FOW em prédios por falta de posição
(`src/LivingWorld.Api/Visual/CityVisibilityFilter.cs:12-13`).

**15.1 endereça?** **Sim — OQ-1 resolvida como projeção derivada na API.** `GlobalCityMarker` ganha
`Bounds`/`BoundsAreDerived` e `CityBuildingMarker` ganha `Location`/`LocationIsDerived`, calculados
no projector; `LivingWorld.Domain` não é tocado e o hash não muda (precedente exato:
`GlobalSnapshot.Width/Height`, `src/LivingWorld.Api/Visual/GlobalProjector.cs:20-26`). O anel
client-side de `CityView.tsx:38-50` é deletado.

Detalhe que a substituição precisa corrigir e não só mover de lugar: o anel atual posiciona por
**índice de iteração** (`CityView.tsx:41`), então a posição de um prédio muda quando a lista muda de
ordem ou tamanho. A derivação na API tem de ser estável por `BuildingId`. E como continua sendo
derivada e não autorada, o modelo de dados do cliente carrega `sizeIsDerived: boolean` para o
renderer distinguir visualmente derivado de autorado — a nota textual atual (`CityView.tsx:104`) é
fraca demais.

### 6. Movimento em escala mundo-múndi ("andar até a saída") não implementado

**Ainda verdadeiro.** `PlayerMovementValidator` só valida passo adjacente dentro do mapa
(`src/LivingWorld.Simulation/Visibility/PlayerMovementValidator.cs:11-18`) e o publish é sempre no
escopo da cidade do NPC (`VisualInputEndpoints.cs:31-32`). O drill-down continua sendo botão
(`web/src/components/CityView.tsx:62-64`).

**15.1 endereça?** **Não, e a lacuna se dissolve em duas partes distintas.** A parte "jogador anda até
a saída" é Player Mode → fora de escopo (§12), e o master prompt nem a pede. A parte que **permanece
viva** é diferente: o §11 pede um conceito genérico de `SpatialPortal` para descrever transições entre
espaços — para NPCs conduzidos pelo motor e para a navegação do Observer — em vez de regras
hardcoded. São coisas relacionadas mas não iguais: portal é *como o sistema modela a transição*, não
*dar movimento a um personagem jogável*.

**15.1 endereça?** **Sim, na parte de portal — OQ-2 resolvida como conceito canônico de domínio.**
`SpatialPortal` entra em `LivingWorld.Domain` com `WorldState.Portals` marcado `[Canonical]`, autorado
por cenário no molde de `SettlementAnchor` (`src/LivingWorld.Domain/Geography/MapCell.cs:16-18`), e é
a única mudança desta fase que **altera hash e exige regravar `tests/golden/world-hashes.json`**.

Ressalva honesta descoberta ao escopar: **não há "lógica de transição hardcoded" para rerotear.**
`MigrationSystem` troca só `Npc.City`/`Household.City` via `JoinCity`
(`src/LivingWorld.Simulation/Cities/MigrationSystem.cs:58,60`;
`src/LivingWorld.Domain/Population/Npc.cs:310`) e **nunca toca `CurrentLocation`**; nenhum sistema
compara coordenadas para decidir entrada em espaço. Fazer a migração "chegar pelo portão" mudaria
posição de NPC — comportamento novo, explicitamente fora de escopo. Portanto o portal entra como dado
canônico **à frente do seu consumidor de motor**: nesta fase quem o lê são a projeção da API e a
navegação do cliente. A parte "jogador anda até a saída" continua fora (Player Mode, §12).

---

## Lacunas novas encontradas nesta análise

| # | Lacuna | Evidência | Efeito nesta fase |
| --- | --- | --- | --- |
| 7 | **A API nunca avança o tick.** O mundo está congelado no browser | `src/LivingWorld.Api/Program.cs:52-54` | Bloqueava toda a story "NPCs em movimento". **OQ-3 resolvida: construir.** Vira `TickLoopService` (Engine-facing, read-model/API only) |
| 8 | **`SimulationHost` não tem porta HTTP.** *(OQ-3: resolver nesta fase)* Pause/Resume/SetSpeed/FastForward existem e são inalcançáveis pelo cliente | `SimulationHost.cs:10-22` vs. ausência de `Map*` em `Program.cs` | Vira `SimulationControlEndpoints` (Engine-facing). Zero lógica nova — só tradução HTTP |
| 9 | **`RealtimeGateway._log` cresce sem limite por escopo.** Hoje inofensivo porque `Publish` quase nunca é chamado; com tick loop vira vazamento de memória linear no tempo | `src/LivingWorld.Api/Realtime/RealtimeGateway.cs:14,61-65` (nenhuma poda) | Correção **obrigatória** no mesmo passo do tick loop, não opcional |
| 10 | **Inspecionar um NPC muta o mundo.** `NpcInspectionQuery.Inspect` chama `MaterializationSystem.EnsureMaterialized` antes de montar o DTO | `src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs:17` | Em tensão com "selecionar não altera a simulação" (§43.6). Regra de design: o inspector usa os campos do snapshot do escopo por padrão; `GET /npcs/{id}` só sob ação explícita "Ver detalhes", nunca em hover ou clique simples |
| 11 | **Refetch completo por delta.** Cada frame recebido = 1 GET do snapshot inteiro, recomputando todas as camadas (Terrain é uma entrada por célula) | `web/src/hooks/useRealtimeSnapshot.ts:45-47` + `src/LivingWorld.Api/Visual/GlobalProjector.cs:45-46` | Com tick loop a 8x isso é o backlog do §21 movido para o transporte. Exige delta tipado (`ScopeTickDelta`) |
| 12 | **Nenhum teste cobre câmera, interpolação ou LOD multi-nível** — nada disso existe, e os testes de canvas atuais fazem hit-test assumindo o canvas dimensionado pelo mundo | `web/tests/GridCanvas.test.tsx`, `WorldMapView.test.tsx`, `CityView.test.tsx` (helper de `getBoundingClientRect`, ver `.specs/STATE.md` Handoff) | Esses testes precisam ser reescritos para o espaço de coordenadas da câmera. `Camera`/`LodPolicy`/`InterpolationBuffer` são puros e testáveis sem jsdom |
| 13 | **Proxy do Vite esquecido é bug recorrente** — já aconteceu com `/worlds` e com `/periods` | `web/vite.config.ts:9-13`; `.specs/STATE.md` Handoff (dois relatos) | O endpoint novo `/simulation` precisa entrar no proxy no **mesmo commit**; consta no Done-when da task |
| 14 | **`ScenarioFormState` é grande e o creator tem 882 linhas** — reescrever a apresentação sem preservar o modelo perderia campos que AD-001 exige manter | `web/src/components/CreateWorldForm.tsx` (882 linhas), `web/src/scenarioDefaults.ts` (653 linhas) | O World Creator visual **reusa** `ScenarioFormState`/`scenarioFormToJson`/`jsonToScenarioForm`/`buildCells` e substitui só a camada de UI |
| 15 | **Estado canônico novo quebra os goldens por construção** — o hash é montado por reflexão sobre as propriedades públicas `[Canonical]` de `WorldState`; uma coleção nova entra automaticamente, e propriedade sem `[Canonical]`/`[Volatile]` reprova o teste de cobertura | `src/LivingWorld.Simulation/WorldSnapshot.cs:12-16,29-38`; baseline em `tests/golden/world-hashes.json` (3 entradas), regravado por `dotnet test --filter ZZZ_record_golden_hashes` (`tests/LivingWorld.Tests/GoldenHashesTests.cs:19-29`) | Consequência direta de OQ-2. A regravação é **commit separado e explícito** — o próprio doc-comment do arquivo (`GoldenHashesTests.cs:6-8`) exige isso, "nunca efeito colateral do gate". A task deve provar antes que a mudança de hash vem **só** da coleção nova: mundo sem nenhum portal declarado ⇒ hash inalterado |
| 16 | **Portais precisam sobreviver ao round-trip de persistência**, não só ao hash — `PersistentWorldRunner`/`SqliteWorldRepository` serializam o snapshot inteiro | `src/LivingWorld.Simulation/WorldSnapshot.cs:35` (`Serialize` cobre toda propriedade pública), `src/LivingWorld.Api/Program.cs:34-44` | O teste de round-trip de portais é parte da task de domínio, não opcional — um mundo criado por `POST /worlds/create` e recarregado tem de trazer os portais de volta |

---

## Restrição de processo declarada pelo usuário

> "Não rode os testes regressivos do motor com frequência, pois demoram muito para rodar — foque em
> testes novos (das novas features) e rode o regressivo apenas ao final da fase."

**Nota de ordem de entrega (2026-08-07):** o usuário pediu frontend-contra-mock → backend →
integração (ver `tasks.md`, "Ordem de entrega em 3 estágios"). Isso não altera nenhuma lacuna acima,
mas muda *quando* três delas param de doer: as lacunas 7 (tick loop), 8 (`SimulationHost` sem porta
HTTP) e 11 (refetch por delta) deixam de bloquear a demonstração visual — no Estágio 1 o movimento e
o controle de tempo vêm de fontes mock tipadas contra o contrato real, e as lacunas continuam
verdadeiras no motor até o Estágio 2 fechá-las. As lacunas 5 (posição de prédio) e 15/16 (goldens e
round-trip de portal) também só são endereçadas no Estágio 2, com o consumo no cliente no Estágio 3.

Medido: a suíte .NET com o filtro padrão são ~1178 testes em ~25 minutos
(`.specs/STATE.md` Handoff, duas medições). A cadência de gate desta fase está em `tasks.md`
(seção "Gate Check Commands") e é normativa: **por task**, só os testes novos/alterados daquela task
+ a suíte web (vitest, segundos); **`scripts/verify.sh` e os testes `Category=Scenario` só no
fechamento da fase**, depois da última task.

---

## Deferred Ideas

- **T0 vs T9**: `web/src/data/mock/mockScopeKey.ts` (T0) mapeia `Building` para `building:{id}`,
  documentado explicitamente como chave interna de indexação do mock, não a canônica. `T9`
  entregou a canônica de verdade — `toScopeKey` em `web/src/map-engine/space.ts` — que mapeia
  `Building` para `interior:{id}` (paridade real com `VisualScope.ScopeKey`). As duas convivem
  sem colisão hoje porque nada ainda liga os mocks de T0 ao `SpatialContext` de T9 (essa fiação é
  T10-T14). Quando algum desses tasks passar `SpaceId` para os mocks de T0, trocar
  `mockScopeKey` por `toScopeKey` (eliminar o helper duplicado) em vez de manter os dois formatos.
