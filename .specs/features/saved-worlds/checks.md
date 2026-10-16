# saved-worlds — checks

Profile: standard
Plan: `.specs/features/saved-worlds/plan.md`

## Checks

**C1** - A e B listáveis; carregar A preserva hash (AC 1)
Proof: `bash scripts/test.sh --project integration --filter SavedWorldEndpointsTests.Creating_B_preserves_A_and_loading_A_restores_its_hash`

**C2** - 404 ausente não muda mundo (AC 2)
Proof: `bash scripts/test.sh --project integration --filter SavedWorldEndpointsTests.Load_missing_world_keeps_the_current_hash`

**C3** - DELETE 409 atual, 204 outro e 404 ausente (AC 3)
Proof: `bash scripts/test.sh --project integration --filter SavedWorldEndpointsTests.Delete_guards_current_and_removes_only_the_requested_world`

**C4** - Create duplicado 409 sem sobrescrita (AC 4)
Proof: `bash scripts/test.sh --project integration --filter SavedWorldEndpointsTests.Duplicate_seed_is_rejected_without_overwriting`

**C5** - Catálogo e mundo sobrevivem restart e importam legado (AC 5)
Proof: `bash scripts/test.sh --project integration --filter SavedWorldEndpointsTests.Restart_preserves_catalog_and_current_world`

**C6** - Menu lista, carrega e confirma apagar; vazio e erro (AC 6)
Proof: `bash scripts/client-test.sh --filter test_saved_worlds`

**C7** - Zero round-trips durante tick (AC 7)
Proof: `bash scripts/test.sh --project integration --filter ZeroRoundTripsTests`
Proof: `bash scripts/test.sh --project integration --filter ApiClockPersistenceBoundaryTests`

**C8** - Gates e medidas antes/depois (AC 8)
Proof: `bash scripts/test.sh --project longrunning --filter ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| GET /worlds statuses (1) | 200 C1 | - |
| POST /worlds/{worldId}/load statuses (2) | 200 C1 · 404 C2 | - |
| DELETE /worlds/{worldId} statuses (3) | 204 C3 · 404 C3 · 409 C3 | - |
| POST /worlds/create statuses (3) | 200 C1 · 400 C4 · 409 C4 | - |
| catálogo e mundo (2) | catálogo C5 · mundo C1 | - |

- Prova local das decisões C1–C5: `bash scripts/test.sh --project integration --filter SavedWorldLibraryTests.Library_lifecycle_preserves_saved_worlds_and_rejects_invalid_transitions_without_http`.

## Test policy

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decisões observáveis | integração na borda e teste local quando houver decisão interna | todos os resultados declarados |
| Instrumentação | prova do consumidor | sem teste espelho |

## Swept

- validation: C3
- failure modes: C3
- idempotency: C4
- authorization: existing - API local segue política dos endpoints atuais
- concurrency: C5 - fronteira de troca serializada com tick
- data lifecycle: C3, C5
- dependency failure: C2 - falha sem trocar mundo
- state transitions: C3
- observability: C6 - erro visível no menu

## Handoff

- Estimativa: fontes relevantes < 400 KB / 4 = <100k tokens; sob orçamento de 150k, um builder.
- Decisões humanas: opção B (#65), SQLite por mundo e seed duplicada 409 (#67).
