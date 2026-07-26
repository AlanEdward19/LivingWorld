# ADR-0014: Regra de classificação — canônico vs. volátil

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
A Fase 1 partiu o hash do mundo em canônico e volátil, e exige por reflexão que todo campo
esteja classificado. Faltava o **critério** da classificação. Sem ele, três conflitos
apareceram em fases diferentes e todos eram o mesmo problema:

- o nome de uma entidade emergente está fora do hash (ADR-0013), mas "que nome esta cultura
  usa" é crença, e crença está dentro;
- a aparência de sprite derivada da genética entra no hash ou não;
- memória compactada muda o hash ou não (Fase 10).

Classificar caso a caso produz decisões inconsistentes e um hash em que ninguém confia.

## Decisão
Um campo é **canônico se alimenta uma decisão**. É **volátil se é recomputável a partir do
canônico, ou se é cosmético sem efeito causal**.

O teste é sempre o mesmo: *alguma decisão de NPC, sistema ou regra lê este campo?* Se lê,
é canônico — mesmo que pareça enfeite. Se não lê, e some sem perda, é volátil.

Aplicações que estavam em disputa:

| Campo | Classe | Por quê |
|---|---|---|
| `EntityId` | canônico | tudo referencia |
| **Denominação** (`cultura × entidade → token`) | canônico | culturas discordarem do nome é fato do mundo, e decisões leem a discordância |
| **Rótulo** (texto exibido do token) | volátil | nenhuma decisão lê a string; é recomputável por composição |
| Traços de aparência (altura, cor, marcas) | canônico | genéticos, e alimentam atração e reconhecimento |
| Sprite derivado dos traços | volátil | função pura do canônico + versão do renderizador |
| Memória acima do limiar de importância | canônico | alimenta decisão e diálogo |
| Resumo compactado de memória | volátil | recomputável a partir do que sobrou |
| Índices, caches, agregados recomputáveis | volátil | por definição |
| `LOD(branch, tick)` | canônico | ver ADR-0012: define o mundo, não a sua exibição |

Regra de conflito: **na dúvida, canônico.** Falso canônico custa hash instável; falso
volátil custa mundo irreprodutível, que é muito pior.

## Alternativas consideradas
- **Classificar por camada** (tudo do `Domain` é canônico, tudo de apresentação é volátil) —
  simples e erra nos casos difíceis: denominação vive no domínio e o texto dela não deveria
  contar.
- **Tudo canônico** — hash à prova de bala e inutilizável como gate: trocar um renderizador
  ou reindexar passa a "mudar o mundo".

## Consequências
- **Positivas**: um critério só resolve nome, sprite, memória e LOD de uma vez; o teste
  gerado por reflexão da Fase 1 ganha uma segunda asserção — todo campo canônico é lido por
  ao menos um caminho de decisão, e nenhum campo volátil é; renderizador e índice podem
  evoluir sem tocar no hash.
- **Negativas / trade-offs**: "alimenta uma decisão" exige rastrear leitura, o que é análise
  estática de verdade e não uma anotação; um campo hoje cosmético que amanhã vira entrada de
  decisão muda de classe e **quebra os golden hashes** — mudança legítima, mas barulhenta.
- **Follow-ups**: a Fase 1 passa a testar a regra, não só a presença da classificação.
