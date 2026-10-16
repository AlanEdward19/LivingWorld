# NPC authoring tools — closeout

| Requisito | Estado | Evidência |
| --- | --- | --- |
| AUT-01 | Verified | tests/LivingWorld.Tests.Unit/Authoring/WorldAuthoringCommandsTests.cs (Rewrite_personality_is_atomic...); tests/LivingWorld.Tests.Integration/Authoring/AuthoringEndpointTests.cs (Invalid_personality_returns_bad_request_without_partial_rewrite) |
| AUT-02 | Verified | WorldAuthoringCommandsTests.cs (Break_relationships_removes_only_both_directions_of_selected_pair) |
| AUT-03 | Verified | WorldAuthoringCommandsTests.cs (Force_action_rejects_values_outside_the_catalog...; force action usa o tick atual) |
| AUT-04 | Pending | API/eventos de autoria existem (src/LivingWorld.Api/Authoring/AuthoringEndpoints.cs); nenhuma UI de cliente (Godot/web) de intervencao encontrada |
