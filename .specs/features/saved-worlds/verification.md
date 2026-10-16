# Saved worlds verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3
**Round**: 1 - full, novo contexto
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

Issue #67 aberta via `gh`; planos/checks saved-worlds, godot-main-menu, ADR-0035, regras apontadas e diff completo base..HEAD lidos. Aprovações de contrato/arquitetura registradas no ADR. Não há fonte externa visual binding no perfil standard.

## Proof runs

I = `bash scripts/test.sh --project integration --filter 'SavedWorldEndpointsTests|SavedWorldLibraryTests|ZeroRoundTripsTests|ApiContractTests'`: 23/23, resultados individuais em `/private/tmp/lw67-final-integration-named.log`.
D = `bash scripts/test.sh --project longrunning --filter 'Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes|ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline'`: 2/2.
G = `bash scripts/client-test.sh`: 258/258. Abreviações: IT=`tests/LivingWorld.Tests.Integration/`; GD=`client/godot/tests/`; LR=`tests/LivingWorld.Tests.LongRunning/`.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | A/B preservados e roundtrip/pausa | I: SavedWorldEndpointsTests.Creating_B_preserves_A_and_loading_A_restores_its_hash | IT:World/SavedWorldEndpointsTests.cs:36–41 `Assert.Contains(worlds!, w => w.Name == "A" && w.CurrentDate == 35)`; `Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current))`; `Assert.True(...IsPaused)` | PASS |
| C2 | ausente 404 conserva hash | I: SavedWorldEndpointsTests.Load_missing_world_keeps_the_current_hash | IT:World/SavedWorldEndpointsTests.cs:55–56 `Assert.Equal(HttpStatusCode.NotFound, ...StatusCode)` / `Assert.Equal(before, WorldSnapshot.CanonicalHash(host.Current))` | PASS |
| C3 | excluir 409/204/404 | I: SavedWorldEndpointsTests.Delete_guards_current_and_removes_only_the_requested_world | IT:World/SavedWorldEndpointsTests.cs:65–70 `Assert.Equal(HttpStatusCode.Conflict, ...StatusCode)`; NoContent/NotFound; `Assert.DoesNotContain(worlds!, w => w.Seed == 67003)` | PASS |
| C4 | duplicado 409 sem sobrescrever; inválido400 | I: SavedWorldEndpointsTests.Duplicate_seed_is_rejected_without_overwriting | IT:World/SavedWorldEndpointsTests.cs:81–84 `Assert.Equal(HttpStatusCode.Conflict, ...StatusCode)`; `Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current))`; `Assert.Equal(HttpStatusCode.BadRequest, ...StatusCode)` | PASS |
| C5 | reinício, atual e legado | I: SavedWorldEndpointsTests.Restart_preserves_catalog_and_current_world + An_existing_legacy_world_is_imported_with_its_hash_and_historical_events | IT:World/SavedWorldEndpointsTests.cs:111 `Assert.True(hash == WorldSnapshot.CanonicalHash(host.Current), ...)`; :144 hash; :147–151 EventId123, Marriage após importação/carga | PASS |
| C6 | lista/carga/confirmar/vazio/erro/carregando | G: test_saved_worlds (4 testes) | GD:test_saved_worlds.gd:35 `check(menu.confirmation.visible, ...)`; :36 sem DELETE; :40/:42 exclusão/vazio; :95 `check(menu.busy, ...)`; :96 Carregando; :99 só uma chamada | PASS |
| C7 | persistência fora tick | I: ZeroRoundTripsTests (2 testes) | IT:ZeroRoundTripsTests.cs:49 `Assert.Equal(midpointCount, interceptor.Count)`; monta relógio próprio :41, nunca composição Program.cs:92/223; mutante nessa composição sobrevive | FAIL |
| C8 | gates e sensores antes/depois | D: ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline + gates e comparações próprias | LR:Performance/ScaleScenarioSensorTests.cs:102/105 assert CPU/alocação; comparação estrita +3%/+2% abaixo | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| GET /worlds (1 status; 7 campos DTO) | SavedWorldEndpoints.cs:11; SavedWorldLibrary.cs:15; ADR-0035 | 200 → C1; shape → I ApiContractTests.Response_shape_matches_the_committed_contract(saved-worlds) | - |
| load (2) | SavedWorldEndpoints.cs:12–13 | 200 C1 / 404 C2 | - |
| DELETE (3) | SavedWorldLibrary.cs:140–151 | 204/404/409 C3 | - |
| create (3) | WorldCreateEndpoints.cs:23–32 | 200 C1 / 400 C4 / 409 C4 | - |
| persistência (3 superfícies) | Program.cs:92/223; TickLoopService.cs:78–81 | salvar/trocar/reiniciar C1/C5; relógio efetivo do servidor | composição real do relógio e interceptor no consumidor API |
| menu (6 estados) | saved_worlds_menu.gd:59–118 | lista/carga/confirmação/vazio/erro/loading C6 | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decisões observáveis | SavedWorldLibrary / endpoints | borda I C1–5; local Library_lifecycle_preserves_saved_worlds_and_rejects_invalid_transitions_without_http | yes |
| Instrumentação | Program.cs / TickLoopService / persistência | prova no consumidor efetivo | no — ZeroRoundTripsTests monta outro relógio; mutação de I/O no tick da API não é detectada |

Swept existing: endpoints locais de criação também não exigem autorização; `worldId:guid` impede caminho do cliente. Sem nova dependência/migração aplicada editada. Gate serializa troca/tick. Relógio novo recomposto ao carregar; banco anterior preservado.

## Faults injected

Reprodução: `python3 /private/tmp/lw67-mutations.py` e `python3 /private/tmp/lw67-more.py`.
Scratch `/private/tmp/lw67-final-mutant`; arquivos restaurados, real porcelain vazio antes/depois. Logs `/private/tmp/lw67-final-mutations/`.
| Mutation | Location | Killed |
| --- | --- | --- |
| não salvar A antes de B | SavedWorldLibrary.cs:108 (S1) | yes |
| ausente retorna sucesso | SavedWorldLibrary.cs:127 (S2) | yes |
| permitir apagar atual | SavedWorldLibrary.cs:143 (S3) | yes |
| desativar guard de seed repetida | SavedWorldLibrary.cs:107 (S4valid) | yes |
| retornar legado em vez de snapshot salvo | SavedWorldLibrary.cs:55 (S5) | yes |
| excluir antes de confirmar | saved_worlds_menu.gd:104 (S6) | yes |
| anexar ISimulationSystem Hourly à composição API; Tick chama repository.LoadEvents(BranchId.Root) | Program.cs:94, classe temporária VerifierDatabaseTick (S7assembly); compilou, ZeroRoundTripsTests 2/2 exit0 | no |

Mutante S7assembly testa a montagem efetiva: a prova não a executa. Não foi observado I/O indevido na implementação atual; é uma lacuna de nível e discriminação. Tentativa S4 com constante false deu erro de compilação e foi descartada; S4valid compilou e caiu em assert de 409.

## Gate and performance

`bash scripts/verify.sh`: exit0, 2299 passed / 3 skipped; `bash scripts/client-test.sh`: exit0, 258 passed. I 23/23 e D 2/2.
Criação: 1955.349 → 1713.986 ms (-12.34%). Motor: 1.019 → 1.040 µs/NPC-tick (+2.06%); 255351 → 256245 B/tick (+0.35%).
FPS gráfico stress:500: [73.7, 74.3, 69.7] → [73.7, 74.9, 74.1]; medianas 73.7 → 74.1.
Comandos próprios serializados: `bash scripts/compare-creation-cost.sh /private/tmp/lw67-final-base /private/tmp/lw67-final-creation-serial`; `bash scripts/client-stress.sh [base/client/godot]`; `bash scripts/compare-simulation-cost.sh /private/tmp/lw67-final-base /private/tmp/lw67-final-engine`.
Dados e logs completos `/private/tmp/lw67-final-*`; medição inicial concorrente descartada antes da repetição serial.

## Issue completeness and ranked gaps

1. C7: colocar o interceptor no relógio/host efetivamente montado pela API e exercitar tick fora de snapshot; I/O introduzido como sistema desse relógio deve reprovar.
2. Parte1 #67 permanece FAIL por distâncias de diária; ver relatório lodging-and-rent. Cabeçalho godot-main-menu/out-of-scope atualizados; STATE removeu pendência CI, run develop 38823719663 success confirmado via gh.
3. #65: relatório independente cobre até abbefa5; `git diff abbefa5..2ab9fcb -- src tests/LivingWorld.Tests.LongRunning/Population tests/baselines/family-diversity.json` vazio. Alterações posteriores só reforçam testes de hospedagem e docs; aprovação genética não foi reaberta.
