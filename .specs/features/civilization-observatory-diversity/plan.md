# Observatório de civilização: diversidade de trajetórias (issue #112, fatia C)

Status: implementado (fatia C, só o gate); verificação independente fica com o orquestrador. Depende de A e B.

## Problem

Nenhum número separa "seeds diferentes geraram histórias diferentes" de "toda seed dá a mesma vila" (seção 6 da
visão). Sem ele, uma sub-issue pode achar que enriqueceu o mundo quando só mexeu na média.

## Flow

1. `CivilizationRunner` produz um `SeedRun` por seed (fatia A/B).
2. `TrajectoryDiversity.Compute(runs)` (new, função pura) devolve `DiversityResult` (índice geral e por métrica).
3. `CivilizationReport` grava o resultado em `summary.diversity`.
4. `tests/baselines/civilization-diversity.json` guarda o valor de 20 seeds × 10 anos (gate).

## Métrica (documentada também em `docs/domain/civilization-observatory.md`)

Métricas: população total, cidades ativas, Gini, tesouro per capita, tecnologias (lista aberta: `DiversityMetric`
extensível para distância cultural e política quando existirem). Para cada métrica:

- escala = média de `|valor|` sobre todas as seeds e anos (se 0, a métrica vale 0);
- distância entre duas seeds = média, sobre os anos, de `|a(t) − b(t)|`, dividida pela escala;
- índice da métrica = média sobre todos os pares de seeds; índice geral = média das métricas.

Por que escala relativa e não min-max: min-max inflaria ruído minúsculo de uma métrica quase constante. Só
`+ − × ÷` e `Math.Abs`. Menos de 2 seeds ou nenhum ano dá 0. A ordem das seeds não importa.

## Impact

| Front | O que muda |
| --- | --- |
| domain | termo novo: diversidade de trajetórias |
| stored data | baseline novo `tests/baselines/civilization-diversity.json`; nenhum existente é regravado |
| public surface | `summary.diversity` no JSON do `civ-report`; contrato `shape.civ-report.json` regravado (aditivo) |

## Portas de mão única

A definição da métrica: mudá-la invalida o baseline e as comparações antigas (regrava-se por commit explícito).

## Critérios (EARS)

1. Num mundo congelado (mesma série em todas as seeds) a diversidade deve ser exatamente 0.
2. Com as mesmas séries embaralhadas de forma diferente por seed a diversidade deve ser > 0 (par de controle R4).
3. Num par sintético de curvas opostas ([1,3] e [3,1]) o índice deve ser o valor feito à mão (população 1, geral 0,2).
4. Multiplicar a série por mil não deve mudar o índice da métrica; reordenar as seeds não deve mudar o índice.
5. A diversidade de 20 seeds × 10 anos do cenário padrão deve bater com o baseline (`Category=Scenario`) e ser > 0.

Limiar (R3): o valor esperado vem do baseline gravado com 20 seeds, não de literal no teste.

## Baseline

Gate gravado: `Gate.Index` e `Gate.ByMetric` (20 seeds, seeds 1 a 20, 10 anos, `default.json`).
`technologies` vale 0 no cenário padrão (nenhuma tecnologia emerge em 10 anos): o baseline registra isso.
A parte do cenário de referência de séculos (nightly) fica para a fatia D, que depende de decisão do dono.

## Desempenho

Cálculo O(seeds² × anos) uma vez por relatório. O teste `Scenario` roda 20 seeds × 10 anos em paralelo
(fora do gate rápido, entra em `verify.sh --full`). Tempo de parede não é critério agora (worktrees concorrentes).
`compare-simulation-cost.sh` não rodado de propósito; o orquestrador mede ao final.

## Fora de escopo

Cenário de referência e nightly de séculos (D), `CityIndicators` (E), distância cultural/política (sub-issues futuras).
