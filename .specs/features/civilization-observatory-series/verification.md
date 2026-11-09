# Verificação independente, rodada 5: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F1 a F3, todos de poder de teste). Código verificado: `b84dca28`, base `b29ed9f2`.
Verifier sem autoria; análise concluída antes de abrir o `verification.md` anterior. A recontagem independente não
achou nenhum valor errado: o código publicado está certo, mas quebras reais em métricas publicadas passam em todos os
testes. Medição de custo e critério de +5% adiados por decisão do orquestrador (não contam aqui).

## Falhas

| Id | Onde | Quebra que nenhum teste detecta | O que precisa mudar |
| --- | --- | --- | --- |
| F1 | `CivilizationObserver.cs:40` e `:41` | `sum.Deaths +=` → `=` e `sum.ByCause[...] + n` → `= n`: na fusão, a cidade que absorve perde as mortes de uma das origens. Muda `deaths`, `deaths_hunger_thirst` e `net_migration` do relatório: `default.json` seed 19 ano 1 publica 9 em vez de 10 mortes; `test-scifi` seed 1 ano 1, 81 em vez de 100 | Em `CivilizationScopeTests.cs:144`, dar mortes (com causas) à cidade que absorve **e** à absorvida no mesmo ano e exigir a soma, por cidade e no mundo |
| F2 | `CivilizationObserver.cs:45` | `sum.DeathsByBand[i] +=` → `=`: `life_expectancy_at_birth` errada quando as duas origens têm morte na mesma faixa. `test-living-village` seed 1 ano 1: 22,5 em vez de 2,5 (9 valores em 20 seeds × 10 anos; `test-scifi`, 14; `default`, 0) | No mesmo teste, mortes da mesma faixa nas duas origens e e₀ esperado à mão |
| F3 | `CivilizationObserver.cs:168` | Tirar `DeathsByBand.Count` do `Math.Max`: morte em faixa acima de todo vivo (cidade extinta, morte do mais velho) some da tábua. `default.json` seed 2 ano 8, cidade 2: 10,5 vira 0 (18 valores); `test-scifi`, 73 | Teste com morte em faixa mais velha que todo vivo e com cidade sem vivos, e₀ esperado à mão (`CivilizationObserverTests.cs:28` e `CivilizationScopeTests.cs:210` não geram o caso) |

## Fatia A: série anual

| Critério | Resultado | Evidência própria |
| --- | --- | --- |
| Observatório não altera `CanonicalHash` | PASS | Harness: 20 seeds × 10 anos com e sem observatório, 0 diferenças, 20 hashes distintos. Teste no gate. Mutante que escreve no mundo reprovado |
| Soma das cidades = total = vivos + pool | PASS | 200 anos: soma confere; `population_unassigned` = 0; recálculo pelo estado sem divergência |
| Conservação com fonte independente | PASS | `CivilizationConservationTests`: braço com nascimentos e `net_migration` = 0; braço com emigração real, = −(ids que saíram do pool). `pool.json` próprio (pool 60, taxa 5): −240 no total, sem valor positivo |
| Sink = estado | PASS | Nascimentos 99 e mortes 206 (20 seeds × 10 anos) iguais a `BirthDate`/`DeathDate`, incluindo arquivo frio |
| Causas somam mortes | PASS | Fome/sede 156, idade 48, outras 2, doença 0; cada causa confere com contagem própria do sink e com `world.Facts` (149 + 7, 48, 2) |
| Tábua de vida | PASS | 50, 280/11 e 15 nos testes; reimplementação própria da fórmula bate em todo ano e cidade |
| Cada métrica publicada | PASS | 4.600 valores de mundo e 7.000 de cidade (`default`, 20 × 10) sem divergência; Gini pela soma pareada; `cities_founded`/`cities_merged` pela variação de `world.Cities` e de tombstones (93 e 63). Sem divergência em `test-living-village`, `test-scifi`, `pool.json`, `periods/medieval` e `periods/prehistoric` |
| Poder de teste das métricas | **FAIL** | F1, F2 e F3 |
| Fórmulas e aproximações documentadas | PASS | Plano e `docs/domain/civilization-observatory.md` (61 linhas) |

## Fatia B: CLI e relatório

| Critério | Resultado | Evidência própria |
| --- | --- | --- |
| Dois processos, JSON byte-idêntico | PASS | `civ-report.sh` duas vezes, 20 × 10: `cmp` ok, SHA-256 `54b07219…`. Teste LongRunning `Civ_report_…` passa, com controle de seed diferente |
| Paralelo = sequencial | PASS | 20 × 10 com `Parallel.For` e com grau 1: bytes iguais |
| 20 seeds × 10 anos sem exceção | PASS | Script saiu 0 em ~15 s |
| JSON válido contra o contrato | PASS | Testes de contrato passam. O teste só olha o 1º item de cada lista; varredura própria dos 200 anos e 500 cidades: 0 desvios |
| Argumentos inválidos | PASS | `seeds` 0 lança `ArgumentOutOfRangeException` no CLI real; cenário inexistente sai 2 |
| Cultura de formatação | PASS | pt-BR, ar-SA e en-US: bytes iguais (escritor invariante) |
| Estado estático e ordem de `Dictionary` | PASS | Só contadores, caches por instância e `Building.LayoutVersion` (só invalida cache, monotônico). Iterações de `Dictionary` só somam inteiros; a saída ordena por chave ordinal e cidade por id |

## Fatia C: diversidade

| Critério | Resultado | Evidência própria |
| --- | --- | --- |
| Congelado = 0, embaralhado > 0 | PASS | Dados reais: congelado 0; embaralhado 0,0413; real 0,2688 |
| Valores à mão, escala, ordem das seeds | PASS | `TrajectoryDiversityTests`; mutantes de cada termo reprovados |
| Baseline de 20 seeds | PASS | Recalculado em Python a partir do JSON: bate com relatório e baseline em 1e-12. Baseline do cenário de referência depende da fatia D (fora) |

## Regras comuns

| Item | Resultado | Evidência |
| --- | --- | --- |
| Goldens/hashes existentes | PASS | `git diff --name-status`: só 4 modificados (`.gitignore`, `scripts/README.md`, `Program.cs`, `DeterminismTwoProcessTests.cs`); baseline e contrato são novos |
| Escopo | PASS | Nenhum arquivo de `scenarios/`, `client/` ou `CityIndicators`; D e E intocadas |
| R3 (limiar literal) | PASS | Limiar vem do baseline; literais restantes são resultado feito à mão ou controle "> 0" |
| Gate | PASS | `verify.sh` saiu 0 antes e depois das mutações (2512 aprovados, 3 ignorados) |
| Scenario e LongRunning novos | PASS | `CivilizationDiversityBaselineTests` 3/3; `Civ_report_…` 1/1 |

## Mutações

136 aplicadas uma a uma por script (observatório 68, sink 14, tábua 6, Gini 5, relatório 18, diversidade 13, runner 3,
comando 7 e `Program.cs` 2), restauradas com `git checkout --` e `git status` limpo ao final. 127 reprovadas (125 no
gate, 2 no LongRunning, sempre com teste nomeado). Cada sobrevivente rodou no CLI, 20 × 10, nos três cenários do
repositório, e o JSON foi comparado com o original:

| Mutante | Valores publicados que mudam | Classificação |
| --- | --- | --- |
| Fusão `sum.Deaths =`; `sum.ByCause = n` | `default` 3 e 2; `test-scifi` 44 e 31 | **FAIL** (F1) |
| Fusão `sum.DeathsByBand =` | `test-living-village` 9; `test-scifi` 14 | **FAIL** (F2) |
| `count` sem `DeathsByBand.Count` | `default` 18; `test-scifi` 73 | **FAIL** (F3) |
| `FoundedAtTick < tick` | 0 | Não bloqueante: 0 de 305 fundações em ano exato (+ 647 em 100 seeds × 25 anos); `OrganizationTicks` = 1 nos três cenários, a fundação cai no tick seguinte ao mês |
| Fusão em cadeia `while` → `if` | 0 | Não bloqueante: nenhum evento em cidade de cadeia em 160 seeds. Não provado inalcançável; convém cobrir junto com F1 |
| Fusão `sum.Births =` | 0 | Não bloqueante: nunca duas origens com nascimento em 160 seeds. Mesma linha de F1; convém cobrir junto |
| Chaves em ordem decrescente | 0 valores | Não bloqueante: o contrato ordena as chaves; só muda a ordem, estável entre processos |
| `Take(years)` removido na diversidade | 0 | Não bloqueante: inalcançável pelo CLI (toda seed tem os mesmos anos) |

## Observações não bloqueantes

- `summary.final.life_expectancy_at_birth` entra na média com 0 quando o ano não teve mortes; o 0 está documentado como indefinido.
- `workshops` conta todo `Workplace` (mercado e celeiro inclusive), como o código chama "oficina".
- Causa `age` inclui combate (documentado); doença não ocorre nos cenários do repositório, coberta por eventos injetados.
- `JsonWriterOptions` usa o fim de linha do sistema: a identidade de bytes vale no mesmo SO.
- O número baixo de habitantes no `default` (média 14,65 no ano 10) é do mundo, não do observatório.
