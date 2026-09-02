# Experience Checklist — §191 Agent, §192 Map, §193 Table

## §191 — Agent View QA (Mira Valen)

| Pergunta | Resposta | Evidência |
| --- | --- | --- |
| Consigo saber imediatamente o que ele está fazendo? | Sim | `currentIntent`: "Looking for affordable grain" |
| Consigo saber por quê? | Sim | Botão "Why?" → `WhyPanel` com 3 fatores |
| Consigo saber onde ele está? | Sim | `agent-location`: "Oakbridge" |
| Consigo ver família/relações? | Sim | Household + relações (Rowan · trusted, Corvin · disliked employer) |
| Consigo descobrir diferenças físicas relevantes? | Parcial | `bodySummary.build` — uma linha; fixture sem detalhe §51-52 |
| Consigo acessar vida e história? | **Não, a partir daqui** | `LifeView` (T22) sem link no `AgentView` no fechamento inicial |
| Consigo acessar eventos causais? | Sim | Fatores do Why abrem `CausalExplorer` |

**Veredito §191**: 5/7 sim, 1 parcial (fixture), **1 gap de navegação** (`LifeView` sem entry
point no fluxo real — corrigido na rodada do shell; ver [shell-updates.md](living-world-experience-shell-updates.md)).

## §192 — Map QA

| Pergunta | Resposta | Evidência |
| --- | --- | --- |
| Consigo saber onde olhar? | **Não totalmente** (inicial) | Marcadores no canto do viewBox — corrigido depois |
| Existe ruído demais? | Não | Fundo escuro, poucos elementos |
| Markers se sobrepõem? | Não no fixture | 11 agents com `gridPosition` distintos |
| Zoom muda informação semanticamente? | **Sim** | Mundo = rótulos; distrito = prédios; agente = NPCs clicáveis |
| Evento importante fica perceptível? | **Não** (inicial) | Sem marcadores de evento — corrigido depois |
| Posso selecionar facilmente uma entidade? | Sim | Clique settlement/building/NPC navega corretamente |

**Veredito §192**: núcleo (zoom, seleção) passa. Câmera e event markers eram gaps de polish —
corrigidos na rodada de fidelidade (ver [shell-updates.md](living-world-experience-shell-updates.md)).

## §193 — Table Mode QA

**Não aplicável.** Demo só Observer/Debug — Table Mode fora do escopo de `phase-16-3-web`.
