# Fase 15.1 (Redesign do frontend VTT) Specification

## Problem Statement

A Fase 15 entregou um cliente web funcional (React+TS em `web/`, realtime WS/SSE, projeções por escopo, FOW, tokens de NPC) e o usuário rejeitou o resultado como produto: a experiência é "admin dashboard + formulário de configuração", não "construir e observar um mundo". Três falhas estruturais sustentam essa rejeição, todas verificadas no código: (1) **não existe câmera** — `GridCanvas.tsx` só tem botões `+`/`−` de zoom (`web/src/components/GridCanvas.tsx:161-178`), sem pan, sem wheel, sem zoom focado no cursor; (2) **nada se move** — o processo da API nunca avança o tick (`src/LivingWorld.Api/Program.cs:52-55`: "este host ainda não ticka automaticamente"), e o único `RealtimeGateway.Publish` do sistema é disparado por um POST de movimento manual (`src/LivingWorld.Api/VisualInput/VisualInputEndpoints.cs:32`); (3) **cidade é um ponto e prédio não tem posição** — `City.Location` é um único `CellCoord` (`src/LivingWorld.Domain/Cities/City.cs:11`) e `Building` não tem coordenada nenhuma (`src/LivingWorld.Domain/Cities/Building.cs:6-12`), então o cliente inventa um anel de layout (`web/src/components/CityView.tsx:38-50`).

Esta fase redesenha o frontend em cima do mesmo motor: Map Engine com câmera de verdade, espaços hierárquicos (WorldSpace > CitySpace > BuildingSpace), LOD por zoom, NPCs se movendo com interpolação puramente visual, inspector flutuante contextual, controle de tempo, e — depois disso — o World Creator como editor visual em vez de wizard de formulário.

## Goals

- [ ] Map Engine único e compartilhado (câmera, grid, entidades, LOD, seleção, input) reusado por Observer Mode **e** World Creator — hoje `WorldMapView`/`CityView`/`MapGridEditor`/`MapOverlay` só compartilham o `GridCanvas` burro, e cada uma reimplementa zoom, seleção e montagem de marcadores.
- [ ] Navegação espacial contínua WORLD → CITY (→ BUILDING) com breadcrumb, sem parecer troca de página administrativa.
- [ ] NPCs visivelmente em movimento no espaço observado, com interpolação apresentacional entre estados autoritativos do motor e descarte de frames em velocidade alta (nunca backlog).
- [ ] Semântica previsível: clique simples **seleciona**; navegação espacial exige ação distinta (double click / botão Open / zoom profundo / breadcrumb).
- [ ] Inspector flutuante universal à direita, contextual por tipo de entidade, com dados e ações que o motor de fato fornece — zero informação inventada.
- [ ] Controle de tempo (Pause / velocidade / +1 tick) refletindo o que o hospedeiro realmente suporta.
- [ ] Separação explícita de estado: SIMULATION (autoritativo) / VIEW (câmera + espaço observado) / SELECTION (o que está inspecionado).
- [ ] World Creator progressivo: presets → editor visual no mapa → inspector contextual → parâmetros avançados atrás de progressive disclosure.

## Out of Scope

| Feature | Reason |
| --- | --- |
| Player Mode (WASD de personagem, click-to-move de personagem, HUD de jogador, estado de personagem controlável, tecla M do modo jogador, regras de entrada/saída específicas de jogador) | Master prompt §12 exclui explicitamente; retomado por volta da Fase 25 (`docs/roadmap/phase-25-players.md`). A arquitetura não deve inviabilizar o modo, mas não deve antecipá-lo. |
| Mudança em regra determinística de domínio/simulação (comportamento, economia, natalidade, mortalidade, pathfinding autoritativo) | Master prompt §36 e `rules/simulation-determinism.md`: motor é source of truth. A **única** adição canônica desta fase é o dado descritivo de `SpatialPortal` (OQ-2); tudo o mais é read-model/API. |
| **Comportamento** de portal: regras de quem pode atravessar qual portal, custo/tempo de travessia, efeito econômico ou social da travessia, e mover a posição de NPC ao migrar | OQ-2 autoriza **modelar o dado**, não inventar mecânica. Verificado que hoje não existe transição espacial no motor para "rotear": `MigrationSystem` troca só `Npc.City`/`Household.City` via `JoinCity` (`src/LivingWorld.Simulation/Cities/MigrationSystem.cs:58,60`; `src/LivingWorld.Domain/Population/Npc.cs:310`) e **nunca toca `CurrentLocation`**. Fazer a migração "chegar pelo portão" seria comportamento novo — fica para a fase dona do movimento inter-espaço. |
| Autoria procedural de portais (gerar portões automaticamente por tamanho/terreno da cidade) | Portal é dado de cenário autorado, como `SettlementAnchor` (`src/LivingWorld.Domain/Geography/MapCell.cs:16-18`). Geração procedural é regra nova. |
| Arte curada (pixel-art, tiles ilustrados, sprites desenhados) | Sem pipeline de assets no projeto; cor procedural determinística por id continua o teto (AD-003, `web/src/colorById.ts:4-7`). |
| Nova biblioteca de renderização (PixiJS/Phaser/WebGL) | Master prompt §34 pede avaliar o que já existe antes de adicionar lib. Canvas 2D atual não foi medido saturando ainda — ver Assumptions. |
| Modelar dados canônicos das 5 camadas hoje `NotYetModeled` (Roads/Borders/Kingdoms/Climate/Mountains) e das 5 camadas city-only | Exige conceitos de domínio inexistentes (`src/LivingWorld.Api/Visual/Layers/GlobalLayerBuilder.cs:32-33`, `CityLayerBuilder.cs:16-18`) — pertence às fases donas da mecânica. |
| Conceito de "evento em andamento" no domínio | `GlobalSnapshot.ActiveEvents` é sempre `[]` (`src/LivingWorld.Api/Visual/GlobalProjector.cs:48`); o motor só tem histórico ponto-a-ponto. |
| Memória espacial persistida por NPC (células descobertas) para "áreas visitadas permanecem visíveis" | Estado canônico novo, entra no hash — task de domínio, não de frontend (`src/LivingWorld.Simulation/Visibility/PlayerVisibilityService.cs:27-31`). |
| `POST /visual/player/{id}/interact` | Nunca implementado e sem AC que defina o que "interagir" significa (`src/LivingWorld.Api/VisualInput/VisualInputEndpoints.cs:9-13`); é superfície de Player Mode, fora de escopo. |

---

## Assumptions & Open Questions

Toda ambiguidade está resolvida ou registrada aqui.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Tecnologia de render | Continuar em Canvas 2D puro (`GridCanvas.tsx`), com viewport culling + camada única de redraw; nenhuma lib nova | Master prompt §34 manda avaliar antes de adicionar; o gargalo real hoje não é o canvas, é o refetch HTTP por delta (`web/src/hooks/useRealtimeSnapshot.ts:45-47`) redesenhando o grid inteiro. Adicionar PixiJS antes de medir seria complexidade não paga. | n |
| "Motor trabalha com X,Y,Z" (master prompt §9) | **Falso hoje** — `CellCoord` é `(int X, int Y)` (`src/LivingWorld.Domain/Geography/GeographyIds.cs:5`); não existe Z em lugar nenhum do domínio. A arquitetura espacial desta fase é 2D por espaço, com hierarquia de espaços substituindo o eixo Z. | Não inventar um eixo que o motor não tem; a hierarquia WorldSpace/CitySpace/BuildingSpace já entrega o que o §9 realmente quer (sem matriz gigante única). | n |
| Escala entre espaços | Relação declarada em constante única no cliente (`world tile : city tile : building tile`), não multiplicadores espalhados | Master prompt §10 exige relação definida, não metros reais. O domínio não tem nenhuma noção de escala física para derivar isso. | n |
| Interpolação visual | Só apresentação: posição autoritativa continua sendo a do último snapshot do motor; interpolação nunca é escrita de volta nem consultada por lógica | Master prompt §5/§36. | y |
| Modo Player no seletor atual da UI | **Removido** do header nesta fase (`web/src/App.tsx:85-105`), junto com `PlayerMoveControls` e o overlay de tecla M | Master prompt §12 lista explicitamente "não implementar HUD de jogador / tecla M / estado de personagem controlável". Decisão do usuário (OQ-4 resolvida). | **y** |
| FOW (`CityVisibilityFilter`/`PlayerVisibilityService`) e `VisualInputEndpoints` | Backend permanece **intacto**; o cliente simplesmente não assina em `ViewerMode.Player` nesta fase | Remover código de servidor testado (`tests/LivingWorld.Tests/Visual/CityVisibilityFilterTests.cs`) por causa de uma decisão de UX destruiria trabalho que a Fase 25 vai querer. | **y** |
| Ordem de entrega | Vertical slice 1 (Map Engine + Observer + NPCs em movimento + inspector + tempo) **antes** do World Creator; a fundação engine-facing (tick loop e portais) vem antes de tudo, porque a slice consome as duas | Master prompt §40/§41. Construir a navegação do Observer sem portais e depois reescrevê-la por cima deles seria trabalho dobrado. | y |

### Decisões resolvidas (eram OQ-1..OQ-4) — confirmadas pelo usuário em 2026-08-06

| ID | Decisão | Evidência que a motivou | Consequência de escopo |
| --- | --- | --- | --- |
| **OQ-1** ✅ | **Footprint de cidade/prédio é projeção derivada na API**, não campo de domínio. `GlobalProjector` calcula bounds da cidade; `CityProjector` distribui prédios de forma determinística por `BuildingId`. **Zero impacto em hash/goldens**; `LivingWorld.Domain` não é tocado. | `City` só tem `Location` (`src/LivingWorld.Domain/Cities/City.cs:10-14`); `Building` não tem posição alguma (`Building.cs:6-12`); hoje o cliente inventa um anel (`web/src/components/CityView.tsx:38-50`). Precedente exato: `GlobalSnapshot.Width/Height` foi adicionado assim na Fase 15 (`src/LivingWorld.Api/Visual/GlobalProjector.cs:20-26`). | Task **Engine-facing (read-model/API only)**. O footprint continua marcado como derivado na UI (`sizeIsDerived`), e o anel client-side é deletado. |
| **OQ-2** ✅ | **`SpatialPortal` vira conceito canônico real em `LivingWorld.Domain`** — cidades e prédios declaram entradas/saídas nomeadas (portão norte, docas, porta da frente) como dado de domínio, e as transições entre espaços passam a referenciar portais em vez de coordenada hardcoded. **Esta é a única mudança desta fase que altera hash/goldens.** | Grep por `portal\|entrance\|gateway\|doorway\|transition` em `src/LivingWorld.Domain` e `src/LivingWorld.Simulation`: **zero ocorrências**. A "entrada" hoje é só o cliente trocando `focus` (`web/src/App.tsx:126,134`). | Task **Engine-facing (DOMAIN — altera hash/goldens)**, com fronteira estrita: **só dado descritivo**. Ver a linha correspondente em Out of Scope — nenhuma regra nova de quem pode usar qual portal, nenhum efeito econômico/social, nenhuma mudança de posição de NPC. |
| **OQ-3** ✅ | **Sim: a fase constrói o loop de tick e os endpoints de controle de tempo.** Fica em cima de `SimulationHost`, que é declaradamente estado de hospedeiro fora do snapshot/hash. **Zero impacto em hash/goldens.** A correção do crescimento ilimitado de `RealtimeGateway._log` é dependência obrigatória do loop. | `SimulationHost.Pause/Resume/SetSpeed/FastForward` **existem** (`src/LivingWorld.Simulation/SimulationHost.cs:10-22`) e são fora do hash por design (`:3-4`), mas **nenhum endpoint os expõe** (`src/LivingWorld.Api/Program.cs:55,85` só registra no DI) e **não existe loop** (`Program.cs:52-54`: "este host ainda não ticka automaticamente"). O mundo está congelado no browser hoje. | Tasks **Engine-facing (read-model/API only)** — infraestrutura e wiring, nenhuma regra de simulação nova. |
| **OQ-4** ✅ | **Remover a superfície de Player Mode do cliente agora** — seletor de modo, `PlayerMoveControls`, WASD, overlay de tecla M. Backend (`VisualInputEndpoints`, `/move`, `/interact`) **fica intocado**, reservado para a Fase 25. | Superfície atual: `web/src/App.tsx:28-39,85-105,139-141`, `web/src/components/PlayerMoveControls.tsx:10-19,36-43`, `web/src/components/MapOverlay.tsx`, `web/tests/PlayerMoveControls.test.tsx`. | Task pequena, **frontend puro, só remoção** — não é reescrita. |

**Open questions:** none — todas resolvidas acima antes da aprovação da spec.

---

## User Stories

### P1: Map Engine com câmera de verdade ⭐ MVP

**User Story**: Como observador, quero navegar o mapa com pan e zoom fluidos (scroll = zoom no cursor, arrastar = pan) para explorar o mundo como em um VTT/Google Maps, e não por dois botões `+`/`−`.

**Why P1**: É o pré-requisito de todo o resto. Hoje não existe câmera: `GridCanvas` desenha o grid inteiro no tamanho `width*zoom × height*zoom` e o único controle é `onZoomChange` por botão (`web/src/components/GridCanvas.tsx:64-65,161-178`); não há `onWheel`, não há drag, não há offset de viewport.

**Acceptance Criteria**:

1. WHEN o usuário rola a roda do mouse sobre o mapa THEN o sistema SHALL alterar o zoom mantendo fixa a coordenada de mundo sob o cursor (zoom focado no cursor).
2. WHEN o usuário arrasta com o botão primário em espaço vazio (ou com o botão do meio) THEN o sistema SHALL deslocar a câmera na direção oposta ao arrasto, sem alterar seleção nem estado de simulação.
3. WHEN o zoom ou o pan mudam THEN o sistema SHALL renderizar apenas as células e entidades cujo bounding box intersecta o viewport atual (viewport culling), nunca o grid inteiro.
4. WHEN o usuário tenta afastar além dos limites do espaço atual THEN o sistema SHALL clampar a câmera de modo que o espaço permaneça visível (nunca perder o mapa da tela).
5. WHEN a câmera muda THEN o sistema SHALL manter o hash canônico do mundo inalterado e não emitir nenhuma escrita para a API.

**Independent Test**: abrir o mapa-múndi, rolar/arrastar; conferir que a célula sob o cursor não se move ao dar zoom, que o número de células desenhadas por frame cai ao dar zoom in, e que nenhuma requisição de escrita sai do cliente.

---

### P1: Espaços hierárquicos e navegação Observer World ↔ City ⭐ MVP

**User Story**: Como observador, quero entrar numa cidade e voltar ao mundo com a sensação de continuidade espacial, orientado por um breadcrumb "World / Cidade X", e não trocando de tela administrativa.

**Why P1**: Master prompt §8/§9/§13/§37. Hoje a "navegação" é `setFocus` trocando qual componente monta (`web/src/App.tsx:123-150`), cada view recalcula seu próprio zoom inicial no mount (`WorldMapView.tsx:20-22`, `CityView.tsx:26-28`) e não há breadcrumb algum — só um botão "← mapa-múndi" (`CityView.tsx:62-64`).

**Acceptance Criteria**:

1. WHEN o sistema representa uma entidade posicionada THEN ela SHALL declarar o espaço a que pertence (`WorldSpace` | `CitySpace(cityId)` | `BuildingSpace(buildingId)`) e sua posição **local** naquele espaço.
2. WHEN o observador abre um `CitySpace` THEN o sistema SHALL exibir um breadcrumb com a cadeia de espaços ancestrais e SHALL permitir voltar a qualquer ancestral clicando nele.
3. WHEN o observador retorna a um espaço já visitado THEN o sistema SHALL restaurar a câmera daquele espaço no estado em que estava (posição e zoom), não resetar para o fit inicial.
4. WHEN o observador transiciona entre espaços THEN o sistema SHALL usar uma transição visual contínua (fade/zoom) em vez de troca abrupta de tela.
5. WHEN o observador está num espaço THEN o sistema SHALL renderizar somente entidades daquele espaço (nunca NPCs de outro espaço).

**Independent Test**: entrar numa cidade, mover a câmera, voltar ao mundo, reentrar — a câmera da cidade volta onde estava; o breadcrumb mostra os dois níveis; nenhum NPC de outra cidade aparece.

---

### P1: NPCs em movimento com interpolação visual ⭐ MVP

**User Story**: Como observador, quero ver NPCs se deslocando pelo mapa em tempo real, com movimento suave entre os estados que o motor produz.

**Why P1**: É o sinal de "mundo vivo" que a Fase 15 não entregou. Hoje **nada se move**: nenhum tick automático (`src/LivingWorld.Api/Program.cs:52-54`), nenhum publish por tick (`RealtimeGateway.Publish` só em `VisualInputEndpoints.cs:32`), e o cliente sequer interpola — cada frame recebido dispara um refetch HTTP completo e um redraw total (`web/src/hooks/useRealtimeSnapshot.ts:39-48`).

**Acceptance Criteria**:

1. WHEN o motor avança um tick e a posição de um NPC no espaço observado muda THEN o sistema SHALL entregar a nova posição ao cliente pelo canal realtime sem que o cliente precise refazer um fetch completo do snapshot.
2. WHEN o cliente recebe a posição do tick N+1 para um NPC que estava em outra célula no tick N THEN o sistema SHALL animar visualmente o deslocamento entre as duas posições.
3. WHEN a interpolação está em curso THEN a posição autoritativa consultada por qualquer leitura (inspector, seleção, hit-test de clique) SHALL ser a do último estado recebido do motor, nunca a posição interpolada.
4. WHEN chegam estados do motor mais rápido do que o renderer consegue animar THEN o sistema SHALL descartar os estados intermediários e saltar para o mais recente, nunca acumular fila de animações.
5. WHEN o cliente está parado (aba em background, ou nenhum estado novo) THEN o sistema SHALL não gerar movimento visual algum (sem extrapolação/adivinhação de posição).

**Independent Test**: com a simulação rodando a 1x, observar um NPC atravessar células suavemente; subir para 8x e conferir que a posição desenhada acompanha a última recebida (sem atraso crescente) e que a UI não trava.

---

### P1: Selecionar ≠ navegar ≠ agir, com inspector flutuante ⭐ MVP

**User Story**: Como observador, quero clicar em qualquer entidade e ver um painel flutuante à direita com os dados dela, mantendo o mapa visível e interativo atrás — sem que o clique me jogue para outro contexto espacial.

**Why P1**: Master prompt §14/§17/§18. Hoje o clique já abre um painel (`web/src/components/SidePanel.tsx`), mas: a seleção é estado local duplicado em cada view (`WorldMapView.tsx:23`, `CityView.tsx:29`) e se perde ao navegar; não existe double click, nem highlight da entidade selecionada, nem Esc para fechar; e o painel oferece um botão "Entrar" que mistura inspeção com navegação (`WorldMapView.tsx:62`).

**Acceptance Criteria**:

1. WHEN o usuário dá um clique simples numa entidade espacial THEN o sistema SHALL selecioná-la, destacá-la visualmente no mapa e abrir o inspector à direita, **sem** alterar o espaço observado.
2. WHEN o usuário dá double click numa cidade (ou usa o botão "Abrir" do inspector, ou dá zoom além do limiar de entrada) THEN o sistema SHALL abrir o `CitySpace` correspondente.
3. WHEN o usuário pressiona Esc, clica no X do inspector ou clica em espaço vazio THEN o sistema SHALL limpar a seleção e fechar o inspector, sem efeito colateral no mundo.
4. WHEN o usuário seleciona outra entidade com o inspector aberto THEN o sistema SHALL substituir o conteúdo do inspector pela nova entidade, mantendo o painel aberto.
5. WHEN o inspector exibe ações THEN cada ação SHALL corresponder a uma capacidade que o motor realmente expõe; ações sem lastro SHALL não ser renderizadas (nunca botão decorativo).
6. WHEN o usuário navega para outro espaço com uma entidade selecionada THEN o sistema SHALL preservar a seleção se a entidade existir no novo espaço, e limpá-la caso contrário.

**Independent Test**: clicar numa cidade (inspector abre, mapa continua no mundo), dar double click (entra na cidade), Esc (fecha painel), clicar num NPC (inspector troca de tipo sem fechar).

---

### P1: Inspector de NPC e de Cidade com dados reais ⭐ MVP

**User Story**: Como observador, quero que o inspector mostre o que o motor sabe sobre a entidade — nada a mais, nada inventado.

**Why P1**: Master prompt §15/§16 exige "só o que o motor de fato fornece". Hoje o inspector de cidade mostra 2 campos (população e posição — `WorldMapView.tsx:64-67`) enquanto `CityPopulationQuery` já expõe riqueza, saúde, desigualdade, economia e habitação (`src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-53`); e o inspector de NPC mostra posição e ação (`CityView.tsx:87-90`) enquanto `NpcInspectionQuery.Inspect` já monta nome, idade, cultura, cidade, household, pais, cônjuge, profissão, empregador, saúde e as 4 needs (`src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs:38-44`).

**Acceptance Criteria**:

1. WHEN uma cidade é selecionada THEN o inspector SHALL exibir os indicadores derivados que `CityPopulationQuery` fornece (população, riqueza, saúde, desigualdade, economia, habitação) e destacar o footprint/marcador da cidade no mapa.
2. WHEN um NPC é selecionado THEN o inspector SHALL exibir os campos que `NpcInspectionQuery` fornece (identidade, idade, profissão, empregador, família, saúde, needs, posição, ação atual).
3. WHEN um campo não tem dado no motor THEN o inspector SHALL omiti-lo ou marcá-lo explicitamente como não modelado, seguindo o padrão `NotYetModeled` já usado nas camadas (`src/LivingWorld.Api/Visual/Layers/LayerBuildResult.cs`) e em `InteriorSnapshot.OccupancyModeled` (`src/LivingWorld.Api/Visual/InteriorProjector.cs:11`).
4. WHEN ids internos aparecem (terreno, bioma, profissão, tipo de prédio) THEN o inspector SHALL resolvê-los para rótulos legíveis quando houver catálogo, e exibir o id cru apenas em modo avançado/debug.

**Independent Test**: selecionar uma cidade e conferir cada número contra a mesma consulta no backend; selecionar um NPC e conferir contra `GET /npcs/{id}`.

---

### P1: Controles de tempo ⭐ MVP

**User Story**: Como observador, quero pausar, retomar, mudar a velocidade e avançar exatamente um tick, com indicação clara da velocidade atual.

**Why P1**: Sem isso o mundo simplesmente não anda no browser (ver OQ-3). Verificado: `SimulationHost.Pause/Resume/SetSpeed/FastForward` existem (`src/LivingWorld.Simulation/SimulationHost.cs:10-22`) e são estado de hospedeiro fora do hash (`SimulationHost.cs:3-4`), mas nenhum endpoint os expõe e não há loop de tempo real (`src/LivingWorld.Api/Program.cs:52-55`).

**Acceptance Criteria**:

1. WHEN a UI está aberta e a simulação não está pausada THEN o sistema SHALL avançar o mundo continuamente no ritmo configurado e publicar as mudanças do escopo observado pelo canal realtime.
2. WHEN o usuário aciona Pause THEN o sistema SHALL parar de avançar ticks e a UI SHALL indicar o estado pausado; retomar SHALL continuar do mesmo tick.
3. WHEN o usuário escolhe uma velocidade THEN o sistema SHALL aplicá-la no hospedeiro e SHALL exibir a velocidade corrente selecionada.
4. WHEN o usuário aciona "+1 tick" com a simulação pausada THEN o sistema SHALL avançar exatamente um tick e permanecer pausado.
5. WHEN qualquer controle de tempo é acionado THEN o sistema SHALL não alterar `WorldState` de forma não determinística — o controle é do hospedeiro e SHALL permanecer fora do snapshot/hash canônico.
6. WHEN a velocidade é alterada THEN o sistema SHALL não recriar a conexão realtime nem perder a seleção/câmera atual.

**Independent Test**: pausar (nada se move), +1 tick (uma mudança discreta), 8x (mundo acelera, UI acompanha), e rodar N ticks com a UI conectada conferindo que o hash canônico é idêntico ao de N ticks sem UI.

---

### P1: Separação Simulation / View / Selection ⭐ MVP

**User Story**: Como desenvolvedor, quero que estado de simulação, estado de visualização e estado de seleção sejam três coisas distintas e explícitas, para que navegar/zoom/selecionar nunca toque na simulação e para que a UI não re-renderize o mundo inteiro a cada tick.

**Why P1**: Master prompt §33. Hoje: o estado de simulação chega por `useRealtimeSnapshot` e é jogado direto no corpo do `App` (`web/src/App.tsx:45-52`), fazendo todo o subtree re-renderizar; a câmera vive dentro de cada view (`WorldMapView.tsx:20`, `CityView.tsx:26`) e morre ao navegar; a seleção é duplicada em duas views (`WorldMapView.tsx:23`, `CityView.tsx:29`); e cada delta recebido dispara um refetch HTTP completo (`web/src/hooks/useRealtimeSnapshot.ts:45-47`).

**Acceptance Criteria**:

1. WHEN o estado de simulação é atualizado THEN o sistema SHALL atualizar o render sem re-renderizar componentes de UI que dependem só de VIEW ou SELECTION.
2. WHEN a câmera muda (pan/zoom) THEN o sistema SHALL não emitir nenhuma requisição ao servidor nem alterar o estado de simulação em memória.
3. WHEN a seleção muda THEN o sistema SHALL não alterar câmera nem espaço observado (exceto quando o usuário aciona Follow explicitamente).
4. WHEN o número de entidades no espaço observado cresce THEN o sistema SHALL não criar um elemento DOM por entidade (render em canvas, não em nós).
5. WHEN um delta chega pelo canal realtime THEN o sistema SHALL aplicá-lo incrementalmente ao estado de simulação, sem refetch completo do snapshot como caminho normal (refetch fica reservado a reconexão/erro).

**Independent Test**: instrumentar contagem de renders do React durante 100 ticks — componentes de HUD/inspector não re-renderizam por tick; o canvas sim.

---

### P2: LOD por zoom com nível agregado

**User Story**: Como observador, quero que muitos NPCs distantes virem densidade/cluster em vez de centenas de tokens sobrepostos, e que a representação suba de detalhe conforme eu aproximo.

**Why P2**: Legibilidade e performance, mas a slice 1 já é demonstrável com os dois níveis atuais. Hoje o LOD é binário (`isToken = zoom >= lodTokenThreshold`, default 18 — `web/src/components/GridCanvas.tsx:36,59,111-130`), definido por prop em cada view, sem nível agregado e sem gerência central.

**Acceptance Criteria**:

1. WHEN o zoom está abaixo do limiar de agregação THEN o sistema SHALL renderizar densidade/cluster por região em vez de entidades individuais.
2. WHEN o zoom está entre o limiar de agregação e o de token THEN o sistema SHALL renderizar cada entidade como dot individual em movimento.
3. WHEN o zoom está no ou acima do limiar de token THEN o sistema SHALL renderizar token com anel/cor derivada do id e, acima de um limiar adicional, rótulo/info.
4. WHEN os limiares mudam THEN o sistema SHALL aplicá-los sem nova requisição ao servidor (decisão puramente de cliente sobre o mesmo estado).
5. WHEN o LOD muda de nível THEN o sistema SHALL manter a mesma identidade de entidade (a entidade é a mesma, só a representação muda).

**Independent Test**: gerar um mundo com muitos NPCs, afastar até o nível agregado e aproximar até token, conferindo que o número de primitivas desenhadas por frame não explode.

---

### P2: Cidade com footprint espacial no World Map

**User Story**: Como observador, quero que uma cidade ocupe uma área no mapa-múndi proporcional ao que ela é, não um ponto de uma célula.

**Why P2**: Master prompt §6/§10. Resolvido por **OQ-1**: o footprint é derivado na camada de projeção da API (mesmo padrão de `GlobalSnapshot.Width/Height`, `src/LivingWorld.Api/Visual/GlobalProjector.cs:20-26`), sem tocar `LivingWorld.Domain` e sem impacto em hash/goldens.

**Acceptance Criteria**:

1. WHEN uma cidade é renderizada no `WorldSpace` THEN o sistema SHALL desenhar sua área/bounds, não um marcador pontual.
2. WHEN o zoom aumenta sobre uma cidade THEN o sistema SHALL revelar progressivamente detalhe estrutural (contorno → contorno + rótulo → contorno + estruturas), conforme dado disponível.
3. WHEN o footprint é derivado e não autorado no domínio THEN a UI SHALL marcá-lo explicitamente como derivado (mesmo padrão da nota atual em `web/src/components/CityView.tsx:104`).
4. WHEN o usuário clica dentro do footprint THEN o sistema SHALL selecionar a cidade (o footprint inteiro é a hit area, não só a célula central).

**Independent Test**: comparar visualmente duas cidades de populações muito diferentes — as áreas diferem; clicar em qualquer ponto interno seleciona.

---

### P2: Layers/overlays selecionáveis

**User Story**: Como observador, quero ligar e desligar camadas (terreno, bioma, rios, recursos, e as agregadas de população/riqueza) independentemente da representação estrutural do mapa.

**Why P2**: Existe hoje só como legenda informativa (`web/src/components/LayerLegend.tsx:18-24` lista nomes e diz "disponível"/"ainda não modelada"), sem nenhum toggle: `WorldMapView` sempre desenha Terrain como cor de célula e Rivers como overlay, hardcoded (`WorldMapView.tsx:46-47`).

**Acceptance Criteria**:

1. WHEN o usuário liga/desliga uma camada THEN o sistema SHALL adicionar/remover aquela camada do render sem nova requisição (o payload já traz todas as camadas suportadas — `src/LivingWorld.Api/Visual/GlobalProjector.cs:45-46`).
2. WHEN uma camada está `NotYetModeled` THEN o sistema SHALL exibi-la desabilitada com a razão, nunca como opção que não faz nada.
3. WHEN múltiplas camadas compatíveis estão ativas THEN o sistema SHALL compô-las em ordem determinística de z-order declarada.

**Independent Test**: desligar Terrain (células voltam ao fundo neutro), ligar Resources (pontos aparecem), conferir que camadas não modeladas estão desabilitadas.

---

### P2: Follow

**User Story**: Como observador, quero seguir uma entidade em movimento com a câmera, e parar de seguir explicitamente.

**Why P2**: Master prompt §19. Não existe nada disso hoje.

**Acceptance Criteria**:

1. WHEN o usuário aciona Follow no inspector de uma entidade THEN a câmera SHALL acompanhar a posição autoritativa dela a cada atualização, mantendo o inspector aberto.
2. WHEN o usuário aciona o botão explícito de parar THEN o sistema SHALL cessar o follow mantendo a câmera onde está.
3. WHEN o usuário move a câmera manualmente durante um Follow THEN o sistema SHALL cancelar o Follow e indicar isso na UI.
4. WHEN a entidade seguida sai do espaço observado THEN o sistema SHALL cessar o follow e informar o motivo, sem trocar de espaço automaticamente.

**Independent Test**: seguir um NPC em movimento, arrastar a câmera, conferir que o follow cancela e o botão volta ao estado inicial.

---

### P2: World Creator — presets e editor visual

**User Story**: Como criador de mundo, quero começar de um preset e então **construir** o mundo clicando no mapa, com os parâmetros numéricos disponíveis mas fora do caminho.

**Why P2**: Master prompt §22-30. Hoje o creator é um wizard de 6 abas de formulário (`web/src/components/CreateWorldForm.tsx:18-24`, 882 linhas) com o editor de mapa como um bloco dentro da aba "Mapa" (`web/src/components/MapGridEditor.tsx`), e os "templates" são 3 variações de tamanho/população do mesmo cenário (`src/LivingWorld.Api/DefaultPeriodSeeder.cs`, AD-004).

**Acceptance Criteria**:

1. WHEN o usuário abre o World Creator THEN o sistema SHALL apresentar primeiro uma tela curta de presets (nome, seed, tamanho aproximado, preset) com um botão de criar, sem exigir nenhum parâmetro avançado.
2. WHEN o mundo inicial é criado THEN o sistema SHALL abrir imediatamente o editor visual, com o mapa ocupando a maior parte da tela, toolbar no topo e inspector contextual à direita.
3. WHEN nada está selecionado no editor THEN o inspector SHALL mostrar a configuração geral do mundo; quando terreno/cidade/NPC/prédio está selecionado, SHALL mostrar as propriedades daquela entidade.
4. WHEN uma propriedade representa posição, tamanho, região, caminho ou relação espacial THEN a edição primária SHALL ser no mapa (ferramenta + clique), e o campo numérico SHALL existir apenas como leitura/fallback no inspector.
5. WHEN parâmetros avançados são necessários THEN o sistema SHALL apresentá-los por progressive disclosure (accordion/drawer por área), nunca dezenas de inputs simultâneos na tela principal.
6. WHEN listas de pares id→valor são editadas THEN o sistema SHALL usar tabelas/chips/selectors com nomes legíveis em vez de linhas repetidas de "id: [] valor: [] remover".
7. WHEN o mundo é submetido THEN o sistema SHALL enviar o mesmo contrato de cenário que `POST /worlds/create` já aceita hoje (`src/LivingWorld.Api/WorldCreateEndpoints.cs`), sem novo formato.

**Independent Test**: criar um mundo em menos de 30 segundos partindo de um preset, depois posicionar um assentamento clicando no mapa e conferir que o JSON enviado é o mesmo shape que o wizard atual produz.

---

### P3: BuildingSpace

**User Story**: Como observador, quero entrar num prédio e ver um grid próprio com o que o motor souber daquele interior.

**Why P3**: `InteriorProjector` hoje só devolve identidade + tipo + `OccupancyModeled: false` (`src/LivingWorld.Api/Visual/InteriorProjector.cs:11,20`), e o domínio não modela paredes, cômodos, móveis nem "NPC dentro de qual prédio". Um BuildingSpace visualmente rico exigiria dados que não existem.

**Acceptance Criteria**:

1. WHEN o observador abre um `BuildingSpace` THEN o sistema SHALL renderizar um grid local com breadcrumb "World / Cidade / Prédio" e retorno ao ancestral.
2. WHEN o motor não modela ocupação/estrutura interna THEN o sistema SHALL declarar isso explicitamente no espaço, sem desenhar cômodos/móveis fictícios.

---

### P1: SpatialPortal como conceito canônico de domínio ⭐ MVP — **altera hash/goldens**

**User Story**: Como sistema, quero que entradas e saídas de um espaço sejam dado de domínio nomeado ("portão norte", "docas", "porta da frente"), para que transições entre espaços referenciem portais em vez de coordenada hardcoded — e para que uma cidade possa ter várias entradas sem código específico por entrada.

**Why P1**: Decisão **OQ-2** do usuário, escolhendo a opção maior que a recomendação original. Passa a ser P1 (e não P3) porque a navegação do Observer entre espaços consome os portais: construir a navegação em cima de um `setFocus` ad-hoc (`web/src/App.tsx:126,134`) e depois reescrevê-la por cima dos portais seria trabalho dobrado. Verificado que hoje não existe nada disso: grep por `portal|entrance|gateway|doorway|transition` em `src/LivingWorld.Domain` e `src/LivingWorld.Simulation` retorna **zero** ocorrências.

**Fronteira desta story (estrita)**: modela o **dado descritivo** do portal e o expõe. Não introduz mecânica — ver as duas linhas de portal em Out of Scope. `MigrationSystem` não é alterado.

**Acceptance Criteria**:

1. WHEN o mundo é construído THEN cada portal SHALL ser dado canônico com identidade, rótulo legível, espaço/posição de origem e espaço/posição de destino, e SHALL entrar no snapshot e no hash canônico como `[Canonical]` (`src/LivingWorld.Simulation/WorldSnapshot.cs:12-16` — propriedade pública sem `[Canonical]`/`[Volatile]` reprova o teste de cobertura).
2. WHEN um mundo com portais é serializado e reidratado THEN o round-trip SHALL preservar todos os portais e o hash canônico SHALL ser idêntico.
3. WHEN um espaço declara múltiplas entradas THEN o sistema SHALL suportar N portais para o mesmo par de espaços, distinguíveis por rótulo, sem nenhum ramo de código por entrada.
4. WHEN um cenário é carregado THEN os portais SHALL vir do cenário autorado, pelo mesmo caminho de dado declarativo de `SettlementAnchor`, e um cenário sem portais declarados SHALL continuar válido.
5. WHEN o cliente navega de um espaço para outro THEN a transição SHALL resolver por um portal consultado da projeção, nunca por comparação de coordenada embutida no cliente.
6. WHEN o dado canônico muda por causa desta story THEN os goldens SHALL ser regravados em commit explícito e justificado (`tests/golden/world-hashes.json`, via `dotnet test --filter ZZZ_record_golden_hashes` — `tests/LivingWorld.Tests/GoldenHashesTests.cs:19-29`), nunca como efeito colateral do gate.

**Independent Test**: carregar um cenário com uma cidade de dois portões, conferir round-trip de snapshot com hash estável, e navegar no cliente entrando pelo portão sul e depois pelo norte — a mesma cidade, dois portais distintos, nenhum código específico por portão.

---

## Edge Cases

- WHEN a conexão realtime cai durante a navegação THEN o sistema SHALL reidratar o snapshot do espaço atual e retomar sem escrita no mundo, preservando câmera e seleção.
- WHEN o snapshot recebido pertence a um escopo diferente do observado THEN o sistema SHALL descartá-lo em vez de renderizá-lo (a guarda atual — `web/src/App.tsx:52` — deve sobreviver ao redesenho).
- WHEN a entidade selecionada deixa de existir no snapshot (morte, desmaterialização) THEN o sistema SHALL fechar/limpar a seleção com uma mensagem, nunca renderizar dados congelados como se fossem atuais.
- WHEN o mapa tem dimensões muito grandes THEN o sistema SHALL continuar renderizando por viewport, sem alocar um canvas proporcional ao mundo inteiro (o teto atual de ~12000px — `web/src/gridFit.ts:5` — deixa de ser relevante quando a câmera existir).
- WHEN a aba fica em background e volta THEN o sistema SHALL saltar para o estado mais recente do motor, sem reproduzir a fila de estados perdidos.
- WHEN o mundo é trocado por `POST /worlds/create` durante uma sessão de observação THEN o sistema SHALL resetar espaço/câmera/seleção para um estado válido em vez de referenciar ids do mundo antigo.
- WHEN um espaço tem zero entidades THEN o sistema SHALL renderizar o grid vazio com indicação clara, nunca uma tela em branco.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| VTT2-01..05 | P1: Map Engine com câmera de verdade | Design | Pending |
| VTT2-06..10 | P1: Espaços hierárquicos e navegação Observer | Design | Pending |
| VTT2-11..15 | P1: NPCs em movimento com interpolação visual | Design | Pending |
| VTT2-16..21 | P1: Selecionar ≠ navegar ≠ agir + inspector flutuante | Design | Pending |
| VTT2-22..25 | P1: Inspector de NPC e Cidade com dados reais | Design | Pending |
| VTT2-26..31 | P1: Controles de tempo | Design | Pending |
| VTT2-32..36 | P1: Separação Simulation / View / Selection | Design | Pending |
| **VTT2-62..67** | **P1: SpatialPortal como conceito canônico de domínio** | Design | Pending |
| VTT2-37..41 | P2: LOD por zoom com nível agregado | - | Pending |
| VTT2-42..45 | P2: Cidade com footprint espacial (API-derived, OQ-1 resolvida) | - | Pending |
| VTT2-46..48 | P2: Layers/overlays selecionáveis | - | Pending |
| VTT2-49..52 | P2: Follow | - | Pending |
| VTT2-53..59 | P2: World Creator — presets e editor visual | - | Pending |
| VTT2-60..61 | P3: BuildingSpace | - | Pending |

**ID format:** `VTT2-[NUMBER]` — prefixo novo para não colidir com os `VTT-NN` da Fase 15.

**Numeração por AC:** cada ID corresponde a um AC na ordem em que aparece na story (ex.: VTT2-01 = AC1 do Map Engine).

**Nota de numeração:** `VTT2-62..67` continuam sendo os ids do SpatialPortal (agora 6 ACs, não 3) mesmo com a story promovida a P1 — os ids não foram renumerados de propósito, para não invalidar referências já feitas em `design.md`/`context.md`.

**Coverage:** 67 requisitos totais; mapeamento para tasks é feito em `tasks.md` (Test Co-location Validation).

---

## Success Criteria

Alinhados aos 20 critérios do master prompt §42, restritos ao que é verificável:

- [ ] O grid do espaço atual renderiza com pan/zoom fluidos e zoom focado no cursor.
- [ ] Uma cidade ocupa região (não ponto) no mapa-múndi, com o footprint derivado marcado como tal.
- [ ] Uma cidade com dois portões é navegável pelos dois, com portais como dado canônico e nenhum ramo de código por entrada; goldens regravados em commit explícito.
- [ ] Detalhes aparecem progressivamente conforme o zoom aumenta (agregado → dot → token → token+info).
- [ ] Abrir uma cidade parece continuação espacial, com breadcrumb permitindo orientação e retorno; câmera do espaço é preservada.
- [ ] NPCs se movem visualmente e a UI não fica atrasada em velocidade alta (sem backlog de animações).
- [ ] Só entidades relevantes ao espaço e ao viewport são renderizadas.
- [ ] Clique simples abre inspector com dados reais e **não** muda o contexto espacial; navegação exige double click / botão / zoom profundo.
- [ ] Pause, mudança de velocidade e +1 tick funcionam e a velocidade corrente é visível.
- [ ] N ticks com a UI conectada e navegando produzem o mesmo hash canônico que N ticks sem UI.
- [ ] Nenhuma regra de simulação (movimento autoritativo, pathfinding, economia, comportamento) existe no cliente.
- [ ] Nenhuma funcionalidade de Player Mode é implementada nesta fase.
- [ ] Criar um mundo a partir de um preset leva menos de 30 segundos e não exige nenhum parâmetro avançado.
