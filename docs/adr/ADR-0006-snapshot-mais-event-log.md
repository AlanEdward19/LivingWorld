# ADR-0006: Persistência por snapshot periódico + event log append-only

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
Simular 100 anos são ~36.500 ticks diários. Persistir cada NPC a cada tick é inviável e
desnecessário: a simulação roda em memória e só precisa de durabilidade em pontos de
retomada. Ao mesmo tempo, o produto promete **história consultável** — linha do tempo,
dinastias, crônicas — e isso é justamente o registro dos eventos, não o estado final.

## Decisão
Vamos persistir em duas trilhas complementares:

- **Snapshot** do estado do mundo em intervalo configurável (ex.: a cada ano de mundo),
  escrito em lote, uma transação por snapshot. É o ponto de retomada.
- **Event log append-only** com todo evento que muda o mundo, carimbado com o tick. É a
  fonte da história. **Imutável**: corrigir o passado é emitir um evento compensatório,
  nunca um `UPDATE`.

Retenção é configurável: eventos pessoais de NPCs comuns comprimem com o tempo; eventos
historicamente significativos ficam. População agregada persiste como contadores, não
como linhas de NPC (ver `docs/domain/simulation-lod.md`).

## Alternativas consideradas
- **Event sourcing puro** — reconstruir o mundo do ano 100 replayando 36.500 ticks a cada
  carga é caro demais para um jogo, e o motor já é determinístico o suficiente para que o
  snapshot não perca informação relevante.
- **Só estado atual (CRUD)** — mais simples, mas apaga exatamente o produto: sem log não
  há linha do tempo, crônica, dinastia nem narrativa derivada.

## Consequências
- **Positivas**: escrita barata durante a simulação; história é subproduto natural, não
  feature extra; snapshot + log dá replay entre dois pontos sem replayar tudo.
- **Negativas / trade-offs**: duas representações para manter coerentes; o log cresce e
  exige política de retenção desde cedo; retomada perde o que ocorreu entre o último
  snapshot e a parada, a menos que o log seja reaplicado.
- **Follow-ups**: Fase 1 define formato de snapshot e hash de mundo; Fase 10 define a
  política de compressão histórica.
