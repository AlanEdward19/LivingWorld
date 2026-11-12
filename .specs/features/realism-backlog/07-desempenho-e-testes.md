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

## Regras que valem para qualquer otimização

- Medir antes e depois na mesma máquina, em pares alternados; alocação é exata, tempo não.
- Otimização que não muda o mundo tem de manter goldens e baselines sem regravar.
- Não reduzir horizonte, seeds nem asserção; não mover para o noturno.
- Vários gates ao mesmo tempo na mesma máquina distorcem tempos e os asserts de tempo.
