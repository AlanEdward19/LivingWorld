# Experience Checklist — §190 Design QA

Respondido por tela (World, Settlement, Household, Agent, Causal Explorer, Timeline).

| Pergunta | World | Settlement | Household | Agent | Causal Explorer | Timeline |
| --- | --- | --- | --- | --- | --- | --- |
| Qual pergunta esta tela responde? | "O que está acontecendo no mundo?" | "O que está acontecendo em Oakbridge?" | "Quem mora aqui, com o quê?" | "Quem é essa pessoa, o que ela faz?" | "Por quê, e o que isso causou?" | "O que aconteceu, em ordem?" |
| Qual é o primeiro elemento que vejo? | Título do mundo + resumo | Nome do settlement | Nome do household | Token do NPC + nome | Resumo do evento (dourado) | Filtro de tipo |
| Existe ação principal clara? | Sim — clicar num assentamento | Sim — clicar num household/NPC | Sim — clicar num membro | Sim — "Why?" | Sim — clicar numa consequência | Parcial — só filtrar |
| Há informação irrelevante? | Não | Não | Não | Não | Não | Não |
| Consigo navegar pra entidade relacionada? | Sim (settlement) | Sim (household, NPC via mapa) | Sim (agent) | Sim (household, causal, timeline) | Sim (timeline) | **Não** — eventos não abrem Causal Explorer |
| Consigo voltar? | N/A (raiz) | Sim, breadcrumb+Back | Sim | Sim | Sim | Sim |
| Funciona sem portrait? | Sim | Sim | Sim (NpcToken procedural) | Sim | N/A | N/A |
| Funciona com nomes longos? | Risco baixo (flexbox) | idem | idem | idem | idem | idem |
| Funciona com qualquer gênero de mundo? | Sim — UI neutra | idem | idem | idem | idem | idem |
| Parece um mundo ou um dashboard? | **Mundo** | **Quase mundo** (Pulse mini-tabela) | Mundo | Mundo | Mundo | **Mais dashboard** |

**Veredito §190**: 5/6 telas leem como "mundo". Settlement Pulse e Timeline são os pontos mais
"dashboard" — gap documentado, não bloqueante (T29 documenta, não redesenha).
