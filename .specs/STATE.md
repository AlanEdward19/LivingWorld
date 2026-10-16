# STATE

Índice. Decisões (`AD-NNN`) e handoff vivem em `.specs/state/` (cada arquivo ≤100 linhas, teto de `check-docs.sh`).

## Decisões e handoff

- [`state/decisions-01.md`](state/decisions-01.md) — decisões: AD-001 … AD-010
- [`state/decisions-02.md`](state/decisions-02.md) — decisões: AD-011 … AD-019
- [`state/decisions-03.md`](state/decisions-03.md) — decisões: AD-020 … AD-028
- [`state/decisions-04.md`](state/decisions-04.md) — decisões: AD-029 … AD-034
- [`state/handoff-01.md`](state/handoff-01.md) — handoff: (continuação)
- [`state/handoff-02.md`](state/handoff-02.md) — handoff: Histórico — gate hygiene (pré-16.3 Execute)
- [`state/handoff-03.md`](state/handoff-03.md) — handoff: Fixes commitados nesta retomada (eram 2 agentes background mortos antes do commit final) … Feature parada, pronta pra Execute quando o usuário disser — `real-household-workplace-buildings`
- [`state/handoff-04.md`](state/handoff-04.md) — handoff: Histórico anterior (fase 15.1 stage 4, pré-dynamic-city-growth) — mantido como referência … Histórico anterior (fase 15.1 bugfix, pré-fase-16) — mantido como referência

## Estado das specs (specs-closeout, 2026-10-12)

Uma linha por diretório de `.specs/features` em formato antigo; rastreio em cada spec.md/closeout.md. Lista de pendentes de estado: `bash scripts/specs-state.sh`.

- phase-04-needs | Parcial | Verified 15, Pending 1, Deferred 0
- phase-05-economy | Parcial | Verified 27, Pending 2, Deferred 0 (ECON-04/10..13/19 reverificados 2026-10-13)
- phase-06-skills | Parcial | Verified 15, Pending 0, Deferred 1
- city-house-layout | Parcial | Verified 3, Pending 2, Deferred 0 (closeout.md)
- npc-authoring-tools | Parcial | Verified 3, Pending 1, Deferred 0 (closeout.md)
- phase-07-family | Parcial | Fase fechada; 32 Verified, 4 Pending (FAM-06, 20, 22, 32 aberto por AD-088), 0 Deferred
- phase-08-cities | Entregue | Fase fechada; 9 Verified (CITY-01..09), 0 Pending, 0 Deferred
- phase-09-scale | Parcial | Fase fechada (AD-070); closeout.md: PERF-05/06/14 Verified (2026-10-13), PERF-07 Parcial, Pending PERF-04/12/16/17 e meta final, 1 Deferred (100 anos nightly)
- directional-city-bounds-growth | Spec | Nao implementado; closeout.md: 0 Verified, 6 Pending, 0 Deferred
- emergent-vehicles | Spec | Plano apenas, sem codigo; closeout.md: 0 Verified, 12 Pending, 0 Deferred
- phase-10-history | Entregue | 26 Verified, 0 Pending, 0 Deferred; ROADMAP.md atualizado para fechada (2026-10-14)
- phase-11-llm | Entregue | 6 grupos Verified, 0 Pending, 0 Deferred
- phase-12-narrative | Entregue | 6 grupos Verified, 0 Pending, 0 Deferred; ROADMAP.md atualizado para fechada (2026-10-14)
- phase-13-periods | Parcial | 7 grupos Verified, 1 Pending (PERIOD-11..13: sem teste que prove o payload do guia aceito por POST /periods), 0 Deferred
- phase-28-cognition | Parcial | 30 Verified, 1 Pending (COG-11 painel visual: web aposentado, sem painel Godot), 0 Deferred; ROADMAP.md marca a fase como parcial (2026-10-14)
- phase-15-map-visual | Parcial | Servidor/FOW/tokens provados por testes; cliente React retirado (ADR-0022); 2 Verified, 3 Pending, 0 Deferred (grupos VTT).
- phase-15.1-live-polish | Parcial | closeout.md criado; backend provado, HUD/render ao Godot; 8 Verified, 2 Pending, 3 Deferred.
- phase-15.1-stage-4-living-world | Parcial | closeout.md criado; cadeias de motor/API provadas (Stage4 tests); 5 Verified, 1 Pending, 1 Deferred.
- phase-15.1-vtt-frontend-redesign | Parcial | Portais canônicos provados; UI web retirada (ADR-0022); 1 Verified, 5 Pending, 9 Deferred (15 grupos).
- phase-16-3-web | Adiado | Demo web obsoleta (código web/ removido, ADR-0022); 0 Verified, 0 Pending, 20 Deferred.
- phase-16-powers | Parcial | 11 Verified, 0 Pending, 0 Deferred nas linhas POW-01..12; ressalva: efeitos/custos seguem switch C# fechado, motor dinâmico/catálogo web reabertos (memória phase16 reopen); ROADMAP.md marca a fase como parcial (2026-10-14).
- phase-16-1-power-engine | Parcial | closeout.md: 95 Verified, 0 Pending, 2 Deferred (PWR-40/41, entregues na 16.2); registro de mecânicas C# fechado, não dinâmico.
- phase-16-2-power-evolution | Entregue | 15 Verified, 0 Pending, 0 Deferred (EVO-01..22).
- phase-16-3-world-cohesion | Entregue | 37 Verified, 0 Pending, 0 Deferred (COH-01..66; COH-66 cita gate arquivado, não reexecutado).
- phase-16-4-world-realism | Parcial | 33 Verified, 1 Pending (REALISM-22: WorldEvent em vez de Fact), 0 Deferred.
- phase-16-perf-test-suite | Entregue | 7 Verified, 0 Pending, 1 Deferred (PERF-05, 3x nunca reexecutado; PERF-06 com desvio aprovado AD-005; PERF-04 N/A justificado).
- phase-17-divinity | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-18-timelines | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-19-cosmos | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-20-interdimensional | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-21-ontogeny | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-22-imperfection | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- phase-23-intrigue | Spec | Nada implementado; fase apenas especificada (criterios pendentes no ROADMAP).
- dynamic-city-growth | Entregue | 5/5 CITYGROW verificados por testes unit em Cities/ e Cities/Founding (entregue 2026-08-23).
- dead-code-audit | Entregue | closeout.md: 12 AUDIT + 17/18 EXEC verificados (2026-10-14), EXEC-02 descartado por decisao (DecisionMetrics mantido)
- real-household-workplace-buildings | Entregue | 8/8 HOMEWORK verificados por testes unit; tasks.md removido.
- trilha-c-dwarf-fortress-worldgen | Spec | 0/15 WGN verificados: 10 pendentes (sem endpoint years/branch efemero/kinds novos), 5 deferidos (tela web aposentada, ADR-0022); tasks.md mantido.
- emergence-first-slice | Spec | plano sem checks nem código
- god-console | Spec | plano sem checks nem código
- godot-art-audio-pass | Spec | plano sem checks nem código
- godot-server-deltas | Entregue | servidor (develop) e cliente Godot; ver checks.md e verification.md
- history-narrative-close | Spec | plano sem checks nem código
- player-incarnation | Spec | plano sem checks nem código
- specs-closeout | Entregue | só documentação; ver checks.md e verification.md
