# Experience Checklist — atualizações do shell

## Shell completo (doc §5/§26-29/§39-48/§105-107)

Após T1–T31, o usuário pediu Top Bar, Explorer, Inspector e Timeline bar com fidelidade ao
doc. Implementado fora de `tasks.md` (conectivo, como `App.tsx`/`tokens.css`).

**Resolve sem ação adicional:**

- Gap 2 (`LifeView` sem entry) — **corrigido**: "View full life" no `AgentView`; Explorer
  reusa `WorldFeed`/`StoryThreads`.
- Gap 1 (Pulse "dashboard") — contexto melhor: Inspector 340px + mapa no Center Stage (doc §74).

Gaps 3–5 corrigidos na rodada seguinte (abaixo).

## Fidelidade total ao doc (exceto itens desabilitados)

Pedido: implementar o restante do doc, exceto Simulation Controls, World Selector
switch/duplicate/export, Table/Inhabit Mode.

**Gaps restantes — corrigidos:**

- **Câmera** — `viewBox` pelo bounding box do conteúdo por nível de zoom.
- **Event markers** — settlements/agents de Story Thread com pulso (doc §103,
  `prefers-reduced-motion`).
- **Body detail** — `AgentFixture.bodyDetail` + drawer "View details" (doc §51-52).

**Itens novos implementados:**

- Places por região (`RegionFixture`, §42).
- People Nearby/Notable (`AgentFixture.notable`, §43).
- Organizations (`OrganizationFixture`, §44).
- Teclado nos marcadores (`role="button"`, §149).
- Atalhos W/F/`/`/? (§148) — sem Space/1/2/3 (sem simulação).
- Severidade de evento (`WorldEventFixture.severity`, §173).
- Toast crítico (`CriticalEventToast`, §172).

**Deliberadamente fora:** som ambiente (§170), mobile completo (§151-152, Out of Scope),
virtualização de mapa (§157-158), Table Mode (§116-124).

**Re-veredito "parece um mundo?"** — Reforçado: Center Stage mantém mapa; Inspector não
duplica o centro (doc §66/§87).
