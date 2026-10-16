# Verificação — object-resources (#47 fatia 4)

Verifier independente (contexto em branco). Branch `feat/objetos-comida-fatia4` (68ca13c) contra `origin/develop` (6fb72ed).
**Veredito: PASS.** Nenhum critério declarado ficou sem proteção; sobreviventes são menores.

## Gates rodados por mim
| Comando | Resultado |
|---|---|
| `bash scripts/verify.sh` | OK (unit 2452 passed, 3 skipped, 0 failed) |
| `bash scripts/test.sh --project integration` | 448 passed |
| `bash scripts/client-test.sh` | 287 passed, 0 failed (avisos de vazamento do Godot no encerramento são ruído) |
| `bash scripts/check-docs.sh` | OK |
| `bash scripts/test.sh --project longrunning --filter GoldenHashes` | 4 passed, 1 skipped (o recorder) |

## Critérios
| Critério | Veredito | Evidência |
|---|---|---|
| Paridade: descritores equivalentes = sem catálogo | PASS | `ObjectResourceTests.Equivalent_descriptors_leave_the_npc_world_exactly_as_without_a_catalog` (digest de NPCs com fome/sede/ação/local + `ResourceConsumed`, 8 dias, 2 seeds; `hunger:100` explícito e `Catalog()` padrão). Mutantes que mudam o padrão (`return 90`, `hunger:90`) reprovam este teste. |
| Paridade: hash/JSON de mundo sem Objects | PASS | Prova minha (sonda temporária, 5 seeds x 30 dias x 40 NPCs): `CanonicalHash` e SHA do `WorldSnapshot.Serialize` idênticos entre base e HEAD, sem catálogo. |
| Paridade: mundo com catálogo da fatia 3 | PASS | Mesma sonda, catálogo padrão sem os objetos `category=resource` (nove peças, sem resourceId/effects de recurso): hash e JSON idênticos à base nas 5 seeds (`probe-base.txt` == `probe-head.txt`). |
| Tratamento: `hunger:50` consome mais | PASS | `A_food_that_restores_less_makes_the_npcs_eat_more_in_at_least_18_of_20_seeds` (verde). `Changing_the_descriptors_level_changes_the_world`, `The_same_seed_and_descriptors_give_the_same_world_twice`. Mutantes de `Min`, nível fixo 100 e alvo trocado reprovam. |
| Conservação de dinheiro/estoque | PASS | `Smaller_meals_keep_the_money_and_stock_accounting_closed_every_day` (90 dias, `hunger/thirst:70`), `A_smaller_meal_still_consumes_one_unit_of_each_resource`. |
| Validação na borda | PASS | Loader recusa resourceId <=0/texto, duplicado, nível fora de 0–100 (limites inclusivos testados), alvo cruzado nos dois sentidos, alvo repetido; cada uma tem mutante morto (tabela abaixo). |
| Desempenho | PASS | Ver seção própria. |

## Questionamentos de desenho
- (a) `max(HungerAt, ConsumeLevel)` sem descritor: `HungerAt` fica em 0–100 (`LazyNeed.ValueAt` faz clamp, `NeedAsInt` arredonda) e `max(x,100)=100`, igual a `SetHunger(100)`; `WithValue` faz clamp. Idêntico. Efeito colateral aceito: com nível <100 o valor lido é o inteiro arredondado, não o fracionário (49,6 vira 50): irrelevante na regra de hoje.
- (b) Único caminho de consumo que restaura fome/sede é `TryApplyEat` (`grep SetHunger/SetThirst`: o resto é mecânica extraordinária, não consumo de comida). Cobertos pela refatoração: refeição preparada (via `FoodResolver.ResolveMeal`, consulta pelo id do recurso escolhido), ração do empregador (`TryRation` na mesma condição), Buy→Eat (compra deposita, come na mesma função). **Ficou de fora**: ramo `EconomyRules.Enabled == false` (fixa 100/100 sem recurso, sem descritor) e nenhum teste usa descritor em id de refeição preparada (só ids 1 e 2). Aceitável: sem recurso consumido não há descritor aplicável, e o caminho do id é o mesmo; registrado como lacuna menor.
- (c) Determinismo: diff sem `Random`, `DateTime`, `Guid` nem libm; `Math.Round(double, AwayFromZero)` é aritmética IEEE exata, `double.TryParse` usa `InvariantCulture`. Seguro.
- (d) `DefaultObjects` ganha 2 objetos: entram no catálogo do período padrão (`DefaultPeriodSeeder`) e no `Catalog` do snapshot de interior (9 vira 11, teste ajustado). O cliente só instancia por id de mobília, então ignora os recursos (client-test verde). Sem efeito em presets ou NPCs (digest igual, nível 100).

## Mutações válidas (compilam; suíte `Objects` + `DefaultPeriodSeeder`)
Todas revertidas (`git status` limpo).
| Alvo | Mutação | Resultado |
|---|---|---|
| ConsumeLevel | sem catálogo/descritor -> 90 | morta (3 testes, inclui paridade) |
| ConsumeLevel | alvo ausente -> 90 | morta |
| ConsumeLevel | arredondamento ToEven / truncar | mortas (`The_level_rounds_halves...`) |
| ConsumeLevel | `Math.Ceiling` | **sobreviveu** (menor, abaixo) |
| TryApplyEat | sem max (fome, sede) / Min (fome, sede) | mortas |
| TryApplyEat | alvos trocados fome<->sede (2) / consulta água p/ fome | mortas |
| TryApplyEat | nível fixo 100 (fome, sede) | mortas |
| Loader | resourceId `<=0` -> `<0`; sem unicidade | mortas |
| Loader | nível: `<0`->`<-1`, `0` recusado, `>100`->`>101`, `100` recusado | mortas |
| Loader | alvo cruzado sem restrição (2 variantes); `isResource` sempre falso | mortas |
| Loader | alvo repetido aceito | morta |
| Catálogo | `ForResource` nulo; índice por posição | mortas |
| Catálogo | `TryAdd` -> última vence | **sobreviveu** (equivalente, abaixo) |
| Serialização | `resourceId` sempre serializado | morta |
| DefaultObjects | ids 1->3, 2->4; `hunger:90`, `thirst:90`; sem comida; sem água; categoria | mortas |
Não compilou (descartada, coberta por troca de alvo): ConsumeLevel `if (true)`.

## Sobreviventes menores (sem FAIL)
- `Math.Ceiling` no lugar de `AwayFromZero`: só difere em frações não-meio (62,3 -> 63 em vez de 62); o teste usa 62,5. Sugestão: caso 62,3 ou 62,7.
- `TryAdd` vs última-vence em `ByResource`: inalcançável, o loader garante unicidade.
- Mutante "texto aceito" que montei era equivalente (cai no `<=0`); descartado.

## Desempenho (`MeasureHarness`, 1000 NPCs, 3 rodadas alternadas base/HEAD, catálogo desligado)
| | rodada 1 | rodada 2 | rodada 3 | melhor |
|---|---|---|---|---|
| base µs/NPC-tick | 0,813 | 0,812 | 0,812 | 0,812 |
| HEAD µs/NPC-tick | 0,816 | 0,817 | 0,808 | 0,808 |
Mediana 0,812 -> 0,816 (100,5%); melhor 99,5%. Teto 103%: OK. Alocação/tick: base 258 698–258 806, HEAD 258 781–258 784 (100,0%; teto 102%): OK.
Ruído ~0,6% entre rodadas. Vilas (5 seeds) e mundo vivo (3 seeds) idênticos em população, emprego, mentoria e eventos entre base e HEAD nas 3 rodadas.
Catálogo ligado (informativo): base 0,839 / 240 658 B; HEAD 0,822 / 240 658 B (ruído a favor).
