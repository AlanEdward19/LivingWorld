# Verification — ontogeny-debts (#93) — Verifier independente

(escrito incrementalmente)

## Critério 1 — acolhimento (PASS)
- Baseline: `bash scripts/test.sh --scenarios --filter "FullyQualifiedName~Orphan_care"` -> Passed 1/1 (2 min); `--filter OntogenyDebtsTests` (rápidos) 11/11.
- Mutação A (`TryTakeIn` retorna false sempre, OrphanCareSystem.cs:~66): reprova `An_orphan_without_relatives...` e `A_relative_is_preferred...` (rápidos) e `Orphan_care_leaves_no_child...` (cenário). Restaurada.
- Mutação B (tier de parentes desligado): reprova `A_relative_is_preferred_over_a_more_generous_stranger...`. Mutação C (inversão real: comunidade antes de parentes): idem reprova. Restauradas.
- Crítica: o plano pedia "controle com mortes maiores em >=18/20 seeds"; o teste exige só `fewer>=3`, `fewer>worse`, `controlDeaths>treatedDeaths` (20 seeds, 6 anos). É mais fraco que o plano, porém robusto contra ruído (mundos divergem); a invariante forte (0 meses-criança seguidos sem adulto) é exata e é o que discrimina a mutação A. Controle `OrphanCare=false` também desliga o TryTakeIn na morte dos pais (correto) mas NÃO desliga BringDependents (5b), logo o controle não é o estado pré-#93 puro — aceitável, pois isola o sistema do órfão.

## Critérios 2, 3, 4, 5, 5b — mutações (todas PASS; cada uma restaurada com `git checkout --`)
- 2 (choro): `NonUrgentCandidate` sem `CaregiverHearsCry` (BehaviorDecisionSystem.Schedule.cs:~69) -> reprova `The_decision_wakes_the_caregiver_to_buy_when_the_child_cries_and_keeps_sleeping_otherwise`. Julgamento: o gancho só no ramo `routineAction==Sleep` é correto — fora do sono a despensa sem comida/água já aciona `NeedsRestocking` (mesma condição de ação Buy), então nenhum caso não-sleep é perdido; o teste também cobre "sem choro continua dormindo".
- 3 (exposição): `ExposureFor` devolvendo 1 com cuidador -> reprova `Exposure_is_graded...` e `With_partial_attention_two_caregivers_raise_more_than_one_in_every_seed`. `ExposureFor` (OntogenyRules.cs) usa produto em laço, sem Math.Pow (grep sem Pow/Random/DateTime/Guid nos arquivos novos e no diff de src). Padrão 1,0 coberto por teste (binária).
- 4 (adotiva): `AdoptUpbringing` virado no-op (OntogenyMechanic.cs) -> reprova `An_adopted_child_ends_closer...` (que também cobre pais como cuidadores: sem mudança).
- 5 (pool): guarda `Development is not null` removida de MaterializationSystem.Dematerialize -> reprova `A_child_in_development_cannot_be_dematerialized...`.
- 5b (casamento): ambas as chamadas `BringDependents` removidas -> reprova `A_parent_who_marries...` e `Children_stay_with_the_remaining_adult...` (Family/Marriage: 103 passam, 2 falham). MarriageSystemTests alterado (colega de casa vira adulto de 30 anos em vez de bebê id MakeNpc): válido — o bebê agora é legitimamente levado, o teste ainda prova que o domicílio compartilhado permanece com quem não casou; não mascara regressão.
- Nota: OrphanCareSystem checa só `OntogenyRules.OrphanCare` (padrão true também em `Disabled`), logo roda mesmo com ontogenia desligada (é por isso que goldens mudaram); intencional (sede não depende de ontogenia), mas registrar.

## Critério 6 — determinismo (PASS)
- `dotnet src/LivingWorld.Workers/bin/Release/net10.0/LivingWorld.Workers.dll hash 1 2160` em 2 processos: ambas as saídas `e9c6209f...ecf5;af8edeb6...1a0b` (idênticas, e iguais ao InlineData(1UL) de OptimizationEquivalenceTests).
- Hash muda ao desligar o acolhimento: o teste de cenário `hashesChanged > 0` passa (Orphan_care...). Sem Random/DateTime/Guid/Math.Pow nas linhas adicionadas em src (grep do diff vazio).

## Extras (PASS)
- `OrphanTakenIn` rotulado em LivingEventPresentationCatalog.cs:27 e WorldEventKind.cs:198 (classificação coberta pelo gate de integração do verify --full).
- `OrphanCare`/`AttentionPerCaregiver` validados (OntogenyRules.Create (0,1]; loader OntogenyScenarioLoader.cs:81-86 rejeita tipo inválido; teste Invalid_attention_is_rejected_by_name) e documentados em docs/domain/period-authoring-schema.md:89-90 (uma linha cada, sem duplicata).
- `bash scripts/check-docs.sh` -> `check-docs: OK`; `bash scripts/lint.sh` -> exit 0.

## Desempenho (PASS) — ABBA, 8 execuções (N,B,B,N,N,B,B,N), scale1000 (melhor de 3 por execução), máquina ruidosa
| arm | µs/NPC-tick | alloc/tick |
|---|---|---|
| N run1 | 0,766 | 255264 |
| B run2 | 0,744 | 250899 |
| B run3 | 0,743 | 250974 |
| N run4 | 0,768 | 255297 |
| N run5 | 0,775 | 255236 |
| B run6 | 0,805 (atípica) | 250974 |
| B run7 | 0,766 | 250851 |
| N run8 | 0,764 | 255278 |
- Nova (21d): média 0,768, mediana 0,767. Base (21a-base, sem ontogenia): média 0,765, mediana 0,755 (ruído da base: 0,743–0,805, ~8%). Razão média 100,5%, mediana 101,6%; sem a execução atípica da base (run6) média da base 0,751 -> 102,3%. Teto 103%: dentro. Alocação 255,26k vs 250,9k = 101,7% (<= 102%): dentro (margem fina de 0,3 pp, porém determinística: variação entre execuções < 0,1%).
- Controle extra: 21c (branch empilhada, sem os 5 itens da #93) mediu 0,779 µs / 255171 B na mesma máquina -> a 21d não adiciona custo mensurável sobre a 21c; a alocação 101,7% vs base vem de 21a–21c, não da #93. A regressão de tempo é ruído; a de alocação é real mas pequena e anterior a esta issue.

## Efeito no mundo (PASS)
- Mortes por desidratação, vila 5 seeds × 10 anos: 21c = 3+0+3+1+2 = 9; 21d = 1 (seed 3), idêntico nas 4 execuções N (determinístico). Confirma 9 -> 1.
- População do ano 10 (vila): 21d = 94+96+77+74+73 = 414; base = 93+108+77+70+72 = 420. Confirma 414 vs 420. (21c: 85+92+73+70+70 = 390.)

## Gate final (PASS)
`bash scripts/verify.sh --full` (log /tmp/v/vfull_verifier.log): linha `verify: OK`, wrapper exit 0.

## Veredito: PASS (sem FAILs). Ressalvas: (a) critério estatístico do teste de cenário é mais fraco que o do plano (>=3 seeds, não >=18/20), compensado pela invariante exata de 0 meses descobertos e pela mutação reprovar; (b) alocação 101,7% vs base (teto 102%) com folga de 0,3 pp, herdada da 21a–21c; (c) OrphanCareSystem roda mesmo com ontogenia desligada.
