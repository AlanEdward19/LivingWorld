# Lodging and rent verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..18953eb1e930db62b3797c11148e85a2d7849a37
**Round**: 2 - scoped, novo Verifier em contexto em branco
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

verified at 18953eb1e930db62b3797c11148e85a2d7849a37; decisões imutáveis carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3.

Issue #67 aberta por `gh issue view 67 --json title,body`; planos/checks completos, AD-104..107, `git show 8f046f9`, diff integral base..HEAD e regras indicadas lidos. Perfil standard; nenhuma fonte externa visual binding.
O plano contém 17 critérios numerados (8/9 ausentes), não 19 critérios distintos. Nesta rodada, só os PASS independentes da rodada 1 são carried, com proveniência; nenhum relato do autor foi aceito.

## Proof runs

verified at 18953eb1e930db62b3797c11148e85a2d7849a37. Logs próprios `/private/tmp/lw67-r2-{unit,integration,longrunning,client,verify}.log`. Wrapper temporário de dotnet acrescentou apenas logger console normal aos scripts, exibindo cada teste.

U = `bash scripts/test.sh --filter LodgingSystemTests`: 45/45, nomes individuais em `/private/tmp/lw67-r2-unit.log`.
D = `bash scripts/test.sh --project longrunning --filter 'Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes|ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline'`: 2/2; log `lw67-r2-longrunning.log` no mesmo diretório.
G = `bash scripts/client-test.sh`: 258/258. Além disso, proofs estreitos de mutação registrados em `/private/tmp/lw67-r2-mutations/`.
Abreviações: UT=`tests/LivingWorld.Tests.Unit/Cities/LodgingSystemTests.cs`; GD=`client/godot/tests/`; LR=`tests/LivingWorld.Tests.LongRunning/`.

## Checks

Provas completas verified at 18953eb1e930db62b3797c11148e85a2d7849a37; análise/assertions dos PASS anteriores carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3. C13/C14/C16 (hospedagem) e C7 (mundos) foram reavaliados no novo HEAD.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | transferência e conservação | U: Rent_moves_money_from_the_head_to_the_landlord_without_creating_or_losing_any | UT:293–295 `Assert.Equal(400, world.FindNpc(household.Head)!.Wallet.Amount)` / `Assert.Equal(before, TotalMoney(world))` | PASS |
| C2 | atraso/eventos reais | U: Unpaid_and_evicted_events_include_the_actual_tick_and_household | UT:191 `Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.RentUnpaid ... e.Tick == world.CurrentDate.TotalHours ...)` | PASS |
| C3 | despejo após carência | U: Unpaid_rent_builds_arrears_and_eviction_comes_only_after_the_grace_months | UT:308 `Assert.Equal(Inn(world).Id, household.Dwelling)`; :312 `Assert.Null(household.Dwelling)` | PASS |
| C4 | preço, id, capacidade, célula | U: Homeless_families_choose_cheapest_then_id_without_exceeding_capacity | UT:229 `Assert.Equal(cheap[0].Id, first.Dwelling)`; :232 `Assert.All(cheap, b => Assert.Single(world.Households, h => h.Dwelling == b.Id))` | PASS |
| C5 | chão após despejo | U: An_evicted_household_sleeps_on_the_ground_and_is_pushed_to_migrate | UT:329–330 `Assert.Equal(RestPlaceKind.Ground, rest.Kind)` / `Assert.Equal(world.RestPlaceCatalog.GroundEfficiency, rest.RecoveryEfficiency)` | PASS |
| C6 | migração causal | U: A_homeless_household_is_treated_as_needing_to_migrate_and_a_housed_one_is_not | UT:370 `Assert.Equal(elsewhere.Id, evicted.PendingRelocationCity)`; :372 `Assert.Null(housed.PendingRelocationCity)` | PASS |
| C7 | cenário sem termos inerte | U: A_scenario_without_lodging_terms_changes_nothing + C19 | UT:415 `Assert.Equal(money, TotalMoney(world))`; :108 `Assert.Equal(beforeHash, WorldSnapshot.CanonicalHash(world))` | PASS |
| C10 | fórmula exata | U: Price_formula_includes_exact_centrality_amenity_waterfront_and_demand_terms | UT:264 `Assert.Equal((long)Math.Round(pricing.BaseValue * 2 * demand), BuildingValuation.ValueOf(context, building, centralRecipe))`; :271/:279 amenidade/água | PASS |
| C11 | pressões e limites | U: Rent_rises_with_demand_and_stays_inside_the_scenario_limits + Establishments_have_a_dynamic_value_too_but_no_rent | UT:266 `Assert.Equal(LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, context.City.Id) / (double)vacancies, context.LabourPressure)`; :268 `Assert.Equal(need / 3.0, context.LodgingPressure)`; :490/:496 extremos | PASS |
| C12 | aluguel de hoje | U: Dynamic_rent_is_what_the_tenant_actually_pays | UT:559 `Assert.Equal(800, world.FindNpc(household.Head)!.Wallet.Amount)`; :560 `Assert.Equal(treasury + 200, Landlord(world).Treasury.Amount)` | PASS |
| C13 | diária após sono e estritamente mais perto | U: Completing_the_travellers_sleep_pays_once_at_the_action_boundary + Nightly_lodging_must_be_strictly_closer_than_home | UT:248/250–251 conserva/paga; :645–647 casos 3/4/5 vs casa 4; :657 `Assert.Equal(eligible, GuestLodging.FindRoom(world, npc, household.Location).HasValue)`; :658 verifica resolver | PASS |
| C14 | distância mínima estrita e fallback | U: A_traveller_near_home_or_too_poor_or_with_no_free_room_does_not_take_a_room + Nightly_lodging_requires_strictly_more_than_the_declared_distance | UT:616/621/626 fallback; :630–632 distância 2/3/4 vs limite 3; :640 `Assert.Equal(eligible, GuestLodging.FindRoom(world, npc, household.Location).HasValue)`; :641 verifica resolver | PASS |
| C15 | vagas/aluguel/diária/valor | G: test_ui_modules::test_hover_text_comes_from_store_and_server_labels | GD:test_ui_modules.gd:161 `check("2/3 vagas" in inn_text and "aluguel 120/mês" in inn_text and "diária 45" in inn_text and "valor 12000" in inn_text, ...)` | PASS |
| C16 | leitos até piso livre/andares | G: test_asset_catalog | GD:test_asset_catalog.gd:145 `eq(view.furniture.get_child_count(), plain + 4, ...)`; :162 `eq(beds, 18, ...)`; :179 `eq(ground.get_child_count() + upper.get_child_count(), 5, ...)` | PASS |
| C17 | loader rejeita incoerência | U: Loader_rejects_each_invalid_lodging_boundary + Loader_rejects_each_inconsistent_pricing_field | UT:133/:178 `Assert.False(ScenarioLoaderV2.LoadWorld(root.ToJsonString()).IsSuccess)`; 5+10 casos | PASS |
| C18 | determinismo dois processos | D: Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes | LR:DeterminismTwoProcessTests.cs:16–18 `Assert.Equal(a, b)` / `Assert.Equal(100, long.Parse(a.Split(';')[1]))` / `Assert.NotEqual(a, RunCommand("lodging-hash 43 720"))` | PASS |
| C19 | zero alocação sem termos/criação ≤+5% | U: Without_lodging_the_monthly_system_allocates_nothing_and_preserves_the_hash; compare-creation-cost.sh | UT:106 `Assert.Equal(0, allocated)`; :108 hash; comparação independente abaixo | PASS |

## Coverage

verified at 18953eb1e930db62b3797c11148e85a2d7849a37 nas linhas diária/visual/persistência; demais linhas carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3 após confirmar zero alteração de produção/helpers/fixtures no fix.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| decisões de diária (5) | GuestLodging.cs:41–55 | rate/vaga/dinheiro → U; limite 2/3/4 → C14; proximidade 3/4/5 vs 4 → C13 | - |
| validações (15 amostras) | CityCatalog.cs:53–83; UT:112–164 | 5 lodging + 10 pricing → C17 | - |
| fórmula e pressões (5) | BuildingValuation.cs:77–86 | centralidade/amenidade/água/pressão de moradia/pressão laboral → C10/C11 | - |
| visual (5) | app.gd:812–818; interior_view.gd:74–110 | vagas/aluguel/diária/valor/leitos → C15/C16 | - |

## Test policy rows

verified at 18953eb1e930db62b3797c11148e85a2d7849a37 para todas as superfícies do fix; demais decisões e Swept existing carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3.

Fronteiras/causalidade: atendida; as quatro contraprovas de remoção/igualdade C13/C14 falham no novo HEAD. Níveis unit, ação no relógio, processo separado e Godot reais foram executados.
Swept existing: nenhum item; n/a de autorização/dependência é coerente com motor em memória. Migração foi reprovada pela remoção exclusiva de IsHomeless.

## Faults injected

verified at 18953eb1e930db62b3797c11148e85a2d7849a37; demais mutantes mortos carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3 (mesma produção).
Reprodução própria: `python3 /private/tmp/lw67-r2-mutations.py`; logs `/private/tmp/lw67-r2-mutations/` e `lw67-r2-mutation-results.log`.
Scratch `/private/tmp/lw67-r2-mutant` em HEAD, cada arquivo restaurado; porcelain real vazio antes/depois das oito injeções. Nenhum erro de compilação contado como morte.
| Mutation | Location | Killed |
| --- | --- | --- |
| remover limite mínimo; trocar <= por < | GuestLodging.cs:45; testes de distância, inclusive igualdade, falham | yes |
| remover comparação com casa; trocar >= por > | GuestLodging.cs:45; testes de proximidade, inclusive igualdade, falham | yes |
| omitir leitos | interior_view.gd:74; teste renomeado e outros dois falham (7 pass / 3 fail) | yes |
| carried: débito/carência/evento/alocação/chão/migração/preço/loader/diária/determinismo/visual | rodada 1 em 2ab9fcb, mesmos arquivos de produção | yes |

## Gate and performance

Gates verified at 18953eb1e930db62b3797c11148e85a2d7849a37: `bash scripts/verify.sh` exit0, 2305 passed / 3 skipped; `bash scripts/client-test.sh` exit0, 258 passed. U 45/45; I 24/24; D 2/2, nomes individuais nos logs acima. Godot avisa recursos no encerramento, sem falha de teste.
Desempenho carried from 2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3: criação 1955.349 → 1713.986 ms (-12.34%); motor 1.019 → 1.040 µs/NPC-tick (+2.06%); 255351 → 256245 B/tick (+0.35%); FPS medianos 73.7 → 74.1.
Proveniência: relatório round1 no git em 2ab9fcb, logs `/private/tmp/lw67-final-*`, comparações seriais por `compare-creation-cost.sh`, `compare-simulation-cost.sh`, `client-stress.sh`, base 10bec92. Fix só testes/docs, portanto sem impacto no executável/performance; sensor D rerodado verde no HEAD.

## Issue completeness and scope

verified at 18953eb1e930db62b3797c11148e85a2d7849a37: diff real 2ab9fcb..HEAD adiciona seis casos locais, sensor EF na API, renomeia teste Godot e corrige docs/checks; nenhum fixture/helper/shared config mudou. Distâncias C13/C14 e montagem API C7 agora comprovadas por mutação; zero pendência silenciosa.
Cabeçalho/out-of-scope godot-main-menu e STATE são coerentes com validação pendente antes deste relatório. CI develop consultado via gh: run 38823719663 success; pendência removida. Atualizar planos para Verificado cabe ao orquestrador após este PASS.
#65 carried from abbefa5: `git diff abbefa5..HEAD -- src tests/LivingWorld.Tests.LongRunning/Population tests/baselines/family-diversity.json` vazio; sem impacto na aprovação independente de família.
