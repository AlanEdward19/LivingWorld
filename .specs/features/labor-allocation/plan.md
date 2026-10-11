# Labor allocation

## Problem

Quem trabalha em quê é decidido por um acidente de empate, não por necessidade. `ProfessionScoreOf`
(`BehaviorDecisionSystem.Profession.cs`) pontua cada profissão por habilidade × personalidade × fração de vagas
abertas; empate vence o menor id. Na primeira hora todo adulto vira profissão 1 e é contratado na fazenda; quem já
tem emprego troca de profissão sem trocar de emprego, então oficinas vazias nunca recebem gente. Medido nesta
branch (AD-089, AD-090):

- no `scenarios/default.json` com 20 NPCs, serraria e pedreira ficam com 0 empregados por 10 anos; com 40 NPCs têm gente;
- o fixture do gate só se alimenta por esse empate (todos vão para a fazenda): uma margem de troca de 1,25 derrubou a
  população do ano 10 de 59 para 24 (seed 3) e de 59 para 23 (seed 5); "sair do emprego ao trocar de ofício" levou a
  seed 5 de 67 para 16 e o mundo ao vivo a 0 vivos.

Quem paga é o mundo vivo: a economia não reage a escassez (faltou comida, ninguém vai plantar) e cada indústria nova
fica vazia. Quando isto entrar, o preço alto de um bem atrai trabalhadores para produzi-lo e a vila continua alimentada
sem depender de um empate.

## Flow

Reaproveita `Workplace.Prices` (atualizado pelo `MarketPricingSystem`), as receitas de `EconomyCatalog` e as operações
`Workplace.Fire/Hire` e `Npc.Fire/Hire`, em vez de criar um mercado de trabalho paralelo.

1. `BehaviorDecisionSystem` (exists) - hourly, chama a avaliação de ofício de cada adulto
2. `LaborValueIndex` (new, no door - placement per conventions) - uma vez por dia, valor do trabalho de um trabalhador por oficina: saída da receita × preço local × parcela vendável (estoque cheio vale quase nada), menos o insumo ao preço-piso, mínimo 1
3. `EvaluateProfessionSwitch` (exists) - compara o valor da oficina atual com o da melhor oficina com vaga real, na mesma cidade
4. `JobMove` (new, door 1) - troca atômica: sai da oficina atual e entra na nova no mesmo tick, grava o tick da troca
5. `EmploymentSystem` (exists) - continua contratando desempregados por profissão

## Impact

| Front | What changes |
| --- | --- |
| domain | termo novo: valor do trabalho (renda esperada por trabalhador numa oficina, saída × preço) |
| domain | `Npc.Profession` passa a seguir o emprego: quem muda de oficina muda de ofício junto; hoje troca sozinho. Quem lê hoje: rotina (`RoutineOf`), `SkillPracticeSystem`, `WagePaymentSystem` |
| stored data | campo canônico novo `LastJobChangeTick` no NPC; snapshots antigos carregam como 0; goldens e pins regravados uma vez |
| scenarios | gate (`ScenarioRunner`) e `default.json` mudam de comportamento: o empate-pelo-menor-id deixa de existir |
| domain | `MarketIndex.NearestSelling` exige preço além de estoque (achado: a forja com 10 de trigo sem preço atraía quem ia comprar) |

## Relations

None - no stored-data shape change

## Surface

None - nothing consumed outside

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. tick da última troca de emprego | `Npc.LastJobChangeTick` (inteiro, canônico, 0 = nunca trocou) | derivar a carência do evento `Hired`: o log é fora do estado e não sobrevive a snapshot, a carência voltaria a zero a cada carga |

- Nada mais aqui é difícil de reverter

## Criteria

### S1: Sem empate arbitrário (P1)

**Acceptance Criteria**

1. WHEN duas profissões têm o mesmo valor do trabalho THEN the system SHALL manter a profissão e o emprego atuais do NPC
2. WHILE o NPC está empregado e a melhor oficina vale no máximo 1,25 vezes a atual the system SHALL não trocá-lo
3. IF o NPC trocou de emprego há menos de 30 dias THEN the system SHALL não avaliar nova troca

**Independent test:** mundo com duas oficinas de mesmo valor: ninguém troca em 360 dias.

### S2: Preço move gente (P1)

**Acceptance Criteria**

4. WHEN o preço do bem principal de uma oficina sobe a ponto de seu valor passar 1,25 vezes o da atual e há vaga real na mesma cidade THEN the system SHALL mover o NPC no mesmo tick, saindo da oficina antiga e entrando na nova
5. WHEN a produção de trigo é cortada (braço tratamento) THEN the system SHALL ter mais empregados na fazenda no dia 180 que o controle em pelo menos 18 de 20 seeds
6. IF a oficina nova não tem vaga THEN the system SHALL manter o NPC onde está, sem demissão

**Independent test:** par controle/tratamento com choque de produção de trigo.

### S3: Oficinas novas ganham gente (P1)

**Acceptance Criteria**

7. WHEN há oficina com vaga e valor do trabalho acima da atual de algum empregado THEN the system SHALL ocupar a oficina em até 90 dias
8. The system SHALL ter serraria e pedreira do `default.json` com pelo menos 1 empregado cada aos 90 dias em pelo menos 18 de 20 seeds com 20 NPCs

**Independent test:** `default.json` com 20 NPCs, 20 seeds.

### S4: A vila não piora (P1)

**Acceptance Criteria**

9. The system SHALL manter a soma da população do ano 10 do fixture do gate (5 seeds do `MeasureHarness`) em pelo menos 95% do medido antes
10. The system SHALL manter a soma da população do ano 10 do mundo ao vivo (3 seeds, 20 NPCs) em pelo menos 100% do medido antes
11. The system SHALL limitar as trocas de emprego a no máximo 0,5 por NPC por ano em média no gate
12. The system SHALL manter µs por NPC-tick do fixture de 1.000 NPCs em no máximo 105% e alocação por tick em no máximo 102% do medido antes

**Independent test:** `MeasureHarness` antes e depois, rodadas alternadas.

## Out of scope

| Excluded | Why |
| --- | --- |
| negociação de salário, sindicato, greve | sem instituição que consuma |
| migração de trabalhadores entre cidades | `MigrationSystem` já cuida; aqui só dentro da cidade |
| exigência de escolaridade ou licença | Fase 21 e seguintes |
| renda de capital e dono de oficina | AD-044: oficina sem dono NPC |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| margem de troca | 1,25 | valor já testado nesta branch como piso de histerese | n |
| carência entre trocas | 30 dias | evita ida e volta; ordem de grandeza de um mês de salário | n |
| valor da oficina sem mercado | piso de preço do cenário | `Prices` só existe em oficina de mercado | n |
| o salário declarado entra no valor? | não: o valor é saída × preço; o salário continua pagando | salário fixo por profissão não reage a escassez | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface

## Sources

- `docs/audits/phase-gap-review.md` e AD-089/AD-090 - medições da margem de troca e da saída do emprego
- `.specs/features/economy-depth/checks.md` (Deviations) - serraria e pedreira vazias com 20 NPCs
