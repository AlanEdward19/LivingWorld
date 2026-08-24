# ADR-0020: Mapa inicial dimensionado pela população

- **Status**: aceito
- **Data**: 2026-08-23
- **Decisores**: Alan

## Contexto

Cada household e workplace novo precisa de um `Building` real, com footprint entre 4x3 e 6x5
células e sem colisões. O mapa procedural padrão era sempre 10x10, inclusive para cenários de
1.000–10.000 NPCs; portanto não havia área física suficiente para materializar todos os prédios
sem sobreposição ou coordenadas fora do mapa.

## Decisão

Mundos procedurais criados por `ScenarioRunner` dimensionam o lado do mapa a partir da população
inicial. O orçamento reserva o pior caso atual de 30 células por prédio para até um prédio por
NPC, mais os workplaces iniciais, arredonda o lado para múltiplo do `regionSize` e preserva 10x10
como mínimo. A fórmula é determinística e não usa RNG adicional.

Mapas JSON ainda procedurais (seed e dimensões, sem `Cells`) também crescem quando necessário,
preservando seed, catálogo, custos e assentamentos. Mapas com `Cells` totalmente autoradas mantêm
exatamente o tamanho declarado; se não houver terra suficiente, o loader retorna o sinal explícito
de escassez e não inventa células que o usuário não desenhou.

## Alternativas consideradas

- **Casas com footprint artificial de uma célula** — rejeitada porque perde a aparência física
  pedida e ainda não comporta milhares de households num mapa 10x10.
- **Permitir sobreposição ou coordenadas fora do mapa** — rejeitada porque viola ocupação e torna
  projeção, clique e crescimento urbano incoerentes.
- **Não materializar casas no cenário de escala** — rejeitada porque cria dois modelos causais
  diferentes e viola a garantia de um prédio real por household.

## Consequências

- **Positivas**: default e escala passam a ter espaço determinístico para todos os prédios; o
  placement continua usando uma única regra de ocupação.
- **Negativas / trade-offs**: mapas de populações grandes consomem mais memória e alteram hashes e
  baselines que incluem a geografia procedural.
- **Follow-ups**: validar os sensores de performance e atualizar apenas baselines cujo delta seja
  explicado pelo novo tamanho canônico do mapa.
