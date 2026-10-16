# Lodging and rent verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..2ab9fcbdbbc43b167b763f1ccf7553b0069e08f3
**Round**: 1 - full, novo contexto
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

Issue #67 aberta por `gh issue view 67 --json title,body`; planos/checks completos, AD-104..107, `git show 8f046f9`, diff integral base..HEAD e regras indicadas lidos. Perfil standard; nenhuma fonte externa visual binding.
O plano contém 17 critérios numerados (8/9 ausentes), não 19 critérios distintos. Nenhum relato anterior usado como prova.

## Proof runs

U = `bash scripts/test.sh --filter LodgingSystemTests`: 39/39, nomes individuais em `/private/tmp/lw67-final-unit-named.log`.
D = `bash scripts/test.sh --project longrunning --filter 'Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes|ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline'`: 2/2; log `lw67-final-longrunning-named.log` no mesmo diretório.
G = `bash scripts/client-test.sh`: 258/258. Além disso, proofs estreitos de mutação registrados em `/private/tmp/lw67-final-mutations/`.
Abreviações: UT=`tests/LivingWorld.Tests.Unit/Cities/LodgingSystemTests.cs`; GD=`client/godot/tests/`; LR=`tests/LivingWorld.Tests.LongRunning/`.

## Checks

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
| C13 | diária após sono, apenas hospedagem mais perto | U: Completing_the_travellers_sleep_pays_once_at_the_action_boundary | UT:248/:250–251 tesouro antes/depois e conservação corretos; remover `distance >= homeDistance` não reprova nenhum dos 39 testes | FAIL |
| C14 | distância mínima e fallback | U: A_traveller_near_home_or_too_poor_or_with_no_free_room_does_not_take_a_room | UT:616/:621/:626 `Assert.NotEqual(RestPlaceKind.Lodging, ...)`; remover GuestMinDistance mantém 39/39 | FAIL |
| C15 | vagas/aluguel/diária/valor | G: test_ui_modules::test_hover_text_comes_from_store_and_server_labels | GD:test_ui_modules.gd:161 `check("2/3 vagas" in inn_text and "aluguel 120/mês" in inn_text and "diária 45" in inn_text and "valor 12000" in inn_text, ...)` | PASS |
| C16 | leitos até piso livre/andares | G: test_asset_catalog | GD:test_asset_catalog.gd:145 `eq(view.furniture.get_child_count(), plain + 4, ...)`; :162 `eq(beds, 18, ...)`; :179 `eq(ground.get_child_count() + upper.get_child_count(), 5, ...)` | PASS |
| C17 | loader rejeita incoerência | U: Loader_rejects_each_invalid_lodging_boundary + Loader_rejects_each_inconsistent_pricing_field | UT:133/:178 `Assert.False(ScenarioLoaderV2.LoadWorld(root.ToJsonString()).IsSuccess)`; 5+10 casos | PASS |
| C18 | determinismo dois processos | D: Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes | LR:DeterminismTwoProcessTests.cs:16–18 `Assert.Equal(a, b)` / `Assert.Equal(100, long.Parse(a.Split(';')[1]))` / `Assert.NotEqual(a, RunCommand("lodging-hash 43 720"))` | PASS |
| C19 | zero alocação sem termos/criação ≤+5% | U: Without_lodging_the_monthly_system_allocates_nothing_and_preserves_the_hash; compare-creation-cost.sh | UT:106 `Assert.Equal(0, allocated)`; :108 hash; comparação independente abaixo | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| decisões de diária (5) | GuestLodging.cs:41–55 | rate/limite/comparação/vaga/dinheiro → C13/C14 e U sem diária | limite positivo ≤GuestMinDistance; hospedagem tão longe/mais longe que casa |
| validações (15 amostras) | CityCatalog.cs:53–83; UT:112–164 | 5 lodging + 10 pricing → C17 | - |
| fórmula e pressões (5) | BuildingValuation.cs:77–86 | centralidade/amenidade/água/pressão de moradia/pressão laboral → C10/C11 | - |
| visual (5) | app.gd:812–818; interior_view.gd:74–110 | vagas/aluguel/diária/valor/leitos → C15/C16 | - |

## Test policy rows

Fronteiras/causalidade: não atendida integralmente; as contraprovas de C13/C14 sobrevivem. Níveis unit, ação no relógio, processo separado e Godot reais foram executados.
Swept existing: nenhum item; n/a de autorização/dependência é coerente com motor em memória. Migração foi reprovada pela remoção exclusiva de IsHomeless.

## Faults injected

Reprodução das contraprovas: `python3 /private/tmp/lw67-mutations.py` e `python3 /private/tmp/lw67-more.py` (scripts efêmeros preservados para auditoria).
Scratch `/private/tmp/lw67-final-mutant`, restaurado por arquivo após cada run; real `git status --porcelain` vazio antes/depois. Mutantes compilaram e falharam por assertions quando marcados yes.
| Mutation | Location | Killed |
| --- | --- | --- |
| débito omitido / evento errado / carência ignorada | LodgingSystem.cs:79/87/89 (L1/L2/L3) | yes |
| pior preço / eficiência cama / sem migração por sem-teto | LodgingSystem.cs:117; RestPlaceResolver.cs:29; MigrationSystem.cs:123 (L4/L5/L6) | yes |
| atraso indevido sem termos / centralidade zero / pressão constante | LodgingSystem.cs:28; BuildingValuation.cs:79/49 (L7/L10/L11) | yes |
| preço fixo / diária sem débito / aceitar aluguel zero | LodgingSystem.cs:59; GuestLodging.cs:65; CityCatalog.cs:53 (L12/L13/L17) | yes |
| sem guard de alocação / crédito com ProcessId / aluguel visual zero / sem leitos | LodgingSystem.cs:28/78; app.gd:812; interior_view.gd:74 (L19/L18/L15actual/L16) | yes |
| retirar `homeDistance <= terms.GuestMinDistance` | GuestLodging.cs:45 (L14all); U inteiro 39/39, exit 0 | no |
| retirar `distance >= homeDistance` | GuestLodging.cs:45 (L13closer); U inteiro 39/39, exit 0 | no |

## Gate and performance

`bash scripts/verify.sh`: exit 0, 2299 passed / 3 skipped; `client-test.sh`: exit 0, 258 passed. Gera warnings de recursos Godot no encerramento, sem falhas de teste.
Criação: 1955.349 → 1713.986 ms (-12.34%). Motor: 1.019 → 1.040 µs/NPC-tick (+2.06%); 255351 → 256245 B/tick (+0.35%).
FPS gráfico stress:500: [73.7, 74.3, 69.7] → [73.7, 74.9, 74.1]; medianas 73.7 → 74.1.
Reprodução própria serial: `bash scripts/compare-creation-cost.sh /private/tmp/lw67-final-base /private/tmp/lw67-final-creation-serial`; `bash scripts/client-stress.sh [base/client/godot]`; `bash scripts/compare-simulation-cost.sh /private/tmp/lw67-final-base /private/tmp/lw67-final-engine`.
Medição inicial concorrente descartada antes da repetição serial. Logs e JSON de amostras em `/private/tmp/lw67-final-*`.

## Ranked gaps

1. C13/C14: acrescentar controles com hospedagem próxima quando distância de casa está positiva abaixo/no limiar; excluir hospedagem em distância igual/maior que casa. As duas remoções devem reprovar.
2. Checks C15 aponta teste de leitos que não testa preço: trocar a prova por `test_ui_modules::test_hover_text_comes_from_store_and_server_labels` (mutação real detectada).
