# Revisão de trabalho pendente (2026-10-12)

Auditoria estática de `ROADMAP.md`, `docs/roadmap/`, `.specs/features/`, `docs/audits/phase-gap-review.md`,
`STATE.md` e issues abertas. Cada item abaixo tem plano lean em `.specs/features/<slug>/plan.md` e uma issue.
Já abertas antes (não duplicadas): #20 `trade-ledger`, #21 `snapshot-positional-binary`,
#22 `labor-allocation` (fechar critérios), #23 mundo ao vivo perde ~65% da população.

## Itens novos
| Issue | Slug | Tema | Categoria | Prioridade |
| --- | --- | --- | --- | --- |
| #25 | `history-narrative-close` | fechar fases 10 e 12 | roadmap | P2 |
| #26 | `emergence-first-slice` | Fase 24, tecnologia por composição | roadmap | P3 |
| #27 | `player-incarnation` | Fase 25, encarnar e desaparecer | roadmap | P3 |
| #28 | `god-console` | Fase 26, pausa com teto e auditoria | roadmap | P3 |
| #29 | `gap-review-criteria-restore` | critérios fracos do gap review | spec | P1 |
| #30 | `specs-closeout` | rastreio e limpeza de specs | spec | P2 |
| #31 | `estate-inheritance` | herança de bens na morte | realism | P1 |
| #32 | `taxes-treasury` | imposto e tesouro da cidade | realism | P2 |
| #33 | `disease-epidemics` | doença e contágio | realism | P2 |
| #34 | `weather-drought` | ano de safra e seca | realism | P2 |
| #35 | `personality-heredity` | herança de personalidade | realism | P2 |
| #36 | `crime-and-theft` | furto por necessidade | realism | P3 |
| #37 | `tick-cost-scaling` | custo por NPC-tick plano até 10 mil | optimization | P1 |
| #38 | `event-log-size-budget` | sensor e meta de bytes do event log | optimization | P1 |
| #39 | `godot-server-deltas` | delta de interior, calendário e contrato | godot | P2 |
| #40 | `godot-art-audio-pass` | áudio, mobília e modelos | godot | P3 |

Prioridade: P1 = distorce o que o gate diz provar, ou o teto de custo; P2 = realismo ou fase com base pronta; P3 = depende de outra fase.

## Fases do roadmap sem entrega
- Fases 10 e 12: `pendente`, mas há código em `History/` e `Narrative/` e as linhas HIST-01..08 seguem Pending: #25.
- Fase 14 (Unreal): adiada por decisão (2026-07-29); o cliente é Godot (ADR-0022). Sem issue.
- Fase 15 (mapa web): `pendente` no índice, mas o cliente web foi removido (ADR-0022); o status precisa de revisão (#30).
- Fases 16 a 23: `spec`, bloqueadas pela regra do roadmap, com `design.md` e `tasks.md` no formato antigo; ativar uma é decisão de escopo.
- Fases 24, 25 e 26: primeira fatia em #26, #27 e #28. Fase 27 (cinemática) sem issue: exige 10 e 12 fechadas (#25).

## Specs sem plano lean ou verificação
- 37 diretórios em `.specs/features/` sem `checks.md` nem `verification.md`; tabelas Pending em `phase-04..11`: #30.
- `emergent-vehicles` e `npc-authoring-tools`: spec sem implementação confirmada; veículos depende de #26.
- Critérios mais fracos que o roadmap, sem AD (F3 a F7): #29.

## Otimização
- `scale-sensor.json`: 1,06 µs por NPC-tick a 1.000 NPCs e 1,51 a 5.000: #37.
- Snapshot em 43.863 e 56.846 B/NPC/ano contra a meta de 50 da Fase 9: #21. Event log sem sensor: #38.
- `RescheduleBatchParallel` sem uso em produção; avaliado em #37, só com hash idêntico.

## Cliente Godot
- Delta de interior e de `step`, calendário e forma congelada de `GET /npcs/{id}`: #39.
- Som, arte final, mobília e modelos por profissão: #40. Modo jogador: #27.
- CI do cliente antes do merge (goldens regravados num Mac arm64) segue pendência de `STATE.md`.

## Realismo (verificado por grep em `src/`)
| Lacuna | Evidência | Issue |
| --- | --- | --- |
| Herança de bens | só `Inherit` de poder extraordinário | #31 |
| Imposto e tesouro | `Mint/Destroy` sem sistema | #32 |
| Doença e contágio | só fauna e mecânica extraordinária | #33 |
| Clima e seca | só delta sazonal de temperatura | #34 |
| Personalidade herdada | AD-034 nunca fechado | #35 |
| Crime | nenhum `Theft/Crime` | #36 |
| População do mundo ao vivo | ~14 vivos por seed no ano 5 | #23 |

Sem issue por falta de evidência ou por depender de fase bloqueada: religião, imigração de fora do mapa,
educação formal (F8 só conta prédios), novo casamento de viúvos.
