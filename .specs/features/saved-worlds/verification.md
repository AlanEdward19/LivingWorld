# Saved worlds verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..abbefa5
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

Issues #65/#67 abertas via `gh issue view`; ADR-0035, planos/checks e startup lidos diretamente.
Contrato por seed, SQLite por mundo, pausa e confirmação coincidem com a decisão aprovada.

## Checks

Prefixo I=`tests/LivingWorld.Tests.Integration/World/`; G=`client/godot/tests/`.
Rodado `bash scripts/test.sh --project integration --filter 'SavedWorld|ZeroRoundTripsTests'`: 10 passed, 0 failed.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | A/B listáveis e hash de A recuperado | SavedWorldEndpointsTests.Creating_B_preserves_A | I/SavedWorldEndpointsTests.cs:40 `Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current))`; :41 `Assert.True(...IsPaused)` | PASS |
| C2 | ausente 404 preserva mundo | Load_missing_world_keeps | I/SavedWorldEndpointsTests.cs:55 `Assert.Equal(HttpStatusCode.NotFound, ...)`; :56 hash igual | PASS |
| C3 | DELETE 409/204/404 | Delete_guards_current | I/SavedWorldEndpointsTests.cs:65–67 códigos esperados; :69 `Assert.DoesNotContain(worlds!, w => w.Seed == 67003)` | PASS |
| C4 | identidade duplicada 409, sem overwrite; 400 inválido | Duplicate_seed_is_rejected | I/SavedWorldEndpointsTests.cs:81 `Assert.Equal(HttpStatusCode.Conflict, ...)`; :82 hash igual; :84 BadRequest | PASS |
| C5 | reinício, catálogo, legado e tick zero | Restart_preserves_catalog + An_existing_legacy_world, ticks 0/17 | I/SavedWorldEndpointsTests.cs:111 hash de reinício; :148 `e.EventId == 123 && e.Kind == "Marriage"`; :150–151 evento após recarga | PASS |
| C6 | lista, carga, vazio, erro, busy, confirmação | client-test.sh completo | G/test_saved_worlds.gd:38 `check(not ("DELETE /worlds/a" in transport.calls), ...)`; :99 `eq(transport.calls.size(), 1, ...)`; :103 estado vazio | PASS |
| C7 | zero SQL no tick | ZeroRoundTripsTests, mês e ano | tests/LivingWorld.Tests.Integration/ZeroRoundTripsTests.cs:48 `Assert.Equal(midpointCount, interceptor.Count)`; :53 exige sensor SQL ativo | PASS |
| C8 | gates e custo antes/depois | verify.sh, client-test.sh, comparadores e stress | medidas próprias abaixo; ScaleScenarioSensorTests.cs:102/105 assertam alocação e CPU | PASS |

A prova local `I/SavedWorldLibraryTests.cs:33–55` cobre transições sem HTTP, pausa anterior e falha ao montar relógio.
Inicialização real conferida em `src/LivingWorld.Api/Program.cs:82–104`: catálogo inicializa antes do relógio.
Tick serial e troca usam o mesmo `library.Gate`; gravação periódica vem depois de `worldHost.Clock.Tick` em TickLoopService.cs:74–77.

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| GET /worlds (1) | SavedWorldEndpoints.cs:11 | 200 C1 | - |
| load (2) | SavedWorldEndpoints.cs:12–13 | 200 C1; 404 C2 | - |
| delete (3) | SavedWorldLibrary.Delete | 204/404/409 C3 | - |
| create (3) | WorldCreateEndpoints.cs:25–35 | 200 C1; 400/409 C4 | - |
| legado (2) | issue + repositório tick-zero | tick 0/17 C5, evento 123 preservado | - |
| menu (5) | plano AC6 | carregando, vazio, erro, lista/carga, confirmação C6 | - |
| metadados (7) | ADR-0035 e shape.saved-worlds.json | ApiContractTests.Response_shape_matches_the_committed_contract: todos os campos | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Decisões observáveis | SavedWorldLibrary/Endpoints e menu | regra local, integração HTTP, transporte falso | yes |
| Instrumentação | custo, round-trips e stress | consumidor e medições próprias | yes |

Swept existing: novas rotas usam a mesma política local anônima de create; nenhuma autorização preexistente foi removida.
CI citado confirmado por `gh run view 38823719663`: commit 10bec92, job client success; pendência removida de STATE.
`godot-main-menu/plan.md:69` aponta para a entrega de mundos salvos e mantém as demais telas na #59.

## Faults injected

| Mutation | Location | Killed |
| --- | --- | --- |
| S1: remover save anterior | SavedWorldLibrary | yes - C1 |
| S2: ausente retorna sucesso | SavedWorldLibrary.Load | yes - C2 |
| S3: atual retorna 404 | SavedWorldLibrary.Delete | yes - C3 |
| S4: seed repetida retorna sucesso | SavedWorldLibrary.Create | yes - C4 |
| S5: ignorar catálogo no boot | SavedWorldLibrary.Initialize | yes - C5 |
| S5legacy: reset destrutivo ao salvar tick zero | Open/resetOnTickZero | yes - C5, importação/histórico |
| UI-S6 e UI-Sbusy: apagar sem diálogo e permitir ação duplicada | saved_worlds_menu.gd | yes - C6 |
| S7boundary: persistir dentro do laço protegido | ZeroRoundTripsTests, scratch | yes - C7, sensor detecta SQL indevido |
| S8cost: alocação regressiva no sistema social | RelationshipSystem | yes - C8, sensor de custo |

Scratchs isolados; baseline real `git status --porcelain` vazio, preservado até estes relatórios.

## Gate

`bash scripts/verify.sh`: exit 0, 2298 passed / 3 skipped; `bash scripts/client-test.sh`: exit 0, 258 passed.
`bash scripts/test.sh --project longrunning --filter 'FamilyDiversityCalibrationTests|FamilyPairedScenarioTests|DeterminismTwoProcessTests|ScaleScenarioSensorTests'`: 32 passed / 2 skipped, 17m27s; século completo e FAM-33/contrafactuais verdes.
Integração: `--filter 'SavedWorld|ZeroRoundTripsTests'` 10 passed; `--filter 'ApiContractTests|WorldCreateEndpointsTests|PositionalSnapshotPersistenceTests'` 27 passed. Ambos por `bash scripts/test.sh --project integration`.
`bash scripts/compare-creation-cost.sh /tmp/lw-verifier-base /tmp/verifier-creation-cost`: 2355,396 → 1929,044 ms (razão 0,819, limite 1,05).
`bash scripts/compare-simulation-cost.sh /tmp/lw-verifier-base /tmp/verifier-simulation-cost`: 2,694 → 1,846 µs/NPC-tick; 255402 → 256268 B/tick (+0,339%, limite +2%).
`bash scripts/client-stress.sh /tmp/lw-verifier-base/client/godot` e `bash scripts/client-stress.sh`: FPS 73,2/71,4/73,1 → 73,3/74,0/72,6; medianas 73,1 → 73,3. Carga concorrente: números não demonstram aceleração.
Logs próprios em `/tmp/verifier-*`; medições por 3 pares/rodadas, base 10bec92. `WorldCreationCostTests` foi copiado à base somente como instrumentação, sem alterar o motor. Árvore real limpa antes/depois das mutações.

## Status após aprovação

Marcar plano mundos salvos verificado e atualizar main-menu/STATE quanto a esta fatia; #67 permanece aberta enquanto hospedagem tiver FAIL.
