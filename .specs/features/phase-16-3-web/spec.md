# Fase 16.3-web — World Explorer UX Demo Specification

Fonte: `Fase pós-16.2 — Living World Cohesion_ Causalidade, Agência, Atenção, Complexidade e Experiência.md` (doc do usuário, seções 101-148 — referenciadas abaixo como `doc#N`) + `.specs/features/phase-16-3-world-cohesion/spec.md` (Out of Scope: "web vira spec própria").

## Problem Statement

O doc de Living World Cohesion define uma reinvenção completa da experiência web — de "mapa + tabelas + cards + JSON" (doc#102) para um **World Explorer** que provoca "quem é essa pessoa? por que ela fez isso?" (doc#103). Essa reestruturação (IA de navegação, Why?/Causal Explorer, Timeline/Life/Follow, zoom semântico) é grande e arriscada o suficiente pra não ser validada só depois de integrada ao backend real (`phase-16-3-world-cohesion`). Esta spec entrega um **projeto novo, isolado, com dados falsos (fixture estático)** — sem simulação real, sem API real — só pra provar que a experiência funciona antes de qualquer integração custosa.

## Goals

- [ ] Provar visualmente o fluxo `World → Settlement → Household → Agent → Why? → Causal Explorer → Timeline` (doc#105, #156) navegável sem perder contexto ao voltar.
- [ ] Reusar o token visual de NPC já validado no cliente web atual (`web/src/npcAppearance.ts` + `web/src/components/NpcTokenSvg.tsx` — fenótipo procedural: skin/hair/hairStyle/clothing/clothingAccent, estável por id) — não redesenhar a pessoa. Tema geral (cores de painel/tipografia), prédios, cidades e tiles do mapa são **redesenhados** nesta demo (ver Goals abaixo e AC dedicado).
- [ ] Redesenhar a aparência de prédios/casas/cidades/tiles do mapa — usuário não gosta do estilo atual (`web/src/map-engine/**`); esta demo é a oportunidade de propor um visual novo pra esses elementos, mantendo só o token de NPC como âncora reconhecível.
- [ ] Demo roda 100% com dado fixo (fixture do próprio doc — vila de Oakbridge/Mira Valen/household Valen), zero chamada de rede, zero dependência do backend `LivingWorld.Api`.
- [ ] Mapa com zoom semântico funcional nos 3 níveis descritos no doc (mundo/distrito/agente, doc#106/#131) — mesmo que com dado estático.
- [ ] Checklist de experiência (doc#147) aplicado ao final — perguntas tipo "consigo entender por quê?", "parece um mundo ou um dashboard?" respondidas contra a demo antes de aprovar integração futura.

## Out of Scope

Explicitamente excluído. Documentado para prevenir scope creep.

| Feature | Reason |
| --- | --- |
| Integração com `LivingWorld.Api`/backend real | Decisão explícita do usuário — esta é uma demo de UX, dado é fixture estático; integração real é trabalho futuro depois que `phase-16-3-world-cohesion` (backend) e esta demo (UX validada) estiverem prontos |
| Simulação rodando, avanço de tempo real, WebSocket/`RealtimeGateway` | Sem simulação — fixture é um snapshot congelado de um momento (mais o histórico de eventos que já "aconteceu" até aquele momento) |
| Reescrita/substituição do cliente `web/` de produção | Projeto novo e separado (`web-demo/`), não toca `web/src/**` existente — cliente de produção continua rodando como está até decisão futura de merge/substituição |
| Múltiplos cenários/mundos, troca de fixture em runtime, editor de fixture | Um único fixture fixo (Oakbridge) é suficiente pra validar a experiência; troca de cenário é complexidade desnecessária pra uma demo de UX |
| Autenticação, multi-usuário, permissões | Demo local, single-user, sem backend — não se aplica |
| Mobile/responsivo completo | Prioridade desktop (doc#144) — mobile fica fora desta demo |
| Editor/Authoring de mundo (criar cenário do zero) | Fora do escopo do doc#101-148 (que é sobre EXPLORAR um mundo, não criar um) |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Fixture de dado | Literalmente o exemplo do doc — vila Oakbridge, Mira Valen (34, Baker, household Valen: cônjuge Tomas, filhos Eli/Nora), relações (Rowan/trusted, Corvin/disliked employer), cadeia causal "harvest below normal → grain stock declined → grain prices rose → Valen household failed purchase → Mira became VeryHungry → Mira left work early", Story Thread "The Oakbridge Food Crisis" (doc#108-130) | Escolha explícita do usuário — reusar o próprio exemplo do doc facilita validar se a UI entrega exatamente a experiência que o doc descreve, sem inventar dado novo | y |
| Mapa | Entra completo, com zoom semântico nos 3 níveis (mundo/distrito/agente) mesmo sendo dado estático | Escolha explícita do usuário ("Tudo entra") | y |
| Projeto/stack | Novo projeto `web-demo/` (sibling de `web/`), mesma stack (React + Vite + TypeScript) do cliente atual — reduz fricção de setup e mantém familiaridade, mas código próprio (sem import cruzado com `web/src/**`, é descartável/protótipo) | Doc não especifica stack; reusar a stack já validada no repo é o caminho de menor atrito (REUSE > CREATE) sem acoplar os dois projetos | n — assumido, revisar se usuário preferir outra stack |
| Escopo do "mesmo design atual" | Só o **token visual de NPC** (`npcAppearance.ts`/`NpcTokenSvg.tsx` — fenótipo procedural skin/hair/clothing) é reusado literalmente. Painéis/cores/tipografia (`global.css`), prédios, casas, cidades e tiles do mapa (`map-engine/**`) são redesenhados do zero nesta demo | Correção explícita do usuário: "Por design atual, eu me refiro aos tokens dos NPCs, só isso. Demais pode mudar" + "Não gosto da aparência dos prédios/casas, cidades e nem do estilo dos tiles que temos hoje" | y |
| Estilo "RimWorld" mencionado pelo usuário | Refere-se à CLAREZA de inspeção de pawn (doc#4.2, #113 — Agent Inspector card com "Currently/Why?/Condition/Household/Relationships/Recent life") — padrão de UX/legibilidade, não de skin visual | Doc já usa RimWorld como referência de padrão de UX (legibilidade/thresholds/prioridades); consistente com a correção acima (só o token de NPC é fixo, o resto é redesenho livre) | y |
| Direção do redesenho de prédios/tiles/cidades | **Decidido em Design (2026-08-25)**: isométrico simplificado — blocos pseudo-3D flat-shaded (3 faces sombreadas fixas), sem textura/gradiente/animação. Ver `design.md` § Tech Decisions | Escolha explícita do usuário entre as 3 direções propostas em Design | y |
| Semantic zoom com dado estático | Os 3 níveis (mundo/distrito/agente) mostram densidades de informação diferentes (doc#106-107, #131) sobre o MESMO fixture — não há múltiplas cidades/regiões reais, então zoom "mundo" mostra Oakbridge + 1-2 assentamentos vizinhos fictícios mínimos (só rótulo, sem detalhe), só pra provar a transição de zoom, não pra simular geografia completa | Fixture único (Oakbridge) não tem "mundo" real ao redor; um mínimo de contexto vizinho é necessário pra zoom "mundo" não ficar vazio, sem inventar um mundo inteiro | n — assumido |
| Animação/movimento no mapa (doc#132-133) | **SUPERSEDIDO (AD-018, 2026-08-26)** — ver `LivingWorld_Frontend_Final.md` (novo doc consolidado). NPCs agora visíveis e em movimento (scripted/decorativo, não derivado de simulação real) em todo nível de zoom | Usuário testou a demo, não bateu com a experiência esperada — pediu explicitamente "ver os NPCs vivendo no mundo" | y |
| Ocultar NPCs em zoom "mundo"/"distrito" (P1b AC1-2, doc#106 antigo) | **SUPERSEDIDO (AD-018)** — doc novo §14: "Agents não desaparecem apenas porque a câmera está distante." NPCs sempre visíveis, densidade de detalhe (não presença) muda por zoom | Doc consolidado é explícito e o usuário confirmou que este doc manda agora sobre o `LivingWorld — Frontend Experience & Design System.md` anterior nos pontos em que conflitam | y |

**Open questions:** nenhuma sem marcação — todas resolvidas acima ou log de assumption com racional.

---

## User Stories

### P1: World Explorer — fluxo vertical completo ⭐ MVP

**User Story**: Como usuário validando a proposta de UX do doc, quero navegar `World → Settlement → Household → Agent → Why? → Causal Explorer → Timeline` sobre o fixture de Oakbridge, pra sentir se a experiência cumpre o North Star do doc ("o que está acontecendo ali? quem é essa pessoa? por que ela fez isso?").

**Why P1**: É o vertical slice que prova a tese inteira do doc (doc#156-157) — sem ele, nenhuma outra tela isolada prova nada sobre COESÃO de navegação, que é o ponto central.

**Acceptance Criteria**:

1. WHEN o usuário abre a demo THEN o sistema SHALL mostrar a **World View** com mapa (zoom nível "mundo") mostrando Oakbridge e rótulos mínimos de 1-2 assentamentos vizinhos, mais um resumo do que está acontecendo no mundo (doc#107) derivado do fixture.
2. WHEN o usuário clica em Oakbridge no mapa OU na lista THEN o sistema SHALL navegar pra **Settlement View** mostrando Settlement Pulse (população, food/employment/migration/construction, eventos recentes — doc#108/#125) com os valores exatos do fixture do doc.
3. WHEN o usuário clica no household Valen (a partir da Settlement View ou de busca) THEN o sistema SHALL navegar pra **Household View** mostrando os membros (Mira/Tomas/Eli/Nora), árvore familiar simples, estoque/recursos, eventos recentes (doc#124/#574-608 exemplo).
4. WHEN o usuário clica em Mira Valen THEN o sistema SHALL navegar pra **Agent View** mostrando identidade/profissão/localização, intent atual ("Looking for affordable grain"), condição (Healthy·Tired·Hungry), corpo resumido, household, relações importantes (Rowan/Corvin), eventos de vida recentes, e um botão **Why?** (doc#109/#113).
5. WHEN o usuário clica em **Why?** THEN o sistema SHALL mostrar o painel de motivos em linguagem humana ("household food is low", "grain prices rose", "she is hungry" — doc#114) com pelo menos um fator clicável.
6. WHEN o usuário clica num fator do painel Why (ex.: "grain prices rose") THEN o sistema SHALL abrir o **Causal Explorer** mostrando `WHY? → Harvest below normal` e `CONSEQUENCES →` a árvore ramificada do doc#117-118 (Valen household reduced purchases → Mira became VeryHungry → Mira left work early; Baker reduced production; Migration pressure increased), com os sistemas envolvidos listados (doc#118: Agriculture/Economy/Household/Needs/Decision/Employment).
7. WHEN o usuário clica num evento do Causal Explorer THEN o sistema SHALL navegar pra esse ponto na **Timeline** (doc#120), preservando de onde veio (breadcrumb/histórico de navegação, doc#105 "sem perder contexto ao voltar").
8. WHEN o usuário usa o botão "voltar"/breadcrumb em qualquer ponto do fluxo THEN o sistema SHALL retornar à tela anterior com o estado de navegação preservado (não reseta pro World View).

**Independent Test**: Abrir a demo, seguir clique-a-clique World→Settlement→Household→Agent→Why→Causal Explorer→Timeline sem nenhum passo quebrar ou perder contexto; comparar telas contra os exemplos literais do doc (seções 108-120) pra conferir fidelidade.

---

### P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades ⭐ MVP

**User Story**: Como usuário, quero que o zoom do mapa mude a RESOLUÇÃO da informação (não só o tamanho), pra sentir a diferença entre "olhar o mundo" e "olhar uma pessoa" — E quero um visual novo de terreno/prédio/cidade, porque não gosto do estilo atual (`map-engine/**`).

**Why P1**: Doc#106 trata zoom semântico como definição central — sem isso o mapa é só decoração, não parte funcional da hierarquia de navegação. O redesenho visual é correção explícita do usuário sobre o estilo atual, e como o mapa é reconstruído do zero nesta demo (projeto novo, sem import de `web/src/map-engine`), é o momento natural de propor o visual novo em vez de portar o antigo.

**Acceptance Criteria — revisadas por AD-018 (2026-08-26), ver `LivingWorld_Frontend_Final.md` §14/§45/§96:**

1. WHEN o zoom está no nível "mundo" THEN o mapa SHALL mostrar assentamentos, rótulos, eventos importantes marcados, **E também todo NPC do fixture como um ponto pequeno posicionado perto do seu assentamento** — nenhuma entidade com localização física fica oculta só por causa do zoom (doc novo §14/§46, revoga o AC anterior "nenhum NPC visível").
2. WHEN o usuário dá zoom até o nível "distrito/assentamento" (dentro de Oakbridge) THEN o mapa SHALL mostrar edifícios importantes e áreas **E os NPCs desse assentamento posicionados juntos, na mesma cena** — prédios e pessoas nunca são alternativas mutuamente exclusivas de um toggle (doc novo §21-22, revoga o AC anterior "ainda sem NPCs").
3. WHEN um NPC é exibido em qualquer nível de zoom THEN sua posição SHALL refletir um movimento decorativo/scripted entre pontos do fixture (não derivado de simulação real — trade-off explícito do usuário sobre o doc novo §82, que pede posição sempre real) — clicável em qualquer ponto do trajeto, abrindo o Agent View.
4. WHEN o usuário clica num NPC/settlement diretamente no mapa em qualquer nível THEN o sistema SHALL navegar pra view correspondente, mesmo comportamento de clicar na lista (P1 AC2-4) — mapa e lista são duas entradas pro mesmo fluxo, nunca telas divergentes.
5. WHEN o mapa renderiza terreno, prédios ou o ícone de cidade/assentamento em qualquer nível de zoom THEN o visual SHALL ser um desenho novo (proposto na fase Design desta spec), não uma reprodução do estilo de tile/prédio/cidade do cliente `web/` atual.

**Independent Test**: Confirmar que NPCs aparecem em TODOS os níveis de zoom (nunca somem), que eles se movem entre pontos do fixture ao longo do tempo (decorativo, documentado como não-simulado), e que clicar num NPC em qualquer zoom abre o mesmo Agent View que clicar na lista; comparar lado a lado com screenshot do `web/` atual e confirmar que só o token de NPC é reconhecível como igual — terreno/prédio/cidade são visivelmente diferentes.

---

### P2: Timeline, Life View, Follow, World Feed

**User Story**: Como usuário, quero explorar o mundo ao longo do tempo (não só o estado atual), e "seguir" uma pessoa/lugar/thread pra acompanhar o que muda.

**Why P2**: Aprofunda a experiência temporal do doc (#120-129) mas não é necessário pra provar a tese central do fluxo causal (P1) — a demo já prova a coesão sem isso; Timeline/Life/Follow enriquecem, não validam a arquitetura.

**Acceptance Criteria**:

1. WHEN o usuário abre a **Timeline** (de qualquer ponto de entrada — World/Settlement/Household/Agent) THEN o sistema SHALL mostrar os eventos do fixture em ordem cronológica, filtráveis por escopo (World/Settlement/Household/Agent/tipo de evento — doc#121).
2. WHEN o usuário abre a **Life View** de Mira Valen THEN o sistema SHALL mostrar os marcos de vida do fixture (nascimento, mudou pra Oakbridge, virou aprendiz de padeira, casou com Tomas, filhos nasceram, virou master baker, pai morreu, atual — doc#122) numa timeline vertical/ramificada.
3. WHEN o usuário clica **Follow** num Agent/Household/Settlement/Thread THEN o sistema SHALL marcar a entidade como seguida (destaque visual persistente na sessão) sem alterar nenhum dado do fixture (doc#128 — "Follow altera apresentação, nunca simulação").
4. WHEN o usuário abre o **World Feed** THEN o sistema SHALL mostrar uma lista cronológica agrupada dos eventos do fixture com timestamps, priorizados por relevância (doc#129-130).

**Independent Test**: Abrir Timeline filtrada por household Valen, ver só os eventos relevantes; seguir Mira e confirmar destaque persiste ao navegar pra outra tela e voltar.

---

### P3: Story Threads, Experience/Debug Mode, Search

**User Story**: Como usuário, quero encontrar cadeias interessantes automaticamente e alternar entre visão "humana" e "técnica" da mesma informação.

**Why P3**: Nice-to-have que demonstra ideias adicionais do doc (#116, #126-127, #138) mas não é necessário pra validar a experiência central — pode ficar pra depois sem bloquear a decisão de integração.

**Acceptance Criteria**:

1. WHEN o usuário abre **Story Threads** THEN o sistema SHALL mostrar "The Oakbridge Food Crisis" (18 events · 4 households · 11 Agents · 6 systems — valores do fixture, doc#126) como card clicável que abre o Causal Explorer nesse thread.
2. WHEN o usuário alterna **Experience Mode ↔ Debug Mode** THEN o sistema SHALL trocar a linguagem/detalhe exibido (humano vs. técnico: WakeReason/Utility score/Tick — doc#116) sem trocar a navegação atual (mesma tela, mesma entidade selecionada).
3. WHEN o usuário usa a **busca global** THEN o sistema SHALL retornar resultados agrupados por People/Places/Households/Events/Threads (doc#138) filtrados pelo texto digitado, usando só o conteúdo do fixture.

**Independent Test**: Abrir Story Threads, clicar no card, cair no Causal Explorer certo; alternar Debug Mode na Agent View de Mira e ver campos técnicos aparecerem sem perder a seleção.

---

## Edge Cases

- WHEN o usuário navega direto pra uma URL profunda (ex.: `/agent/mira-valen`) THEN o sistema SHALL carregar o estado correto sem exigir passar por World View primeiro (deep-linking básico, mesmo sendo fixture estático).
- WHEN o Causal Explorer chega numa cadeia sem mais causa conhecida (raiz) THEN o sistema SHALL indicar claramente "sem causa anterior conhecida" em vez de mostrar um nó vazio/quebrado.
- WHEN o usuário clica **Follow** numa entidade já seguida THEN o sistema SHALL tratar como "deixar de seguir" (toggle), não duplicar o destaque.
- WHEN a janela é redimensionada abaixo do breakpoint desktop THEN o sistema SHALL manter os painéis legíveis (sem exigir suporte mobile completo — doc#144) — não precisa ficar bonito, precisa não quebrar.
- WHEN o usuário busca um termo sem resultado no fixture THEN o sistema SHALL mostrar estado vazio explícito ("nada encontrado"), nunca uma lista quebrada/undefined.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| WEBX-01 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-02 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-03 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-04 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-05 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-06 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-07 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-08 | P1: World Explorer — fluxo vertical | Tasks | Verified |
| WEBX-11 | P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades | Tasks | Verified |
| WEBX-12 | P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades | Tasks | Verified |
| WEBX-13 | P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades | Tasks | Verified |
| WEBX-14 | P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades | Tasks | Verified |
| WEBX-15 | P1b: Zoom Semântico + Redesenho de Tiles/Prédios/Cidades | Tasks | Verified |
| WEBX-21 | P2: Timeline, Life, Follow, Feed | Tasks | Verified |
| WEBX-22 | P2: Timeline, Life, Follow, Feed | Tasks | Verified |
| WEBX-23 | P2: Timeline, Life, Follow, Feed | Tasks | Verified |
| WEBX-24 | P2: Timeline, Life, Follow, Feed | Tasks | Verified |
| WEBX-31 | P3: Threads, Experience/Debug, Search | Tasks | Verified |
| WEBX-32 | P3: Threads, Experience/Debug, Search | Tasks | Verified |
| WEBX-33 | P3: Threads, Experience/Debug, Search | Tasks | Verified |

**ID format:** `WEBX-[NUMBER]` (World Explorer), agrupado por dezena (01-08 = P1, 11-14 = P1b, 21-24 = P2, 31-33 = P3).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 20 total, 20 mapeados a tasks (T1-T31), 0 unmapped.

---

## Success Criteria

- [ ] Fluxo completo `World → Settlement → Household → Agent → Why? → Causal Explorer → Timeline` navegável ponta-a-ponta sem quebrar nem perder contexto.
- [ ] Zoom semântico nos 3 níveis do mapa muda densidade de informação, não só escala visual.
- [ ] Checklist de experiência do doc (`docs/ui/living-world-experience-checklist.md`, doc#147) respondido "sim" pras perguntas centrais ("consigo entender por quê?", "parece um mundo ou um dashboard?") contra esta demo.
- [ ] Zero chamada de rede/API real — demo roda inteiramente offline com o fixture.
- [ ] Token de NPC reconhecível como o mesmo do cliente `web/` atual; prédios/casas/cidades/tiles do mapa são um visual novo, não uma reprodução do estilo atual.
