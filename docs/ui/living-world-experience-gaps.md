# Experience Checklist — perguntas centrais e gaps

## Perguntas centrais (`spec.md` Success Criteria)

- **"Consigo entender por quê?"** → **Sim.** Why → Causal Explorer → cadeia de consequências
  (Mira → "grain prices rose" → árvore de 18 eventos).
- **"Parece um mundo ou um dashboard?"** → **Majoritariamente mundo**, com exceções documentadas
  (Settlement Pulse e Timeline). Spec permite gap documentado em vez de fechamento forçado.

## Gaps iniciais (T29 — nenhum corrigido na mesma task)

1. Settlement Pulse e Timeline leem mais como "dashboard".
2. `LifeView` (T22) sem entry point no `App.tsx`.
3. Mapa não centraliza câmera nos assentamentos.
4. Sem marcadores de evento no mapa (doc §103).
5. Body sem detalhe físico completo — limitação do fixture `bodySummary`.

Nenhum bloqueia fechamento da fase quando as perguntas centrais não são unanimemente "sim".

## Status após shell + fidelidade

| # | Gap | Status |
| --- | --- | --- |
| 1 | Pulse/Timeline "dashboard" | Parcial — Pulse no Inspector com mapa sempre visível |
| 2 | LifeView sem entrada | **Corrigido** — botão "View full life"; Explorer Events/Threads |
| 3 | Câmera excêntrica | **Corrigido** — viewBox por bounding box |
| 4 | Sem event markers | **Corrigido** — pulso em settlements/agents de thread |
| 5 | Body resumido | **Corrigido** — `bodyDetail` + drawer no AgentView |

Ver detalhes em [shell-updates.md](living-world-experience-shell-updates.md).
