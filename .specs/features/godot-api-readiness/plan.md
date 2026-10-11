# API pronta para o cliente Godot

## Problem
O cliente Godot (`godot-client`) precisa mostrar casas com vários andares, listar NPCs e animar ações, mas a API
não serve esses dados hoje. Fatos verificados no código:
- `BuildingFootprintGenerator` gera uma planta 2D "sem parâmetro de andar"; não existe número de andares por prédio.
  O material `Stair` existe, mas nenhum andar real o usa. A planta era regenerada pelo `web/` (removido); a API não a envia.
- `InteriorSnapshot` traz só `Id, City, BuildingTypeId, Occupants(Npc, Floor, LocalCell)`: sem paredes, portas nem escadas.
- `CityResidentMarker` traz id, célula e ação; não traz nome. Não há rota que liste NPCs com nome, idade ou cidade.
- Os 8 valores de `ActionType` não têm rótulo servido ao cliente (a verificar no catálogo de apresentação).
Quem paga: o cliente teria de inventar plantas, nomes e quem dorme onde (ADR-0005/0014). Sem evidência numérica.

Ao entregar, o cliente lê planta por andar, ocupantes por andar, lista paginada de NPCs e um rótulo por ação.

## Flow
Reusa `BuildingFootprintGenerator` (planta determinística pura) e `InteriorProjector`; só estende, não duplica.

1. `BuildingFootprintGenerator` (exists) - planta base por prédio
2. `BuildingLayoutGenerator` (new, door 1) - deriva `Floors` e `Stair` de `(BuildingId, BuildingTypeId)`, puro, sem persistir
3. `InteriorProjector` / `CityProjector` (exists) - incluem o layout no snapshot (interior completo; cidade só `floors` e planta do térreo)
4. `GET /npcs` (new, door 2) lê `WorldState.Npcs` e devolve página; `GET /visual/catalog/actions` (new, door 3) devolve rótulo por `ActionType`
5. `ApiContractTests` (exists) congela as mudanças em `tests/contracts/`

## Impact
| Front | What changes |
| --- | --- |
| domain | new term: `Floors` - número de andares do prédio, derivado do tipo e do id; hoje inexistente |
| stored data | nothing to migrate: layout derivado, nada persistido; hash canônico não muda |

## Relations
`None - no stored-data shape change`

## Surface
| Route | In | Out | Status |
| --- | --- | --- | --- |
| `GET /visual/subscribe` (scope Interior) | `scope`, `refId`, `mode` | `layout` (andares, células com material, escadas) · `occupants` (npc, andar, célula, ação) | `200` |
| `GET /visual/subscribe` (scope City) | `scope`, `refId`, `mode` | cada prédio ganha `floors` e `groundFootprint` do térreo | `200` |
| `GET /npcs` | `cityId?`, `q?`, `skip`, `take` | `items[id, name, cityId, action, location]` · `total` | `200`, `400` |
| `GET /visual/catalog/actions` | none | `[actionType, label]` por `ActionType` | `200` |

## Landing
| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1 Layout derivado, não persistido | `Floors = f(BuildingId, BuildingTypeId)`; escada em célula `Stair` do footprint | gravar layout no mundo: exige migração e quebra mundos antigos |
| 2 Forma de `GET /npcs` | `{items:[{id,name,cityId,action,location}], total}`; paginação `skip/take`, `take` ≤ 200 | enviar lista na assinatura da cidade: incha o delta a cada tick |
| 3 Rótulo de ação servido | `[{actionType:int,label:string}]` cobrindo todo `ActionType` | rótulo literal no cliente: viola o texto equivalente do servidor |

## Criteria
### S1: Andares e planta no interior (P1)
Um prédio expõe seus andares, paredes, portas e escadas.
**Acceptance Criteria**
1. The system SHALL derivar `Floors` (≥ 1) de `(BuildingId, BuildingTypeId)` sem ler tick nem estado do mundo; mesmas entradas, mesmo layout.
2. WHEN o escopo Interior é assinado THEN the system SHALL devolver `layout` com, por andar, as células e seu `BuildingMaterial`.
3. WHERE `Floors` > 1 the system SHALL colocar exatamente uma célula `Stair` por andar, na mesma célula `(x,y)` em todos os andares.
4. WHEN o escopo Interior é assinado THEN the system SHALL listar cada ocupante com seu `FloorLevel` dentro de `[0, Floors-1]`.
5. IF um `Npc.Interior` aponta andar fora de `[0, Floors-1]` THEN the system SHALL rejeitar a entrada com `Result` de falha e SHALL manter o hash do mundo inalterado.
6. WHEN o escopo Cidade é assinado THEN the system SHALL incluir em cada prédio `floors` e a planta do térreo.
### S2: Lista de NPCs (P1)
1. WHEN `GET /npcs` é chamado com `cityId` THEN the system SHALL devolver só NPCs vivos daquela cidade, ordenados por `name` e depois `id`.
2. WHEN `q` é informado THEN the system SHALL filtrar por nome contendo `q`, sem diferenciar maiúsculas.
3. IF `take` > 200 ou `skip` < 0 THEN the system SHALL responder `400`.
4. The system SHALL devolver `total` independente de `skip` e `take`.
### S3: Rótulo de ação (P1)
1. WHEN `GET /visual/catalog/actions` é chamado THEN the system SHALL devolver um item por valor de `ActionType`, sem lacuna nem duplicata.
2. IF um novo valor de `ActionType` não tem rótulo THEN the build SHALL falhar no teste de cobertura.
### S4: Relógio avisa quem assina (P2)
1. WHEN `POST /simulation/step` ou `/simulation/advance-year` responde `200` THEN the system SHALL publicar um delta com `sequence` seguinte para cada escopo com assinante.

## Out of scope
| Excluded | Why |
| --- | --- |
| NPC ocupar prédios na simulação | Plano próprio: `npc-building-occupancy` (muda estado persistido e o hash) |
| Eventos ativos na projeção (`ActiveEvents` vazio) | O motor não tem leitura de evento em curso; fora desta entrega |
| Altura de terreno, animação, mobília e cômodos | Apresentação do cliente; o servidor envia só ação, posição e planta mínima |

## Assumptions
| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Número de andares | 1 a 3, derivado do tipo e do id; casa inicial (`-1`) com 2 | Cobre "mais de 1 andar" sem inflar a planta | n |
| Cidade só com térreo | Interior traz todos os andares | Mantém o delta da cidade leve | n |
| Terreno 3D | O cliente usa as camadas já servidas em `Layers`; se faltar tipo de terreno, abre-se pedido próprio | Não verificado nesta fase | n |

**Open questions:** none - all resolved or logged above.

## Observable
| Surface | Decision | Landing |
| --- | --- | --- |
| API `GET /npcs` | error shape and codes; empty state | AC 2.3; AC 2.1 (`items: []`) |
| all new `GET` | versioning, rate limits | n/a - API v1 interna |

## Sources
- ADR-0018 - endereço espacial com andar; ADR-0022 - Godot substitui o web
