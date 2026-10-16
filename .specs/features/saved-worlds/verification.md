# Saved worlds verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..18953eb1e930db62b3797c11148e85a2d7849a37
**Round**: 2 - scoped, novo Verifier em contexto em branco
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

verified at 18953eb1e930db62b3797c11148e85a2d7849a37; decisões imutáveis carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3.

Issue #67 aberta via `gh`; planos/checks saved-worlds, godot-main-menu, ADR-0035, regras apontadas e diff completo base..HEAD lidos. Aprovações de contrato/arquitetura registradas no ADR. Não há fonte externa visual binding no perfil standard.

## Proof runs

verified at 18953eb1e930db62b3797c11148e85a2d7849a37. Logs próprios `/private/tmp/lw67-r2-{unit,integration,longrunning,client,verify}.log`. Wrapper temporário de dotnet acrescentou apenas logger console normal aos scripts, exibindo cada teste.

I = `bash scripts/test.sh --project integration --filter 'SavedWorldEndpointsTests|SavedWorldLibraryTests|ZeroRoundTripsTests|ApiClockPersistenceBoundaryTests|ApiContractTests'`: 24/24, resultados individuais em `/private/tmp/lw67-r2-integration.log`.
D = `bash scripts/test.sh --project longrunning --filter 'Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes|ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline'`: 2/2.
G = `bash scripts/client-test.sh`: 258/258. Abreviações: IT=`tests/LivingWorld.Tests.Integration/`; GD=`client/godot/tests/`; LR=`tests/LivingWorld.Tests.LongRunning/`.

## Checks

Provas completas verified at 18953eb1e930db62b3797c11148e85a2d7849a37; análise/assertions dos PASS anteriores carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3. C13/C14/C16 (hospedagem) e C7 (mundos) foram reavaliados no novo HEAD.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | A/B preservados e roundtrip/pausa | I: SavedWorldEndpointsTests.Creating_B_preserves_A_and_loading_A_restores_its_hash | IT:World/SavedWorldEndpointsTests.cs:36–41 `Assert.Contains(worlds!, w => w.Name == "A" && w.CurrentDate == 35)`; `Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current))`; `Assert.True(...IsPaused)` | PASS |
| C2 | ausente 404 conserva hash | I: SavedWorldEndpointsTests.Load_missing_world_keeps_the_current_hash | IT:World/SavedWorldEndpointsTests.cs:55–56 `Assert.Equal(HttpStatusCode.NotFound, ...StatusCode)` / `Assert.Equal(before, WorldSnapshot.CanonicalHash(host.Current))` | PASS |
| C3 | excluir 409/204/404 | I: SavedWorldEndpointsTests.Delete_guards_current_and_removes_only_the_requested_world | IT:World/SavedWorldEndpointsTests.cs:65–70 `Assert.Equal(HttpStatusCode.Conflict, ...StatusCode)`; NoContent/NotFound; `Assert.DoesNotContain(worlds!, w => w.Seed == 67003)` | PASS |
| C4 | duplicado 409 sem sobrescrever; inválido400 | I: SavedWorldEndpointsTests.Duplicate_seed_is_rejected_without_overwriting | IT:World/SavedWorldEndpointsTests.cs:81–84 `Assert.Equal(HttpStatusCode.Conflict, ...StatusCode)`; `Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current))`; `Assert.Equal(HttpStatusCode.BadRequest, ...StatusCode)` | PASS |
| C5 | reinício, atual e legado | I: SavedWorldEndpointsTests.Restart_preserves_catalog_and_current_world + An_existing_legacy_world_is_imported_with_its_hash_and_historical_events | IT:World/SavedWorldEndpointsTests.cs:111 `Assert.True(hash == WorldSnapshot.CanonicalHash(host.Current), ...)`; :144 hash; :147–151 EventId123, Marriage após importação/carga | PASS |
| C6 | lista/carga/confirmar/vazio/erro/carregando | G: test_saved_worlds (4 testes) | GD:test_saved_worlds.gd:35 `check(menu.confirmation.visible, ...)`; :36 sem DELETE; :40/:42 exclusão/vazio; :95 `check(menu.busy, ...)`; :96 Carregando; :99 só uma chamada | PASS |
| C7 | persistência fora tick nos relógios reais | I: ZeroRoundTripsTests + ApiClockPersistenceBoundaryTests.Api_bootstrap_created_and_restored_clocks_never_execute_database_commands_inside_tick | IT:ApiClockPersistenceBoundaryTests.cs:26/30/32 percorre bootstrap/create/load; :39 `Assert.Equal(0, probe.CountDuring(() => host.Clock.Run(host.Current, 30 * 24)))`; :37/:40 controles SQL positivos; ZeroRoundTripsTests.cs:49 também verde | PASS |
| C8 | gates e sensores antes/depois | D: ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline + gates e comparações próprias | LR:Performance/ScaleScenarioSensorTests.cs:102/105 assert CPU/alocação; comparação estrita +3%/+2% abaixo | PASS |

## Coverage

verified at 18953eb1e930db62b3797c11148e85a2d7849a37 nas linhas diária/visual/persistência; demais linhas carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3 após confirmar zero alteração de produção/helpers/fixtures no fix.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| GET /worlds (1 status; 7 campos DTO) | SavedWorldEndpoints.cs:11; SavedWorldLibrary.cs:15; ADR-0035 | 200 → C1; shape → I ApiContractTests.Response_shape_matches_the_committed_contract(saved-worlds) | - |
| load (2) | SavedWorldEndpoints.cs:12–13 | 200 C1 / 404 C2 | - |
| DELETE (3) | SavedWorldLibrary.cs:140–151 | 204/404/409 C3 | - |
| create (3) | WorldCreateEndpoints.cs:23–32 | 200 C1 / 400 C4 / 409 C4 | - |
| persistência (3 relógios) | Program.cs:92/223; WorldCreateEndpoints.cs:27 | bootstrap/criado/restaurado → C7, todos executados com sensor EF real | - |
| menu (6 estados) | saved_worlds_menu.gd:59–118 | lista/carga/confirmação/vazio/erro/loading C6 | - |

## Test policy rows

verified at 18953eb1e930db62b3797c11148e85a2d7849a37 para todas as superfícies do fix; demais decisões e Swept existing carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decisões observáveis | SavedWorldLibrary / endpoints | borda I C1–5; local Library_lifecycle_preserves_saved_worlds_and_rejects_invalid_transitions_without_http | yes |
| Instrumentação | Program.cs / TickLoopService / persistência; novo sensor API | C7 consumidor real, consulta e Save positivos; SQL por tick detectado nas três montagens | yes |

Swept existing: endpoints locais de criação também não exigem autorização; `worldId:guid` impede caminho do cliente. Sem nova dependência/migração aplicada editada. Gate serializa troca/tick. Relógio novo recomposto ao carregar; banco anterior preservado.

## Faults injected

verified at 18953eb1e930db62b3797c11148e85a2d7849a37; demais mutantes mortos carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3 (mesma produção).
Reprodução própria: `python3 /private/tmp/lw67-r2-mutations.py`; logs `/private/tmp/lw67-r2-mutations/` e `lw67-r2-mutation-results.log`.
Scratch `/private/tmp/lw67-r2-mutant` em HEAD, cada arquivo restaurado; porcelain real vazio antes/depois das oito injeções. Nenhum erro de compilação contado como morte.
| Mutation | Location | Killed |
| --- | --- | --- |
| SQL no relógio bootstrap | Program.cs:94; C7 assertion :39 recebe 720, esperava 0; caller :26 | yes |
| SQL no relógio criado via endpoint | WorldCreateEndpoints.cs:27; C7 :39 recebe 720; caller :30 | yes |
| SQL no relógio recarregado via endpoint | Program.cs:223; C7 :39 recebe 720; caller :32 | yes |
| carried: não salvar A, 404 errado, apagar atual, duplicar seed, perder snapshot, excluir sem confirmação | rodada 1 em 2ab9fcb, mesmas fontes de produção | yes |

## Gate and performance

Gates verified at 18953eb1e930db62b3797c11148e85a2d7849a37: `bash scripts/verify.sh` exit0, 2305 passed / 3 skipped; `bash scripts/client-test.sh` exit0, 258 passed. U 45/45; I 24/24; D 2/2, nomes individuais nos logs acima. Godot avisa recursos no encerramento, sem falha de teste.
Desempenho carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3: criação 1955.349 → 1713.986 ms (-12.34%); motor 1.019 → 1.040 µs/NPC-tick (+2.06%); 255351 → 256245 B/tick (+0.35%); FPS medianos 73.7 → 74.1.
Proveniência: relatório round1 no git em 2ab9fcb, logs `/private/tmp/lw67-final-*`, comparações seriais por `compare-creation-cost.sh`, `compare-simulation-cost.sh`, `client-stress.sh`, base 10bec92. Fix só testes/docs, portanto sem impacto no executável/performance; sensor D rerodado verde no HEAD.

## Issue completeness and scope

verified at 18953eb1e930db62b3797c11148e85a2d7849a37: diff real 2ab9fcb..HEAD adiciona seis casos locais, sensor EF na API, renomeia teste Godot e corrige docs/checks; nenhum fixture/helper/shared config mudou. Distâncias C13/C14 e montagem API C7 agora comprovadas por mutação; zero pendência silenciosa.
Cabeçalho/out-of-scope godot-main-menu e STATE são coerentes com validação pendente antes deste relatório. CI develop consultado via gh: run 38823719663 success; pendência removida. Atualizar planos para Verificado cabe ao orquestrador após este PASS.
#65 carried from abbefa5: `git diff abbefa5..HEAD -- src tests/LivingWorld.Tests.LongRunning/Population tests/baselines/family-diversity.json` vazio; sem impacto na aprovação independente de família.
