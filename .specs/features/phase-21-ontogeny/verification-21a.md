# Verificação independente — Fase 21a (#70, `feat/ontogeny-model`)

Verifier de contexto em branco; evidência própria (comando + saída). Veredito: **PASS** (3 observações, nenhuma reprova).

| # | Critério | Veredito | Evidência |
|---|---|---|---|
| 1a | Toda ação declara marco exigido (reflexão) | PASS | `OntogenyRules.Create` (OntogenyRules.cs:~40) falha nomeando a ação sem requisito; teste `Create_fails_naming_each_action_that_lacks_a_declared_milestone` itera `Enum.GetValues<ActionType>()` (14/14 passam). `ActionCatalog.Create` não exige `Cry` (ActionCatalog.cs:28, como `Steal`). |
| 1b | Nenhum NPC age acima do marco em 10 anos | PASS | `No_npc_acts_above_its_development_across_ten_years` verde (40 NPCs, seed 42, 3.600 dias, checagem diária). |
| 1c | Mutação: desligar o filtro reprova | PASS | (i) `//if (!eligibility.Allows(action)) continue;` em BehaviorDecisionSystem.Utility.cs: 2 falhas (`A_newborn_never_selects...`, `A_hungry_newborn...`). (ii) Filtro + `ConstrainByDevelopment` juntos desligados: 3 falhas, incluindo `No_npc_acts_above..._ten_years`. Tudo restaurado (`git checkout`; `git status` limpo). |
| 2 | Recém-nascido: progresso 0, só Cry/Eat/Sleep/Idle; Cry pela fome | PASS | `Newborn_has_zero_progress_and_only_cry_eat_sleep_idle_are_eligible` itera todas as ações; `A_hungry_newborn_with_an_empty_pantry_cries...` (despensa vazia: Cry; com comida: Eat); `CryUtilityOf` = fome × (1 − Language), sem script/idade. `The_cry_fades_as_language_appears` verde. |
| 3 | Desligar muda o hash em 10 anos | PASS | `Turning_the_subsystem_off_changes_the_canonical_hash_in_ten_years` verde. |
| 10 | Determinismo em 2 processos; sem Random/DateTime/GUID | PASS | `grep -rnE "Random\|DateTime\|Guid" src/*/Ontogeny` = 0 ocorrências. `Workers hash 1 2160` rodado 2x: ambos `9293520a…704a;f8187800…ef1`, iguais ao pin de `OptimizationEquivalenceTests` (a base dá `96b783d2…`, diferente, esperado). LongRunning (128 verdes) inclui goldens. |
| P | Desempenho (MeasureHarness, 1.000 NPCs × 30 d, 2 rodadas alternadas base/nova) | PASS (borderline em µs) | µs/NPC-tick base 0,756 / 0,739; nova 0,756 / 0,765. Razão por rodada 100,0% e 103,5%; média 101,7%; melhor-de-2 102,3%. Alocação/tick base 250.914 / 250.829; nova 254.197 / 254.192 = 101,3% nas duas (teto 102%). Snapshot 12.267 → 12.320 B/vivo. Arquivos: /tmp/v/{base,new}{1,2}.txt. |
| S | Snapshot: `Development` omitido vs `null` | PASS | `Npc.Development` sem `JsonIgnore` (Npc.cs:~115): `null` sai explícito; `Born_children_start_as_newborns_and_the_state_survives_a_snapshot_round_trip` verde (hash canônico igual após ida e volta). Snapshot sem bloco `OntogenyRules` volta `Disabled` (WorldSnapshot.cs). |
| A | ADR-0028 existe e é coerente | PASS | 31 linhas, índice atualizado, AD-115 e plan.md batem (null = completo, ligada por padrão, Cry opcional). |
| C | Contrato público | PASS (aditivo) | Único efeito visível: nova ação `Chorando` em `ActionLabelCatalog` (ActionLabelCatalog.cs:22). `tests/contracts` não mudou; nada removido/renomeado. Mudança de formato do snapshot (Development/OntogenyRules) coberta por ADR-0028 e escolha do usuário. |
| D | Docs ≤ 100 linhas | PASS | `bash scripts/check-docs.sh` = `check-docs: OK`. |
| V | `bash scripts/verify.sh --full` | PASS | `verify: OK`, EXIT 0: Unit 2208 ok, 18 ok, Integration 384 ok, LongRunning 128 ok (0 falhas). |

## Observações (não reprovam)
1. **Cobertura fraca de `ConstrainByDevelopment`** (BehaviorDecisionSystem.cs, rotina/percepção/controle externo): desligar só essa chamada deixa os 14 testes verdes (`Passed: 14`). O invariante de 10 anos só cai se o filtro da utility também cair; falta um teste que force rotina/controlador a pedir ação inelegível. Recomenda-se teste de par de mutação para essa camada antes da 21b.
2. **µs/NPC-tick no limite**: uma das duas rodadas deu 103,5% (ruído de máquina; base variou 0,739–0,756). Média 101,7% e AD-115 (5 rodadas, 101,6%) dentro do teto; vale reconfirmar no CI.
3. **Efeito de realismo medido**: população da vila no ano 10 cai (ex. seed 1: 93 → 87; seed 3: 77 → 73) e aparece `Dehydration` (1–3 eventos/seed, antes 0), que não existia na base. AD-115 declara a queda (−6%) como deliberada; a desidratação residual (provavelmente criança sem acesso a água) não está citada e merece olhar na 21b.
