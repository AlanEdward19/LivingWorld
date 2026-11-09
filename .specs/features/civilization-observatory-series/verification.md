# Verificação independente, rodada 9: observatório de civilização (#112, fatias A, B e C)

**Veredito: PASS** no escopo restrito abaixo. Código verificado: `c3f62709`, base `main` = `38972071`. Verifier sem autoria.

**Escopo restrito por decisão do dono do projeto:** só o fechamento de F7, o delta desde a rodada 8 e o `--full`. Sem nova caça a mutantes. O relatório completo da rodada 8 (critérios por fatia, 93 mutações, determinismo e pureza) está em `d3756771`, e o desta rodada não o refaz.

## Situação dos achados

| Id | Situação | Base |
| --- | --- | --- |
| F7 | **Fechado** | Itens 1 a 3 |
| F8 | **Observação**, não reprova esta rodada, por decisão do dono | Ordem da soma em `summary.final.*.mean`, magnitude de 1 ulp. Não reavaliado; o código não mudou (item 4) |
| F5, F6 | Fechados na rodada 8 | Testes seguem no filtro `Observation`, que passa |

## Item 1: mutante de F7

Em `CivilizationObserver.cs:103`, `(double)city.AggregatePool.WealthSum / city.AggregatePool.Count` virou `(double)(city.AggregatePool.WealthSum / city.AggregatePool.Count)`. Comando: `bash scripts/test.sh --filter FullyQualifiedName~Observation`.

| Código | Saída | Resultado |
| --- | --- | --- |
| Real (`c3f62709`) | Passed! | 0 reprovados, 97 aprovados |
| Mutante | sai 1 | 1 reprovado, 96 aprovados |

O único reprovado é `Gini_uses_the_fractional_mean_wealth_of_the_pool`, na linha 353 do teste: `Expected: 0,083333333…`, `Actual: 0,085570469798657678`. O valor obtido é exatamente o da divisão inteira refeita à mão (abaixo): o motivo é o certo. Mutação revertida com `git checkout`.

## Item 2: valor refeito à mão, a partir de `WealthInequality.cs`

Blocos: pool com `Count` 3 e `WealthSum` 100, logo valor 100/3 e peso 3; NPC com valor 50 e peso 1. Ordem por valor: pool, depois NPC. Pessoas 4, riqueza 100 + 50 = 150.

| Bloco | Δ população | Lorenz (início → fim) | Área somada |
| --- | --- | --- | --- |
| Pool | 3/4 | 0 → 100/150 = 2/3 | 3/4 · (0 + 2/3) = 1/2 |
| NPC | 1/4 | 2/3 → 1 | 1/4 · (2/3 + 1) = 5/12 |

Área 11/12; `gini` = 1 − 11/12 = **1/12**, igual ao esperado do teste. Com divisão inteira: valor 33, riqueza 99 + 50 = 149, área 3/4 · 99/149 + 1/4 · 248/149 = 136,25/149, `gini` = 12,75/149 = 0,0855704697…, igual ao `Actual` do mutante.

Pré-condições checadas no próprio teste: carteira do NPC = 50 (antes de `Sample`); `AggregatePool.Count` = 3 e `WealthSum % Count` ≠ 0 (depois de `Sample`). `WealthSum` = 100 não é reafirmado por valor: vem do construtor `AggregatePopulationPool(3, 100, 0)` (`AggregatePopulationPool.cs:6`) e não há tick entre a montagem e a amostra. O NPC ser o único da cidade decorre da montagem: cidade nova e um único `JoinCity`.

## Item 3: o teste não é vácuo

Passa no código real e reprova só com o mutante de `:103`, com a diferença que a parte fracionária explica. Logo os dados passam por esse caminho: `city.AggregatePool.Count > 0` é verdadeiro e o bloco do pool entra em `g.Wealth`. O teste lê `Metrics["gini"]` da cidade montada via `Single`, que lança exceção se ela não estiver entre as cidades ativas amostradas.

## Item 4: delta

| Checagem | Resultado |
| --- | --- |
| `git diff d3756771 c3f62709 --stat` | Só `tests/LivingWorld.Tests.Unit/Observation/CivilizationObserverValueTests.cs`, 21 inserções |
| `git diff 37233e15 c3f62709 -- src/` | Vazio (0 bytes): segue válida a medição de custo da rodada 7 (98,66% CPU, 99,99% alocação) |
| `git diff d3756771 c3f62709 -- tests/golden tests/baselines scenarios` | Vazio: nenhum golden regravado |
| `git status` depois da mutação e do `--full` | Limpo; árvore idêntica a `c3f62709` |

## Item 5: gate

`bash scripts/verify.sh --full`: saída 0, `verify: OK`, 17 min 23 s, carga de 6,4 a 3,9. Sem falha de custo; sem repetição.

| Suíte | Aprovados | Ignorados | Falhas |
| --- | --- | --- | --- |
| Unit (gate) | 2547 | 3 | 0 |
| Unit `Scenario` | 23 | 1 | 0 |
| Integration | 450 | 0 | 0 |
| LongRunning | 146 | 8 | 0 |

Unit (gate) tem 1 aprovado a mais que na rodada 8 (2546): o teste novo.

## Critério de gate +5%

**Sem conclusão.** Não remedido nesta rodada; o ruído da rodada 7 persiste e o critério não conta no veredito. O `src/` idêntico (item 4) só mantém válida a medição da rodada 7, que não fecha o critério.

## Não verificado nesta rodada

- Nova caça a mutantes e reclassificação dos sobreviventes da rodada 8. Nenhuma mutação além da de F7.
- F8 e os critérios por fatia, além do que o `--full` exercita.
- `--nightly` (`Category=Heavy`).
- Fatias D e E e o baseline do cenário de referência.
