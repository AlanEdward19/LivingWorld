# Closeout - Phase 15.1 Live Polish (specs-closeout 2026-10-12)

Cliente web removido (ADR-0022): itens de HUD/render viraram contrato de API + Godot.

| Requisito | Estado | Evidência |
|---|---|---|
| LIVE-01 Continue carrega mundo persistido | Verified | tests/LivingWorld.Tests.Integration/EventLog/PersistentWorldRunnerTests.cs (Disk_database_reopened_by_a_new_runner_resumes_the_last_saved_world) |
| LIVE-02 seeds distintas geram mapas distintos | Verified | tests/LivingWorld.Tests.Unit/Geography/Map/MapGeneratorTests.cs (Different_seed_generates_a_different_map) |
| LIVE-03 spawn distribuído | Verified | tests/LivingWorld.Tests.Unit/Population/PopulationGeneratorTests.cs (Initial_households_are_distributed_deterministically_across_supplied_spawn_cells) |
| LIVE-03b Idle dá um passo | Verified | tests/LivingWorld.Tests.Unit/Behavior/Decision/BehaviorDecisionSystemTests.cs (Completing_idle_moves_one_valid_step...) |
| LIVE-04 status traz tick e ano | Verified | tests/LivingWorld.Tests.Integration/Simulation/SimulationControlEndpointsTests.cs (asserts status.Year) |
| LIVE-05 HUD atualiza | Deferred | HUD era do cliente web (ADR-0022); Godot em .specs/features/godot-client/ |
| LIVE-06 footprint compacto <= metade do mapa | Pending | BuildingFootprintAndPlacementTests prova limite pela menor dimensão (City_bounds_never_exceed...), não o teto de metade |
| LIVE-07 paleta de terreno | Deferred | Render do cliente web (ADR-0022) |
| LIVE-08 assentamento vira cidade | Pending | Sem teste localizado para o mapeamento assentamento->cidade/primeira casa |
| LIVE-09 residente interno fora do mapa-múndi | Verified | tests/LivingWorld.Tests.Integration/Simulation/TickLoopServiceTests.cs (World_delta_does_not_publish_a_resident_inside_the_city_footprint); tests/LivingWorld.Tests.Unit/Visual/Projection/GlobalProjectorTests.cs (Build_keeps_a_resident_inside_the_city_footprint_off_the_world_map) |
| LIVE-10 passo ambiente dentro da cidade | Verified | BehaviorDecisionSystemTests (Completing_an_ambient_action_stays_inside_the_home_city_footprint) |
| LIVE-11 sem fome em mundo em branco | Verified | tests/LivingWorld.Tests.Unit/Behavior/Needs/NeedsDecaySystemTests.cs (Hunger_never_decays_and_npc_never_starves_when_the_scenario_decay_rate_is_zero) - cobre a taxa zero, não o mundo em branco por completo |
| LIVE-12 raio do token <= 10px | Deferred | Render do cliente web (ADR-0022) |

Contagem: 8 Verified (LIVE-01,02,03,03b,04,09,10,11), 2 Pending (06,08), 3 Deferred (05,07,12).
