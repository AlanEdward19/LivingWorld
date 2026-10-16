## Requirement Traceability

| Requirement ID | Story | Phase | Status |
|---|---|---|---|
| FAM-01 | P1: Relação assimétrica — substrato e formação | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/RelationshipSystemTests.Cohabiting_household_members_gain_bidirectional_relationship_entries |
| FAM-02 | P1: Relação — nunca se encontrar = nunca se conhecer | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/RelationshipSystemTests.Npcs_without_shared_household_or_workplace_get_no_relationship_entries |
| FAM-03 | P1: Relação — evolução por evento nomeado | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/RelationshipTests.ApplyEvent_applies_the_declared_delta_to_the_events_axis |
| FAM-04 | P1: Relação — decaimento sem contato | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/RelationshipSystemTests.Relationship_without_recent_contact_decays_toward_neutral_without_overshooting |
| FAM-05 | P1: Relação — assimetria comprovável (A→B ≠ B→A) | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/RelationshipTests.AtoB_and_BtoA_diverge_after_different_events_proving_asymmetry |
| FAM-06 | P1: Atração — score combinando idade/saúde/status/habilidade/afinidade/relação | Design | Pending - so ha prova do termo cultura (tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.AttractionScore_is_higher_when_cultures_match); demais termos do score sem teste achado |
| FAM-07 | P1: Cortejo — dura tempo declarado, não instantâneo | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.Tick_starts_courtship_and_schedules_completion_when_attraction_passes |
| FAM-08 | P1: Cortejo — rejeição `Incesto` (parentesco 1º grau) | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.Reject_sibling_pair_returns_Incesto |
| FAM-09 | P1: Cortejo — rejeição `ForaDaFaixaEtária` | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.Tick_rejects_pair_outside_fertility_window_with_ForaDaFaixaEtaria |
| FAM-10 | P1: Cortejo — rejeição `SemAfinidade` | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.Tick_rejects_low_attraction_with_SemAfinidade |
| FAM-11 | P1: Cortejo bem-sucedido registrado antes do casamento | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.HandleEvent_logs_CourtshipSucceeded_before_Marriage |
| FAM-12 | P1: Casamento cria household novo com estoque próprio | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/MarriageSystemTests.Marry_creates_new_household_with_marriage_initial_stock |
| FAM-13 | P1: Concepção exige janela de fertilidade + saúde + relação + recursos | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Lifecycle/NatalitySystemTests.Tick_does_not_conceive_when_{health,relationship,resource}_floor_not_met |
| FAM-14 | P1: Concepção agenda nascimento no scheduler (nunca varredura) | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Lifecycle/NatalitySystemTests.Tick_schedules_birth_without_creating_child_immediately |
| FAM-15 | P1: Mãe morre antes do parto → nascimento falha silenciosamente | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Lifecycle/NatalitySystemTests.HandleEvent_is_silent_when_mother_died_before_birth |
| FAM-16 | P1: Risco de parto — mãe e criança | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Lifecycle/NatalitySystemTests.HandleEvent_maternal_death_risk_kills_mother_without_creating_baby / HandleEvent_infant_death_risk_creates_no_live_baby |
| FAM-17 | P1: Morte de ambos os pais dissolve e redistribui household | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/HouseholdRedistributionTests (Orphaned_children_join_living_grandparent_household e irmaos) |
| FAM-18 | P1: Hereditariedade — `Vitality` genético, fórmula + mutação por `NpcId` | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Body/HeredityServiceTests.InheritVitality_* ; NatalitySystemTests.Baby_inherits_vitality_from_parents |
| FAM-19 | P1: `Upbringing` ambiental — origem distinta de `Vitality` | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Body/HeredityServiceTests.DeriveUpbringing_signature_has_no_vitality_or_gene_parameter |
| FAM-20 | P1: Separação genético/ambiental no snapshot (campos distintos) | Design | Pending - nenhum teste de snapshot achado que prove campos distintos; so separacao por assinatura (HeredityServiceTests.DeriveUpbringing_*) |
| FAM-21 | P1: Ambiente pesa na mesma ordem de grandeza que genética em resultado de vida | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs Environmental_channel_dilutes_vitality_wallet_correlation_below_channel_off_bootstrap_ci (LongRunning) |
| FAM-22 | P1: Sem função de aptidão artificial / ranking de "melhor NPC" | Design | Pending - ausencia de funcao de aptidao nao tem teste/guarda dedicado |
| FAM-23 | P2: Cenário de deriva neutra (acasalamento aleatório, seleção desligada) | Design | Verified - tests/LivingWorld.Tests.Unit/Population/Family/CourtshipSystemTests.NeutralDriftEnabled_forms_pair_without_attraction_threshold |
| FAM-24 | P2: Cenário contrafactual de household (mesmo genoma, riqueza diferente) | Design | Verified - tests/LivingWorld.Tests.Shared/Population/HouseholdCounterfactualHarness.cs + FamilyPairedScenarioTests.Rich_vs_poor_household_wealth_median_difference_bootstrap_ci95_excludes_zero |
| FAM-25 | P2: Cenários são aditivos ao harness, nunca ao caminho default | Design | Verified - ScenarioRunner.DefaultFamilyRules (NeutralDriftEnabled false) + tests/LivingWorld.Tests.Shared/Population/NeutralDriftScenarioHarness.cs (Neutral_drift_disabled_matches_default_scenario_runner: o braco desligado e o hash do default); rodado 2026-10-13 |
| FAM-26 | Verificação: linhagens rastreáveis, `esperado = anos/idadeMédiaPrimeiroParto` | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Birth_count_over_horizon_is_compatible_with_scenario_derived_expectation |
| FAM-27 | Verificação: população final vs baseline de 20 seeds | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Final_population_over_the_seed_set_stays_alive_and_near_committed_average_baseline |
| FAM-28 | Verificação: toda criança tem `PaiId`/`MãeId` válidos e vivos na concepção | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Every_birth_in_horizon_has_valid_parents_alive_at_conception_and_fertile_mother_age |
| FAM-29 | Verificação: nenhum nascimento com mãe fora da janela de fertilidade | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Every_birth_in_horizon_has_valid_parents_alive_at_conception_and_fertile_mother_age |
| FAM-30 | Verificação: incesto negativo (10 anos, zero casamentos 1º grau) | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Ten_years_default_scenario_has_zero_marriages_between_first_degree_relatives |
| FAM-31 | Verificação: incesto positivo (cenário dedicado, rejeição `Incesto`) | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Dedicated_sibling_cohabitation_scenario_rejects_courtship_with_Incesto |
| FAM-32 | Diversidade calibrada, controle negativo e piso positivo em 100 anos (ADR-0034) | Design | Implementing - FamilyDiversityCalibrationTests; baseline de 20 seeds gravado, verificação independente pendente |
| FAM-33 | Verificação: bootstrap `\|r\|` genética×sucesso, IC95 abaixo do mundo sem canal ambiental | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Environmental_channel_dilutes_vitality_wallet_correlation_below_channel_off_bootstrap_ci |
| FAM-34 | Verificação: distância mesma-genética/seeds-ambientais ≥ distância mesma-ambiental/genéticas-diferentes | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Household_wealth_distance_is_at_least_as_large_as_genome_distance |
| FAM-35 | Verificação: contrafactual household — medianas diferem e distribuições se sobrepõem (≥ overlap de genomas extremos) | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Rich_vs_poor_household_wealth_median_difference_bootstrap_ci95_excludes_zero |
| FAM-36 | Verificação: flag off (hereditariedade + formação de casais) muda `Hash(world)` em 10 anos | Design | Verified - tests/LivingWorld.Tests.LongRunning/Population/FamilyPairedScenarioTests.cs.Turning_off_heredity_and_courtship_changes_world_hash_after_ten_years |

**ID format:** `FAM-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage (specs-closeout 2026-10-12):** 36 linhas — 32 Verified, 4 Pending (FAM-06, FAM-20, FAM-22, FAM-32).
