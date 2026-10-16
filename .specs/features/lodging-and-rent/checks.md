# Lodging and rent — checks

Profile: standard
Plan: `.specs/features/lodging-and-rent/plan.md`

## Checks

**C1** - Comportamento e fronteiras do AC 1
Proof: `bash scripts/test.sh --filter "Rent_moves_money_from_the_head_to_the_landlord_without_creating_or_losing_any"`

**C2** - Comportamento e fronteiras do AC 2
Proof: `bash scripts/test.sh --filter "Unpaid_and_evicted_events_include_the_actual_tick_and_household"`

**C3** - Comportamento e fronteiras do AC 3
Proof: `bash scripts/test.sh --filter "Unpaid_rent_builds_arrears_and_eviction_comes_only_after_the_grace_months"`

**C4** - Comportamento e fronteiras do AC 4
Proof: `bash scripts/test.sh --filter "Homeless_families_choose_cheapest_then_id_without_exceeding_capacity"`

**C5** - Comportamento e fronteiras do AC 5
Proof: `bash scripts/test.sh --filter "An_evicted_household_sleeps_on_the_ground_and_is_pushed_to_migrate"`

**C6** - Comportamento e fronteiras do AC 6
Proof: `bash scripts/test.sh --filter "A_homeless_household_is_treated_as_needing_to_migrate_and_a_housed_one_is_not"`

**C7** - Comportamento e fronteiras do AC 7
Proof: `bash scripts/test.sh --filter "A_scenario_without_lodging_terms_changes_nothing"`

**C10** - Comportamento e fronteiras do AC 10
Proof: `bash scripts/test.sh --filter "Price_formula_includes_exact_centrality_amenity_waterfront_and_demand_terms"`

**C11** - Comportamento e fronteiras do AC 11
Proof: `bash scripts/test.sh --filter "Rent_rises_with_demand_and_stays_inside_the_scenario_limits|Establishments_have_a_dynamic_value_too_but_no_rent"`

**C12** - Comportamento e fronteiras do AC 12
Proof: `bash scripts/test.sh --filter "Dynamic_rent_is_what_the_tenant_actually_pays"`

**C13** - Comportamento e fronteiras do AC 13
Proof: `bash scripts/test.sh --filter "Completing_the_travellers_sleep_pays_once_at_the_action_boundary"`

Proof: `bash scripts/test.sh --filter Nightly_lodging_must_be_strictly_closer_than_home`

**C14** - Comportamento e fronteiras do AC 14
Proof: `bash scripts/test.sh --filter "A_traveller_near_home_or_too_poor_or_with_no_free_room_does_not_take_a_room"`

Proof: `bash scripts/test.sh --filter Nightly_lodging_requires_strictly_more_than_the_declared_distance`

**C17** - Comportamento e fronteiras do AC 17
Proof: `bash scripts/test.sh --filter "Loader_rejects_each_invalid_lodging_boundary|Loader_rejects_each_inconsistent_pricing_field"`

**C15** - Preços e vagas visíveis (AC 15)
Proof: `bash scripts/client-test.sh --filter test_ui_modules::test_hover_text_comes_from_store_and_server_labels`

**C16** - Leitos até o piso livre, inclusive andares (AC 16)
Proof: `bash scripts/client-test.sh --filter test_asset_catalog`

**C18** - Seed reproduz hospedagem ativa em dois processos (AC 18)
Proof: `bash scripts/test.sh --project longrunning --filter Lodging_pricing_and_nightly_rates_are_deterministic_with_actual_rent_in_two_processes`

**C19** - Zero alocação sem hospedagem e criação 6.000 ≤ +5% (AC 19)
Proof: `bash scripts/test.sh --filter Without_lodging_the_monthly_system_allocates_nothing_and_preserves_the_hash`
Proof: `bash scripts/compare-creation-cost.sh <checkout-develop>`

## Coverage

No set rows - os critérios enumerados são 17; o plano legado não tem AC 8 e 9.
Todos os números existentes foram preservados.

## Test policy

Fronteiras/causalidade exigem mutação independente; não adotar relato do autor como prova.

## Swept

- validation: C17
- failure modes: C2, C3, C14
- idempotency: C13
- authorization: n/a - não há operação remota no sistema
- concurrency: C18 - relógio serial, processos independentes
- data lifecycle: C4, C7
- dependency failure: n/a - sistema puramente em memória
- state transitions: C2, C3, C4
- observability: C15, C16
