# City house layout — closeout

| Requisito | Estado | Evidência |
| --- | --- | --- |
| HOUSE-01 | Verified | tests/LivingWorld.Tests.Unit/Cities/BuildingFootprintAndPlacementTests.cs (Initial_house_is_always_compact_3_by_3_with_an_internal_floor_cell) |
| HOUSE-02 | Verified | mesmo arquivo (Derived_building_orientations_are_deterministic_and_vary_between_identities) |
| HOUSE-03 | Pending | orientacao chega ao marcador (tests/LivingWorld.Tests.Integration/Stage4/CityBuildingMarkerContractTests.cs); contencao de cada celula na extensao/limites nao encontrada em teste |
| HOUSE-04 | Pending | nenhum teste encontrado que prove o seeding da celula de piso interior como casa do household |
| HOUSE-05 | Verified | tests/LivingWorld.Tests.Integration/Stage4/PurposefulCommuteTests.cs (commute, trabalha so apos chegar, retorno ao sair da janela) |
