# Genética e Família

Como pessoas se aproximam, formam famílias, têm filhos e passam adiante — ou não — o que
são. Modelo de relações, reprodução e hereditariedade.

## Relações

Formam-se por proximidade, compatibilidade, convivência, status, cultura, interesses,
atração, necessidade econômica e aliança política. Ninguém "decide" gostar de alguém: a
relação emerge de encontros repetidos, sobre eixos numéricos que evoluem com eventos.

| Eixo | Sobe com | Cai com |
|---|---|---|
| Confiança | promessa cumprida, tempo, ajuda em crise | traição, mentira descoberta |
| Afeto | convivência, presentes, cuidado | abandono, agressão |
| Respeito | competência, feito público, cargo | fracasso visível, humilhação |
| Dívida | empréstimo, favor recebido | pagamento, retribuição |

Como a convivência vira vínculo (AD-128): em casa todos convivem; no trabalho cada um passa o dia com os
poucos de quem mais gosta dentro da própria turma; quem socializa conversa com alguém por perto. Afeto e
confiança têm um teto por par (temperamentos parecidos, o quanto o outro é agradável e leal), sobem cada
vez menos perto dele e esfriam aos poucos sem contato, até o par ser esquecido. Parentes não se esquecem.
O resultado é um punhado de vínculos fortes e muitos fracos por pessoa.

## União e fim da união (AD-136)

Cada um escolhe o par pelo que o outro tem e só namoram se os dois se quiserem; união com parente de r ≥ 1/8
(primo-irmão, meio-irmão, tio) é tabu. O casal leva o que tinha em casa: nada é criado no casamento. A mágoa sobe
com briga de temperamento, penúria, distância, luto de filho e traição, é perdoada aos poucos e baixa o teto do
afeto: o amor esfria de verdade. Quem tem propensão e oportunidade pode ter um caso, que pode dar filho do amante
e ser descoberto pelo boato que corre pelos vínculos (perdão, separação ou violência). Quem deixou de amar pode
sair no ritmo da norma da época (`UnionRules`: na vila medieval quase sem divórcio, com abandono e separação de
fato; no moderno, divórcio). Filho pequeno fica com a mãe, os maiores se dividem; quem sai volta para os pais ou
abre casa, e só se une de novo depois do tempo da norma; viúvo espera o luto. Assim surgem padrastos,
meios-irmãos e irmãos criados em casas diferentes.

## Reprodução

Depende de idade, fertilidade, saúde, relação, cultura, recursos, condições sociais,
mortalidade e eventos históricos — guerra e fome derrubam natalidade sem regra especial.
Fecundidade natural de época (AD-130, `ReproductionRules`): todo mês a mulher casada pode conceber
num dia qualquer, com chance que sobe na adolescência, culmina aos 20–29, cai depois dos 35 e zera
na menopausa; o homem segue fértil na velhice e pode casar depois dos 45. Não há concepção na gravidez
nem no intervalo da lactação, o que dá partos a cada 2 a 3 anos. Na concepção ficam decididos o sexo
(~105 meninos por 100 meninas, gêmeos raros), a duração (~38 semanas, com prematuros) ou a perda
precoce; aborto, natimorto e morte materna crescem na adolescência precoce, depois dos 35, com
muitos partos e com saúde ruim. Gravidez **agenda um evento futuro de parto**, sem varredura diária.
A morte por idade é risco mensal da tábua de época pelo estado atual, nunca data sorteada no berço.

## Hereditariedade

```
atributoFilho = atributoPai*pesoPai + atributoMãe*pesoMãe + mutação + influênciaAmbiental
```

A mutação garante variação; o termo ambiental garante que o mundo pese. Traço contínuo (vitalidade, gene de taxa,
estatura) segue o modelo infinitesimal (AD-134): filho = média dos pais + segregação mendeliana com metade da variância
aditiva da população, o que mantém a variância estável entre gerações (média simples a cortava pela metade). A
estatura herdada (h² ≈ 0,8) ainda perde centímetros com fome na infância. Separação dura:

| Genético (predisposição) | Ambiental (adquirido) |
|---|---|
| Físico, saúde, fertilidade, aparência | Educação, profissão, crenças |
| Parte do potencial cognitivo | Cultura, idioma, valores |
| Temperamento básico | Habilidades, conhecimento, traumas |

**Habilidade nunca é herdada** — é a oficina que faz o ferreiro, não o sangue. Quem cria importa (AD-132): o
órfão criado por tios aprende o ofício deles, e o adotado se parece com os pais biológicos no herdado e com os
adotivos no criado.

## Algoritmo genético emergente

Não existe função artificial de aptidão nem ranking de "melhor indivíduo". A seleção
**emerge do mundo**: resistentes sobrevivem a epidemias, hábeis na agricultura prosperam no
campo, diplomáticos na política, adaptados ao frio sobrevivem no gelo. Em jogo: genoma,
características herdáveis, recombinação, mutação, pressão ambiental, sucesso reprodutivo,
mortalidade e migração — e o que vence muda quando o ambiente muda.

## Aviso de design: genética não é destino

Um modelo em que **bom gene = boa vida** é um bug de design, não um resultado interessante.
Ambiente, cultura, sorte e experiência devem pesar tanto ou mais que a herança: o nobre
tolo e o camponês brilhante precisam ser frequentes, ou a simulação vira planilha de
eugenia com cenário medieval. Sempre que um atributo herdado influenciar um resultado de
vida, algo do ambiente deve pesar na mesma ordem de grandeza.

## Famílias e linhagens

O **household** é a unidade econômica: compartilha moradia, estoque, renda e obrigações; é
nele que a criança aprende, o idoso é sustentado e a herança se transfere. Acima dele, a
**linhagem** — dinastias rastreáveis por gerações, com sobrenome, propriedades, títulos,
dívidas e rivalidades que sobrevivem a quem as criou. Household se dissolve; linhagem vira
história.

## Ver também
- [npc.md](npc.md) — atributos e personalidade que entram na herança
- [time-and-ticks.md](time-and-ticks.md) — nascimento como evento agendado
- [behavior.md](behavior.md) — como a relação altera a decisão
- [memory.md](memory.md) — memória social que sustenta confiança e dívida
- [society.md](society.md) — normas culturais de união e herança
