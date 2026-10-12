# Servidor empurra interior, calendário e contrato (issue #39)

## Problem
O cliente Godot relê o snapshot do interior a cada 1 s porque o servidor só publica delta de mundo e cidade; assume dia de
24 h porque o calendário do cenário não é servido; e `GET /npcs/{id}` e os deltas não têm forma congelada em `tests/contracts/`.
Quem paga: o interior atrasa até 1 s e custa uma leitura completa por segundo; um cenário com outro calendário mostra a hora
errada; mudar o delta quebra o cliente sem que nenhum teste avise. Sem medida de custo registrada.
Ao entregar, o interior atualiza no mesmo tick, a hora usa o calendário do cenário e o contrato dessas rotas está congelado.

## Flow
Reusa `RealtimeGateway` (cursor, replay) e `InteriorProjector`; só acrescenta um escopo ao laço que já publica.
1. `TickLoopService` (exists) - para cada escopo `interior:<id>` com assinante, projeta os ocupantes e publica `InteriorTickDelta`
2. `RealtimeGateway` (exists) - atribui `sequence`, guarda no log e entrega ao canal e ao `replay`
3. `SimulationControlEndpoints` (exists) - `GET /simulation/status` passa a trazer o calendário de `WorldState`
4. `ApiContractTests` (exists) - congela `shape.npc-detail`, `shape.visual-delta`, `shape.interior-delta`
5. Cliente (exists) - `WorldStore` aplica o delta de interior; o `App` deixa de reler; `DayCycle` usa `hoursPerDay`

## Impact
| Front | What changes |
| --- | --- |
| domain | existing term: `hoursPerDay` - deixa de ser 24 fixo no cliente; quem lê hoje: `DayCycle` e a barra de hora |
| stored data | nothing to migrate: só leitura |

## Relations
`None - no stored-data shape change`

## Surface
| Route | In | Out | Status |
| --- | --- | --- | --- |
| `GET /visual/ws` (scope Interior) | `scope`, `refId`, `mode` | delta `{upserts[npc, floor, localCell, action], removed[npc]}` | `101`, `403` |
| `GET /simulation/status` | none | `isPaused`, `ticksPerSecond`, `tick`, `year`, `hoursPerDay`, `daysPerMonth`, `monthsPerYear` | `200` |

## Landing
| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1 Forma do delta de interior | `InteriorTickDelta(tick, upserts[], removed[])` com o `InteriorOccupant` completo | diff em 4 listas (entrou, moveu, trocou andar, saiu): o cliente teria de juntar quatro caminhos e ainda perderia a mudança de ação |
| 2 Calendário no status | três inteiros ao lado de `tick` e `year` | rota nova: mais uma chamada por segundo só para três números |

## Criteria
### S1: Delta de interior (P1)
O interior atualiza no mesmo tick.
**Acceptance Criteria**
1. WHEN um NPC entra, sai, muda de andar, de célula ou de ação num prédio com assinante THEN the system SHALL publicar um delta com `upserts` e `removed` desse prédio no mesmo tick.
2. IF o prédio do escopo não existe THEN the system SHALL não publicar delta nem derrubar o laço.
3. The delta de interior SHALL carregar `sequence` e `replay` como os de mundo e cidade.
### S2: Calendário e contrato (P1)
A hora vem do cenário e o contrato fica congelado.
**Acceptance Criteria**
4. WHEN `GET /simulation/status` responde THEN the system SHALL trazer `hoursPerDay`, `daysPerMonth` e `monthsPerYear` do calendário do mundo.
5. The contract SHALL congelar a forma de `GET /npcs/{id}`, do delta de mundo/cidade e do delta de interior em `tests/contracts/`.
### S3: Cliente (P2)
O cliente usa o que o servidor entrega.
**Acceptance Criteria**
6. WHILE o interior está aberto the client SHALL aplicar o delta de interior sem reler o snapshot.
7. WHILE o relógio informa `hoursPerDay` the client SHALL calcular a hora e o dia com ele.

## Out of scope
| Excluded | Why |
| --- | --- |
| Delta de interior para NPC que anda dentro do prédio | O motor só muda andar e célula na entrada; movimento interno é fase própria |
| Nomes de meses | O calendário serve só as três durações |

## Assumptions
| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Primeiro delta de um escopo | Traz todos os ocupantes | O snapshot já os tem; reenviar é idempotente e mais simples que lembrar o que o cliente viu | n |

**Open questions:** none - all resolved or logged above.

## Observable
| Surface | Decision | Landing |
| --- | --- | --- |
| API `GET /visual/ws` interior | acesso negado | existing - `403` do `RealtimeGateway.Authorize` |

## Sources
- Issue #39; `.specs/features/godot-client/plan.md` (premissa "Interior ao vivo")
