# godot-api-readiness checks

Profile: light
Plan: `.specs/features/godot-api-readiness/plan.md`

14 checks in 3 slices · 3 one-way doors · 0 open

## Checks

### S1 - Andares e planta no interior · 6 files · 40 KB · ~10k

**C1** - `Floors` ≥ 1 e idêntico para as mesmas `(BuildingId, BuildingTypeId)` em duas chamadas e em ordem diferente (AC 1.1)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLayoutGeneratorTests.Floors_are_pure_and_at_least_one"`

**C2** - O escopo Interior devolve `layout` com, por andar, cada célula e seu `BuildingMaterial` (AC 1.2)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~InteriorProjectorTests.Layout_lists_cells_with_material_per_floor"`

**C3** - Com `Floors` > 1, cada andar tem exatamente 1 célula `Stair` e ela está no mesmo `(x,y)` em todos os andares (AC 1.3)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLayoutGeneratorTests.Multi_floor_has_one_stair_at_same_xy"`

**C4** - Todo ocupante do snapshot Interior tem `Floor` em `[0, Floors-1]` (AC 1.4)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~InteriorProjectorTests.Occupant_floor_within_building_floors"`

**C5** - Entrar com andar fora de `[0, Floors-1]` devolve falha e `Hash(world)` não muda (AC 1.5)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~InteriorEntryTests.Floor_out_of_range_fails_and_hash_unchanged"`

**C6** - O escopo Cidade traz em cada prédio `floors` e a planta do térreo (AC 1.6)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~CityProjectorTests.Building_marker_has_floors_and_ground_footprint"`

### S2 - Lista de NPCs · 3 files · 12 KB · ~4k

**C7** - `GET /npcs?cityId=X` devolve só vivos da cidade X, ordenados por `name` e depois `id`; cidade sem NPC dá `items: []`, `total: 0` (AC 2.1)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~NpcDirectoryEndpointTests.City_filter_returns_living_sorted"`

**C8** - `q=ana` acha "Ana" e "ANA", sem diferenciar maiúsculas (AC 2.2)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~NpcDirectoryEndpointTests.Query_is_case_insensitive"`

**C9** - `take=201` e `skip=-1` respondem `400` (AC 2.3)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~NpcDirectoryEndpointTests.Invalid_paging_returns_400"`

**C10** - `total` é igual com `skip=0,take=1` e `skip=5,take=50` (AC 2.4)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~NpcDirectoryEndpointTests.Total_ignores_paging"`

### S3 - Rótulo de ação · 2 files · 6 KB · ~2k

**C11** - `GET /visual/catalog/actions` devolve 1 item por valor de `ActionType` (8), sem lacuna nem duplicata (AC 3.1)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~ActionLabelCatalogTests.Endpoint_lists_every_action_once"`

**C12** - Um `ActionType` sem rótulo faz o teste de cobertura falhar (AC 3.2)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~ActionLabelCatalogTests.Every_enum_member_has_label"`

### Transversal · 2 files · 20 KB · ~4k

**C18** - O contrato regravado em `tests/contracts/` (`openapi.v1.json`, `shape.*.json`) bate com as rotas e respostas novas (Surface)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~ApiContractTests"`

**C19** - Assinar escopo sem permissão continua respondendo `403` sem corpo (Surface, status 403)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~RealtimeGatewayEndpointTests.Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `ActionType` (8) | C11 e C12, ambos table-driven sobre todos os 8 por reflexão | - |
| `GET /npcs` statuses (2) | 200 C7 · 400 C9 | - |
| `GET /visual/catalog/actions` statuses (1) | 200 C11 | - |
| `GET /visual/subscribe` statuses (2) | 200 C2 · 403 C19 | - |
| derivação por prédio (3) | piso ≥ 1 C1 · escada C3 · andar do ocupante C4 | - |
| contrato congelado (2 arquivos) | `openapi.v1.json` C18 · `shape.*.json` C18 | - |

- Claims naming status code or response shape: C2, C6, C7, C9, C11 - each proof crosses the boundary
- Nenhum outro check afirma mais que o caso único que sua prova exercita

## Swept

- validation: C9
- failure modes: C5
- idempotency: n/a - as rotas novas são `GET` sem efeito
- authorization: existing - `RealtimeGateway.Authorize` cobre `/visual/*`; `/npcs` é leitura sem papel
- concurrency: n/a - leitura de `WorldState` no mesmo ponto das rotas `/npcs/{id}` existentes
- data lifecycle: n/a - nada persistido nesta entrega
- dependency failure: n/a - sem serviço externo
- state transitions: n/a - rotas só de leitura; a transição de ocupação é do plano `npc-building-occupancy`
- observability: n/a - sem requisito de log nesta entrega

## Handoff

- S1 = 10k (Domain + Api), S2 = 4k (Api), S3 = 2k (Api), transversal = 4k, total ≈ 20k, abaixo do budget de 150k - one builder
- Mechanism: one builder (36k < 150k)
- **Boundary:** C1–C12, C18, C19 fechados; sem commit pendente de decisão do usuário (ver STATE)
- **Settled mid-build:** ocupação do prédio é persistida na simulação (escolha do usuário); contrato ganhou `shape.city-subscribe`, `shape.interior-subscribe`, `shape.npc-directory`, `shape.action-catalog`; `material` e `actionType` saem como número (mapa em `BuildingMaterial` e `/visual/catalog/actions`)
- **Abandoned:** S4 (NPC entra no prédio) saiu daqui para o plano `npc-building-occupancy`: muda estado persistido e hash
