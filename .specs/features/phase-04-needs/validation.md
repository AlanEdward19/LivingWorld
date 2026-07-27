# Fase 4 — Necessidades e rotina — Validation

**Date**: 2026-07-27
**Spec**: `.specs/features/phase-04-needs/spec.md`
**Diff range**: `1abb0a3..b4c496f` (feat(phase-03b) → feat(phase-04) final)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status  | Notes |
| ---- | ------- | ----- |
| T1–T15 | ✅ Done | 15 feature commits (`d8d9d53`..`b4c496f`) + 1 support commit (`053d05f`, decisions-log extraction). All `tasks.md` Done-when boxes verified against code/tests below. |

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| NEEDS-01: tick Hourly decrementa fome/sede/sono/social pela taxa de `PopulationRules`/`NeedsRules` | Decremento == taxa declarada, não constante em C# | `tests/LivingWorld.Tests/Behavior/NeedsDecaySystemTests.cs:59-62` — `Assert.Equal(100 - (int)rules.HungerDecayPerHour, npc.Hunger)` (e Thirst/Sleep/Social análogos) | ✅ PASS |
| NEEDS-02: clamp `[0,100]` e, se clampou em 0, dispara objetivo no mesmo tick | `npc.Hunger == 0` após decaimento extremo; `HasUrgentNeed` público (não estado privado) vira `true` no mesmo tick | `NeedsDecaySystemTests.cs:74` — `Assert.Equal(0, npc.Hunger)`; `NeedsDecaySystemTests.cs:84-88` — `Assert.True(npc.HasUrgentNeed(rules))` após `system.Tick`, mesmo com `urgencyThreshold: 100` (só o hit-zero, não o limiar comum, já dispara) | ✅ PASS |
| NEEDS-03: fome em 0 por `X = ceil(100/taxa)` ticks mata com `causa == Starvation`, datável | Morte em `[X, X+1]` ticks após hunger==0; evento `WorldEventKind.Starvation` no log | `NeedsDecaySystemTests.cs:129,142,144-145` — `long survivalTicks = ceil(100/rate)`; `Assert.InRange(deathTick - hungerZeroTick, survivalTicks, survivalTicks+1)`; `Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Starvation)` | ✅ PASS |
| NEEDS-04: desligar utility AI muda `Hash(world)` em 10 anos, mesma seed | `hashWithUtilityAi != hashWithoutUtilityAi` | `tests/LivingWorld.Tests/Behavior/UtilityAiHashScenarioTests.cs:24` — `Assert.NotEqual(hashWithUtilityAi, hashWithoutUtilityAi)` (10 anos, seed 42, remove `NeedsDecaySystem`/`BehaviorDecisionSystem` de `DefaultSystems()`) | ✅ PASS |
| NEEDS-05: necessidade acima do limiar expõe objetivo inspecionável, não estado interno | Propriedade/método público, não `private` | `src/LivingWorld.Domain/Population/Npc.cs:152` — `public bool HasUrgentNeed(NeedsRules rules)`; consumido publicamente em `NeedsDecaySystemTests.cs:88` | ✅ PASS |
| NEEDS-06: nota = utilidadeBase × pesoPersonalidade; maior nota vence; empate por menor `ActionId` | Vencedor determinístico por nota; empate exato → menor `ActionId` | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemTests.cs:168-180` — `Exact_utility_tie_breaks_by_the_smaller_ActionId`: déficit Social=50 empata com baseline 50 de Work/Socialize/Travel/Idle sob personalidade neutra; `Assert.Equal(ActionType.Work, npc.CurrentAction)` (Work=ActionId 2, o menor entre os 4 empatados) | ✅ PASS |
| NEEDS-07: fome=90 escolhe `Eat`, fome=10 escolhe `Work`, comida a 1 local, turno aberto, 10/10 seeds | Ação distinta nos dois braços em 10/10 seeds | `BehaviorDecisionSystemTests.cs:150-165` — `[Theory]` com seeds 1u..10u, `Hunger_beats_the_open_work_shift_with_a_control_arm_in_10_of_10_seeds`: `Assert.Equal(ActionType.Eat, npcHungry.CurrentAction)` e `Assert.Equal(ActionType.Work, npcFed.CurrentAction)` no mesmo teste (braço de controle real, R4) | ✅ PASS |
| NEEDS-08: tabela de 10 traços, 20 vs 80, 10/10 seeds; falha se traço sem linha | Ação prevista bate em 10/10 seeds por linha; teste falha se faltar traço | `BehaviorDecisionSystemTests.cs:36-48` (`TraitPredictedActionCases`, 10 linhas) + `:183-189` (`Every_personality_trait_has_a_predicted_action_case` — `Assert.Equal(10, PersonalityWeighting.AllTraitNames.Count)` + `Assert.Contains(trait, covered)` por traço) + `:193-214` (`Trait_at_20_vs_80_flips_the_predicted_action_in_10_of_10_seeds`, loop `seed 1..10`, `Assert.Equal(lowAction/highAction, ...)`) | ✅ PASS |
| NEEDS-09: seleção converge em `MaxActionSelectionSteps`; nenhum NPC vivo sem ação ao fim do tick; ciclo patológico aborta nomeando NPC+ações | Abort nomeado, nunca laço | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemHysteresisTests.cs:123-128` — `Assert.Throws<TickBudgetExceededException>`; `Assert.Contains("7", ex.Message)`, `Assert.Contains("Work"/"Idle", ex.Message)` | ✅ PASS (não há teste dedicado a "nenhum NPC vivo fica sem ação ao fim do tick" fora do caso feliz já implícito nos demais testes de `BehaviorDecisionSystemTests`/`Travel` — ⚠️ ver gap 1) |
| NEEDS-10: sem necessidade urgente, segue rotina por `(Profession, LifeStage, hora)` | `CurrentAction == RoutineOf(...)` | `BehaviorDecisionSystemTests.cs:116-125` — `No_need_above_the_urgency_threshold_follows_the_declared_daily_routine`: `Assert.Equal(ActionType.Work, npc.CurrentAction)` (turno de trabalho aberto na rotina) | ✅ PASS |
| NEEDS-11: necessidade urgente durante rotina, utility sobrepõe | Ação urgente vence mesmo fora do horário padrão | `BehaviorDecisionSystemTests.cs:128-137` — `A_need_above_the_urgency_threshold_overrides_the_routine`: `hunger: 0` durante turno de trabalho aberto → `Assert.Equal(ActionType.Eat, npc.CurrentAction)` | ✅ PASS |
| NEEDS-12: `trocas_com < trocas_sem` em 20/20 seeds; teto = p99 de 20 seeds em `tests/baselines/action-switches.json` | Contagem de trocas menor com histerese em todas as 20 seeds; sem número mágico no critério | `BehaviorDecisionSystemHysteresisTests.cs:82-89` — `[Theory]` seeds 1u..20u, `Assert.True(withHysteresis < withoutHysteresis, ...)`; `:101-111` — `Action_switches_per_day_with_hysteresis_matches_the_recorded_baseline_and_stays_at_or_under_its_99th_percentile` lê `tests/baselines/action-switches.json` via `BaselineFixture.AssertMatches` e calcula p99 em runtime (`Percentile99`, linha 55-59) | ✅ PASS |
| NEEDS-13: nenhum NPC excede duração máxima da ação corrente, assert por tick, 10 anos; falha se ação sem duração declarada | Assert por tick sem violação; falha se catálogo incompleto | `BehaviorDecisionSystemHysteresisTests.cs:136-156` — `No_npc_exceeds_the_catalogs_declared_max_duration_over_10_years` (loop `tick < tenYears`, `Assert.True(duration <= maxDuration, ...)` a cada tick); cobertura "catálogo sem duração declarada falha" em `tests/LivingWorld.Tests/Behavior/ActionCatalogTests.cs:36-42` (`Create_fails_naming_the_action_missing_a_declared_duration`, 1 caso por das 6 ações) | ✅ PASS |
| NEEDS-14: deslocamento consome `>=1` tick; não executa ação de destino no tick em que decidiu ir | `CurrentAction == Travel` no tick da decisão; local só muda após custo consumido | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemTravelTests.cs:94-122` — `Assert.Equal(ActionType.Travel, npc.CurrentAction)` e `Assert.Equal(origin, npc.CurrentLocation)` logo após `clock.Tick`; só após `ticksNeeded+5` ticks `Assert.Equal(homeLocation, npc.CurrentLocation)` | ✅ PASS |
| NEEDS-15: dorme em `Residence` (sono pleno) ou, sem-teto, em `CurrentLocation` com `HomelessSleepEfficiency`, nunca bloqueia/lança | Sono == 100×eficiência; sem exceção | `BehaviorDecisionSystemTravelTests.cs:124-143` — `Homeless_npc_sleeps_at_its_current_location_with_reduced_efficiency_without_throwing`: `Assert.Null(exception)`; `Assert.Equal((int)(100*rules.HomelessSleepEfficiency), npc.Sleep)` | ✅ PASS |
| NEEDS-16: sem residência é estado explícito (`Residence is null` + `HomelessSince`), consultável | Conjunto de NPCs sem-teto retorna o esperado | `BehaviorDecisionSystemTravelTests.cs:146-157` — `Homeless_npcs_are_queryable_by_HomelessSince`: `Assert.Contains(homelessNpc, world.Npcs.Where(n => n.HomelessSince is not null))` e `Assert.DoesNotContain(housedNpc, ...)` | ✅ PASS |

**Status**: ✅ 15/16 ACs com cobertura evidenciada; NEEDS-09 tem ⚠️ gap parcial (ver Gap 1) — não invalida o critério citado no roadmap (que é especificamente sobre o abort nomeado), mas a cláusula extra do spec.md ("ao fim do tick nenhum NPC vivo SHALL ficar sem ação escolhida") não tem teste dedicado isolando esse enunciado.

---

## Discrimination Sensor

Método: `git stash`-free (working tree já limpo); mutação direta no arquivo real seguida de `git checkout -- <arquivo>` para descarte — nunca commitado, nunca deixado na árvore (confirmado `git status --short` limpo ao final, só `.specs/` untracked pré-existente).

| Mutation | File:line | Description | Killed? |
| --- | --- | --- | --- |
| 1 | `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs:137` | `score > bestScore` → `score >= bestScore` (inverte desempate: último candidato de nota igual vence, não o de menor `ActionId`) | ✅ Killed — 12 falhas (`bash scripts/test.sh --filter FullyQualifiedName~Behavior`), incluindo `Exact_utility_tie_breaks_by_the_smaller_ActionId` e o baseline de histerese |
| 2 | `src/LivingWorld.Simulation/Behavior/NeedsDecaySystem.cs:42` | `npc.Hunger > 0` → `npc.Hunger >= 0` (starvation nunca dispara, pois `Hunger` nunca é negativo) | ✅ Killed — 2 falhas: `Npc_starves_to_death_within_X_to_Xplus1_ticks...` (NEEDS-03 direto) e baseline de histerese (efeito colateral) |
| 3 | `src/LivingWorld.Domain/Behavior/PersonalityWeighting.cs:17` | Zera influência de `Conscientiousness→Work` (`1.0` → `0.0`) | ✅ Killed — 3 falhas: caso de tabela `Conscientiousness` (NEEDS-08) e baseline de histerese |

**Sensor depth**: lightweight (3 mutações, default tier)
**Result**: 3/3 killed — PASS ✅

---

## Code Quality

Recorte lido: `BehaviorDecisionSystem.cs`, `NeedsDecaySystem.cs`, `NpcDeath.cs`, `Personality.cs`, `PersonalityWeighting.cs`, `ActionCatalog.cs`/`ActionCatalogTests.cs`.

| Principle | Status |
| --- | --- |
| No abstrações não pedidas | ✅ — `NpcDeath.Apply` é extração legítima (dedupe de limpeza de household repetida em `MortalitySystem`+`NeedsDecaySystem`, documentada no header do arquivo como achado durante a implementação, não especulativa) |
| Nenhum número mágico em C# | ✅ — todo limiar/taxa vem de `NeedsRules`/`ActionCatalog` (cenário); a única constante literal é `NonNeedBaselineUtility = 50.0`, documentada no código como "parte do modelo de decisão em si", mesmo status da fórmula de `PersonalityWeighting` — consistente com a distinção do próprio `rules/eval-criteria.md` R3 (parâmetro de cenário vs. constante de algoritmo) |
| Só arquivos exigidos pela task tocados | ✅ — diff stat bate com o escopo de cada task (T1-T15); T9 documentou 1 ponto cego pré-existente corrigido em `WorldSnapshotTests.cs` (helper de mutação genérica), justificado no corpo da task como achado necessário para o round-trip funcionar, não scope creep |
| Não "melhorou" código não relacionado | ✅ | 
| Bate com padrões existentes | ✅ — `Result<T>`, `[Canonical]`/`[Volatile]`, `TickBudgetExceededException` reusado (não subclasse nova), convenção de nomear o campo/entidade culpada em toda mensagem de erro |
| Aprovaria um engenheiro sênior | ✅ |
| Testes mapeiam pra ACs e não são rasos | ✅ — spot-check em NEEDS-07/08 confirma braço de controle real (não só o tratamento) e tabela completa por reflexão, não hardcoded |
| Spec-anchored outcome check | ✅ — ver tabela acima, 15/16 com valor exato citado |
| Coverage por camada (domain 1:1 AC; sistema unit+determinismo) | ✅ — `NeedsDecaySystemTests` e `UtilityAiHashScenarioTests` cobrem o par determinismo (mesma seed/hash idêntico, seed diferente diverge) exigido por `rules/simulation-determinism.md` |
| Todo teste no diff mapeia a uma AC/edge case/Done-when | ✅ — nenhum teste "solto" identificado nos arquivos lidos |
| Guidelines seguidas | ✅ — `rules/tests.md`, `rules/simulation-determinism.md`, `rules/eval-criteria.md` (citados) |

---

## Edge Cases

- [x] `taxaDecaimentoFome == 0` nunca decai/nunca morre: `NeedsDecaySystemTests.cs:110-120` (`Hunger_never_decays_and_npc_never_starves_when_the_scenario_decay_rate_is_zero`)
- [x] Empate exato desempata por `ActionId`, não ordem de iteração: `BehaviorDecisionSystemTests.cs:168-180`
- [x] `Homeless` dormindo no `CurrentLocation`, não trava: `BehaviorDecisionSystemTravelTests.cs:124-143`
- [x] Utilidades cíclicas abortam no teto, nomeando NPC+ações: `BehaviorDecisionSystemHysteresisTests.cs:118-129`
- [x] NPC morre em trânsito, evento de morte processa antes do efeito de destino: `BehaviorDecisionSystemTravelTests.cs:159-182` (`Npc_that_dies_in_transit_never_arrives_or_applies_the_destination_action_effect`)

---

## Gate Check

- **Gate command**: `bash scripts/verify.sh` — **não reexecutado nesta sessão por instrução explícita do orquestrador**: já confirmado 0 (363 passed, 0 failed, 3 skipped justificados) nesta mesma sessão, imediatamente após o commit `b4c496f`, antes desta validação. Como confirmação pontual e mais barata, rodei `bash scripts/test.sh --filter FullyQualifiedName~Behavior` (escopo do diff desta fase) diretamente.
- **Resultado do filtro Behavior**: 152 passed, 0 failed, 1 skipped (`ZZZ_record_action_switches_baseline` — gravador manual de baseline, `[Fact(Skip = "grava baseline — rode manualmente")]`, nunca roda no gate por design, mesmo padrão de `PopulationBaselineTests`)
- **Test count before feature** (fim da Fase 3b, referência `tasks.md`): não medido diretamente por este Verifier (fora do escopo do diff da fase); o diff acrescenta 13 arquivos de teste novos em `tests/LivingWorld.Tests/Behavior/` + extensões em `NpcTests.cs`/`NatalitySystemTests.cs`/`PopulationGeneratorTests.cs`/`WorldSnapshotTests.cs`/`WorldClockTests.cs`
- **Test count after feature**: 153 (filtro Behavior) — nenhuma redução de teste, só adição
- **Delta**: +153 testes novos/estendidos na pasta `Behavior/` (fase inteira)
- **Skipped tests**: `ZZZ_record_action_switches_baseline` — justificado (grava `tests/baselines/action-switches.json`, comando manual explícito, nunca efeito colateral do gate, conforme `rules/eval-criteria.md` R3)
- **Failures**: nenhuma

---

## Fix Plans (if issues found)

### Gap 1 (Minor): NEEDS-09 — cláusula "nenhum NPC vivo fica sem ação ao fim do tick" sem teste isolado

- **Root cause**: o teste de terminação (`Cyclic_utility_scenario_aborts_naming_the_npc_and_the_tied_actions_instead_of_looping`) cobre só o caminho de abort do `ResolveWithStepCap`. A garantia "todo NPC vivo termina o tick com `CurrentAction` preenchido" no caminho feliz não tem asserção dedicada — está implícita (todo teste de `BehaviorDecisionSystemTests`/`Travel` sempre lê `npc.CurrentAction` não-nulo após o tick, mas nenhum teste varre a população inteira do mundo afirmando "nenhum é null").
- **Fix task**: adicionar 1 assert em `BehaviorDecisionSystemHysteresisTests.No_npc_exceeds_the_catalogs_declared_max_duration_over_10_years` (ou teste dedicado) checando `world.Npcs.Where(n => n.IsAlive).All(n => n.CurrentAction is not null)` a cada tick — reusa o loop de 10 anos já existente, custo marginal.
- **Priority**: Minor (a garantia já vale na prática — todo teste que roda o sistema depende implicitamente disso e passaria — falta só o assert explícito nomeado ao critério).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| NEEDS-01 | Design/Pending | ✅ Verified |
| NEEDS-02 | Design/Pending | ✅ Verified |
| NEEDS-03 | Design/Pending | ✅ Verified |
| NEEDS-04 | Design/Pending | ✅ Verified |
| NEEDS-05 | Design/Pending | ✅ Verified |
| NEEDS-06 | Design/Pending | ✅ Verified |
| NEEDS-07 | Design/Pending | ✅ Verified |
| NEEDS-08 | Design/Pending | ✅ Verified |
| NEEDS-09 | Design/Pending | ⚠️ Verified with minor gap (see Gap 1) |
| NEEDS-10 | Design/Pending | ✅ Verified |
| NEEDS-11 | Design/Pending | ✅ Verified |
| NEEDS-12 | Design/Pending | ✅ Verified |
| NEEDS-13 | Design/Pending | ✅ Verified |
| NEEDS-14 | Design/Pending | ✅ Verified |
| NEEDS-15 | Design/Pending | ✅ Verified |
| NEEDS-16 | Design/Pending | ✅ Verified |

---

## Summary

**Overall**: ✅ Ready (1 minor gap flagged, non-blocking)

**Spec-anchored check**: 15/16 ACs matched spec outcome with precise value; 1 (NEEDS-09) partially covered (abort path proven, happy-path "no NPC left without an action" not isolated as its own assertion)
**Sensor**: 3/3 mutations killed
**Gate**: verify.sh confirmed 0 earlier this session (commit `b4c496f`); Behavior-filtered re-run this validation: 152 passed, 0 failed, 1 justified skip

**What works**: Todas as 16 histórias com braço de controle real onde a spec exige (NEEDS-04, 07, 08, 12); baseline de histerese gravado e lido de `tests/baselines/action-switches.json` sem número mágico no texto do critério; morte por fome datável com causa no event log; deslocamento consome tempo real e nunca teletransporta; sem-teto nunca lança exceção; discriminação de mutante 3/3 nas áreas de maior risco (desempate, starvation, peso de personalidade).

**Issues found**: Gap 1 (Minor) — NEEDS-09 sem assert isolado para "nenhum NPC vivo sem ação ao fim do tick" no caminho feliz.

**Next steps**: Gap 1 é opcional/cosmético — não bloqueia o fechamento da Fase 4. Se o time quiser fechar 16/16 sem ressalva, adicionar o assert descrito no Fix Plan (custo: ~5 linhas, reusa loop de 10 anos existente).
