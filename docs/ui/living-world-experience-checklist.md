# Living World Experience Checklist — aplicado à demo `web-demo/`

**Fonte real do checklist**: `LivingWorld — Frontend Experience & Design System.md`, seções
**190 (Design QA checklist)**, **191 (Agent View QA)**, **192 (Map QA)** e **193 (Table Mode
QA)**. `spec.md` desta feature citava "doc#147" — checado contra o doc real, seção 147 é "Deep
Links", não o checklist; as duas perguntas centrais que `spec.md` citava literalmente
("consigo entender por quê?", "parece um mundo ou um dashboard?") pertencem à seção 190. Table
Mode QA (193) não se aplica — esta demo não implementa Table Mode.

**Evidência**: demo rodada localmente via `npm --prefix web-demo run dev` (porta 5174),
percorrida manualmente no browser (World → Oakbridge → Valen Household → Mira Valen → Why? →
Causal Explorer → Timeline → Back), screenshots e leitura de DOM incluídas inline.

---

## § 190 — Design QA checklist

Respondido por tela (World View, Settlement View, Household View, Agent View, Causal Explorer,
Timeline), critério a critério.

| Pergunta | World | Settlement | Household | Agent | Causal Explorer | Timeline |
| --- | --- | --- | --- | --- | --- | --- |
| Qual pergunta esta tela responde? | "O que está acontecendo no mundo?" | "O que está acontecendo em Oakbridge?" | "Quem mora aqui, com o quê?" | "Quem é essa pessoa, o que ela faz?" | "Por quê, e o que isso causou?" | "O que aconteceu, em ordem?" | 
| Qual é o primeiro elemento que vejo? | Título do mundo + resumo | Nome do settlement | Nome do household | Token do NPC + nome | Resumo do evento (dourado) | Filtro de tipo |
| Existe ação principal clara? | Sim — clicar num assentamento | Sim — clicar num household/NPC | Sim — clicar num membro | Sim — "Why?" | Sim — clicar numa consequência | Parcial — não há ação além de filtrar |
| Há informação irrelevante? | Não | Não | Não | Não | Não | Não |
| Consigo navegar pra entidade relacionada? | Sim (settlement) | Sim (household, NPC via mapa) | Sim (agent) | Sim (household, causal, timeline) | Sim (timeline) | **Não** — eventos não são clicáveis pra abrir o Causal Explorer de volta |
| Consigo voltar? | N/A (raiz) | Sim, breadcrumb+Back | Sim | Sim | Sim | Sim |
| Funciona sem portrait? | Sim (sem portrait aqui) | Sim | Sim (NpcToken sempre presente, é procedural — nunca falta) | Sim | N/A | N/A |
| Funciona com nomes longos? | Não testado com nome extremo — layout é flexbox/lista, não hardcoda largura, risco baixo | idem | idem | idem | idem | idem |
| Funciona com qualquer gênero de mundo? | Sim — fixture é o único dado "temático", UI é neutra | idem | idem | idem | idem | idem |
| Parece um mundo ou um dashboard? | **Mundo** — mapa + prosa, não tabela de métricas cruas | **Quase mundo** — `dl` de pulse ainda lê como uma mini-tabela de métricas, mas com prosa ao redor | Mundo — família em prosa, não grid | Mundo — cartão de identidade, não formulário | Mundo — cadeia narrativa em árvore | **Mais dashboard** — lista crua de eventos, sem agrupamento visual forte além do filtro |

**Veredito §190**: 5 de 6 telas respondem "mundo" com confiança razoável. Settlement Pulse e
Timeline são os dois pontos mais "dashboard" da demo — ambos são listas de dados sem a camada
narrativa que as demais telas têm. **Gap documentado, não bloqueante**: nenhuma correção foi
feita agora (fora do escopo de T29, que é aplicar/documentar o checklist, não redesenhar).

---

## § 191 — Agent View QA (Mira Valen)

| Pergunta | Resposta | Evidência |
| --- | --- | --- |
| Consigo saber imediatamente o que ele está fazendo? | Sim | `currentIntent`: "Looking for affordable grain" — primeira linha depois do nome |
| Consigo saber por quê? | Sim | Botão "Why?" abre `WhyPanel` com 3 fatores em linguagem humana |
| Consigo saber onde ele está? | Sim | `agent-location` mostra "Oakbridge" |
| Consigo ver família/relações? | Sim | Botão do household (Valen Household) + lista de relações (Rowan · trusted, Corvin · disliked employer) |
| Consigo descobrir diferenças físicas relevantes? | Parcial | `bodySummary.build` ("Average height · Strong") — resumo de uma linha, sem o nível de detalhe do doc §51-52 (height/weight/muscle mass/etc. — não existe no fixture) |
| Consigo acessar vida e história? | **Não, a partir daqui** | `LifeView` existe (T22) mas não há link a partir do `AgentView` — só é alcançável se algo mais chamar `nav.push({kind:"life", agentId})`, o que hoje não acontece em lugar nenhum do app real |
| Consigo acessar eventos causais? | Sim | Fatores do Why clicáveis abrem `CausalExplorer` no evento certo |

**Veredito §191**: 5 de 7 "sim", 1 parcial (limitação de dado do fixture, não de UI), **1 gap
real de navegação** — `LifeView` foi construído (T22) mas nunca ligado a nenhum ponto de
entrada do fluxo real (`App.tsx`). Gap documentado, não corrigido nesta task (T25 já fechou
"Timeline acessível de X" — Life/Feed/Threads ficaram sem entry point equivalente, e nenhuma
task do tasks.md pedia isso explicitamente para Life/Feed).

---

## § 192 — Map QA

| Pergunta | Resposta | Evidência |
| --- | --- | --- |
| Consigo saber onde olhar? | **Não totalmente** | O SVG do mapa não tem `viewBox`/câmera centralizada nos assentamentos — no load, os 3 marcadores do nível "mundo" aparecem espalhados perto do canto superior esquerdo do `viewBox` 800×600, não centralizados. Achado incidental, não corrigido (fora do escopo desta task) |
| Existe ruído demais? | Não | Fundo escuro, poucos elementos, paleta neutra |
| Markers se sobrepõem? | Não, no fixture atual | 11 agents de Oakbridge com `gridPosition` distintos o bastante pra não colidir visualmente na projeção isométrica testada |
| Zoom muda informação semanticamente? | **Sim** | Confirmado nos testes (T9/T10) e visualmente: mundo = só rótulos; distrito = prédios sem NPC; agente = NPCs clicáveis |
| Evento importante fica perceptível? | **Não** | Nenhum marcador de evento no mapa (doc §103 "event markers") — fora do escopo do design.md desta demo (P1b não pedia isso) |
| Posso selecionar facilmente uma entidade? | Sim | Testado — clique em settlement/building/NPC dispara a navegação correta em todos os 3 níveis |

**Veredito §192**: núcleo funcional (zoom semântico, seleção) passa. Dois gaps de polish
visual documentados como achados, não corrigidos agora: falta de centralização de câmera e
ausência de marcadores de evento no mapa — ambos consistentes com o escopo do design.md
("Camera.ts" citado só como referência de princípio, nunca implementado; marcadores de evento
nunca foram pedidos em nenhuma task do tasks.md).

---

## § 193 — Table Mode QA

**Não aplicável.** Esta demo implementa só o modo Observer (Experience/Debug, doc §129-130) —
Table Mode (doc §116-124) está fora do escopo de `phase-16-3-web` (ver spec.md Out of Scope:
não há sessão de RPG, GM controls, cast list, etc. nesta demo).

---

## Perguntas centrais (citadas literalmente em `spec.md` Success Criteria)

- **"Consigo entender por quê?"** → **Sim.** Fluxo completo Why → Causal Explorer → cadeia
  ramificada de consequências funciona ponta a ponta (verificado ao vivo: Mira → "grain prices
  rose" → árvore de 18 eventos, sistemas envolvidos corretos).
- **"Parece um mundo ou um dashboard?"** → **Majoritariamente mundo**, com 2 exceções
  documentadas acima (Settlement Pulse e Timeline ainda leem como lista de dados). Não é uma
  reprovação binária — a spec já previa que gaps viram achado documentado, não bloqueio de
  fechamento ("se não, vira gap documentado, não fechamento forçado").

---

## Resumo de gaps encontrados (nenhum corrigido nesta task — fora do escopo de T29)

1. Settlement Pulse e Timeline leem mais como "dashboard" que como "mundo".
2. `LifeView` (T22) não tem nenhum ponto de entrada no app real (`App.tsx`) — só é alcançável
   programaticamente.
3. Mapa não centraliza a câmera nos assentamentos/prédios — layout inicial é excêntrico.
4. Nenhum marcador de evento importante no mapa (doc §103) — não estava no escopo de nenhuma
   task.
5. `AgentView`'s "Body" não tem o nível de detalhe do doc §51-52 (height/weight/etc.) porque o
   fixture (`AgentFixture.bodySummary`) só guarda um resumo de uma linha — limitação de dado,
   não de UI.

Nenhum destes bloqueia o fechamento desta fase — a spec explicitamente permite gap documentado
em vez de fechamento forçado quando as perguntas centrais não são unanimemente "sim".

---

## Atualização — Shell completo (doc §5/§26-29/§39-48/§105-107)

Depois do fechamento inicial (T1-T31) e da verificação independente (PASS,
`validation.md`), o usuário pediu explicitamente o shell completo do doc de design
("Frontend Experience & Design System"), não só o tema visual — Top Bar, Explorer, Inspector
e Timeline bar, com fidelidade 1:1 ao doc. Implementado como trabalho adicional fora de
`tasks.md` (mesma categoria do `App.tsx`/`tokens.css` original — conectivo necessário, não
coberto por nenhuma task numerada).

**Isso resolve, sem ação adicional, 2 dos 5 gaps listados acima:**

- Gap 2 (`LifeView` sem entry point) — **corrigido**: `AgentView` ganhou o botão "View full
  life" (doc §61), e a lista de gaps do Verifier também apontava que `WorldFeed`/`StoryThreads`
  tinham o mesmo problema — **corrigido** como efeito colateral do Explorer (tabs Events/Threads
  reusam esses componentes diretamente).
- O Settlement Pulse "ainda lê como dashboard" (gap 1, parte) muda de contexto: antes era uma
  tela inteira sozinha; agora é o conteúdo do Inspector (340px) ao lado do mapa vivo (Center
  Stage) — o mapa nunca desaparece enquanto o Pulse é lido, o que é mais fiel ao doc §74 ("Ao
  selecionar Oakbridge, o centro muda... não necessariamente abre página nova") do que a versão
  anterior (Settlement View como página cheia com o mapa embutido embaixo).

**Gaps que continuam abertos nesta rodada** (3, 4, 5 da lista acima — câmera não centralizada,
sem marcadores de evento no mapa, Body sem detalhe físico) — não tocados nesta rodada
específica do shell, mas resolvidos na rodada seguinte (ver "Atualização — Fidelidade total ao
doc" abaixo).

---

## Atualização — Fidelidade total ao doc (exceto decisões explícitas de desabilitar)

Pedido do usuário: implementar tudo do doc que ainda não tinha sido feito, exceto os 3 itens já
decididos explicitamente como desabilitados (Simulation Controls, World Selector
switch/duplicate/export, Table/Inhabit Mode). Fecha os 3 gaps restantes da rodada anterior e
adiciona itens do doc nunca cobertos antes:

- **Gap 3 (câmera não centralizada)** — **corrigido**. `viewBox` agora é calculado a partir do
  bounding box real do conteúdo renderizado em cada nível de zoom, com fallback pra um extent
  fixo quando não há nada pra centralizar (ex.: Millbrook/Stonehaven sem prédios).
- **Gap 4 (sem marcadores de evento)** — **corrigido**. Settlements/agents tocados por um Story
  Thread ganham um marcador discreto com pulso único no mount (doc §103), respeitando
  `prefers-reduced-motion`.
- **Gap 5 (Body sem detalhe físico)** — **corrigido**. Fixture ganhou `AgentFixture.bodyDetail`
  (height/weight/muscle mass/etc. + "what this affects" por traço, doc §51-52) pros 11 agents;
  `AgentView` ganhou um drawer "View details".

**Itens novos do doc, nunca cobertos antes, implementados nesta rodada:**

- Places agrupado por região (`RegionFixture`, doc §42).
- People "Nearby"/"Notable" reais (doc §43) — fixture ganhou `AgentFixture.notable`.
- Organizations real — "Corvin's Bakery" (`OrganizationFixture`, doc §44).
- Acessibilidade de teclado nos marcadores do mapa — `role="button"`/`tabIndex`/`aria-label`/
  Enter-Space (doc §149, "obrigatório").
- Atalhos de teclado globais W/F/`/`/? (doc §148) — limitados aos que têm ação real nesta demo
  (Space/1/2/3 ficam de fora porque não há simulação rodando pra controlar).
- Severidade de evento (`WorldEventFixture.severity`, doc §173) com acento visual em
  WorldFeed/Timeline/CausalExplorer.
- Toast de evento crítico (doc §172) — `CriticalEventToast`, mostra o evento "critical" do
  fixture ao carregar o app.

**Não implementado, por ser opcional/irrelevante no doc mesmo** (não são gaps, são deliberadamente
fora): som ambiente (§170, doc já diz "opcional... desligado por padrão"), responsivo
mobile/tablet completo (§151-152, já Out of Scope do spec.md), virtualização/clustering de mapa
(§157-158, só relevante em escala — o fixture tem 11 agents), Table Mode completo (§116-124,
decisão explícita do usuário de deixar desabilitado).

**Novo veredito nas perguntas centrais**, re-verificado ao vivo com o shell montado:

- **"Parece um mundo ou um dashboard?"** — Reforçado como "mundo": o usuário nunca vê uma tela
  vazia de mapa (Center Stage sempre mostra o mapa da seleção atual, exceto quando Causal
  Explorer/Timeline/Life/Feed/Threads o substituem de propósito, como o doc pede em §66/§87), e
  o Inspector nunca duplica o que já está no centro (mostra uma nota contextual em vez disso).

**Decisões de adaptação do shell** (onde o fixture não sustenta o doc literal — mostrado
desabilitado, nunca escondido, nunca fabricado): ver `web-demo/README.md` § "Shell — decisões
de adaptação".
