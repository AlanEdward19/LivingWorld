# Memória

O que um NPC lembra, o que esquece e o que isso vira. Memória é o que faz uma pessoa
parecer a mesma pessoa dois anos depois — e o que impede a simulação de explodir de dados.

## Nem tudo vira memória

A maior parte do que um NPC faz é rotina descartável; só o que passa de um limiar de
importância vira registro. Cinco tipos:

| Tipo | O que é | Exemplo |
|---|---|---|
| **Operacional** | Recente e temporária | "está indo ao mercado", "discutiu com o vizinho hoje" |
| **Episódica** | Evento pessoal relevante | "o jogador salvou sua filha", "foi demitido da oficina", "sobreviveu a uma guerra" |
| **Semântica** | Conhecimento do mundo | "a capital fica ao norte", "o mercado abre pela manhã" |
| **Social** | Sobre outras pessoas | "confia em Helena", "tem medo do capitão", "deve dinheiro ao comerciante" |
| **Cultural** | Valores compartilhados | "a magia é proibida", "estrangeiros não são confiáveis" |

A operacional é lixo em horas; a cultural dura séculos e é compartilhada por uma cidade.

## Estrutura e recuperação

Toda memória carrega **importância**, **tick de origem**, **participantes** e **local**.
Recuperar não é varrer tudo: a busca pondera importância, recência e relevância ao contexto
atual. Perguntar ao ferreiro sobre metal traz memórias de oficina, não o casamento da irmã.

## Esquecimento e compactação

Memória antiga não é apagada em bloco — é **degradada em algo mais barato**: resumida,
agrupada com semelhantes, transformada em crença ou convertida em traço de personalidade.

```
várias experiências ruins com nobres
   -> crença "nobres são egoístas"
   -> comportamento mais hostil com nobres
```

Depois disso o NPC continua agindo com base na experiência **sem guardar nenhum episódio**:
a crença é o resíduo comprimido de uma vida, e é ela que entra na decisão.

A compactação roda **por regra**, no motor. Ocasionalmente uma LLM resume um lote em texto
melhor — sempre fora do caminho crítico, nunca durante o tick, e o resultado é texto
descritivo, jamais um novo fato sobre o mundo.

## Conhecimento limitado é invariante

Um NPC só sabe o que **viu, ouviu, aprendeu ou lhe contaram**. Rumores se propagam por
interação e chegam distorcidos, atrasados ou errados. Duas cidades podem acreditar em
versões incompatíveis da mesma batalha, e isso é correto, não inconsistência.

Copiar estado global para dentro da memória de um NPC — o preço real do trigo em outra
província, quem de fato matou o rei, a posição do jogador — **é bug**, mesmo quando torna o
diálogo mais conveniente. Ignorância é conteúdo.

## Custo

Memória cresce com a idade, e um idoso influente é o pior caso. Cada NPC tem um **orçamento
de memória**: atingido o teto, a compactação roda e as de menor importância caem primeiro.

| Resolução do NPC | Orçamento |
|---|---|
| Detalhado | Alto — episódios individuais, relações nomeadas |
| Agregado | Mínimo — só crenças, traços e vínculos historicamente relevantes |

Morta a última testemunha, o que sobra não é memória comprimida — é **relato transmitido**,
e ele se degrada. Esse modelo vive em [historical-memory.md](historical-memory.md).

## Ver também
- [historical-memory.md](historical-memory.md) — o que acontece depois que a testemunha morre
- [npc.md](npc.md) — atributo de memória e sua influência na retenção
- [behavior.md](behavior.md) — como crenças enviesam a pontuação de ações
- [genetics-and-family.md](genetics-and-family.md) — memória social e eixos de relação
- [simulation-lod.md](simulation-lod.md) — orçamento por resolução
- [llm-contract.md](llm-contract.md) — quais memórias entram no contexto de diálogo
