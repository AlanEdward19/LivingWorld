# Desempenho do motor e tempo dos testes (parcial)

## O que o dono pediu (palavras dele)

- "Analise onde podemos ter melhorias de performance (rodar infinitamente menos memória, armazenamento e processamento)."
- "Com performance absurda, e os testes rodar com velocidade mais elevada (ex: 20x ou mais) para rodar tudo em no máximo 5 minutos ou menos sem perder qualidade."
- "Não quero precisar mover coisas pro noturno para acelerar os testes. Queria que todos os testes rodassem normalmente sem precisar do nightly."
- "Pode misturar 1 mundo pros testes, e validar coisas diferentes. Isso ok."

## Medido (antes → depois, contra o `main` de 2026-11-10)

| Grandeza | Antes | Depois |
|---|---|---|
| Tick a 5.000 NPCs (µs por NPC-tick) | 1,23 | ~1,0 (medido antes da fila de despertar; a medir de novo com a máquina livre) |
| Bytes alocados por NPC-tick | 246 | ~140 |
| Disco por NPC vivo por ano | 57 KB | ~6,7 KB |
| Bloco completo a 1.000 NPCs | 6.038 KB | ~580 KB |
| Gravação inteira a 1.000 NPCs (alocação) | 149 MB | 7,6 MB |
| Relações por vivo (escala) | 122 | ~21 |
| `verify.sh --full` | 20 min 40 s | 15 min 27 s (máquina livre, 2026-11-12) |

## Feito

Planta de prédio em cache; versão de eixos por dono; despertares de viagem; RNG de uso único fora do snapshot; bloco em Brotli; codec e diff sobre `JsonDocument`; mundo serializado uma vez por gravação; fila de despertar dedicada; relações por convivência real; gate com um build, suítes em paralelo e uma corrida de 20 seeds × 100 anos compartilhada entre testes (ADR-0044).

## A meta de 5 minutos não foi atingida

- O gate completo gasta cerca de 4.500 s de CPU; em 8 núcleos o piso fica perto de 9,5 minutos mesmo com empacotamento perfeito.
- Os testes ficaram mais pesados por um motivo bom: a vila não se extingue mais, então o teste de 100 anos simula ~170 vivos em vez de ~26.
- Sem cortar teste nem mover para o noturno, os 5 minutos pedem motor mais rápido, mais núcleos, ou mais reaproveitamento de corridas.

## A fazer, do mais rentável para o menos (estimativas, não medidas)

1. **Medir de novo com a máquina livre** o tick (1.000 e 5.000), o gate rápido e o completo, e registrar em `tests/baselines/`.
2. **Perfil do gate**: quais testes consomem os 4.500 s agora (logger trx), para atacar os maiores. Candidatos apontados: século de família (~1.150 s), cenários do Unit, `BytesPerNpc`, conservação de moeda em 100 anos, `LongRunScale`, `PopulationScenario`.
3. **Mais corridas compartilhadas**: seed 42 × 10 anos roda em vários testes; o mundo padrão das seeds 1 a 20 a 10 anos também.
4. **Despertares sem efeito**: 41 a 45% dos despertares não mudam nada. Cortá-los muda quando o NPC reavalia (precisa de comparação demográfica).
5. **Caminho quente da decisão** (#153 itens 2, 4, 6): `BehaviorDecisionSystem.Tick` ainda é ~70% do tick.
6. **Paralelismo do tick por cidade ou região**, com junção em ordem determinística. Decisão de arquitetura (ADR); potencial de vários x em mundos grandes.
7. **Relações em formato numérico no snapshot**: a 5.000 NPCs são 8,5 dos 12 MB do diff de 30 dias; muda o formato em disco (ADR, versão, leitura do antigo). Estimativa: 2x no diff.
8. **Catraca de desempenho** (#158 itens 3 a 6): baseline por sistema, teto que só desce, sensor de snapshot no gate.
9. **Núcleo nativo (Rust)**, #159: só como experimento; o custo hoje é de algoritmo e estrutura de dados, não de linguagem.
10. **Asserts de tempo frágeis**: `RuntimeHistoryCost` (≤1,03) reprovou com 1,045 dentro do gate em 2 de 3 rodadas de 2026-11-12 e deu 1,00 a 1,02 sozinho; ou sai do gate como medição, ou ganha margem estatística. `ScaleScenarioSensor` tem o mesmo risco.

## Tempo do gate, cronometrado em 2026-11-13 (lote AD-143 + AD-145, 51 min)

- Regravar `family-diversity` sozinho: 19 min, e o gate refaz a mesma conta depois. Demais regravações: 1 min.
- `verify --full`: 31 min. LongRunning 28 min, Unit 12 min, Integration 2,5 min, lint 1,7 min, Performance 3 min.
- LongRunning é dominado por 6 testes de 20 seeds × 100 anos (21 a 28 min cada, lado a lado): `FamilyLifeScenario`,
  `FamilyPairedScenario`, `LaborAllocationVillage`, `DefaultVillageCentury`, `FamilyDiversityCalibration` (2).
- Unit: 10 testes somam 64% do tempo (`VillageSurvivesGenerations` 637 s e 375 s, `TechDiscovery` 532 s e 372 s,
  `DiseaseTests` 343 s).
- Depois do corte (mesma árvore, 2026-11-13): junção com `verify.sh --lote` em 12 min 48 s (gate 11 min 19 s; Unit 8 min é o
  novo teto). A regravação de `family-diversity` dentro do `--full` ainda não rodou: validar no primeiro diário.
- Como medir: `LW_TEST_TRX_DIR=<dir> bash scripts/verify.sh --full` (um `.trx` por suíte; tempo por suíte no resumo).

## Fatia custo dos testes (pedido do dono, 2026-11-13): feito, meta de 5 min não atingida

"Precisamos de testes tão longos assim (vários), ou conseguimos reorganizar...". Nenhum teste removido, nenhuma asserção,
seed ou horizonte cortado; goldens e baselines intactos. `verify.sh --lote`, mesma máquina, base `6bb834c0`:

| Suíte (s) | LongRunning | Unit | Integration | lint | fase quieta | gate (parede) | CPU (user+sys) |
|---|---|---|---|---|---|---|---|
| antes | 234 | 488 | 146 | 79 | 182 | 682 (11 min 22 s) | 2.958 |
| depois | 426 | 435 | 121 | 86 | 32 | 470 (7 min 50 s) | 2.891 |

- **Fase quieta só com tempo de relógio** (`Category=Timing`: RuntimeHistoryCost, ObservationalLod, ScaleScenarioSensor,
  WorldCreationCost, BrainDecisionCost). O resto do namespace Performance é determinístico e foi para a fase paralela
  (BrainExperimentHarness rodava ao lado dos asserts de tempo: provável causa do `RuntimeHistoryCost` instável, item 10).
- **Ordem de partida**: `ScenarioFirstCollectionOrderer` começa as coleções com `Category=Scenario` antes das rápidas.
- **Corridas compartilhadas novas (ADR-0044)**: `DefaultVillageSeed42` (LongRunning; seed 42, 10 anos hora a hora e até o
  ano 60/100) serve invariantes de 1 e 10 anos, cidade de todo NPC, varredura referencial de 1 e 10 anos, braço com
  natalidade, livro de trocas, tabela de vida, `VillageSurvives` seed 42 e os 4 testes seed 42 × 10 anos de
  `FamilyPairedScenario`; equivalência pelo hash do ano 1. `DefaultVillageDecade` (Unit) passou a rodar pelo laço de
  `DemographyReport.Run` (ganchos só de observação) e serve também o braço com acolhimento de `Orphan_care` e o calendário
  demográfico; equivalência pelo hash do ano 10 e pela observação de órfãos da seed 1. `DefaultVillageCentury` serve a
  observação de famílias de `FamilyLifeScenario` (4 seeds × 60 anos, antes em série) no `--full`.
- **Suíte certa**: `VillageSurvives` (60 anos) foi do Unit para o LongRunning. **Cadeias**: braços independentes lado a lado.

### O que impede os 5 minutos (decisões do dono, nada aplicado)

11. Limite agora é CPU (~2.890 s em 8 núcleos, piso ~6 min) e cadeias de uma seed (TechDiscovery 30 a, seeds 7 e 42).
12. `MeasureHarness` e `BrainExperimentHarness` não asseveram nada (o próprio código diz "não é gate") e custam ~300 s de
    parede na fase paralela: proposta de tirá-los do gate (`Category=Measure` só no `--nightly` e nos scripts de medida).
13. `VillageSurvives` seed 7 cabe no século (seed 7 está em 1..20) se o dono aceitar levá-la ao `--full` diário.
14. Candidatos a encurtar, precisam de decisão: `Zeroing_the_pace_bonus` (10 anos para provar "muda alguma coisa"),
    `OptimizationEquivalence` arquivo frio (6 anos × 2, fora de `Scenario`), seca de `ClimateTests` (os dois braços são
    iguais nos 5 primeiros anos: daria para clonar o mundo pelo snapshot em memória). Os prefixos estritos (invariantes e
    varredura de 1 ano) ficam: já não custam nada, lidos da corrida compartilhada.
15. `--full` (verde, 2026-11-13): 29 min 36 s, 10.088 s de CPU; o século compartilhado (20 seeds × 100 anos) é o caminho
    crítico, ~28 min sozinho. Sem cortar seeds ou anos, só cai com motor mais rápido (itens 4 a 6).

## Regras que valem para qualquer otimização

- Medir antes e depois na mesma máquina, em pares alternados; alocação é exata, tempo não.
- Otimização que não muda o mundo tem de manter goldens e baselines sem regravar.
- Não reduzir horizonte, seeds nem asserção. Exceção decidida pelo dono em 2026-11-13 ("Sim, uma vez por dia"): os testes
  `Category=Century` (20 seeds × 100 anos) saem do gate de cada lote (`verify.sh --lote`) e rodam no `--full` uma vez por dia.
  Quebra demográfica de longo prazo aparece só no diário: achar o lote culpado é trabalho de quem junta.
- Vários gates ao mesmo tempo na mesma máquina distorcem tempos e os asserts de tempo.
