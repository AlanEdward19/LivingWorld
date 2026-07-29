# Fase 8 — Cidades Validation

**Date**: 2026-07-28
**Spec**: `.specs/features/phase-08-cities/spec.md`
**Diff range**: `f0908f6..60a8207` (22 commits, `72ffbf0`..`60a8207`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status | Notes |
|---|---|---|
| T1–T22 | ✅ Done | All 22 commits present in range, each with its own atomic commit; final commit `60a8207` closes the phase |

---

## Spec-Anchored Acceptance Criteria

### CITY-01: Cidade como entidade agregada derivada

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: City expõe população/governo/economia/recursos/segurança/saúde/educação/infraestrutura/habitação/desigualdade computados de NPCs/edifícios | Todos os 10 campos listados existem e são derivados | `CityPopulationQueryTests.cs:793-800` `Assert.Equal(manualCount + pool.Count, Population(...))`; `:814-821` Wealth; `:824-831` Health; `:834-871` Inequality | ❌ **GAP** — só 4 de 10 campos existem. `City.cs` (grep de `public`/`record`) não tem `Government`/`Economy`/`Resources`/`Security`/`Education`/`Infrastructure`/`Housing`. `grep -rl "CityGovernment\|CityEconomy\|CityResources\|CitySecurity\|CityEducation\|CityInfrastructure\|CityHousing" src/` só bate num comentário de `MaterializationSystem.cs`, nenhum tipo real. `design.md` Tech Decisions prometia "Records vazios/stub (CityGovernment...)" para governo/cultura/tecnologia e "derivados de contagem de Building" para segurança/educação/infraestrutura/habitação — nenhum dos dois foi implementado, nem como stub, nem como query pública. `CityGrowthSystem.HousingCapacity`/`FoodStock` existem mas são `private static`, nunca expostos |
| AC2: agregado recomputado do zero a cada N ticks, divergência de 1 unidade falha | Nenhum campo cacheado — todo read já é do zero | `CityPopulationQueryTests.cs:874-881` `CityPopulationQuery_has_no_mutable_field_backing_the_aggregates` — reflexão confirma zero campos estáticos/instância | ✅ PASS |
| AC3: sem mudança de NPC/edifício entre ticks, agregados byte-idênticos | Nenhuma escrita manual de campo de cidade | Mesma prova arquitetural do AC2 (sem cache, recompute é sempre idempotente) | ✅ PASS (satisfeito por construção) |

### CITY-02: Crescimento e encolhimento

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: déficit reduz população por emigração, taxa proporcional ao déficit do cenário | `emigrants = floor(rate * excess)`, nunca fixo | `CityGrowthSystemTests.cs:191-204` `Tick_reduces_aggregate_pool_count_when_food_stock_is_below_threshold_for_the_population` — 100 pop, déficit 100%, limiar 20%, taxa 0.5 → `Assert.Equal(60, ...AggregatePool.Count)` (40 emigrantes = floor(0.5×80)) | ✅ PASS |
| AC2: fome zerada, seed pareada → `popTrat < popBase`, diff > spread de 10 seeds baseline | Contagem de acertos + margem sobre spread (R4) | `FoodShortageMigrationScenarioTests.cs:338-361` `Assert.Equal(10, wins)`; `Assert.True(diffs.Min() > spread, ...)` | ✅ PASS |
| AC3: migração nunca perde NPC no caminho, sai de A entra em B no mesmo tick | `Assert.NotEqual(default, npc.City)` sempre | `MigrationSystemTests.cs:358-369` `Migrating_npc_never_ends_up_with_no_city_it_moves_directly_to_the_destination` | ✅ PASS |

### CITY-03: Construção de edifícios

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: obra sem insumo → `Failure`, `Hash(world)` inalterado | `Result.Fail`, hash antes == depois | `ConstructionSystemTests.cs:43-57` `Assert.False(result.IsSuccess); Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world))` | ✅ PASS (confirmado pelo sensor de mutação #1 abaixo) |
| AC2: obra com insumo suficiente consome ao longo dos ticks, conclui só com consumo == receita | `Consumed == recipe.Inputs` ao concluir | `ConstructionSystemTests.cs:87-105` `Assert.Equal(0, city.Stock.GetValueOrDefault(Timber))` após 5 ticks de receita de 10 | ✅ PASS |
| AC3: múltiplas obras processadas em ordem declarada (FIFO), nunca por hash de dicionário | Só a cabeça da fila avança por tick | `ConstructionSystemTests.cs:107-122` `Queue_processes_only_the_head_project_leaving_the_second_untouched` | ✅ PASS |

### CITY-04: Simulation LOD com conservação provada

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: materializar debita exatamente 1 do pool, cria exatamente 1 `Npc` | `pool.Count -= 1`, `world.Npcs.Count += 1` | `MaterializationSystemTests.cs:603-617` `Assert.Equal(4, ...AggregatePool.Count); Assert.Equal(npcCountBefore + 1, world.Npcs.Count)` | ✅ PASS |
| AC2: desmaterializar devolve atributos ao pool e remove a linha | `pool.WealthSum/HealthSum += npc.*`, `world.Npcs` sem a linha | `MaterializationSystemTests.cs:632-648` | ✅ PASS |
| AC3: materializar+desmaterializar sem outra mudança → `Hash(world)` byte-idêntico | Hash antes == depois | `MaterializationRoundTripTests.cs:220-232` — **SPEC-PRECISION GAP documentado**: compara snapshot inteiro MENOS `NextNpcId`/`RngStreams` (contadores monotônicos que o ciclo legitimamente avança); o `CanonicalHash` literal (que inclui esses dois campos) diverge por construção, confirmado empiricamente pelo próprio comentário da classe. A asserção real é mais fraca que "Hash(world) byte-idêntico" ao pé da letra | ⚠️ Spec-precision gap (documentado, decisão consciente, mas o AC não é satisfeito na sua forma literal) |
| AC4: `COUNT(*)` materializados + pool == população total, sem tocar propriedade derivada, todo tick em 10 anos | Invariante a cada tick | `LodConservationScenarioTests.cs:118-144` — a checagem *literal* do AC4 (linha 135-139) é tautológica por construção de T8 (documentado na classe); a checagem que realmente discrimina é o total global (linha 130, `RawGlobalPopulation`) contra o valor inicial, também rodado a cada tick | ✅ PASS (com nota: literal do AC é vazia por arquitetura, mas o teste supre com uma checagem mais forte, ambas presentes no arquivo) |
| AC5: flag desliga LOD/migração → `Hash(world)` após 10 anos diverge | `Assert.NotEqual` do hash | `LodEntersHashTests.cs:564-590` `Assert.NotEqual(WorldSnapshot.CanonicalHash(worldOn), ...(worldOff))` + verificação reforçada excluindo a própria chave `CityRules` | ✅ PASS |

### CITY-05: Política de materialização por relevância

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: papel formal mantém materializado | Nunca elegível a desmaterialização enquanto ocupar o papel | `MaterializationSystemTests.cs:703-715` `Tick_never_dematerializes_an_npc_holding_a_formal_role...` | ✅ PASS (papel formal = chefe de household/mentor; "líder de assentamento" não modelado — SPEC_DEVIATION documentado em `MaterializationSystem.cs:5`, pré-aprovado pelo escopo do prompt) |
| AC2: API/CLI consulta NPC agregado → sistema materializa sob demanda antes de responder | Um NPC que só existe no pool agregado passa a existir como linha real após a consulta | `NpcInspectionQueryTests.cs` `Inspect_succeeds_and_ensures_materialization_for_a_living_npc` — usa `world.Npcs.First()`, ou seja, um NPC **já real**, nunca um membro genuíno do pool agregado | ❌ **GAP não documentado como SPEC_DEVIATION** — `MaterializationSystem.EnsureMaterialized` (`MaterializationSystem.cs:504-509`) só verifica se o `Npc` já existe como linha (`world.FindNpc(npcId)`); nunca chama `MaterializeOne`. Isso é estrutural: o pool agregado (Approach A) não guarda `NpcId` por membro — não existe um id "ainda não materializado" para se consultar. O Independent Test da spec ("Consultar NPC agregado nunca materializado via API → aparece materializado no store logo após a chamada", spec.md linha 188-190) descreve um cenário que a arquitetura atual não consegue produzir nem testar: não há como nomear um NPC agregado por id para disparar a materialização. O comentário de `EnsureMaterialized` reconhece isso em prosa ("só há o que 'garantir' para quem já existe como linha real; um id nunca visto não tem entidade a resolver") mas não está marcado `SPEC_DEVIATION` e não apareceu na lista de desvios pré-aprovados |
| AC3: NPC perde papel relevante e não é alvo de inspeção ativa → elegível a desmaterialização (FIFO/tempo ocioso) | Ociosidade por `MaterializationIdleTicksBeforeEligible` | `MaterializationSystemTests.cs:690-701,717-728` (`Tick_dematerializes_...once_idle_past_the_threshold`, `Tick_does_not_dematerialize_before_the_idle_threshold_is_reached`) | ✅ PASS |

### CITY-06: API + CLI de inspeção somente leitura

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: `GET /npcs/{id}` NPC vivo devolve identidade/família/profissão/atributos/rotina/memórias, sem escrita | DTO completo, 200 | `NpcEndpointTests.cs` `GetNpc_returns_200_with_the_inspection_dto_for_a_living_npc` + `NpcInspectionQueryTests.cs` field-by-field (linhas ~26-49) | ✅ PASS |
| AC2: CLI imprime mesmo conjunto que API, mesmo caminho (zero lógica duplicada) | Ambos chamam `NpcInspectionQuery.Inspect` | `src/LivingWorld.Api/Program.cs:16-19` e `src/LivingWorld.Workers/Program.cs:53-64` chamam literalmente o mesmo `NpcInspectionQuery.Inspect`; `InspectNpcCliTests.cs` prova stdout não vazio para id vivo | ✅ PASS |
| AC3: id inválido → 404 (API) / exit≠0 (CLI), sem exceção não tratada | `Results.NotFound()` / `Console.Error` + exit 1 | `NpcEndpointTests.cs` `GetNpc_returns_404_for_an_id_that_does_not_exist`; `InspectNpcCliTests.cs` `InspectNpc_exits_nonzero_without_an_unhandled_stack_trace_for_an_id_that_does_not_exist` (`Assert.DoesNotContain("Unhandled exception", stderr)`) | ✅ PASS |
| AC4: 100 NPCs, todos vivos iterados sem sorteio, campo a campo por reflexão, falha se campo sem comparação | 100/100 comparados, campo novo reprova o teste de cobertura | `NpcInspectionDtoCoverageTests.cs:651-656` (`Every_dto_property_has_a_registered_comparison`) + `:658-679` (`All_100_living_npcs_match_...`, `Assert.Equal(PopulationCount, compared)`) + `:683-694` checagem B (não-raso, discrimina NPC trocado) | ✅ PASS |

### CITY-07: Migração multifatorial (P2)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: pesos de emprego/comida/segurança/laços familiares vêm do cenário, não ordem fixa em C# | Score ponderado por `CityRules` | `MigrationSystemTests.cs:345-356` (peso de comida isolado), `:397-422` (`Family_ties_can_keep_a_household_in_a_worse_city_when_weighted_heavily`, peso de laço 100 vence comida) | ✅ PASS |
| AC2: household migra em conjunto, move todos os membros no mesmo tick, preserva `HouseholdId` | Todo `household.Members` muda de cidade | `MigrationSystemTests.cs:371-382` só prova preservação do `HouseholdId`; **nenhum teste usa household com >1 membro** (`SeedTwoOneNpcHouseholds` e o teste de laços familiares só criam households de 1 membro cada) | ❌ **GAP de cobertura** — o código (`MigrationSystem.cs:565-571`, `foreach (var memberId in household.Members) ... member.JoinCity(...)`) parece correto por inspeção, mas evidence-or-zero: nenhum teste com household de 2+ membros confirma que um membro não-chefe também migra |

### CITY-08: Fundação de assentamento (P2)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| AC1: todos os limiares batidos → funda em `≤ K` ticks (`K = OrganizationTicks`) | Evento agendado em exatamente `now + K` | `SettlementFoundingSystemTests.cs:468-482` (`Tick_schedules_founding_in_exactly_organization_ticks_when_all_thresholds_are_met`, `Assert.Equal(world.CurrentDate.TotalHours + 10, pending.TargetTick)`); cenário vivo em `SettlementFoundingScenarioTests.cs:428-443` | ✅ PASS — com **SPEC_DEVIATION pré-aprovado**: dos 5 limiares (concentração/recurso/rota/defensabilidade/liderança), só concentração tem sinal real; os outros 4 ficam vacuamente satisfeitos (`unmeasuredLevel = 1.0` fixo, `SettlementFoundingSystem.cs:57`). `SettlementFoundingScenarioTests.cs` roda um par de controle (limiar atingível vs. inatingível) que prova que o único sinal real (concentração) discrimina de verdade — confirmado pelo sensor de mutação #4 abaixo |
| AC2: soma de populações antes/depois do split é idêntica | `Σpop` antes == depois | `SettlementFoundingSystemTests.cs:513-533` `HandleEvent_founds_a_new_city_and_preserves_total_population_across_the_split`; `SettlementFoundingScenarioTests.cs:437-443` mesmo em cenário vivo | ✅ PASS |

### CITY-09: Contagem independente de auditoria

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
|---|---|---|---|
| Contagem lida sem tocar propriedade derivada, soma bate com população total | `COUNT(*) + AggregatePool.Count` lido cru == total | `LodConservationScenarioTests.cs:66-67` (`RawGlobalPopulation`, lê `world.Npcs`/`city.AggregatePool.Count` direto, nunca via `CityPopulationQuery`) comparado contra o total inicial a cada tick, 10 e 100 anos (`:146-157`) | ✅ PASS |

**Status**: ❌ Gaps present — 3 ranked gaps (ver abaixo), o resto (17 de 20 critérios verificáveis) bate com o outcome da spec.

---

## Discrimination Sensor

Todas as mutações injetadas diretamente nos arquivos reais (`src/LivingWorld.Simulation/Cities/*.cs`), rodadas com `bash scripts/test.sh --filter <classe>`, e revertidas com `git checkout --` imediatamente após confirmar o resultado (árvore confirmada limpa antes e depois de cada mutação).

| # | File:line | Mutação | Teste alvo | Killed? |
|---|---|---|---|---|
| 1 | `ConstructionSystem.cs:76` | `amount` → `amount / 2` no guard de insumo suficiente (`StartConstruction`) | `ConstructionSystemTests.StartConstruction_fails_and_leaves_world_hash_unchanged_when_stock_is_insufficient` | ✅ Killed (`Assert.False` recebeu `True`) |
| 2 | `CityPopulationQuery.cs:17` | Remoção de `+ PoolOf(world, city).Count` de `Population` | `CityPopulationQueryTests.Population_matches_manual_alive_npc_count_plus_aggregate_pool_count` + `Population_never_counts_an_npc_assigned_to_a_different_city` | ✅ Killed (2 testes, esperado 6 obteve 1) |
| 3 | `CityGrowthSystem.cs:44-45` | `deficit - threshold` → `deficit + threshold` (inverte a lógica do limiar) | `CityGrowthSystemTests.Tick_reduces_aggregate_pool_count_...` + `Tick_does_not_emigrate_when_deficit_stays_within_the_threshold` | ✅ Killed (2 testes) |
| 4 | `SettlementFoundingSystem.cs:59-63` | `&&` → `\|\|` entre os 5 limiares de fundação (`AllThresholdsMet`) | `SettlementFoundingSystemTests.Tick_does_not_schedule_when_concentration_threshold_is_not_met` | ✅ Killed |

**Sensor depth**: lightweight (padrão da fase, não é P0/crítico de pagamento/auth)
**Result**: 4/4 killed — ✅ PASS

---

## Code Quality

| Principle | Status |
|---|---|
| Minimum code | ✅ — sem abstração para uso único, `AggregatePopulationPool`/`City`/sistemas seguem molde já existente (Household/Workplace/EconomyRules) |
| Surgical changes | ✅ — 49 arquivos tocados, todos dentro do escopo de Cities ou pontos de integração explicitamente previstos em design.md (`Npc.cs`, `Household.cs`, `WorldState.cs`, `ReferentialIntegritySweep.cs`, `Program.cs` ×2) |
| No scope creep | ✅ |
| Matches patterns | ✅ — construtor único de reidratação, `Result<T>`, `CityRules.Create`/`Disabled` no molde de `EconomyRules` |
| Spec-anchored outcome check | ⚠️ — 17/20 critérios batem exatamente; 3 gaps documentados acima |
| Per-layer Coverage Expectation | ✅ na maior parte — domínio com 1:1 AC; sistemas com unit+determinismo; cenário com par causal (R4) onde aplicável |
| Every test maps to a spec requirement | ✅ — nenhum teste "solto" encontrado nas classes revisadas |
| Documented guidelines followed | `rules/tests.md`, `rules/eval-criteria.md`, `rules/simulation-determinism.md` — seguidos (RNG semeado para `CityId`, sem `Guid.NewGuid()` direto fora de teste; um assert por teste na maioria dos casos) |
| SPEC_DEVIATION discipline | ✅ na maioria — 25+ marcações no código, a maior parte justificada e rastreável a uma task; **exceção**: o comportamento de `EnsureMaterialized` (gap #2 abaixo) está documentado em prosa mas não com a tag `SPEC_DEVIATION` nem citado no prompt de pré-aprovação |

---

## Edge Cases (spec.md)

- [x] Cidade com população zero mantém a entidade (nenhum código remove `City` de `world.Cities`; nada no código de emigração/desmaterialização chama qualquer remoção de cidade)
- [x] Dois NPCs de households diferentes migrando pra mesma cidade no mesmo tick não conflitam (`MigrationSystem.Tick` itera households independentemente, `JoinCity` é idempotente, sem trava de capacidade)
- [x] API consultada por NPC morto → 404 (`NpcInspectionQuery.Inspect` checa `!npc.IsAlive` → falha; `NpcEndpointTests`/`NpcInspectionQueryTests.Inspect_fails_for_a_dead_npc`)
- [x] Obra pausa (não reverte) quando insumo é drenado por consumidor concorrente — `ConstructionSystemTests.cs:124-142` `Tick_pauses_without_reverting_progress_when_a_concurrent_consumer_drains_the_stock`

---

## Gate Check

- **Gate command**: `bash scripts/verify.sh` (check-docs + build + lint + full test suite)
- **Result**: 734 passed, 0 failed, 5 skipped — `verify: OK`
- **Skipped tests**: 5 `ZZZ_record_*_baseline` — pré-existentes de fases anteriores (5, 6, 7), recorders de baseline, não relacionados a Cidades, mesmo padrão já presente antes desta fase
- **Cities-scoped run** (`--filter "FullyQualifiedName~Cities"`): 111 passed, 0 failed, 0 skipped

---

## Ranked Gaps

1. **CITY-01 AC1 — campos de cidade incompletos**: `City` expõe apenas população/riqueza/saúde/desigualdade (via `CityPopulationQuery`). Governo, economia (distinta de riqueza), recursos, segurança, educação, infraestrutura e habitação — todos citados literalmente na spec — não existem nem como stub/record vazio, apesar de `design.md` Tech Decisions prometer exatamente isso (`CityGovernment` stub, segurança/educação/infraestrutura/habitação derivados de `Building`). `CityGrowthSystem.HousingCapacity`/`FoodStock` calculam parte disso internamente mas são `private static`, nunca expostos como campo de `City`. **Severidade: Major** (funcionalidade central da entidade "Cidade" declarada na spec, ausente).
2. **CITY-05 AC2 — materialização sob demanda não é exercitável para NPC genuinamente agregado**: `MaterializationSystem.EnsureMaterialized` só verifica existência (`world.FindNpc`), nunca chama `MaterializeOne`. Como o pool agregado (Approach A) não atribui `NpcId` a membros não-materializados, não existe um id de NPC "agregado" para consultar via API/CLI e disparar materialização — o Independent Test da story (spec.md linhas 188-190) descreve um cenário estruturalmente irrealizável na arquitetura atual, e nenhum teste tenta. Documentado em prosa no código, mas não como `SPEC_DEVIATION` nem citado nas decisões pré-aprovadas do prompt. **Severidade: Major** (um AC inteiro da story P1 "Política de materialização" não tem como ser satisfeito nem testado).
3. **CITY-07 AC2 — cobertura de household multi-membro ausente**: todos os testes de `MigrationSystemTests` usam households de exatamente 1 membro (`[head.Id]`); nenhum teste comprova que membros não-chefe de um household maior migram junto. O código (`MigrationSystem.cs` `foreach (var memberId in household.Members) ...`) parece correto por inspeção, mas evidence-or-zero classifica como não coberto. **Severidade: Minor** (provável bug ausente, mas sem prova).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
|---|---|---|
| CITY-01 | In Tasks | ⚠️ Needs Fix (AC1 parcial) |
| CITY-02 | In Tasks | ✅ Verified |
| CITY-03 | In Tasks | ✅ Verified |
| CITY-04 | In Tasks | ✅ Verified (1 spec-precision gap documentado no hash round-trip) |
| CITY-05 | In Tasks | ❌ Needs Fix (AC2) |
| CITY-06 | In Tasks | ✅ Verified |
| CITY-07 | In Tasks | ⚠️ Needs Fix (cobertura AC2) |
| CITY-08 | In Tasks | ✅ Verified |
| CITY-09 | In Tasks | ✅ Verified |

---

## Summary

**Overall**: ⚠️ Issues — núcleo da fase (conservação de LOD, crescimento/encolhimento, construção, fundação, inspeção API/CLI) é sólido e bem discriminado pelos testes e pelo sensor de mutação; 3 gaps genuínos e não pré-aprovados encontrados por verificação independente, sendo 2 deles (`CITY-01`, `CITY-05`) de severidade Major.

**Spec-anchored check**: 17/20 critérios batem com o outcome da spec; 1 spec-precision gap documentado (hash round-trip exclui contadores monotônicos); 2 gaps Major + 1 gap Minor de cobertura.

**Sensor**: 4/4 mutações mortas.

**Gate**: 734 passed, 0 failed (`bash scripts/verify.sh`).

**What works**: conservação de LOD (global e por-cidade) provada por 10 e 100 anos; round-trip de materialização; construção FIFO com falha limpa; fome com controle causal (R4, 10/10 seeds); fundação com par de controle discriminando o único limiar real; inspeção exaustiva de 100 NPCs por reflexão sem sorteio; API e CLI comprovadamente na mesma fonte de consulta.

**Issues found**:
1. `City` não expõe governo/economia/recursos/segurança/educação/infraestrutura/habitação — só população/riqueza/saúde/desigualdade. Fix: adicionar os campos/stubs prometidos em `design.md` Tech Decisions, ou reduzir formalmente o escopo do AC1 da spec com uma nova decisão registrada.
2. `EnsureMaterialized` não materializa NPC agregado sob demanda (só confere existência) — a política de materialização por relevância (CITY-05) não cobre o gatilho de "alvo de inspeção" para quem ainda está no pool. Fix: decidir conscientemente se o Approach A precisa de um mecanismo de "amostrar 1 do pool ao inspecionar um id desconhecido" ou se o AC deve ser reescrito para refletir a limitação estrutural.
3. Adicionar teste de `MigrationSystemTests` com household de 2+ membros confirmando que todos migram.

**Next steps**: rotear os 3 gaps acima como fix tasks (implementador ≠ este verificador); revalidar após a correção, respeitando o limite de 3 iterações fix→re-verify antes de escalar ao usuário.

---

## Re-verificação 2

**Date**: 2026-07-28
**Diff range**: `60a8207..a3d299f` (3 commits: `bdfea26` CITY-01 AC1, `369086c` CITY-05 AC2, `a3d299f` CITY-07 AC2)
**Verifier**: independente, fresh (author ≠ verifier), rodada 2 de 3 do loop fix→re-verify

### Gap 1 — CITY-01 AC1 (campos de cidade incompletos)

| Criterion | Fix evidence | Result |
|---|---|---|
| Governo/Cultura/Tecnologia existem | `City.cs:20-22` `Government/Culture/Technology => *.Empty` (stub records, `CityInstitutions.cs`); teste `CityPopulationQueryTests.cs` `City_exposes_government_culture_and_technology_as_existing_stub_records` — `Assert.NotNull` nos 3 | ✅ RESOLVIDO |
| Economia/Segurança/Educação/Infraestrutura/Habitação, derivados, nunca cacheados | `CityPopulationQuery.cs:46` (`Economy` = alias de `Wealth`, SPEC_DEVIATION explícito), `:51-53` (`Housing`, soma de `HousingCapacityProvided` só da cidade), `:64/67/70` (`Security/Education/Infrastructure` = contagem de `Building` da cidade, SPEC_DEVIATION explícito) | `Housing_equals_sum_of_housing_capacity_of_completed_buildings_for_the_city` prova exclusão de building de outra cidade (`Assert.Equal(8, housing)`, 2 buildings × 4 cap., ignora o 3º de `otherCity`); `Security_education_and_infrastructure_equal_the_completed_building_count_for_the_city` prova `Assert.Equal(2, ...)` nos 3, excluindo building fora da cidade | ✅ RESOLVIDO |

Todos os 10 campos do AC1 agora existem (4 reais desde a rodada 1 + 3 stubs + 3 derivados de `Building`), com `SPEC_DEVIATION` explícito e rastreável para os que não têm sinal de comportamento próprio nesta fase — coerente com o "Out of Scope" da spec ("Governo, cultura, tecnologia como sistemas simulados... Fase 8 só guarda os campos/instituições como estado agregado"). `CityGrowthSystem.HousingCapacity` foi removido (duplicação) e substituído por `CityPopulationQuery.Housing` compartilhado — nenhuma regressão: sensor de mutação #3 da rodada 1 (limiar de déficit) continua killed (verificado no gate abaixo, sem falha em `CityGrowthSystemTests`).

**Veredito: gap fechado.**

### Gap 3 — CITY-07 AC2 (household multi-membro não coberto)

`MigrationSystemTests.cs:163-190` `Household_migration_moves_every_member_not_just_the_head` — household de 2 membros (`[head.Id, otherMember.Id]`), origem sem comida / destino farto; após 1 tick de `MigrationSystem`, `Assert.Equal(destination.Id, otherMember.City)` (linha 188) prova que o membro não-chefe migrou junto, não só `head`. Teste discrimina de verdade: se o código só movesse `household.Head`, este assert falharia.

**Veredito: gap fechado.**

### Gap 2 — CITY-05 AC2 (materialização sob demanda) — exame crítico

Fix em `MaterializationSystem.cs:106-138` (comentário `DESIGN`, ver `git show 369086c`). Mecanismo: como o `AggregatePopulationPool` (Approach A) nunca atribuiu `NpcId` a membros individuais, o único id "endereçável" de um membro nunca materializado é o próximo que `WorldState.NextNpcId` vai emitir — consultar esse id específico agora chama `MaterializeOne` de verdade contra `world.Cities.FirstOrDefault(c => c.AggregatePool.Count > 0)`.

**Pergunta 1 — isso cumpre o AC2 literal, ou é workaround de um único id mágico?**
Cumpre a *letra* do Independent Test da spec ("consultar NPC agregado nunca materializado via API → aparece materializado no store logo após a chamada", spec.md:188-190): `NpcInspectionQueryTests.cs:70-88` (`Inspect_materializes_a_never_touched_aggregate_pool_member_on_demand`) usa `world.NextNpcId` como id — genuinamente nunca tocado, genuinamente vindo do pool agregado (não um NPC já materializado) — e `Assert.Equal(neverTouchedId, result.Value!.Id)` prova que o id consultado é o id retornado, materializado. Isso é estritamente melhor que a rodada 1 (onde nenhum id nomeável existia). **Mas** é, na prática, um único id endereçável por vez — o chamador não pode nomear "qual" membro do agregado quer inspecionar (não há seleção por conteúdo/atributo, só o próximo id do contador global). Isso é consequência estrutural do Approach A (nenhuma identidade individual no pool) já era a limitação de origem, não uma piora introduzida pelo fix — mas o fix não resolve, e não tenta resolver, a "seleção de membro" — só a existência de um id nomeável.

**Pergunta 2 — 2+ cidades com pool não-vazio: comportamento determinístico e testado?**
Determinístico por código: sim — `world.Cities` é `List<City>` (`WorldState.cs:166,174`), `FirstOrDefault` percorre em ordem de inserção, sem RNG. **Testado: não.** Toda fixture nova (`MakeWorldWithCity`, `NpcInspectionQueryTests`) usa exatamente 1 cidade no mundo. Confirmado por mutação (ver sensor abaixo): trocar `FirstOrDefault` → `LastOrDefault` **sobrevive** — nenhum teste do arquivo, nem a suíte inteira de Cities (119 testes), falha. Ou seja, "primeira cidade com pool não-vazio" é uma escolha de código sem nenhuma prova de que é a escolha certa — poderia ter sido `LastOrDefault`, `MinBy(c => c.Id)`, ou qualquer outra ordem, e nada acusaria.

**Pergunta 3 — o teste novo cobre o cenário de verdade, com asserção no valor retornado?**
Sim para o caso de 1 cidade: `Assert.Equal(neverTouchedId, result.Value!.Id)` e `Assert.Equal(poolCountBefore - 1, ...AggregatePool.Count)` (`MaterializationSystemTests.cs:203`) — não é só "não lança exceção". Mas não cobre o caso de 2+ cidades com pool não-vazio simultâneo, que é exatamente o cenário onde a escolha de cidade importa.

**Pergunta 4 — quebra alguma invariante de conservação (T17/T18)?**
Não. `EnsureMaterialized` só é chamado por `NpcInspectionQuery.Inspect` (único call site fora de teste, `NpcInspectionQuery.cs:17` — grep confirma), nunca pelo loop de `Tick`. Internamente chama o mesmo `MaterializeOne` já coberto pelos testes de conservação/round-trip de CITY-04 (T17/T18) — não introduz novo estado persistido em `City` nem no snapshot (o próprio `DESIGN` confirma, e a suíte completa de Cities passa: ver gate abaixo). Conservação intacta.

**Veredito**: o gap original da rodada 1 ("nenhum id nomeável existe, cenário estruturalmente impossível") está **fechado** — o Independent Test literal da spec agora é produzível e testado. Mas a correção introduz uma nova superfície não coberta pelo próprio sensor de discriminação: a seleção de "qual cidade" quando há 2+ pools não-vazios é determinística só por acidente de implementação (`List` + `FirstOrDefault`), sem teste que a trave. **Gap novo, porém de severidade menor** (Minor — lacuna de cobertura de teste, não defeito funcional: não há "resposta certa" a ser violada, já que o pool não tem identidade individual; o risco é regressão silenciosa se alguém trocar a ordem de iteração sem perceber que um teste dependia dela implicitamente).

### Discrimination Sensor (rodada 2)

| # | File:line | Mutação | Teste alvo | Killed? |
|---|---|---|---|---|
| 1 | `MaterializationSystem.cs:133,135` (combinado) | Removido o guard `npcId.Value != world.NextNpcId` **e** trocado `FirstOrDefault`→`LastOrDefault` | Suíte completa `Cities` | ✅ Killed — `MaterializationSystemTests.EnsureMaterialized_fails_for_an_id_that_does_not_exist` falhou (`Assert.False` recebeu `True`) |
| 2 | `MaterializationSystem.cs:135` (isolado, guard do id mantido) | `FirstOrDefault` → `LastOrDefault` (só a escolha de cidade) | Suíte completa `Cities` (119 testes) | ❌ **Survived** — 119/119 passaram; nenhum teste tem 2+ cidades com pool não-vazio simultâneo |

**Sensor depth**: lightweight (2 mutações, conforme escopo da rodada)
**Result**: 1/2 killed — ⚠️ 1 sobrevivente (ver gap novo acima)

### Ranked Gaps (rodada 2)

1. **CITY-05 — seleção de cidade em `EnsureMaterialized` não testada para 2+ cidades com pool não-vazio** (Minor, novo, achado pelo sensor de mutação desta rodada — `LastOrDefault` sobrevive). Fix sugerido: teste com 2 cidades de pool não-vazio confirmando qual é escolhida (documentando a ordem como contrato, ainda que arbitrária) — ou, alternativamente, registrar a arbitrariedade como decisão consciente (`SPEC_DEVIATION`/`STATE.md`) se o time decidir que não vale a pena travar a ordem.

CITY-01 (gap 1) e CITY-07 (gap 3) da rodada 1: **fechados**, evidência acima.

### Summary (rodada 2)

**Overall**: ❌ FAIL — 2 dos 3 gaps da rodada 1 fechados de verdade (CITY-01, CITY-07); o terceiro (CITY-05 AC2) teve seu gap *original* fechado, mas o próprio fix introduziu uma superfície nova sem cobertura, capturada pelo sensor de mutação desta rodada (severidade Minor, não bloqueia conservação nem os outros ACs).

**Spec-anchored check**: CITY-01 AC1 e CITY-07 AC2 — outcome bate com a spec, evidência em `file:line`. CITY-05 AC2 — outcome bate com o Independent Test literal, mas nova lacuna de cobertura encontrada pelo sensor.

**Gate**: `bash scripts/verify.sh` — 742 passed, 0 failed, 5 skipped (`verify: OK`). Skipped: os mesmos 5 `ZZZ_record_*_baseline` de fases anteriores (recorders de baseline, não relacionados a Cidades — mesma lista da rodada 1). Test count subiu de 734 (rodada 1) para 742 (+8: 2 `EnsureMaterialized_*` novos, 1 `Inspect_materializes_a_never_touched_...`, 3 `CityPopulationQuery` novos — `Economy_equals_wealth`/`Housing_equals_.../`Security_education_and_infrastructure_...`, 1 `City_exposes_government_culture_and_technology_...`, 1 `Household_migration_moves_every_member_not_just_the_head` = 8 testes novos dos 3 commits de fix).

**Sensor**: 1/2 mutações mortas (1 sobrevivente, novo achado desta rodada).

**Next steps**: rotear o gap Minor de CITY-05 (teste de 2 cidades com pool não-vazio) como fix task; esta é a rodada 2 de 3 — ainda há margem para 1 rodada de fix→re-verify antes de escalar ao usuário.

---

## Re-verificação 3 (final)

**Date**: 2026-07-28
**Diff range**: `a3d299f..f3295f2` (1 commit: `f3295f2` — fecha o gap Minor da rodada 2)
**Verifier**: independente, fresh (author ≠ verifier), rodada 3 de 3 (última permitida antes de escalar ao usuário)

### Gap único da rodada 2 — CITY-05 AC2 (seleção de cidade em `EnsureMaterialized` não testada para 2+ cidades com pool não-vazio)

Fix: `f3295f2`, arquivo único tocado — `tests/LivingWorld.Tests/Cities/MaterializationSystemTests.cs` (+26 linhas, nenhuma linha de produção alterada). Novo teste:
`MaterializationSystemTests.cs:218-243` `EnsureMaterialized_picks_the_first_city_in_world_order_with_a_non_empty_pool_when_multiple_qualify`.

Evidência de que cobre o cenário exigido (2+ cidades com pool não-vazio simultâneo, não só ausência de exceção):
- Setup cria **duas** cidades com pool não-vazio ao mesmo tempo: `firstCity` via `MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400))` (linha 223) e `secondCity` explicitamente adicionada com `new AggregatePopulationPool(5, 500, 400)` (linhas 224-227, `world.AddCity(secondCity)`) — ambos os pools com `Count == 5` no momento da chamada, nenhum vazio.
- `neverTouchedId = new NpcId(world.NextNpcId)` (linha 228) — id genuinamente nunca materializado, mesmo padrão já validado na rodada 2.
- Asserções após `MaterializationSystem.EnsureMaterialized(world, neverTouchedId)` (linha 233):
  - `Assert.Equal(firstCity.Id, materialized!.City)` (linha 237) — **assere sobre `materialized.City`**, não só `result.IsSuccess`. Prova qual das duas cidades foi escolhida.
  - `Assert.Equal(firstPoolBefore - 1, world.FindCity(firstCity.Id)!.AggregatePool.Count)` (linha 238) — debita exatamente 1 do pool da cidade certa.
  - `Assert.Equal(secondPoolBefore, world.FindCity(secondCity.Id)!.AggregatePool.Count)` (linha 239) — prova que a **segunda** cidade (não escolhida) permanece intocada, o que discrimina de verdade contra `LastOrDefault`.

Isso é exatamente o cenário que faltava na rodada 2: 2 pools não-vazios simultâneos, com asserção no valor retornado (`materialized.City`) e no efeito colateral (contagem de pool de ambas as cidades), não apenas "não lançou exceção".

### Sensor de mutação (rodada 3)

Mutação alvo, mesma da rodada 2: `MaterializationSystem.cs:135` `world.Cities.FirstOrDefault(...)` → `world.Cities.LastOrDefault(...)` (guard do id em `:133` mantido intacto, só a escolha de cidade mutada).

- Árvore confirmada limpa antes (`git status --porcelain -- src/LivingWorld.Simulation/Cities/MaterializationSystem.cs` vazio).
- Mutação aplicada, suíte alvo rodada: `bash scripts/test.sh --filter "FullyQualifiedName~MaterializationSystemTests"`.
- Resultado: **1 falha** — `EnsureMaterialized_picks_the_first_city_in_world_order_with_a_non_empty_pool_when_multiple_qualify` — `Assert.Equal() Failure: Expected: b0735818-... Actual: a71f05aa-...` (id de `firstCity` esperado, `secondCity` obtido). 13 passed, 1 failed, 14 total.
- Mutação revertida (`git checkout -- src/LivingWorld.Simulation/Cities/MaterializationSystem.cs`), árvore confirmada limpa de novo.

**Sensor depth**: lightweight (1 mutação, escopo do gap único remanescente)
**Result**: 1/1 killed — ✅ Mutação que sobrevivia na rodada 2 agora **morre**.

### Gate Check (final)

- **Gate command**: `bash scripts/verify.sh` (check-docs + build + full test suite), síncrono
- **Result**: 743 passed, 0 failed, 5 skipped — `verify: OK`
- **Skipped tests**: os mesmos 5 `ZZZ_record_*_baseline` de fases anteriores (5, 6, 7), não relacionados a Cidades — mesma lista das rodadas 1 e 2
- Test count subiu de 742 (rodada 2) para 743 (+1: o novo teste de seleção de cidade)

### Veredito

O único gap remanescente da rodada 2 (CITY-05 AC2, Minor — seleção de cidade não testada para 2+ pools não-vazios) está **fechado**: o teste novo cobre exatamente o cenário exigido, com asserção sobre o valor retornado e sobre o estado de ambas as cidades, e o sensor de mutação que sobrevivia na rodada 2 agora morre. Nenhum gap novo encontrado. Todos os gaps rastreados nas rodadas 1 e 2 (CITY-01 AC1, CITY-05 AC2, CITY-07 AC2) estão fechados com evidência.

### Summary (rodada 3, final)

**Overall**: ✅ **PASS** — 0 gaps abertos.

**Gate**: 743 passed, 0 failed, 5 skipped (`bash scripts/verify.sh`).

**Sensor**: 1/1 mutação (a que sobrevivia na rodada 2) morta nesta rodada.

**Next steps**: nenhum — fase 8 (Cidades) fechada, sem gaps pendentes de verificação independente.
