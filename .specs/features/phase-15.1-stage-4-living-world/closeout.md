# Closeout - Phase 15.1 Stage 4 Living World (specs-closeout 2026-10-12)

Cliente web aposentado (ADR-0022): provas abaixo são do lado motor/API; render no Godot.

| Requisito | Estado | Evidência |
|---|---|---|
| LWV-01 nenhuma mecânica órfã | Verified | tests/LivingWorld.Tests.Integration/Stage4/CapabilityCoverageTests.cs (Every_concrete_simulation_system_has_exactly_one_classification, Every_world_event_kind_..., Every_living_capability_declares_a_frontend_consumer_key) |
| LWV-02 pessoas agem/trabalham (API) | Verified | tests/LivingWorld.Tests.Integration/Stage4/PurposefulCommuteTests.cs, NpcLivingInspectorTests.cs, LivingTimelineTests.cs, LifecycleEventLabelContractTests.cs; exibição de mapa/inspector é do Godot |
| LWV-03 sono, comida, plantio, água | Verified | tests/LivingWorld.Tests.Integration/Stage4/RestQualityTests.cs, CropLifecycleTests.cs, WaterLogisticsTests.cs, CookingLifecycleTests.cs (Raw_wheat_does_not_restore_hunger) |
| LWV-04 assentamentos evoluem | Verified | tests/LivingWorld.Tests.Integration/Stage4/AutonomousConstructionTests.cs, LivingScopeConstructionVisualTests.cs, CityBuildingMarkerContractTests.cs, SettlementFoundingVisibilityTests.cs, InterCityMigrationVisibilityTests.cs, LiveSettlementEvolutionTests.cs |
| LWV-05 história/período/narrativa/conversa | Pending | Não verificado item a item (LivingInteractionSurfaceTests existe); sem prova de "sem vazamento de verdade" localizada |
| LWV-06 realtime reconstrói estado | Verified | tests/LivingWorld.Tests.Integration/Stage4/LivingDeltaContractTests.cs e *_delta_replays_to_the_fresh_projection (Crop/Water/Cooking); reducer React obsoleto (ADR-0022) |
| LWV-07 animações 2D (CSS/canvas) | Deferred | Specs de animação eram do cliente web (ADR-0022); Godot em .specs/features/godot-client/ |

Contagem: 5 Verified, 1 Pending, 1 Deferred.
