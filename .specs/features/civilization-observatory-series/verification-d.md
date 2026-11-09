# Verificação independente: loader, blocos de cenário e F8 (#112, fatia D)

## Rodada 2 (vigente): **PASS**

Alvo: `3940b612`, contém a `main` `a567c164`. Extinção do cenário de referência e fatia D incompleta seguem conhecidas e não contam.

**Escopo restrito, e por quê:** a rodada 1 verificou todos os itens em `ec709800` e reprovou só pelas cinco guardas sem
teste. Depois disso só mudaram testes e docs, mais o merge da `main`. Esta rodada confere esse delta, a correção e a
integração; não repete F8, fiação, Heavy nem o custo do caminho JSON, que ficam valendo da rodada 1.

| # | Item | Resultado |
| --- | --- | --- |
| 1 | Delta | PASS: `git diff 38d64fd8 1e8e8ac1 -- src/` vazio; só `WorldRulesWiringTests.cs`, `plan-d.md` e ADR-0043 |
| 2 | Cinco guardas | PASS: 7 de 7 mutantes reprovados |
| 3 | ADR-0043 | PASS: `PersonalityThreshold` "sem limite validado", como o loader (linha 77 lê, linhas 84-86 não limitam) |
| 4 | Integração | PASS |
| 5 | Gate | PASS: `verify.sh --full` sai 0 na primeira execução; `check-docs` OK |

**Item 2.** Cada mutante aplicado sozinho em `WorldRulesScenarioLoader`, com `WorldRulesWiringTests` (íntegro: 22 de 22).
Em todos, exatamente 1 caso de `Invalid_blocks_fail_pointing_at_the_field` reprova (21 passam), com `Assert.False`
(o loader passa a aceitar a entrada) e o `expected` do campo removido. Loader revertido ao fim, `git status` limpo:

| Removido | `expected` do caso que reprova |
| --- | --- |
| `threshold < 1` | `OutbreakPopulationThreshold` |
| `contagion > 1` / `contagion < 0` | `ContagionPerContactDay` (1,5 / -0,1) |
| `lethality > 1` / `lethality < 0` | `Lethality` (1,5 / -0,1) |
| `immunity < 0` | `ImmunityDays` |
| `min < 0` | `MinMilli` |

**Item 4.** Em `git diff main...HEAD`, o único arquivo em golden, baseline ou contrato é `civilization-diversity.json`:
muda o bloco `Gate` (autorizado, AD-125) e entra o bloco `Reference`, novo, que só `ReferenceCivilizationTests` lê e não
altera pin. Fora dele, o único assert com valor mudado é `Lumberjack_is_paid` (autorizado). Testes após o merge:
`Absent_blocks_leave_the_rules_disabled` e `Disease_off_block_matches_no_block_hash` passam; `CivilizationDiversityBaselineTests`
3 de 3; `GoldenHashesTests` 4 passam e 1 pulado (o gravador de hashes, `Skip` próprio).

**Números do `--full`.** Unit 3776 passam, 3 pulados; Scenario 23, 1 pulado; Integration 451; LongRunning 149, 8 pulados. Nenhuma repetição.

**Não verificado nesta rodada:** mutantes de fronteira nas guardas (por exemplo `immunity <= 0`), fora do pedido; `verify.sh --nightly`;
e os itens abertos da rodada 1, abaixo. Nenhum mutante novo fora das cinco guardas foi rodado, logo não há observação desse tipo.

## Rodada 1: FAIL (histórico)

**Veredito: FAIL**, por um único motivo: lacuna de teste nos guardas do loader (item 4).
Alvo: `ec709800` (`worktree-agent-a3f0908d9332b03cc`), contém a `main` `b49a35d6`. Extinção e fatia D incompleta não contam.

## Motivo do FAIL

Cinco validações de `WorldRulesScenarioLoader`, todas previstas no ADR-0043, podem sair sem nenhum teste reprovar:

| Guarda removido | Entrada recusada no código íntegro |
| --- | --- |
| `threshold < 1` | `Disease.OutbreakPopulationThreshold: 0` |
| `lethality is < 0 or > 1` | `Lethality: 1.5` |
| `contagion is < 0 or > 1` | `ContagionPerContactDay: 1.5` |
| `immunity < 0` | `ImmunityDays: -1` |
| `min < 0` | `Climate.MinMilli: -1` |

Cada remoção, isolada, deixou verdes 205 testes (filtro `WorldRulesWiringTests|Period|Scenario`). Com as cinco juntas, um
teste temporário confirmou que as cinco entradas passam a carregar com sucesso, enquanto os 15 de `WorldRulesWiringTests`
seguem verdes. Não são mutantes equivalentes. O comportamento atual está certo; falta teste. Correção: cinco `InlineData` em
`Invalid_blocks_fail_pointing_at_the_field`.

## Resultado por item (só o item 4 reprovou)

1. **F8.** Sem o `OrderBy` do `WriteSummary`, só `Summary_does_not_depend_on_the_input_order_of_the_runs` reprova. CLI
   `default.json` 20×10 com e sem o `OrderBy`: bytes idênticos (sha256 `1ace5a39…`). `TrajectoryDiversity` já ordena por seed.
2. **Loader.** Remover família, fauna, flora ou temperatura reprova o teste regra a regra (família reprova dois). Remover
   perf é equivalente: `DefaultPerfRules` é `PerfRules.Default`, o mesmo fallback do `WorldState`.
   Medição em `test-civilization`, 3 seeds, 10 anos, vivos no ano 10:
   - íntegro = só família: 90, 97, 79.
   - sem as cinco = sem família: 71, 78, 62.

   Só a família move a população, como o plano afirma. **Ressalva:** os números da tabela (67, 51, 20 e 37, 36, 36) são do
   cenário de `e70ed9b6`; reproduzi 67, 51, 20 exatos nele. O cenário final dá outros valores. Afirmação causal correta, tabela defasada.
3. **Pins.** Em `tests/golden*`, `baselines` e `contracts`, só `civilization-diversity.json`: Gate autorizado e bloco
   `Reference` novo (não altera pin). Reproduzi o Gate pela CLI: índice `0.2544838349350684`, igual ao gravado. Asserts
   com literal: só `Lumberjack_is_paid`, autorizado. `GoldenHashesTests` e determinismo passam no `--full`.
   `WorldSnapshot.FromNode` lê `FamilyRules` do nó (linha 123); `Serialize` grava por reflexão.
4. **Blocos.** Hash de `default.json` (3 seeds, 1 ano) idêntico entre as fontes da `main` e a branch sem os cinco padrões:
   bloco ausente não muda nada. Com o loader completo o hash muda, como o AD-125 prevê. Presente liga (seed 1, 25 anos):
   16 surtos, 4 mortes por doença, 22 furtos descobertos, clima de 502 a 1398; sem os blocos, tudo zero. Reprovam: fiação
   da doença, ausente ligado, `Enabled` ignorado (clima, furto), faixa de furto, `DesperateBelow`, troca de campos,
   lista vazia, duplicado, `DurationDays`, `MaxMilli < MinMilli`. Sobrevivem os cinco acima.
5. **Heavy.** Cinco mutantes nas verificações de extinção, longevidade e moeda reprovam os pares. Teste principal com
   `Skip` com motivo e referência ao `plan-d.md`. Passo Heavy do nightly: 7 passam, 1 pulado, 0 falhas.
6. **`--full`.** Saída 0. Unit 3760 passam, 3 pulados; Scenario 23, 1 pulado; Integration 451; LongRunning 149, 8 pulados.
7. **Custo.** CPU 1,0164 (teto 1,03), alocação 1,0002 (teto 1,02), mediana de 3 rodadas. O script mede `scale1000`, pelo
   caminho `Create` (`ScaleScenarioFixture`), **não o JSON**; logo não enxerga a mudança do loader. Indicativo do JSON
   (`live`, 10 anos, 1 rodada): 927, 1608, 1483 ms contra 1030, 1714, 1445 ms, com populações diferentes.
8. **Docs.** `check-docs` OK. AD-125 coerente com o código. **Incoerência:** o ADR-0043 lista `PersonalityThreshold`
   entre os campos "em [0,1]", mas o loader não o limita, e nem poderia: o padrão é 1.1 e o peso é centrado em 1,0.
   O código está certo; corrigir o texto.

## Não verificado

- 20 seeds × 300 anos: o teste principal está com `Skip`.
- Carga real de um snapshot gravado antes desta branch: conferi por leitura de código.
- Custo do caminho JSON com população igual.
- `Lumberjack_is_paid` calcula o esperado com `ApplyUpbringingWeight`; não mutei `WagePaymentSystem`.
