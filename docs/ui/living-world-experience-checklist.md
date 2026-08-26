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
