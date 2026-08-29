# LivingWorld — World Explorer (Demo)

Demo isolada e descartável do fluxo `World → Settlement → Household → Agent → Why? → Causal
Explorer → Timeline` proposto no doc "Living World Cohesion" (pós-fase 16.2). Prova a
experiência de UX **antes** de qualquer integração com o backend real.

Ver `.specs/features/phase-16-3-web/spec.md` pro contexto completo (Goals/Out of Scope/User
Stories) e `design.md` pras decisões de arquitetura/visual.

## Escopo e isolamento

- **Projeto novo, sem import de `web/src/**`** — código próprio, sem dependência do cliente de
  produção.
- **Zero rede** — todo o dado vem de um fixture estático (`src/fixture/oakbridge.ts`, a vila de
  Oakbridge/Mira Valen do doc). Não fala com `LivingWorld.Api`, não roda simulação, não avança
  tempo real.
- **Descartável** — este projeto existe só pra validar a UX antes de integrar de verdade
  (`phase-16-3-world-cohesion`, backend). Não é o próximo cliente de produção.

## Como rodar

```bash
npm --prefix web-demo install
npm --prefix web-demo run dev     # http://localhost:5173 (ou a porta livre que o Vite escolher)
npm --prefix web-demo test        # suíte de testes (vitest)
npm --prefix web-demo run build   # type-check + build de produção
```

## O que foi portado vs. redesenhado

| Elemento | Origem | Por quê |
| --- | --- | --- |
| Token visual de NPC (`src/npc/appearance.ts`, `src/npc/NpcToken.tsx`) | **Cópia literal** de `web/src/npcAppearance.ts` / `NpcTokenSvg.tsx` | Único elemento explicitamente pedido pra reusar — fenótipo procedural (skin/hair/hairStyle/clothing) já validado no cliente atual |
| Mapa-múndi (`src/map/**`, SVG) | **Redesenho, depois corrigido de isométrico pra top-down** — settlements/agents como pontos, sem prédios | Isométrico (T1-T31) reportado pelo usuário como "não está funcionando bem" (AD-019) — trocado por projeção top-down ortogonal |
| Settlement View — terreno/roads/prédios/NPCs (`src/render/**`) | **Reescrita completa pra Canvas/WebGL (Pixi.js)** — substituiu o SVG declarativo | AD-020: pedido de redesign profundo (referência RimWorld) — usuário escolheu explicitamente Canvas/WebGL sobre manter SVG/React, mesmo sem caso de performance real nesta escala (11 NPCs) |
| Shell de 1 janela (`src/components/TopBar.tsx`/`Explorer.tsx`/`CenterStage.tsx`/`Inspector.tsx`/`TimelineBar.tsx`) | **Novo**, seguindo literalmente `LivingWorld — Frontend Experience & Design System.md` §5/§26-29/§39-46/§47-48/§105-107 | Doc pede um shell único (Top Bar / Explorer + World + Inspector / Timeline) pras 3 perspectivas (Observe/Table/Inhabit) — implementado 1:1 pra Observe, único modo real desta demo |
| Tema geral (cores, tipografia, painéis) — `src/styles/tokens.css` | **Novo**, baseado nos tokens literais do mesmo doc (§202) | `web/` não tinha um design system formal ainda; esta demo é onde ele entra pela primeira vez |
| Navegação/breadcrumb, stores (`NavigationStore`/`followStore`/`modeStore`) | **Novo**, idioma de store igual ao já usado em `web/src/state/*.ts` (`useSyncExternalStore`) | Estado de navegação específico desta demo, sem framework de roteamento |
| Interior de prédio | **Absorvido pelo `SettlementStage`** — `views/BuildingInterior.tsx` (view separada, top-down) foi removido | AD-020: o pedido explícito é revelar o interior fisicamente na mesma cena ao aproximar a câmera ("roof cutaway"), não trocar de tela — a versão anterior (view separada) foi uma decisão de uma rodada anterior, revertida nesta |

### Shell — decisões de adaptação (honestas, não escondidas)

O shell segue o doc literalmente onde o fixture/escopo permite; onde não permite, o padrão
adotado foi **mostrar desabilitado** em vez de esconder como quebrado (mesmo princípio do doc
§6 pro Inhabit Mode), nunca fabricar dado que o fixture não modela:

| Componente do doc | Estado nesta demo |
| --- | --- |
| Mode Selector (§32) | Observe é real; Table/Inhabit aparecem desabilitados com "Coming" — Table Mode é Out of Scope desta spec (decisão explícita do usuário) |
| Simulation Controls (§34-35) | Desabilitados — fixture é um snapshot congelado, não há simulação rodando pra pausar/acelerar (Out of Scope, decisão explícita do usuário) |
| World Selector (§31) | Só "World Details" é real — mundo único, sem troca de fixture em runtime (Out of Scope, decisão explícita do usuário) |
| Notifications (§111-112) | Reais — contagem de eventos que afetam entidades seguidas (`followStore`), não decorativo |
| Explorer "People" filtro (§43) | All/Nearby/Notable/Followed — todos reais (Nearby escopado à seleção atual, Notable via `AgentFixture.notable`) |
| Explorer "Organizations" (§44) | Corvin's Bakery — organização real no fixture, com membros clicáveis |
| Explorer "Places" (§42) | Agrupado por região (`RegionFixture`) |
| Agent Body detail (§51-52) | Drawer "View details" com físico completo + "what this affects" (`AgentFixture.bodyDetail`) |
| Map camera (§192) | `viewBox` centralizado no bounding box real do conteúdo, não mais fixo |
| Event markers (§103) | Pulso único no mount + marcador discreto pra settlements/agents tocados por um Story Thread |
| Event severity (§173) | `WorldEventFixture.severity` — acento visual por nível (routine/notable/major/critical) |
| Critical event toast (§172) | Real — mostra o evento "critical" do fixture ao carregar, dispensável, com atalho pro Causal Explorer |
| Keyboard shortcuts (§148) | W/F/`/`/? implementados (os que têm ação real nesta demo) |
| Map marker accessibility (§149) | Marcadores do mapa são focáveis, com `aria-label` e ativação por Enter/Space |
| Building interiors / roof cutaway (§29-36/§58-60) | Real, e agora **na mesma cena** (AD-020) — focar um prédio com `floors.length > 0` aproxima a câmera e faz o telhado (`Graphics`) desaparecer em alpha, revelando cômodos/móveis/NPCs dentro, sem trocar de view. North Farm fica sem interior modelado (`floors: []`); Rowan (o farmer) fica sem `indoorLocation` — ambos deliberados, não bug |

Só ficaram desabilitados os 3 itens que dependiam de recursos explicitamente fora do escopo
desta demo (Table/Inhabit Mode, simulação real, múltiplos mundos) — decisão do usuário, não
limitação técnica.

### Níveis de LOD

Pedido do usuário: Planeta / Continente / Cidade / Prédios / Interiores, com NPCs presentes em
cada um. Mapeamento nesta demo:

| LOD pedido | Nesta demo | NPCs visíveis? |
| --- | --- | --- |
| Planeta | **Não implementado** — decisão explícita do usuário ("não precisa agora") | — |
| Continente | `SemanticZoomMap` (SVG) — assentamentos + todo NPC como pontinho (AD-018). Terreno estilizado/estradas/rios do doc completo **ficam no backlog** (ver seção "Redesign — o que ficou de fora"), esta rodada focou Settlement View | Sim, sempre |
| Cidade / Prédios / Interiores | `SettlementStage` (Canvas/Pixi, AD-020) — terreno + roads + prédios com footprint real + NPCs, tudo na mesma cena; focar um prédio com interior aproxima a câmera e revela cômodos/móveis/NPCs por dentro (roof cutaway físico, não troca de view) | Sim, sempre — outdoor via patrolPoints, indoor via `indoorLocation` quando o prédio deles está focado |

## Redesign — Settlement View em Canvas/Pixi (AD-020)

Pedido do usuário: redesign profundo de toda a experiência visual, referência conceitual
RimWorld (mapa top-down, prédios com footprint real, roof cutaway físico, NPCs caminhando,
terreno rico, day/night, atividades visíveis, mundo em escala planetária navegável por zoom
contínuo). É essencialmente o escopo de um motor de jogo — perguntado explicitamente, o usuário
escolheu:

1. **Canvas/WebGL (Pixi.js) em vez de manter SVG/React** para o renderer, mesmo sem ganho de
   performance nesta escala (11 NPCs fixos, 3 settlements) — decisão explícita contra a
   recomendação de manter SVG.
2. **Settlement View primeiro**, com o resto do pedido **anotado nesta mesma fase
   (`phase-16-3-web`), sem virar fase nova** — é a lista abaixo.

### O que foi entregue nesta rodada

- `SettlementStage.tsx` — terreno (tiles com variação procedural via `tileNoise`, determinística
  por settlement), roads decorativas conectando prédios a um hub central (`generateRoads`,
  **layout de apresentação, não dado canônico** — mesmo princípio do §82 do doc aplicado à
  camada visual), prédios com footprint real derivado da contagem de cômodos (`buildingFootprint`
  — mais cômodos, prédio maior; sem interior modelado = footprint "de campo" tipo fazenda), NPCs
  como sprites reusando a MESMA identidade visual procedural de `appearance.ts` (não emoji, não
  arte nova).
- **Roof cutaway físico** — focar um prédio (`onFocusBuilding`) aproxima a câmera
  (`cameraState.focusOn`, zoom 2.4×) e faz o telhado desaparecer em alpha enquanto o interior
  (cômodos/móveis/NPCs, mesmo dado de `BuildingFixture.floors`) aparece, tudo dentro da MESMA
  cena — sem navegar pra outra tela. `nav.push({kind:"building"})` continua existindo pra
  URL/breadcrumb/back, mas a apresentação é 100% câmera.
- Pan (arrastar) e zoom (wheel) livres na cena do settlement.
- Movimento outdoor continua decorativo (AD-018, `patrolPositionAt`), agora avaliado por frame
  no ticker do Pixi (60fps) em vez de a cada 200ms — mais suave, sem mudar a natureza decorativa.

**2 bugs reais pegos e corrigidos na primeira passada visual ao vivo** (jsdom/testes não
pegam nenhum dos dois — nem canvas real, nem timing de carregamento de imagem):

- Textura do NPC criada com `Texture.from(image)` antes da `Image` terminar de decodificar —
  textura ficava 0×0, NPC invisível. Corrigido em `npcTexture.ts`: `getNpcTexture` agora é
  assíncrona, espera `image.decode()` antes de criar a `Texture`.
- Variação de terreno somava um inteiro direto no hex da cor (`GROUND_BASE + n`), estourando de
  canal pra canal e virando ruído de cor aleatório (tiles azuis/roxos/vermelhos num "gramado").
  Corrigido com `jitterColor` — varia R/G/B separadamente, clamped 0-255.

**Mais 2 bugs reais + 1 mudança de UX, achados no feedback seguinte do usuário** ("ainda não
consigo entrar nas casas" mesmo depois do fix acima; "tudo que abro na sidebar fica lá"):

- **Bug — captura de pointer cedo demais quebrava TODO clique num prédio/agent/terreno num
  mouse real.** `containerEl.setPointerCapture()` era chamado já no `pointerdown`. Uma vez
  capturado, o browser redireciona o `target` dos `pointermove`/`pointerup` seguintes pro
  elemento que capturou — então o listener do Pixi (que escuta direto no `<canvas>`) nunca via
  esses eventos, e nenhum "pointertap" era sintetizado. Só não aparecia em teste algum porque
  testes disparam o evento sintético direto no objeto Pixi mockado, nunca passam pelo
  redirecionamento de capture real do browser. Corrigido: captura só depois de confirmar que é
  arrasto de verdade (movimento > `CLICK_DRAG_THRESHOLD`), nunca no `pointerdown` em si.
- **AD-021 — navegação entre building/household/agent virou `replace`, não `push`.** Antes, cada
  clique empilhava (`nav.push`) — a sidebar direita nunca "soltava" o que foi aberto, e "voltar"
  tinha que ser feito um passo de cada vez. Agora household/agent/building são irmãos dentro do
  mesmo settlement: focar um substitui o foco anterior (`nav.replace`), e clicar no terreno vazio
  do mapa sempre volta pro settlement (novo prop `onBackgroundClick` do `SettlementStage`).
- **AD-021 — Causal Explorer/Timeline/Life/Feed/Threads abrem por CIMA do mapa, não substituem
  mais o centro.** Usuário: perder a cidade/NPC de vista pra checar "Why?"/Timeline era
  desorientador. `CenterStage` agora sempre monta o mapa (mundo ou settlement, derivado da
  última rota espacial na pilha) e renderiza essas 5 rotas como um painel flutuante por cima,
  com X / clique fora / Esc pra fechar (todos chamam `nav.back()`).

### O que ficou de fora desta rodada — mesma fase, backlog explícito

Nada disto foi escondido ou fingido feito; fica pendente na mesma `phase-16-3-web`:

- **World/Continent View redesign** — mapa-múndi continua SVG com pontos/círculos, sem terreno
  estilizado, rios, estradas, fronteiras ou zoom/pan interativo. Settlement View foi a fatia
  escolhida pelo usuário para esta rodada.
- **Day/night, iluminação, estações** — nenhum estado temporal real existe nesta demo fixture
  (snapshot congelado); implementar isso exigiria inventar um relógio, o que o próprio doc do
  usuário proíbe (§82) sem base em simulação real.
- **Atividades visíveis (sleep/eat/work/talk/etc.) e conversas** — exigiriam um schedule por
  agent que este fixture não modela; adicionar isso seria inventar comportamento, não só visual.
  `AgentFixture` teria que ganhar um novo campo decorativo-mas-explícito (como `patrolPoints` já
  é) antes de qualquer UI em cima — não feito aqui.
- **Veículos, caravanas, viagem entre settlements ("Follow" seguindo alguém pela estrada)** —
  fora do fixture atual (agents não têm rota entre settlements, só patrulha local).
- **Animações de caminhada com frames/estado (idle/walk/sit/sleep)** — o sprite é estático,
  só a posição interpola; não há troca de pose.
- **LOD/instancing pra milhares de agents** — irrelevante nesta escala (11 agents); a arquitetura
  atual (um `Sprite` por agent) já seria o gargalo certo pra resolver primeiro SE isso um dia
  importar de verdade.
- **Multi-floor com escada visível/transição de andar animada** — o seletor de andar troca o
  conteúdo desenhado instantaneamente (sem "Agent sobe a escada, câmera segue").

## Redesign — Sidebars/Inspector/Popup-Drawer (doc "Redesign das Sidebars, Inspector, Timelines")

Segundo doc do usuário, focado em informação: 4 níveis de profundidade (Glance/Compact
Detail/Popup-Drawer/Full Context View) e uma regra central — nunca espremer conteúdo de Nível
3-4 dentro da sidebar de 340px. Escopo combinado com o usuário: começar pela fundação
(Popup/Drawer, §19) + Agent Inspector redesenhado como prova de conceito, antes de espalhar pro
resto.

### O que foi entregue

- **`components/ContextOverlay.tsx`** — `Popup` (pequeno, 280-360px) e `Drawer` (médio,
  420-520px), Nível 3 do doc. Deliberadamente **não bloqueante** (doc §19: "modal contextual não
  bloqueante") — sem backdrop escurecido, diferente do Center Overlay que já existia (AD-021,
  causal/timeline/life/feed/threads) — o resto da tela continua visível/clicável atrás. Fecha
  com X, clique fora, ou Esc.
- **`components/InspectorPrimitives.tsx`** — `SectionHeader`, `EntityRow`, `StatusChips`,
  `MetricRow`, `SectionLink` (doc §29-32) — label/valor sempre na mesma linha, listas como linhas
  clicáveis compactas em vez de blocos de texto.
- **Agent Inspector redesenhado** (`views/AgentView.tsx`, doc §13) — seções CURRENTLY / STATUS /
  BODY / HOUSEHOLD / RELATIONSHIPS / RECENT / WHY?, cada uma compacta. "View physical details",
  "View relationships" e "Explain decision" (Why?) — que antes ficavam permanentemente expandidos
  inline na sidebar — agora abrem em `Popup` (doc §14: "não deixar os fatores ocupando
  permanentemente um card grande").

### Decisões de adaptação / o que ficou de fora

- **`Drawer` ainda sem consumidor real** — construído junto com `Popup` (mesma implementação
  interna, `ContextOverlay`) porque o doc pede os dois como a mesma fundação, mas nenhuma lista
  do Agent Inspector é grande o bastante pra precisar do tier "drawer médio" (todas cabem no
  `Popup`). Vai ganhar uso real quando Household/Settlement/Organization forem redesenhados (ex.:
  "View all households", "View all people") na próxima rodada.
- **"Locate" e "⋯" do header (doc §12)** — não implementados nesta rodada. "Locate" exigiria
  expor um jeito de centralizar a câmera do `SettlementStage` num agent a partir do Inspector
  (fora do escopo desta prova de conceito); "⋯" não tem nenhuma ação real pra abrigar ainda —
  melhor omitir do que criar um menu vazio.
- **Household/Settlement/Organization/Event Inspector, Explorer (§5-10), World/Agent Timeline
  (§20-28)** — doc completo cobre todos esses; só Agent Inspector foi redesenhado nesta rodada,
  o resto é a próxima fatia combinada com o usuário.

## Bugfix — clicar num NPC dentro de casa saía da casa (AD-025)

Três sintomas relatados em sequência, resolvidos juntos:

- **"Clico no NPC da casa, ele seleciona mas sai da casa"** — causa raiz real: clicar num agent
  chama `onSelectAgent`, que troca a rota inteira pra `{kind:"agent"}`; como o foco de prédio
  (`focusBuildingId`) só vinha de `route.kind === "building"`, a seleção do agent colapsava o
  foco junto, mesmo o agent continuando visualmente dentro da casa. Fix: `CenterStage.tsx` ganha
  `useFocusBuildingId`, que preserva o prédio focado se o agent selecionado já morava/trabalhava
  no prédio que já estava focado ANTES do clique (memória via `useRef`).
- **Regressão do fix acima** — "clico num NPC na rua, ele me leva pra casa dele direto": a v1
  ingênua (focar sempre que o agent tem `indoorLocation`) não checava se o prédio já estava
  focado, então QUALQUER clique num agent com casa "puxava" a câmera pra dentro. Corrigido
  checando a memória antes de criar foco novo.
- **"Faltou o comando reverso, clicar fora da casa deveria voltar pra rua"** — o guard do AD-023
  (desabilitava clique-fora-desfoca enquanto um prédio estava focado, pra evitar que um clique
  impreciso saísse sem querer) foi revertido — a causa raiz real era a de cima, não precisa mais
  desse guard, e o usuário pediu de volta o comportamento simétrico.
- **Bug invisível em todos os testes jsdom, só achado ao vivo no browser**: mesmo com
  `focusBuildingId` corretamente computado (confirmado com globals de debug temporários nos dois
  componentes), o overlay `settlement-stage-overlay` não aparecia no DOM real. Causa: o Pixi
  (`containerEl.appendChild(app.canvas)` / `.replaceChildren()` no cleanup) escrevia direto no
  MESMO nó DOM que o React usava pro overlay. O `replaceChildren()` apagava o `<div>` do overlay
  por baixo do React; na reconciliação seguinte o React tentava remover um nó que já não existia,
  lançava `NotFoundError` sem estar capturado, e derrubava a subtree inteira (sem nenhum erro
  visível na UI). Fix: `SettlementStage.tsx` agora tem um `<div ref={containerRef}/>` EXCLUSIVO do
  Pixi, irmão do overlay renderizado pelo React — nunca o mesmo nó — com `tokens.css` fazendo esse
  nó herdar o tamanho cheio do wrapper.

Ver AD-025 em `.specs/STATE.md` para detalhes completos.

## Segunda leva de fixes/redesign (AD-026)

Seis problemas visuais reportados pelo usuário depois da v1 do redesign, todos corrigidos:

- "View life timeline"/"View Timeline" colados → `.section-link` agora é `display:block`.
- Popup de relacionamentos sobrepondo o Inspector → reancorado à esquerda dele (`right: calc(340px + 1rem)`).
- Relacionamentos ganharam estilo "aba social" (ícone por categoria + força do vínculo, `RelationshipRow`) e uma árvore genealógica de verdade (`FamilyTree.tsx`, dados estruturados `familyRole` no fixture — Valen e Miller).
- Follow agora tem efeito real: a câmera trava na posição do agent seguido a cada frame (`SettlementStage`), em vez de só marcar um bookmark.
- Follow removido do Settlement Inspector — seguir uma cidade inteira não fazia sentido.
- Settlement e Building Inspector redesenhados com os primitives existentes (antes eram `<dl>`/`<ul>` cru).

Ver AD-026 em `.specs/STATE.md`.

## World Map (AD-032)

O World View trocou de SVG declarativo (`SemanticZoomMap`, settlements como círculos-pin) pra
`render/WorldStage.tsx`, um segundo renderer Pixi.js dedicado — mesma linguagem visual do
Settlement View (terreno procedural, rios, estradas conectando settlements por uma árvore
geradora mínima, footprints reais proporcionais à população, agents como pontos com cor estável
de fenótipo se movendo pelo trajeto de patrulha). Clicar num settlement ou agent dá zoom na
câmera do mapa mundi ANTES de trocar pra `SettlementStage` (animação de ponte, não um corte
abrupto) — dois renderers separados continuam existindo (decisão explícita do usuário: unificar
os dois numa única Pixi App/câmera é escopo maior, fica pra outra rodada). Follow funciona
igual ao Settlement (mesmo `activeFollowId`/anel/detach ao arrastar).

Novo: `map/worldPosition.ts` resolve a posição ABSOLUTA (grid do mapa mundi) de qualquer agent a
partir do fixture hierárquico atual — a fronteira exata onde plugar uma API real que já mande
X/Y absoluto (pedido do usuário: "no backend não temos separação de mundo/cidade/casa, é uma
posição X/Y absoluta única").

Ver AD-032 em `.specs/STATE.md`.

## World Map — bugs achados ao vivo (AD-033)

- Agents ganharam LOD de verdade: ponto discreto de longe, sprite real de perto (mesma textura
  do Settlement View), trocando sozinho por zoom.
- Clicar um NPC no mapa mundi virou seleção instantânea (abre a sidebar dele) em vez de "entrar
  na cidade" — só clicar um settlement continua com a animação de zoom.
- Hover em settlement/agent/building mostra um card LOD compacto perto do cursor
  (`components/MapHoverCard.tsx`) — mesmo componente usado no World Map e no Settlement View.

Ver AD-033 em `.specs/STATE.md`.

## Terceira leva — follow multi-NPC + polish (AD-027)

Achados testando ao vivo em cima do AD-026:

- Popup ganhou posicionamento de verdade por `anchorRect` (alinhado com a linha do botão, à esquerda dele) — o `right` fixo do AD-026 só resolvia a sobreposição, não o alinhamento pedido.
- Anel de "seguindo" trocado de círculo (cortava o corpo do NPC) pra elipse achatada nos pés.
- Múltiplos NPCs podem ser seguidos (bookmark, lista "Followed" nunca reordena) mas a câmera só trava no último ativado (`followStore.activeFollowId`); clicar um nome já seguido alterna o alvo sem mexer na lista.
- Arrastar o mapa de verdade "desgruda" a câmera de quem ela seguia (sem des-seguir) — só reata clicando o nome de novo ou seguindo outro agent.
- Building Inspector ganhou ícone por tipo + subtítulo, Occupants subiu pro topo.

Nota de processo: um dos bugs reportados ("following não faz nada") era o dev server do Vite com HMR preso numa versão antiga do módulo `SettlementStage.tsx` — resolvido reiniciando o processo, não só editando o código. Ver AD-027 em `.specs/STATE.md`.

## Comparação visual com `web/` (spec P1b Independent Test)

Verificado ao vivo, os dois projetos rodando lado a lado (`web/demo.html`, modo mock offline, vs
`web-demo/`):

- **Token de NPC — idêntico.** `web-demo/src/npc/appearance.ts` é diff-idêntico
  (`diff` sem saída) a `web/src/npcAppearance.ts` — mesmo algoritmo determinístico (hash FNV-1a
  por id), mesma paleta de skin/hair/clothing, mesmo SVG em camadas. Confirmado também por teste
  (`tests/npc/appearance.test.ts`, 5 ids fixos com saída idêntica ao algoritmo original).
- **Prédios/tiles/cidade — visivelmente diferentes.** `web/` usa um grid top-down 2D com
  telhados/paredes texturizados em tom rústico/medieval (`architectureAppearance.ts`); esta demo
  usa blocos isométricos 2:1 flat-shaded (3 faces sombreadas fixas, sem textura), paleta
  neutra/atmosférica (`isoPalette.ts`) — nenhuma sobreposição de estilo entre os dois.
- **Tema geral (painéis/cores/tipografia) — novo nesta demo.** `web/` não tinha design system
  formal; esta demo introduz o primeiro (dark-neutral, accent dourado, cores causais próprias
  pro Causal Explorer) baseado no doc de design do usuário.

## Estrutura

```
src/
  fixture/       dado estático (Oakbridge) + tipos
  npc/           token de NPC portado (appearance.ts + NpcToken.tsx)
  map/           mapa-múndi SVG (IsoProjection top-down, isoPalette, SemanticZoomMap,
                 patrolMath — matemática de patrulha decorativa, AD-018)
  render/        Settlement View — renderer Canvas/Pixi (AD-020): SettlementStage.tsx,
                 settlementLayout.ts (footprint/roads/terreno procedural), cameraState.ts
                 (pan/zoom/foco, puro/testável), npcTexture.ts (reusa appearance.ts como
                 textura Pixi)
  nav/           NavigationStore (pilha de breadcrumb + sync de URL)
  state/         followStore, modeStore (Experience/Debug)
  search/        SearchIndex (busca client-side)
  views/         conteúdo de entidade — Settlement/Household/Agent/Why/CausalExplorer/
                 Timeline/Life/WorldFeed/StoryThreads (consumidos pelo Inspector/CenterStage)
  components/    TopBar, Explorer, CenterStage, Inspector, TimelineBar (shell),
                 CriticalEventToast, Breadcrumb, FollowButton, SearchBar
  styles/        tokens.css (tema visual + layout do shell)
  App.tsx        composition root — monta o shell, troca cada região por
                 NavigationStore.current().kind
```

## Checklist de experiência

Ver [`docs/ui/living-world-experience-checklist.md`](../docs/ui/living-world-experience-checklist.md)
— checklist de design QA do doc do usuário aplicado contra esta demo rodando, com gaps
encontrados documentados (nenhum bloqueante).
