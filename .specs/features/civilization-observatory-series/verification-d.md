# Verificação independente: loader, blocos de cenário e F8 (#112, fatia D)

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

## Resultado por item

| # | Item | Resultado |
| --- | --- | --- |
| 1 | F8 | PASS |
| 2 | Fiação do loader | PASS, com ressalva na tabela do `plan-d.md` |
| 3 | Pins | PASS |
| 4 | Blocos | **FAIL** (teste); comportamento PASS |
| 5 | Heavy | PASS |
| 6 | `verify.sh --full` | PASS |
| 7 | Custo | PASS |
| 8 | Docs | PASS em linhas; uma incoerência no ADR-0043 |

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
