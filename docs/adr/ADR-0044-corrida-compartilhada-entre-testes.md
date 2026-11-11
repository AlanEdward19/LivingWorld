# ADR-0044: Uma corrida de simulação compartilhada entre testes

- **Status**: aceito
- **Data**: 2026-11-11
- **Decisores**: dono do projeto (Alan); implementação por agente

## Contexto
`bash scripts/verify.sh --full` levava ~20 min e boa parte do tempo era a mesma simulação
recomputada: a vila padrão das seeds 1..20 corria 10 anos em `FamilyDiversityCalibrationTests`, `FamilyPairedScenarioTests`
e `LaborAllocationVillageTests`, e de novo 100 anos no teste do século; o `TechDiscoveryTests` rodava as mesmas vilas sob
pressão por 2, 20 e 30 anos em testes separados. A spec `.specs/features/phase-16-perf-test-suite/` proibia qualquer
cache de estado de mundo entre testes ("em nenhuma forma"), por medo de cache em disco ou de GB em memória.

## Decisão
Esta decisão **revoga** aquela proibição, nas palavras do dono: "pode misturar 1 mundo pros testes, e validar coisas
diferentes". Mover testes para o noturno continua proibido: todos rodam no `--full`.
Uma corrida por (cenário, seed, regras, horizonte) pode ser simulada uma vez por processo de teste e lida por vários
testes e classes, sob estas regras:
1. A corrida guarda só **valores observados** (contagens, CVs, chaves, hashes) nos checkpoints de que algum leitor
   precisa; o horizonte é o maior pedido. Nenhum teste recebe o mundo vivo. Teste que precisa mutar ou inspecionar o
   mundo inteiro roda num mundo próprio (ou recebe cópia por snapshot e reidratação).
2. Nada em disco; só memória do processo, dentro de um `Lazy` estático (escala: KB por seed).
3. Nenhuma redução de horizonte, de seeds ou de asserção. Teste de determinismo (corrida "again") continua sendo uma
   simulação nova e independente.
4. Cada corrida tem prova de equivalência: um teste compara o hash canônico da corrida compartilhada com o de uma corrida
   isolada sem sink (`DefaultVillageCenturyTests`); a repetição do `TechDiscoveryTests` compara o ano 20 com uma corrida
   nova. Baselines e goldens não são regravados.

Corridas compartilhadas hoje: `DefaultVillageCentury` (LongRunning) e `TechDiscoveryTests.Pressure` (Unit).

## Alternativas consideradas
- Mover o século para o noturno — rejeitado pelo dono: todos os testes rodam normalmente.
- Snapshot do mundo em disco por chave — cresce com as combinações de (seed, regras) e foi o motivo da proibição antiga.
- Só paralelizar — ajuda, mas não tira a CPU repetida; o século sozinho é ~1/3 da CPU do `--full`.

## Consequências
- **Positivas**: menos CPU no `--full`; o braço real de 10 anos e o século passam a ser a mesma corrida, por construção.
- **Negativas / trade-offs**: testes ficam acoplados à corrida; rodar um só deles com `--filter` paga a corrida inteira
  (ex.: 100 anos para ler o ano 10). Uma falha na corrida derruba todos os leitores dela.
- **Follow-ups**: novas corridas compartilhadas seguem as regras 1–4; ver `rules/tests.md`.
