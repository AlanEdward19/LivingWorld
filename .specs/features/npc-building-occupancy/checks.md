# npc-building-occupancy checks

Profile: light
Plan: `.specs/features/npc-building-occupancy/plan.md`

13 checks in 2 slices · 2 one-way doors · 0 open

## Checks

### S1 - Vínculo no domínio · 7 files · 60 KB · ~15k

**C1** - Todo household semeado tem `Dwelling` apontando para um prédio de tipo `-1` da mesma cidade, e `Household.Location` está dentro do footprint desse prédio; dois households não dividem prédio (AC 1.1)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLinkTests.Seeded_household_dwelling_is_its_house_building_containing_the_bed_cell"`

**C2** - Os workplaces padrão (fazenda e forja) têm `Building` cuja `Position` é a `Location` do workplace e cujo tipo é o `LocationType` (AC 1.2, semeadura)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLinkTests.Default_seeded_workplaces_link_to_their_buildings"`

**C3** - O workplace autorado em cenário V2 tem `Building` igual ao único prédio criado (AC 1.2, cenário)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~ScenarioLoaderV2Tests.Authored_workplace_links_to_its_building"`

**C4** - O workplace criado pela conclusão de obra tem `Building` igual ao prédio construído (AC 1.2, construção)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~ConstructionSystemTests.Completing_project_leaves_a_land_scarce_workplace_queued_and_completes_once_land_is_available"`

**C5** - `Dwelling` apontando para `BuildingId` inexistente faz `ReferentialIntegritySweep.Check` devolver erro (AC 1.3)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLinkTests.Referential_sweep_fails_for_a_dwelling_that_does_not_exist"`

**C6** - Serializar e desserializar preserva `Dwelling` e `Workplace.Building`; snapshot sem os campos carrega `null` (AC 1.4)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingLinkTests.Snapshot_round_trip_keeps_links_and_missing_fields_load_as_null"`

### S2 - Ocupação · 3 files · 14 KB · ~4k

**C7** - `Sleep` na célula do household com `Dwelling` deixa `Interior.Building == Dwelling`, andar em `[0, Floors-1]` (AC 2.1)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Sleep_enters_the_dwelling_when_at_the_household_cell"`

**C8** - `Work` na célula do workplace com `Building` deixa `Interior.Building == Workplace.Building`, andar 0 (AC 2.2)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Work_enters_the_workplace_building_on_the_ground_floor"`

**C9** - `Sleep` fora da célula do household deixa `Interior` nulo (AC 2.3)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Sleep_away_from_the_household_cell_stays_outside"`

**C10** - Mudar a ação de `Sleep` para `Eat` devolve `Interior` a nulo (AC 2.3)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Leaving_the_action_exits_the_building"`

**C11** - Para 5 tipos de prédio × 20 ids × 30 NPCs, `Place` repete o resultado, o andar fica em `[0, Floors-1]` (0 quando térreo) e a célula é caminhável (AC 2.4)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Placement_is_pure_in_range_and_walkable"`

**C12** - Duas execuções da seed 77 por 14 dias dão o mesmo `Interior` de todo NPC dentro de um prédio, e há pelo menos um (AC 2.5)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Same_seed_same_interior_assignments"`

**C13** - `Dwelling` para prédio inexistente mantém o NPC fora (AC 2.3, falha)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~BuildingOccupancySystemTests.Dead_npc_or_missing_building_stays_outside"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| caminhos que criam `Workplace` com prédio (3) | semeadura padrão C2 · cenário V2 C3 · construção C4 | - |
| condições de entrada (2) | `Sleep` C7 · `Work` C8 | - |
| condições que mantêm fora (3) | longe da célula C9 · ação trocada C10 · prédio inexistente C13 | - |
| campos de vínculo no snapshot (2) | `Household.Dwelling` C6 · `Workplace.Building` C6 | - |

- Claims naming a field in the snapshot: C6 - o proof atravessa a serialização
- Nenhum outro check afirma mais que o caso único que sua prova exercita

## Swept

- validation: C5
- failure modes: C13
- idempotency: C12
- authorization: n/a - sistema interno sem chamador externo
- concurrency: n/a - tick é sequencial; `BuildingOccupancySystem` roda depois de `BehaviorDecisionSystem` na mesma thread
- data lifecycle: C6 (snapshot antigo carrega nulo; sem backfill)
- dependency failure: n/a - sem serviço externo
- state transitions: C7, C8, C10
- observability: n/a - sem requisito de log nesta entrega

## Handoff

- S1 = 15k (Domain + Simulation), S2 = 4k (Simulation), total ≈ 19k, abaixo do budget de 150k - one builder
- Mechanism: one builder
