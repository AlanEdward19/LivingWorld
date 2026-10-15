# Verificação 21c (issue #72) — Verifier independente

Branch `feat/ontogeny-language-regression` (HEAD 56cd9dd), worktree LivingWorld-21c. Evidência própria; gravado incrementalmente.

## Divergência do pool agregado (comentário da issue)
PASS. `AggregatePopulationPool(Count, WealthSum, HealthSum)` (src/LivingWorld.Domain/Cities/AggregatePopulationPool.cs:6) é só massa.
`MaterializationSystem.Materialize` sorteia `ageYears = 18 + ...` (MaterializationSystem.cs:143) e constrói o Npc sem `development`
(Development nulo); `Dematerialize` só aceita NPC com `MaterializedAtTick` não nulo (MaterializationSystem.cs:170), isto é, vindo do pool.
Criança nascida na simulação nunca entra no pool; logo `MilestoneProgressSum`/resample não têm consumidor. A divergência está correta.

## Mutações (todas restauradas com `git checkout -- <arquivo>`)
Comando base: `bash scripts/test.sh --filter "FullyQualifiedName~OntogenyLanguageRegression"` (10 testes; sem mutação: 10/10 verdes; `~Ontogeny`: 32/32).
- M7 `OntogenyRules.Advance`: `if (false && exposed && languageGain > 0 && caregiverCultures ...)` (não reparte fluência) -> 5 reprovações
  (An_orphan_raised..., Regression_scales..., The_language_is_kept..., Mutation_ignoring..., Several_caregivers_split...). PASS.
- M8a `MilestoneProgressSystem.Regress`: `if (false && lowered < state) LogEvent` (cai sem evento) -> reprova
  `Every_drop_in_a_milestone_has_a_named_disease_event...`. PASS.
- M8b `ActiveDisease(...) ?? 7` (regride sem checar infecção) -> reprova `Every_drop...` e `The_language_is_kept...`. PASS.
- Mfloor `OntogenyRules.Regress`: `.Max() * 0` (piso = 0, sem proteger marco consolidado) -> **10/10 VERDES: a mutação sobrevive**.
  `A_consolidated_milestone_is_a_floor_and_a_recent_one_is_lost` afirma `sick.GrossMotor >= walk` aos 3 anos, mas 85% do progresso já
  fica acima do limiar do `walk`, então o piso nunca é exercido. **FAIL (teste não discrimina o piso)**; produção parece correta.
- Medge1 `DevelopmentState.Full = 1.0` (sem tolerância) -> reprova `The_language_is_kept_after_development_completes...` (só esse). PASS (indireto).
- Medge2 `OntogenyMechanic.AdvanceOneMonth`: `now >= CompleteAtYears` em vez de `now - monthYears >= ...` -> reprova o mesmo teste. PASS (indireto):
  não há teste dedicado da borda (nenhum teste cita IsComplete/0,9965); a cobertura é o fim-de-ciclo com `Development` nulo e `NativeLanguage` gravado.

## Critério 7 (idioma): PASS
- Evidência: `OntogenyRules.Advance` reparte o ganho de linguagem entre `caregiverCultures` (OntogenyRules.cs ~180-188); `OntogenyMechanic.AdvanceOneMonth` grava
  `NativeLanguage` ao completar (OntogenyMechanic.cs:46); `CourtshipSystem.CulturalAffinityOf` lê `SpokenCulture` (CourtshipSystem.cs:316-318), usado em
  `AttractionFactor.CulturalAffinity` (CourtshipSystem.cs:310). Testes: ninguém nasce falando (20 seeds), órfão fluente no idioma do cuidador (20 seeds), mutação M7 reprova (acima).
- Consumidor causal: o cortejo pontua a afinidade pelo idioma falado; se cultura de nascimento != idioma, o par muda de pontuação.
- Cultura uniforme: sonda própria (seed 42, 40 pop, 25 anos): 1 cultura, `SpokenCulture != Culture` em 0 de 61 NPCs (12 com `NativeLanguage`, todos iguais à cultura),
  0 divergências de `CulturalAffinityOf` contra `a.Culture == b.Culture` (61x30 pares). Comportamento do cortejo não muda em mundo uniforme.
- Persistência: sonda própria `NativeLanguage=5` -> `WorldSnapshot.Serialize/Deserialize` preserva e `CanonicalHash` igual. (Sem teste permanente de round-trip de NativeLanguage;
  o teste de snapshot da 21b só cobre `Development` aos 3 anos. Observação, não FAIL.)

## Critério 8 (regressão): PASS com ressalva
- Teste `Every_drop_in_a_milestone_has_a_named_disease_event_in_the_same_month_and_none_happens_without_illness`: 20 seeds x 10 anos, queda mês a mês == evento no mesmo mês, sufixo `|7`, queda só em doença. Mutações M8a/M8b reprovam.
- Ressalva (FAIL parcial): o PISO (marco consolidado) não é discriminado, mutação Mfloor sobrevive (ver acima).

## Critério 9 adaptado (pool só adulto): PASS no código, teste fraco
- Sonda própria: 20 seeds x 10 `MaterializeOne` = 200 NPCs, idade mínima 18, `Development` nulo em todos.
- O teste `Materialized_people_are_adults_...` do PR roda `ScenarioRunner.Create(42, 40)` por 10 anos e filtra `MaterializedAtTick is not null`; na sonda (25 anos)
  houve 0 materializados, logo o teste é provavelmente vacuoso (passa sem exercer nada). Recomenda-se materializar via `MaterializeOne` como na sonda. Observação (não bloqueia o critério, pois o código é correto).

## Critério 10 (determinismo): PASS
- `grep -nE "Random|DateTime|Guid|Stopwatch" src/LivingWorld.Domain/Ontogeny/*.cs src/LivingWorld.Simulation/Ontogeny/*.cs` -> 0 ocorrências (rc=1).
- `dotnet src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll hash 1 2160` duas vezes (dois processos): ambas saem
  `756164a0...483c;9c561a04...52fa`, idênticas e iguais ao InlineData de `OptimizationEquivalenceTests` (seed 1).
- Golden: `tests/golden/world-hashes.json` regravado (4 entradas) e coberto por AD-117 (comportamento mudou de propósito).

## Eventos/catálogo/docs
- `WorldEventKind.MilestoneRegressed` (WorldEventKind.cs:195), rótulo em `LivingEventPresentationCatalog.cs:27`, capability `ONTOGENY` com `Events=[MilestoneRegressed]` e consumidor `timeline.development`
  (MilestoneProgressSystem.cs:12). `bash scripts/test.sh --project integration --filter "LivingTimelineTests|CapabilityCoverageTests"` -> 12/12 verdes. PASS.
- `bash scripts/check-docs.sh` -> `check-docs: OK`. PASS.
- `RegressionLoss` validado (`(0,1)` em `OntogenyRules.Create`, e `Invalid_regression_loss_is_rejected_by_name`) e documentado, MAS a linha de
  `Ontogeny.RegressionLoss` está **duplicada** em docs/domain/period-authoring-schema.md:88-89 (diff do PR adiciona duas linhas idênticas). **FAIL menor (doc).**

## Desempenho: PASS (margem curta)
`LW_MEASURE_OUT=/tmp/v/{base,new}N.txt bash scripts/test.sh --project longrunning --filter "FullyQualifiedName~MeasureHarness"`, 3 rodadas alternadas
(base = LivingWorld-21a-base, sem ontogenia; nova = 21c, que acumula 21a+21b+21c). scale1000 (980 vivos):
| rodada | base µs/NPC-tick | nova | razão | base alloc/tick | nova | razão |
|---|---|---|---|---|---|---|
| 1 | 0,747 | 0,761 | 101,9% | 250.842 | 255.258 | 101,8% |
| 2 | 0,746 | 0,766 | 102,7% | 250.900 | 255.184 | 101,7% |
| 3 | 0,743 | 0,761 | 102,4% | 250.976 | 255.181 | 101,7% |
Mediana µs 0,746 -> 0,761 (102,0%); pior 102,7% (teto 103%); alocação pior 101,8% (teto 102%). Ruído da base nesta máquina: 0,743-0,747 (0,5%),
logo o excesso (~2%) é real e não ruído. Snapshot 12.267 -> 12.396 B/vivo (+1,1%), igual ao AD-117. O AD-117 relata 101%/0,655-0,756 (outra carga de máquina);
o resultado aqui é um pouco pior (102%) mas dentro dos tetos. Folga pequena para a próxima fase.

## Gate completo: PASS
`bash scripts/verify.sh --full` (log /tmp/v/verify-full.log): termina com `verify: OK`; LongRunning 128 passou, 0 falhou, 8 skipped (os skips são os existentes de baseline/heavy).

## Fechamento da Fase 21 (critérios 1-10)
Testes correspondentes presentes: 1-3 `OntogenyModelTests` (reflexão/catálogo, nenhum NPC age acima do marco em 10 anos, mutação do filtro, recém-nascido só Cry/mamar/dormir, desligar muda hash);
4-6 `OntogenyAcquisitionTests` (cuidador ausente, ordem prazo>tardia>nenhuma + mutação, gene como taxa); 7-9 `OntogenyLanguageRegressionTests`; 10 `DeterminismTwoProcessTests` + golden + `OptimizationEquivalenceTests`.
ROADMAP linha 21 "fechada", STATE "Fase 21 entregue", docs/roadmap/phase-21-ontogeny.md "fechada", ADR-0030 (29 linhas) + índice, AD-117: coerentes. `check-docs: OK`.
Observação de processo: a issue exige parar e perguntar antes de mudar o formato do snapshot; `Npc.NativeLanguage`/`DevelopmentState.Fluency` mudam o snapshot (+1,1%).
O ADR-0030 registra a decisão, mas não há evidência no repositório de aprovação prévia do usuário (o autor deve confirmar).

## Veredito
APROVADO COM RESSALVAS (nenhum FAIL funcional; 2 testes fracos e 1 defeito de doc):
1. FAIL (teste): o piso por marco consolidado não é discriminado: mutação `.Max() * 0` em `OntogenyRules.Regress` sobrevive (10/10 verdes).
   Corrigir exercendo o piso (estado com progresso exatamente no limiar de um marco com janela fechada; esperar que não caia).
2. FAIL (doc): linha `Ontogeny.RegressionLoss` duplicada em docs/domain/period-authoring-schema.md:88-89.
3. Fraco: `Materialized_people_are_adults_...` não materializa ninguém (0 materializados em 25 anos na sonda); o invariante é verdadeiro (200/200 via `MaterializeOne`), mas o teste é vacuoso.
4. Observação: a borda de IsComplete/CompleteAt só é coberta indiretamente (reprova com as duas reversões, via teste de fim de ciclo); desempenho 102,0% mediana / 102,7% pior (teto 103%), alocação 101,8% (teto 102%), folga curta.
