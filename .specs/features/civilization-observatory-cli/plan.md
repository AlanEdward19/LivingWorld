# Observatório de civilização: CLI `civ-report` (issue #112, fatia B)

Status: implementado (fatia B); verificação independente fica com o orquestrador. Depende da fatia A.

## Problem

A série anual (fatia A) só existe dentro de testes. Falta um comando repetível que rode várias seeds e entregue
um arquivo comparável, para as sub-issues de #111 usarem como braço de controle. Evidência: `Program.cs` do
Workers tem ~20 modos e nenhum imprime série temporal.

## Flow

1. `scripts/civ-report.sh [cenário] [seed-inicial] [seeds] [anos] [saída]` compila o Workers e chama o modo.
2. `civ-report` (modo novo em `Program.cs`) chama `CivilizationReportCommand.Run`.
3. `Parallel.For` sobre as seeds; cada uma chama `CivilizationRunner.Run` (mundo, relógio e sink próprios).
4. `CivilizationReport.ToJson` grava `{ scenario, seeds[{seed, years[...]}], summary }`, ordem fixa, sem duração.

Sem persistência. Formato e chaves em `docs/domain/civilization-observatory.md`.

## Impact

| Front | O que muda |
| --- | --- |
| domain | nenhum |
| stored data | arquivo JSON em `artifacts/` (ignorado pelo git); nada no banco |
| public surface | modo CLI novo e `scripts/civ-report.sh`; contrato do JSON em `tests/contracts/shape.civ-report.json` |

## Portas de mão única

O formato do JSON (contrato congelado). Mudar nome de campo depois exige atualizar o contrato e quem o lê.
Chaves de métrica novas são aditivas (o contrato lista as chaves atuais; ao acrescentar, regrava-se o arquivo).

## Critérios (EARS)

1. Quando o mesmo comando rodar em dois processos com as mesmas seeds, os arquivos devem ser byte-idênticos
   (`DeterminismTwoProcessTests`, controle: seed diferente muda o arquivo).
2. O relatório deve ser idêntico com `Parallel.For` e com uma seed por vez (prova de ausência de estado
   compartilhado entre mundos).
3. O JSON deve cumprir `tests/contracts/shape.civ-report.json` (2 seeds × 2 anos no gate; 20 × 10 em `Scenario`).
4. O comando de 20 seeds × 10 anos do cenário padrão deve terminar sem exceção (teste `Scenario`, fatia C).
5. Quando `seeds` ou `anos` forem ≤ 0, o modo deve falhar com exceção de argumento.

## Estado estático

Conferido: o relatório é montado só de valores de retorno; o paralelismo roda mundos independentes (nenhum
sistema usa `Parallel`). Os `static readonly` de `ScenarioRunner` são catálogos imutáveis e a
`ConditionalWeakTable` da fauna é chaveada por instância. O critério 2 é a prova executável.

## Desempenho

Fora do tick. `compare-simulation-cost.sh` não foi rodado (outras worktrees ativas, medição ruidosa); o
orquestrador mede ao final. Sem tempo de parede no JSON nem como critério.

## Fora de escopo

Diversidade de trajetórias (fatia C), cenário de referência (D), `CityIndicators` (E), tela no Godot (#125).
