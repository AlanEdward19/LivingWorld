# ADR-0005: Simulação determinística com RNG semeado por stream

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O objetivo técnico #1 é rodar 100 anos de simulação. Um bug que aparece no ano 73 é
inútil se a próxima execução produzir um mundo diferente. Sem reprodutibilidade não há
teste de cenário, não há replay, não há bisseção de regressão — e a maior parte do valor
do projeto está justamente em comportamento emergente de longo prazo.

## Decisão
Vamos garantir que **mesma seed + mesmo cenário + mesmo número de ticks = mundo
byte-idêntico**, e tratar isso como invariante de build, não como boa intenção:

- Toda aleatoriedade sai do RNG do mundo, derivado em **streams independentes** por
  sistema e por entidade (`ctx.Rng(npcId)`). Streams separados garantem que adicionar um
  sistema novo não desloque a sequência dos existentes.
- Tempo vem de `WorldDate`, nunca do relógio da máquina.
- Iteração que produz efeito no mundo é **ordenada por ID** — nunca ordem de `Dictionary`.
- Dinheiro e estoque são inteiros. Nada de `float` em grandeza acumulada.
- Um teste de arquitetura falha o build se `Random`, `DateTime.Now` ou `Guid.NewGuid`
  aparecerem em `Domain` ou `Simulation`.
- Todo sistema novo entra com teste de determinismo: dois runs de mesma seed com hashes
  iguais, e um par de seeds diferentes que **não** batem (senão o hash não mede nada).

## Alternativas consideradas
- **Determinismo "quando der"** — na prática significa nenhum: uma única chamada a
  `Random.Shared` em qualquer sistema contamina o mundo inteiro e o custo de caçar isso
  depois é muito maior que a disciplina agora.
- **Paralelismo agressivo desde já** — ganharia tempo de parede, mas ordem de execução
  variável destrói a reprodutibilidade antes de haver perfil que justifique.

## Consequências
- **Positivas**: bug de ano 73 vira teste; replay e bisseção viáveis; hash de mundo é um
  eval gate barato e forte.
- **Negativas / trade-offs**: paralelizar sistemas passa a exigir prova de independência
  de ordem; o teste de hash acusa qualquer mudança de regra, inclusive as intencionais —
  atualizar o baseline vira parte da tarefa.
- **Follow-ups**: definir na Fase 1 o algoritmo de hash de mundo e o formato do snapshot.
