# Verificação independente: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F1 a F8, todos de poder de teste). Código verificado: `cee76be4`, base `b29ed9f2`. Verifier sem
autoria. Nenhuma métrica saiu errada na recontagem independente; o que falta é teste que reprove quando ela errar.

## Gates

| Comando | Resultado |
| --- | --- |
| `bash scripts/verify.sh` | exit 0; Unit 2491 ok, 3 skip, 0 falhas |
| `bash scripts/test.sh --scenarios --filter ...CivilizationDiversityBaselineTests` | 3/3 ok |
| `bash scripts/test.sh --project longrunning --filter ...Civ_report` | 1/1 ok |

`compare-simulation-cost.sh` e o critério de +5% não avaliados, por instrução (adiados para o fim da integração).

## Critérios

| Critério | Status | Evidência própria |
| --- | --- | --- |
| A1 observatório não muda o hash | PASS | 20 seeds × 10 anos: `CanonicalHash` igual entre relógio próprio e `CivilizationRunner`; mutações de escrita mortas |
| A2 soma das cidades = total | PASS | recontagem por ano, 20 seeds, mais 6 seeds × 5 anos com pool 50 e taxa 5.0 |
| A2 conservação por fonte independente | PASS | `population_delta` = nascimentos − mortes + migração; `net_migration` = −(ids que saíram do pool); 0 no padrão, −50 com pool 50 |
| A3 tábua sintética (50, 280/11, 15) | PASS | `CivilizationObserverTests.cs:60-79`; mutações em `q` e na faixa aberta mortas |
| A nascimentos e mortes | PASS | sink bruto próprio, `BirthDate`/`DeathDate` e ids distintos batem em todo ano |
| A causas, somam `deaths` | PASS valor; teste FAIL (F1) | sink bruto por `Kind` bate em 26 corridas |
| A fundadas e fundidas | PASS | cidades novas por diferença de id e `MergedIntoCityId`: fundadas 1 a 9, fundidas 0 a 7 por seed |
| A Gini, idade, desemprego, oficinas, prédios, tesouro | PASS valor; teste FAIL (F3, F5, F6) | Gini por dupla soma, fórmula distinta; demais recontados do estado |
| A e₀ na série | PASS unitário; teste FAIL (F4) | só `LifeTable` tem poder; a exposição do observer não |
| A formato aberto | PASS | `CivilizationSample.cs:5`, dicionário `metric → valor` |
| B1 mesma seed, dois processos | PASS | 3 execuções de 20×10 pelo script: sha1 `e928739e…` idêntico; teste LongRunning tem controle de seed |
| B2 20×10 sem exceção e contrato | PASS | `civ-report.sh` exit 0; `Twenty_seeds…matches_the_contract` ok; JSON com 20 seeds e 10 anos |
| B `Parallel.For` sem estado compartilhado | PASS | paralelo e `DOTNET_PROCESSOR_COUNT=1` byte-idênticos; sink estático morto |
| B cultura e ordem | PASS | pt_BR, C/invariante e de_DE idênticos, 0 decimais com vírgula; métricas por chave ordinal |
| B guardas do CLI | PASS | seeds e anos: mutações mortas (`...ValueTests.cs:137-146`) |
| C congelado = 0, embaralhado > 0 | PASS | `TrajectoryDiversityTests.cs:21-42`; `div-always-zero` morta |
| C métrica e baseline de 20 seeds | PASS | recálculo em Python do JSON: índice 0,268753065 = baseline; `technologies` sem poder (F7) |
| C baseline de referência | fora | depende da fatia D, por instrução |
| R3 limiar literal | PASS | testes só têm valor feito à mão sobre entrada sintética, `> 0` de controle e baseline |
| Goldens e escopo | PASS | `git diff --name-status`: 4 modificados (`.gitignore`, `scripts/README.md`, `Program.cs`, `DeterminismTwoProcessTests.cs`); único baseline é novo |
| Determinismo no código | PASS | só `Math.Abs/Min/Max`; sem `Random`, `DateTime`, `Guid` em `Observation/` |

## FAIL

- **F1 causas trocadas passam.** `CivilizationObserverValueTests.cs:36-47` injeta um evento por tipo, e `disease` e `age`
  valem 1. Trocar os dois em `YearEventTally.cs:51-52` sobrevive. Usar contagens distintas por causa.
- **F2 redistribuição de fusão sem teste.** Remover o laço de `CivilizationObserver.cs:37` sobrevive, apesar de ser
  regra do plano. Criar teste com evento em cidade absorvida antes da amostra e exigir o total na absorvente.
- **F3 pool no Gini e na série sem teste.** O issue exige Gini incluindo o pool. Omitir o pool ou dar peso 1 em
  `CivilizationObserver.cs:102-103` sobrevive, e zerar `population_pool` (`:145`) também. O teste de
  `WealthInequality` não passa pelo observer. Afirmar valor numa cidade com pool > 0.
- **F4 exposição do e₀ sem poder.** `alive + deaths` em vez de `alive + deaths / 2.0` (`CivilizationObserver.cs:176`)
  sobrevive: em `...ValueTests.cs:111-134` o morto de 9 anos é o único da faixa, `q` satura em 1 nos dois casos e o
  morto de 21 anos não pesa mais. Usar mortes em faixa populosa, sem saturar.
- **F5 oficinas e prédios por cidade.** Contar o mundo inteiro em cada cidade (`CivilizationObserver.cs:113-114`)
  sobrevive: o fixture de `...ValueTests.cs:79-91` tem uma cidade só. Testar com duas ou mais cidades.
- **F6 desemprego só de adultos.** Trocar o filtro de `CivilizationObserver.cs:95` por `true` sobrevive: no tick 0 todos
  os adultos estão sem `Employer`, então a razão não muda com crianças. Usar adulto empregado no fixture.
- **F7 tecnologias.** Zerar a chave em `CivilizationObserver.cs:62` ou `:160`, contar tudo em `:115` e tirar a série em
  `TrajectoryDiversity.cs:23` sobrevivem. O baseline é 0 e `TrajectoryDiversityTests.cs:16` usa constante 1. Afirmar
  com tecnologia adotada numa cidade, e com série variando por seed.
- **F8 `population_unassigned` sem poder.** Trocar `CivilizationObserver.cs:68` por constante 0 sobrevive; o assert
  `CivilizationObserverTests.cs:36` só compara com 0. Criar braço com NPC vivo fora de cidade ativa.

## Mutações: 62 aplicadas, 48 mortas, 14 sobreviventes

| Grupo | Mutações | Resultado |
| --- | --- | --- |
| Observador que escreve | tesouro creditado, RNG consumido | mortas (teste do hash) |
| População | +1, deslocada por ano, `_previousPopulation` parado | mortas |
| Causas | fome em outras; **disease↔age** | morta; **sobrevive (F1)** |
| Filtro `SourceSystem` | nascimento, morte | mortas |
| Fundações | só ativas, `>=` na janela | mortas |
| Guardas do CLI | seeds, anos | mortas |
| Sink e relatório | drain sem zerar (2), sink estático, chave renomeada, campo extra, seed deslocada, seed ignorada | mortas |
| Fórmulas | `q`, faixa aberta, Gini invertido, mediana | mortas |
| Diversidade | escala 1, distância 0, índice, Gini trocado, tesouro absoluto | mortas |
| Métricas zeradas | 24 chaves | 20 mortas (Gini, tesouro e `cities_active` só no `Scenario`); 4 sobrevivem (F3, F7, F8) |
| Demais sobreviventes | fusão, pool no Gini (2), exposição, oficinas, prédios, desemprego, série e cidade de tecnologia | F2 a F7 |

Cada mutação restaurada com `git checkout -- <arquivo>`; `git status` limpo ao fim. Teste temporário de recontagem
removido antes do gate.

## Observações (não bloqueiam)

- `net_migration` só é testado em 3 anos com pool 50; recontagem própria cobriu 20 seeds × 10 anos sem divergência.
- `CivilizationObserverValueTests`: `Death` mapeia para `age` e inclui combate (documentado no plano).
- O plano da fatia B diz que só há catálogos e `ConditionalWeakTable`, mas existem contadores de processo
  (`Building.s_layoutVersion`, `Relationship.s_axesVersion`). Só invalidam cache; a equivalência paralelo/sequencial confirma.
- `docs/README.md` não indexa `docs/domain/civilization-observatory.md`.
- O baseline compara até 12 casas: qualquer mudança de regra exige regravar, o que o plano assume.
