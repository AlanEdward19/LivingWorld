# Memória histórica — como o passado se degrada

O mundo não guarda um log fiel de tudo. Guarda **relatos**. Depois que as testemunhas
morrem, o fato deixa de existir como registro e passa a existir como história contada —
distorcida a cada boca por onde passa. O motor sabe a verdade; ninguém no mundo sabe.

## Camadas do passado

| Camada | Quem carrega | Fidelidade | Morre quando |
|---|---|---|---|
| **Fato** | só o motor (esqueleto imutável) | total | nunca |
| **Memória viva** | quem testemunhou | alta, já enviesada | a testemunha morre |
| **Relato** | quem ouviu de quem viu | perde detalhe, ganha moral | não é retransmitido |
| **Tradição** | família, guilda, aldeia | distorce por geração | a linhagem se extingue |
| **Registro escrito** | livro, crônica, inscrição | congela no erro do autor | queima, apodrece, se perde |
| **Mito** | a cultura inteira | quase nada do fato | a cultura morre |

O NPC age sobre a **crença**, nunca sobre o fato. Essa é a regra que dá sentido a tudo.

## Meios de transmissão

Cada meio troca fidelidade por alcance. Nenhum é bom em tudo.

| Meio | Distorção por salto | Alcance | Fraqueza |
|---|---|---|---|
| Tradição oral familiar | alta | 3–4 gerações | some com a linhagem |
| Canção, ditado, provérbio | altíssima | séculos | comprime o fato numa moral |
| Livro, crônica | baixa por cópia | séculos | erro de copista, fogo, censura |
| Monumento, inscrição | quase nula | milênios | só cabe um nome e uma data |
| Registro oficial | baixa | enquanto o Estado durar | o Estado edita o que o incomoda |

Um livro está **congelado no viés de quem escreveu**. Copiar introduz erro. Um livro pode
ser perdido por séculos e redescoberto — e então contradizer o que todo mundo acredita.

## Operadores de distorção

Aplicados pelo motor, de forma **determinística e semeada**, a cada salto de transmissão.
A LLM nunca inventa a distorção — ela só põe em prosa o relato que o motor já distorceu.

| Operador | O que faz |
|---|---|
| Troca de atribuição | o feito migra para alguém mais famoso |
| Inflação de magnitude | 200 mortos viram 2.000; três anos viram uma década |
| Compressão temporal | dois eventos distantes viram um só |
| Perda de causa | o efeito sobrevive, a causa vira lição moral |
| Moralização | o evento é reescrito conforme os valores de quem conta |
| Anacronismo | um detalhe do presente é injetado no passado |
| Omissão conveniente | a família não conta a desonra; o Estado não conta a derrota |
| Fusão de personagens | dois avós viram um ancestral único |

A probabilidade de cada operador depende do meio, do tempo decorrido, do prestígio e da
educação de quem transmite, e dos valores da cultura. Trauma preserva o detalhe e corrói
a causa: lembra-se vividamente do que aconteceu e inventa-se o porquê.

## Cânone limitado

Uma comunidade guarda no máximo N relatos vivos. Relato novo entra empurrando o de menor
peso (importância × transmissibilidade × recência). É por isso que a história cabe:
**o custo não cresce com o tempo, cresce com o número de comunidades.**

Mundo de 1M de habitantes por 2.000 anos: ~6 GB, contra ~58 TB de log bruto. O esqueleto
dos fatos domina esse número e pode ser amostrado por relevância histórica (~130 MB).

## Duas consultas, nunca misturadas

- **Verdade** — o que aconteceu. Visão de motor, debug e ferramenta de autor.
- **Crença** — o que este NPC, esta família ou esta cultura acredita que aconteceu.

Nenhum caminho da API de jogo dá acesso à verdade. Se as duas consultas nunca divergem no
cenário de teste, o sistema de distorção não está ligado.

## O que isso desbloqueia

Duas cidades com versões incompatíveis da mesma guerra, ambas agindo com coerência.
O estudioso que acha um livro antigo e contradiz o consenso. A família que carrega uma
rixa cuja origem ninguém lembra direito. Um herói que nunca existiu.

## Ver também
- [history.md](history.md) — event log, linha do tempo, eventos emergentes
- [memory.md](memory.md) — memória individual, os cinco tipos, compactação
- [society.md](society.md) — cultura, conhecimento, transmissão
- [llm-contract.md](llm-contract.md) — a LLM narra o relato, não o fabrica
