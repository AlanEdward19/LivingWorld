# Lodging and rent verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 10bec92325c274f9f630c69fea83062974d73779..abbefa5; comportamento herdado conferido também nos ACs de 8f046f9
**Round**: 2 - full, fresh independent verification after fixes; no inherited evidence
**Verifier**: independent sub-agent (author != verifier), 2026-10-16 UTC

## Binding sources

Li issue #67, AD-104..107, plano/checks, city-building-separation e implementações citadas.
O plano enumera 17 ACs: numeração 1–7, 10–19; ACs 8/9 não existem, sem requisitos descartados.

## Checks

U=`tests/LivingWorld.Tests.Unit/Cities/LodgingSystemTests.cs`; G=`client/godot/tests/`.
Provas locais executadas por verify.sh; provas visuais no client-test.sh; determinismo no projeto longrunning.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | aluguel conserva dinheiro | Rent_moves_money... | U:293–295 `Assert.Equal(400, ...Wallet.Amount)`, crédito de Rent e `Assert.Equal(before, TotalMoney(world))` | PASS |
| C2 | atraso e eventos identificados no tick | Unpaid_and_evicted_events... | U:191–196 `e.Kind == RentUnpaid && e.Tick == ...`, `Assert.Equal(..., household.EvictedAtTick)` | PASS |
| C3 | despejo só após carência | Unpaid_rent_builds_arrears... | U:307–313 `Assert.Equal(1, ...RentArrearsMonths)`, permanece alojado no mês 1 e `Assert.Null(...Dwelling)` no mês 2 | PASS |
| C4 | menor aluguel, id, capacidade e célula | Homeless_families_choose_cheapest... | U:229–232 IDs em ordem, InteriorCellOf e `Assert.Single(world.Households, h => h.Dwelling == b.Id)` | PASS |
| C5 | chão e eficiência | An_evicted_household_sleeps... | U:329–330 `Assert.Equal(RestPlaceKind.Ground, rest.Kind)` e GroundEfficiency | PASS |
| C6 | despejo é causa de migração | A_homeless_household_is_treated... | U:362–364 espera migração, mas retirar `household.IsHomeless` não derruba a prova | FAIL |
| C7 | cenário sem termos não muda | A_scenario_without_lodging + Without_lodging... | U:407–409 dinheiro, Dwelling e arrears iguais; U:108 hash antes/depois igual | PASS |
| C10 | fórmula exata dos fatores | Price_formula_includes_exact... | U:264,271,279 `Assert.Equal((long)Math.Round(...), BuildingValuation.ValueOf(...))` | PASS |
| C11 | pressão por teto/trabalho e limites | Rent_rises_with_demand + Establishments... | U:266/268 pressões exatas; U:482/483 teto 150; U:488 piso 50 | PASS |
| C12 | aluguel efetivamente cobrado é dinâmico | Dynamic_rent_is_what... | U:551–552 `Assert.Equal(800, ...Wallet.Amount)` e tesouro +200 | PASS |
| C13 | diária paga uma vez ao fim da ação | Completing_the_travellers_sleep... | U:248–251 tesouro antes intacto, +Nightly no fim e TotalMoney igual | PASS |
| C14 | sem diária/vaga/dinheiro ou perto de casa mantém descanso | A_traveller_near_home... + A_scenario_without_nightly_rate... | U:608/613/618/627 `Assert.NotEqual(RestPlaceKind.Lodging, ...)` | PASS |
| C15 | vagas, aluguel, diária e valor visíveis | test_ui_modules, além do filtro documentado | G/test_ui_modules.gd:161 exige `2/3 vagas`, `aluguel 120/mês`, `diária 45`, `valor 12000`; :163 valor de estabelecimento | PASS |
| C16 | leito por vaga até piso livre, incluindo andares | test_asset_catalog | G/test_asset_catalog.gd:162 `eq(beds,18,...)`; :177–179 exige piso superior e total 5 | PASS |
| C17 | loader rejeita todas as fronteiras inválidas | Loader_rejects_each... + classe inteira | U:113–118 não inclui zero; U:142–151 mistura rent zero com workplace vazio inválido; mutante <=0→<0 sobrevive | FAIL |
| C18 | determinismo ativo em dois processos | Lodging_pricing_and_nightly_rates_are_deterministic... | tests/LivingWorld.Tests.LongRunning/DeterminismTwoProcessTests.cs:16–18 `Assert.Equal(a,b)`, aluguel100 e outra seed diferente | PASS |
| C19 | zero alocação/hash intacto sem termos; criação ≤+5% | Without_lodging... + compare-creation-cost.sh | U:106 `Assert.Equal(0, allocated)` e :108 hash igual; medição própria abaixo | PASS |

## Coverage

Recomputadas as 17 obrigações do plano. Loader: aluguel <=0, grace<0, diária negativa/sem distância, capacidade e nove campos Pricing; zero não tem prova discriminante.
Descanso: pobre, perto, lotado, sem diária; preço: centralidade, comércio, água, teto/piso, teto/trabalho; visuais: vagas/preços e 0/4/18/5 leitos.

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Fronteiras/causalidade | Lodging, migração, loader, preço, descanso e UI | mutação independente por AC causal/fronteira | no - C6 e C17 sobrevivem |

Swept existing: sem remoto neste sistema; relógio serial; descarte da moradia, eventos e recuperação têm provas próprias. As lacunas abaixo são de discriminação dos testes.

## Faults injected

| Mutation | Location | Killed |
| --- | --- | --- |
| L1: não debitar carteira | LodgingSystem.CollectRent | yes - C1 |
| L2: não registrar atraso | CollectRent | yes - C2 |
| L3: ignorar GraceMonths | CollectRent | yes - C3 |
| L4: permitir lotação excessiva | MoveInHomeless | yes - C4 |
| L5: eficiência de cama no chão | RestPlaceResolver | yes - C5 |
| L6: remover `|| household.IsHomeless` | MigrationSystem.NeedsMigration | no - C6, filtro e classe inteira verdes |
| L7hash/L19fixed: alterar data e alocar sem termos, separadamente | LodgingSystem.Tick | yes - C7/C19, hash e alocação em execuções distintas |
| L10: alterar fator-base da fórmula | BuildingValuation | yes - C10 |
| L11: pressão fixa | ValuationContext | yes - C11 |
| L12: devolver aluguel fixo | BuildingValuation.RentOf | yes - C12 |
| L13: não creditar diária | GuestLodging | yes - C13 |
| L14: ignorar falta de dinheiro | GuestLodging.FindRoom | yes - C14 |
| UI-L15: omitir hospedagem na descrição | app.gd | yes - C15 |
| UI-L16: limitar a um leito | interior_view.gd | yes - C16 |
| L17: aceitar aluguel zero, mantendo negativo inválido | BuildingRecipe.Create | no - C17, teorias e classe inteira verdes |
| L18: crédito dependente do processo | GuestLodging | yes - C18, dois processos divergem |

Scratchs em /tmp, nenhuma mutação na árvore real; ensaio L19 que não compilou foi descartado e corrigido antes de ser contado.

## Gate

`bash scripts/verify.sh`: exit 0, 2298 passed / 3 skipped; `bash scripts/client-test.sh`: exit 0, 258 passed.
`bash scripts/test.sh --project longrunning --filter 'FamilyDiversityCalibrationTests|FamilyPairedScenarioTests|DeterminismTwoProcessTests|ScaleScenarioSensorTests'`: 32 passed / 2 skipped, 17m27s; século completo e FAM-33/contrafactuais verdes.
Integração: `--filter 'SavedWorld|ZeroRoundTripsTests'` 10 passed; `--filter 'ApiContractTests|WorldCreateEndpointsTests|PositionalSnapshotPersistenceTests'` 27 passed. Ambos por `bash scripts/test.sh --project integration`.
`bash scripts/compare-creation-cost.sh /tmp/lw-verifier-base /tmp/verifier-creation-cost`: 2355,396 → 1929,044 ms (razão 0,819, limite 1,05).
`bash scripts/compare-simulation-cost.sh /tmp/lw-verifier-base /tmp/verifier-simulation-cost`: 2,694 → 1,846 µs/NPC-tick; 255402 → 256268 B/tick (+0,339%, limite +2%).
`bash scripts/client-stress.sh /tmp/lw-verifier-base/client/godot` e `bash scripts/client-stress.sh`: FPS 73,2/71,4/73,1 → 73,3/74,0/72,6; medianas 73,1 → 73,3. Carga concorrente: números não demonstram aceleração.
Logs próprios em `/tmp/verifier-*`; medições por 3 pares/rodadas, base 10bec92. `WorldCreationCostTests` foi copiado à base somente como instrumentação, sem alterar o motor. Árvore real limpa antes/depois das mutações.

## Ranked gaps

1. C6: neutralizar outros gatilhos de NeedsMigration no fixture (bounds, comida, emprego/vagas), mantendo controle alojado; exigir falha ao retirar a condição de despejo.
2. C17: provar rent=0 com cenário válido em todo o resto; remover o workplace vazio que mascara a validação e matar o mutante <=0→<0.
Manter plano e STATE como verificação pendente/FAIL; #67 não pode fechar. Correção requer novo Verifier independente.
