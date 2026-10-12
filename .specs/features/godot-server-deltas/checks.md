# godot-server-deltas checks

Profile: light
Plan: `.specs/features/godot-server-deltas/plan.md`

11 checks in 3 slices · 2 one-way doors · 0 open

## Checks

### S1 - Delta de interior
**C1** - Com um assinante no escopo Interior, um NPC que entra no prédio aparece em `upserts` do delta seguinte, e um que sai aparece em `removed` (AC 1)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Enter_and_exit_are_published_as_upserts_and_removed"`

**C2** - Mudar a ação de um ocupante gera `upsert` com a ação nova, e nada muda quando nada mudou (AC 1)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Action_change_is_an_upsert_and_no_change_is_empty"`

**C3** - Assinar o interior de um prédio inexistente não publica delta e o laço segue publicando o escopo Mundo (AC 2)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Missing_building_publishes_nothing_and_loop_survives"`

**C4** - O delta de interior tem `sequence` seguinte e o `replay` o devolve (AC 3)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Interior_delta_has_sequence_and_replays"`

### S2 - Calendário e contrato
**C5** - `GET /simulation/status` traz `hoursPerDay`, `daysPerMonth` e `monthsPerYear` iguais aos de `WorldState.Calendar` (AC 4)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~SimulationControlEndpointsTests.Status_serves_the_world_calendar"`
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~SimulationControlEndpointsTests.Status_serves_a_non_default_calendar"` (mundo 12/10/4, para distinguir do valor padrão)

**C6** - Os arquivos `shape.npc-detail.json`, `shape.visual-delta.json` e `shape.interior-delta.json` existem e batem com as respostas (AC 5)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~ApiContractTests"`

### S3 - Cliente
**C7** - O cliente aplica o delta de interior (entrada, saída, mudança de ação) à store e à cena sem chamar `GET /visual/subscribe` de novo (AC 6)
Proof: `bash scripts/client-test.sh --filter "test_app::test_interior_delta_updates_occupants_without_polling"`

**C8** - Com `hoursPerDay` 12 no status, o tick 18 é meio-dia (12:00) e o tick 12 é meia-noite; ano, mês e dia saem do calendário do servidor, e sem calendário vale 24/30/12 (AC 7)
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_calendar_from_server_drives_hour_and_day"`

**C9** - O `ApiClient` valida `GET /npcs/{id}` e o status, e o stream descarta delta fora do contrato (AC 5)
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_api_client_validates_npc_detail_shape"`
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_api_client_rejects_mistyped_status"`
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_stream_discards_a_delta_that_breaks_the_contract"`
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_stream_validates_interior_deltas_and_accepts_null_action"`

**C10** - Um assinante WebSocket do escopo Interior recebe o frame de delta com `upserts` e `toCursor` após o snapshot (Surface 101)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Websocket_subscriber_receives_the_interior_delta_frame"`

**C11** - Quem assina de novo um interior recebe todos os ocupantes no primeiro delta, porque o servidor esquece o estado do interior que ficou sem assinante entre dois publicados (assumption do plano; mundo e cidade não mudam)
Proof: `bash scripts/test.sh --project integration --filter "FullyQualifiedName~InteriorDeltaTests.Resubscribing_starts_from_every_occupant_again"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| mudanças de ocupante (4) | entrar C1 · sair C1 · mudar ação C2 · sem mudança C2 | - |
| formas congeladas (3) | `npc-detail` C6 · `visual-delta` C6 · `interior-delta` C6 | - |
| campos do calendário (3) | `hoursPerDay` C5 · `daysPerMonth` C5 · `monthsPerYear` C5 | - |
| `GET /simulation/status` statuses (1) | 200 C5 | - |
| `GET /visual/ws` interior statuses (1) | 101 C10 | - |

- Os contratos gravam campos nulos do motor (ação, cidade, local do evento) como `null`, que aceita qualquer valor; o tipo desses campos não é imposto pelo cliente
- Claims que citam rota ou campo (C1, C4, C5, C6) têm prova que atravessa o gateway ou o host HTTP
- Nenhum outro check afirma mais que o caso único que sua prova exercita

## Swept

- validation: C3
- failure modes: C3
- idempotency: n/a - o primeiro delta reenvia todos os ocupantes de propósito (assumption do plano)
- authorization: existing - `RealtimeGateway.Authorize` cobre o subscribe de interior
- concurrency: C4 (sequência e replay sob o mesmo lock do gateway)
- data lifecycle: n/a - o log do gateway já é truncado por escopo
- dependency failure: n/a - sem serviço externo
- state transitions: C1, C2
- observability: n/a - sem requisito de log

## Handoff

- S1 = 8k (Api), S2 = 4k (Api e contratos), S3 = 8k (cliente), total ≈ 20k, abaixo do budget de 150k - one builder
- Mechanism: one builder
