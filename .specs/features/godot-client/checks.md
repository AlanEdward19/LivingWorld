# godot-client checks

Profile: light
Plan: `.specs/features/godot-client/plan.md`

22 checks in 5 slices · 1 one-way door · 0 open

Proofs rodam em Godot headless: `bash scripts/client-test.sh --filter "<arquivo>::<teste>"`. Transporte falso nos testes; sem servidor.

## Checks

### S1 - Cliente de contrato · client/godot/Contract
**C1** - Assinar entrega o snapshot e depois aplica deltas na ordem de `sequence` (AC 1.1)
Proof: `bash scripts/client-test.sh --filter "test_cursor_stream::test_snapshot_then_ordered_deltas"`

**C2** - Delta com `fromCursor.sequence` diferente do último aplicado chama `/visual/replay` com o último cursor e não é aplicado (AC 1.2)
Proof: `bash scripts/client-test.sh --filter "test_cursor_stream::test_gap_triggers_replay_and_discards_delta"`

**C3** - Canal caído reconecta e reidrata por `/visual/replay` a partir do cursor (AC 1.3)
Proof: `bash scripts/client-test.sh --filter "test_cursor_stream::test_channel_drop_reconnects_and_replays"`

**C4** - Resposta `403` vira `access_denied` e nenhum payload chega à store (AC 1.4)
Proof: `bash scripts/client-test.sh --filter "test_cursor_stream::test_403_is_access_denied_without_payload"`

**C5** - Resposta com campo ausente ou de tipo diferente de `tests/contracts/shape.*.json` é rejeitada (AC 1.5)
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_rejects_missing_and_mistyped_fields"`

### S2 - Mundo isométrico 3D · client/godot/Scenes
**C6** - Snapshot do mundo cria um nó por cidade na `location` e um por NPC externo (AC 2.1)
Proof: `bash scripts/client-test.sh --filter "test_world_view::test_snapshot_builds_cities_and_npcs"`

**C7** - Pan, giro e zoom do `CameraRig` não chamam o transporte (AC 2.2)
Proof: `bash scripts/client-test.sh --filter "test_camera_rig::test_navigation_sends_nothing"`

**C8** - Zoom abaixo do limiar de cidade emite troca de escopo para a cidade em foco (AC 2.3)
Proof: `bash scripts/client-test.sh --filter "test_camera_rig::test_zoom_threshold_requests_city_scope"`

**C9** - Delta de posição move só o NPC afetado e a pose a meio do passo fica entre as duas células (AC 2.4)
Proof: `bash scripts/client-test.sh --filter "test_world_view::test_delta_moves_only_that_npc_interpolated"`

### S3 - NPCs vivos
**C10** - Cada um dos 8 `ActionType` tem animação mapeada ou cai na pose neutra declarada (AC 3.1)
Proof: `bash scripts/client-test.sh --filter "test_npc_animation::test_every_action_type_is_mapped"`

**C11** - Ação desconhecida (id 999) usa pose neutra e mostra o rótulo (AC 3.2)
Proof: `bash scripts/client-test.sh --filter "test_npc_animation::test_unknown_action_uses_neutral_pose"`

**C12** - O rótulo exibido vem do catálogo recebido, nunca de texto do cliente (AC 3.3)
Proof: `bash scripts/client-test.sh --filter "test_npc_animation::test_label_comes_from_server_catalog"`

### S4 - Interior multi-andar
**C13** - Entrar numa casa assina o escopo interior e escurece e desfoca o exterior (AC 4.1)
Proof: `bash scripts/client-test.sh --filter "test_app::test_enter_subscribes_and_dims_exterior"`

**C14** - Com o andar N visível, teto e andares acima ficam ocultos (AC 4.2)
Proof: `bash scripts/client-test.sh --filter "test_interior_view::test_upper_floors_and_roof_hidden"`

**C15** - Trocar de andar mostra só os ocupantes daquele andar (AC 4.3)
Proof: `bash scripts/client-test.sh --filter "test_interior_view::test_floor_switch_shows_only_its_occupants"`

**C16** - Sair restaura o exterior e a assinatura anterior (AC 4.4)
Proof: `bash scripts/client-test.sh --filter "test_app::test_exit_restores_exterior_and_scope"`

### S5 - Consulta e relógio
**C17** - A lista pagina `GET /npcs` com `q`, `skip` e `take` ≤ 200 e mostra "nenhum NPC" quando vazia (AC 5.1)
Proof: `bash scripts/client-test.sh --filter "test_panels::test_npc_list_paginates_and_searches"`

**C18** - Selecionar um NPC busca `GET /npcs/{id}` e o destaca na cena (AC 5.2)
Proof: `bash scripts/client-test.sh --filter "test_panels::test_select_npc_fetches_and_highlights"`

**C19** - "Seguir" faz o `CameraRig` acompanhar a posição do NPC até soltar (AC 5.3)
Proof: `bash scripts/client-test.sh --filter "test_camera_rig::test_follow_tracks_until_released"`

**C20** - Selecionar uma cidade mostra nome, população e indicadores do `CitySnapshot` (AC 5.4)
Proof: `bash scripts/client-test.sh --filter "test_panels::test_city_panel_shows_snapshot_fields"`

**C21** - Pausa, retomar, velocidade e passo chamam a rota `/simulation/*` correspondente e o estado mostrado vem de `/simulation/status` (AC 5.5)
Proof: `bash scripts/client-test.sh --filter "test_clock_panel::test_buttons_call_routes_and_show_status"`

**C22** - Servidor fora do ar mostra "sem conexão" e repete com espera 1, 2, 4, 8 s limitada a 8 s (AC 5.6)
Proof: `bash scripts/client-test.sh --filter "test_cursor_stream::test_offline_backoff_is_capped"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `ActionType` (8) | C10, table-driven sobre os 8 valores | - |
| `/simulation/*` comandos (4) | pausa C21 · retomar C21 · velocidade C21 · passo C21 | - |
| entidades desenhadas no mundo (2) | cidade C6 · NPC externo C6 | - |
| escopos assinados (3) | mundo C1 · cidade C8 · interior C13 | - |

- Todo claim que cita rota ou campo de contrato (C1, C2, C4, C5, C17, C18, C21) tem prova que atravessa o transporte falso na fronteira do cliente de contrato
- Nenhum outro check afirma mais que o caso único que sua prova exercita

## Swept

- validation: C5, C17
- failure modes: C2, C3, C22
- idempotency: n/a - o cliente só lê; os comandos de relógio são pedidos ao servidor
- authorization: C4
- concurrency: C2 (delta fora de ordem)
- data lifecycle: n/a - cache do cliente é descartável (reidrata por snapshot + replay)
- dependency failure: C22
- state transitions: C13, C16
- observability: n/a - sem requisito de log nesta entrega

## Handoff

- S1 = 12k, S2 = 20k, S3 = 8k, S4 = 14k, S5 = 14k, total ≈ 68k, abaixo do budget de 150k - one builder
- Mechanism: one builder
- **Settled mid-build:** NPCs da cidade vêm de `livingState.npcs` (mesma fonte dos deltas); o interior é relido a cada 1 s porque o servidor não publica delta de interior; `step`/`+1 ano` reassinam o escopo porque não publicam delta; `InteriorOccupant` ganhou `action` (API)
- **Abandoned:** nada
