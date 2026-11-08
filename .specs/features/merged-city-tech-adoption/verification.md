# Verificação independente da #114: fusão de cidade transfere a adoção

Verifier sem autoria do código. Base `b29ed9f2`, HEAD verificado `9d6eb14e`. Sondas e mutações foram temporárias e
removidas; a árvore ficou limpa antes deste commit. Custo (`compare-simulation-cost.sh`) adiado por instrução.

## Veredito: FAIL

O fix em `MergeCityInto` está correto e cobre o caso central. O critério 2 da issue não está provado (F1) e existe um
caminho que ainda coloca lápide em `AdoptedBy` (F2).

## Critérios da issue

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Regressão: mãe herda, `BonusPct(mãe) > 0`, sem filha em `AdoptedBy` | PASS | `HandleEvent_transfers_...` passa; reprova sem o fix (M1) |
| 2 | Nenhuma lápide em `AdoptedBy` em 10 anos, emergência e fundação ativas | **FAIL** | F1 e F2 abaixo |
| 3 | Determinismo de dois processos | PASS | `--project longrunning`, filtro `DeterminismTwoProcess\|GoldenHashes`: 16 passam, 1 pulado (gravador de golden) |
| 4 | Goldens inalterados | PASS | Mesma execução; `git diff --name-only b29ed9f2...HEAD` sem arquivo golden, baseline ou hash |

## Demais pontos conferidos

| Ponto | Resultado | Evidência |
|---|---|---|
| Sem duplicata quando a mãe já adotava | PASS | `HandleEvent_drops_...`; M3 e M9 reprovam |
| `OriginCity` preservado | PASS | `WorldState.Cities.cs:93`; M7 reprova |
| `CauseEventId` = `CityMerged` | PASS | `SpatialSettlementFoundingSystem.cs:207-210`; M5 reprova |
| `SourceSystem` do sistema de fundação | PASS no código | `SpatialSettlementFoundingSystem.cs:210`; sem teste (O1) |
| Ordem por índice da tecnologia | PASS no código | `WorldState.Cities.cs:85`; sem teste (O1) |
| Outros chamadores de `MergeCityInto` | PASS | grep em `src`, `tests`, `client`: único chamador é `SpatialSettlementFoundingSystem.cs:202` |
| Outros campos com `CityId` na lápide | PASS | grep em Domain e Simulation confere com a lista do plano; os restantes são históricos ou vivem na própria lápide |
| `bash scripts/verify.sh` | PASS | saída 0; Unit 2461 passam, 3 pulados, 0 falhas; `verify: OK` |

## FAIL

### F1: o teste do critério 2 passa de forma vazia

`tests/LivingWorld.Tests.LongRunning/Emergence/MergedCityAdoptionScenarioTests.cs:50` só percorre `world.Technologies`,
e no `default.json` essa lista fica vazia nos 3 seeds. O guarda da linha 55 exige fusão, não tecnologia adotada.

| Sonda (10 anos, amostra diária, com o fix) | Fusões | Tecnologias | Fusões com filha adotante |
|---|---|---|---|
| `default.json`, seeds 1 / 2 / 3 | 1 / 5 / 4 | 0 / 0 / 0 | 0 |
| Emergência facilitada, seeds 1 a 20 | 0 a 7 | 9 a 31 | 0 |

Com a transferência desligada (M1), o teste continua verde: 3 de 3. Não prova o critério.

Correção viável, já medida: injetar uma tecnologia adotada por cada cidade-filha assim que ela surge, e exigir fusão com
filha adotante. Seeds 1 a 20: com o fix, 0 dias com lápide em `AdoptedBy` e `TechnologyAdopted` causal em 19 de 20 seeds
(o seed 5 não funde). Sem o fix, lápide em até 3540 dos 3600 dias nos seeds inspecionados (1, 10, 11 a 16).

### F2: protótipo da filha concluído depois da fusão cria lápide em `AdoptedBy`

`src/LivingWorld.Simulation/Emergence/TechDiscoverySystem.cs:93-94` cria a tecnologia com `[cityId]` a partir do payload
agendado, sem resolver a cidade ativa. O protótipo leva `PrototypeTicks` (720 no padrão), e a fusão pode ocorrer nesse
intervalo.

Reprodução: sonda unitária funde a filha e chama `TechDiscoverySystem.HandleEvent` com o payload real de um protótipo
dela. `Assert.DoesNotContain(daughter.Id, tech.AdoptedBy)` reprova, com o fix presente.

Alcance: reproduzido no handler, não em execução completa. Nas 40 rodadas de sonda de 10 anos o caso não ocorreu
(`pendingOnTombDays=0`), portanto é raro. Quando ocorre, repete o defeito da issue: lápide adotante e bônus ausente para
os moradores.

Correção: em `HandleEvent`, resolver com `world.FindActiveCity(cityId)` e usar a cidade ativa em `AdoptedBy`, mantendo
`OriginCity` na filha; cobrir com teste.

## Mutações (restauradas com `git checkout`)

| Mutação | Resultado | Teste que reprovou |
|---|---|---|
| M1 desliga a transferência | Morta | `transfers` e `drops`; **sobrevive** ao cenário de 10 anos (F1) |
| M2 mantém a filha | Morta | `transfers` e `drops` |
| M3 duplica a mãe | Morta | `drops` |
| M4 omite `TechnologyAdopted` | Morta | `transfers` |
| M5 remove a causa | Morta | `transfers` |
| M6 troca `SourceSystem` | **Sobreviveu** | nenhum (O1) |
| M7 reescreve `OriginCity` | Morta | `transfers` |
| M8 inverte a ordem | **Sobreviveu** | nenhum (O1) |
| M9 emite evento quando a mãe já sabia | Morta | `drops` |

## Observações não bloqueantes

- O1: `SourceSystem` e a ordem estão corretos no código, mas nenhum teste os protege (M6, M8). Basta asserir
  `adopted.SourceSystem` e usar um caso com duas tecnologias.
- O2: `plan.md` e AD-122 dizem que a transferência está pendente do dono; ele já a definiu como padrão.
- O3: a issue diz que o cenário padrão não liga emergência. `scenarios/default.json` tem `Emergence.Enabled: true`; os
  goldens usam `ScenarioRunner.RunAndHash`, sem emergência.
- O4: mundos salvos antes do fix mantêm lápides em `AdoptedBy`; não há migração. A issue não pede.
