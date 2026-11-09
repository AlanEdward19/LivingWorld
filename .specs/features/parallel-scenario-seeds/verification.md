# Verificação final independente: #97 A.1 (paralelizar loops de seeds)

Commit verificado: `9ed92553`, base `b29ed9f2`. Máquina de 8 cores, com outras worktrees ativas.
Escopo: só o item A.1; o teto global de `--full` ≤ 8 min da issue não é exigido aqui.

**Veredito: PASS.** Nenhum critério reprovado. Observações não bloqueantes ao final.

## Critérios
| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Só código de teste | PASS | `git diff --name-only b29ed9f2...HEAD`: só `tests/LivingWorld.Tests.{Unit,Shared}` e `.specs/`. Nada em `src/`, golden ou baseline |
| 2 | Nenhum teste perdeu poder | PASS | Comparação antigo x novo abaixo e mutações B2 a B11 |
| 3 | Sem estado estático que altere a simulação | PASS | Varredura própria abaixo |
| 4 | Paralelo = sequencial | PASS | 48/48 linhas iguais, sequencial x 2 paralelas |
| 5 | Falha cita a seed real do mundo | PASS | B1, B2, B4, B6, B7, B9, B11 |
| 6 | Medições honestas | PASS | Tabela do plano declara ruído e não afirma ganho exato |
| 7 | Gates | PASS | `verify.sh` saiu 0; `test.sh --scenarios` saiu 0 |

## Comparação antigo x novo (por teste alterado)
- Divergência 2 anos (`TechDiscoveryTests.cs:231`): mesmos mundos 1..10 e 1001..1010, mesmo horizonte. `NotEqual("")`
  segue só nas seeds 1..10, como o antigo fazia com `a`. Contagem `different` com o mesmo pareamento (i, i+10).
- 30 anos (`:255`): mesmas 6 corridas; `Count >= 3` por mundo; pares (i, i+3).
- 20 anos x 10 seeds (`:265`): corpo idêntico, asserts por seed dentro do corpo.
- `OntogenyDebtsTests.cs:154`: 20 seeds, dois braços da mesma seed; soma depois, em ordem; mesmos 4 asserts finais.
- `DiseaseTests.cs:234`: 8 seeds; somas `control`, `outbreaks`, `sick` depois do loop, sobre o array.
- `PositionalSnapshotTests.cs:40`: seeds 1..20 (antes `Parallel.For(1, 21)`), mesmo assert.
- Sem captura de variável de loop nem escrita em coleção compartilhada: o único acumulado é local ao corpo
  (`deaths`, sinks por corrida) ou posicional (`results[index]`, `failures[index]`).

## Varredura de estado estático (`src/` e `Tests.Shared`)
Sem `ThreadStatic`, `AsyncLocal` ou `ThreadLocal`.
- `Relationship.AxesVersion`, `Building.LayoutVersion`: monotônicos com `Interlocked`; incremento alheio só causa miss.
- `CityOccupancy.cs:174` e `:483`: atalho incremental exige versão andar exatamente o que a lista cresceu. Mundo
  alheio só afasta a igualdade, levando ao rebuild completo.
- Caches por `ConditionalWeakTable`: chaveados por mundo, mapa ou regras imutáveis. `DefaultMap` e `DefaultSystems`
  criam instâncias novas por chamada.
- `BuildingFootprintGenerator.Cache`: memo de função pura. `TravelTicksCache`: função pura por mapa.
  `GuestLodging.Caches`: `LazyInitializer`, por mundo.
- `DecisionContextCache.*BuildCount`: não atômicos, sem efeito na simulação; lidos só por `DecisionContextCacheTests`.
- `LlmResponseValidator.EnforceValidation`, `DistortionEngine.LlmInvocationProbe`: não alcançados pelos corpos alterados
  em caminho que mude o resultado; exposição anterior a esta entrega.

## Paralelo = sequencial
Teste temporário (removido), com os corpos reais via reflexão: uma passada sequencial e duas por `ParallelSeeds.Map`.
`bash scripts/test.sh --scenarios --filter ...`: Passed 3/3, 6 m 42 s.
| Caso | Valor comparado por seed | Linhas | Iguais |
|---|---|---|---|
| Tech 2 anos, 20 mundos | chave + `CanonicalHash` (20 hashes distintos) | 20 | 20 |
| Órfãos, 20 seeds, 2 braços | mortes + meses-órfão + hash | 20 | 20 |
| Doença, 8 seeds | contagens + 2 hashes | 8 | 8 |

## Mutações (19 aplicadas, 19 mortas, restauradas com `git checkout`; `git status` limpo ao final)
| Id | Mutação | Resultado |
|---|---|---|
| H1 | engole a falha | 4 reprovados |
| H2 | cita índice em vez da seed | reprovado; cita `seed 3` para o mundo 1002 |
| H3 / H4 | faixa deslocada / curta | `Map_over_a_range` |
| H5 | inverte a ordem do resultado | 2 reprovados |
| H6 | varre falhas de trás | `When_several...` (`seed 14`) |
| H7 | grau x2 | `Degree_is_bounded...` |
| H8 | perde `InnerException` | `The_original_exception...` |
| B1 | divergência: assert em `world.Seed != 1004` | `seed 1004 falhou: mundo real 1004` |
| B2 | divergência: vazio na seed 3 | `seed 3 falhou: Assert.NotEqual()` |
| B3 | divergência: compara i com i | `0/10 seeds divergiram` |
| B4 | 30 anos: `world.Seed != 1002` | `seed 1002 falhou: mundo real 1002` |
| B5 | 30 anos: compara i com i | `0/3 pares` |
| B6 | 20 anos: segunda corrida da seed 7 usa seed 8 | `seed 7 falhou: Assert.Equal()` |
| B7 | órfãos: `world.Seed != 13` | `seed 13 falhou: mundo real 13` |
| B8 | órfãos: controle soma braço tratado | assert de mortes de crianças |
| B9 | doença: `plain.Seed != 5` | `seed 5 falhou: mundo real 5` |
| B10 | doença: `Sick = 0` | assert de população final |
| B11 | codec: `world.Seed != 17` | `seed 17 falhou: mundo real 17` |

## Gates
- `bash scripts/verify.sh`: saída 0, `verify: OK`; 2467 ok, 3 skip, 1 m 15 s de teste, 1 m 59 s no total.
- `bash scripts/test.sh --scenarios`: saída 0; 20 ok, 1 skip (`ZZZ_record_baseline`), 5 m 51 s.

## Observações não bloqueantes
- Medições: não refiz o braço "antes". Os 5 m 51 s de agora não são comparáveis aos 618 s da issue (4 cores, outra carga).
- O plano não cita `GuestLodging.Caches`, `BuildingFootprintGenerator.Cache` e `TravelTicksCache`; os três são seguros.
- Remover só `MaxDegreeOfParallelism` não é observável em máquina de até 8 cores, limite que o próprio plano registra.
- Falso positivo em `CityOccupancy.cs:483` exigiria prédio construído antes do cache, adicionado depois e `JoinCity`
  no intervalo; não achei esse caminho e os hashes não divergiram, mas a busca não foi exaustiva.
