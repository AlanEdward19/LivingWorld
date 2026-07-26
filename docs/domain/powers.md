# Potência — poderes, magia e o extraordinário

Como mutantes, magos, deuses, aliens e portadores de artefato cabem num modelo só, sem virar
cinco motores paralelos.

## Um modelo, não cinco subsistemas

Mutação genética, magia arcana, dádiva divina, implante tecnológico e artefato alienígena **não são
cinco sistemas**. São a mesma coisa com fontes diferentes. Um poder é sempre um **modificador sobre
um sistema que já existe** — mortalidade, produção, relação, aprendizado, deslocamento, tempo —,
nunca um caso especial dentro do motor. O porquê é custo: cinco subsistemas paralelos multiplicam por cinco o trabalho de toda fase
futura — economia nova teria que saber de magia *e* de tecnologia alienígena *e* de milagre.
Com um modelo só, um poder novo é dado, não código. **Potência** = a capacidade de violar uma
regra padrão do mundo, com preço.

## Os seis eixos de um poder

| Eixo | O que descreve |
|---|---|
| Fonte | genética/mutação, divina, arcana, tecnológica, alienígena, artefato |
| Efeito | qual sistema existente ele modifica, e em quanto |
| Custo | o que é debitado por uso, com ou sem sucesso |
| Probabilidade | dificuldade contra capacidade do portador |
| Modo de falha | o que acontece quando não dá certo — nunca "nada" |
| Consequência social | como quem viu reage |

A fonte quase não importa para o motor: importa para a cultura, que reage diferente a um milagre e
a uma máquina. Curar alguém é o mesmo efeito sobre saúde; ser queimado por bruxaria depende de quem
contou a história. **Moedas de custo:** fadiga, saúde, longevidade, sanidade, recurso raro
consumido, dívida com uma entidade, e **atenção de algo que era melhor não ter notado você**.

## Tudo calculado, nada garantido

Nenhum poder é um "sim" automático. Toda invocação é uma rolagem contra o RNG semeado do mundo —
determinismo vale para potência como vale para clima e para parto. O custo é cobrado no uso, não
no sucesso: tentar e falhar continua caro.

```
custo cobrado sempre
chance = capacidade(portador, poder) - dificuldade(efeito, alvo, contexto)
roll   = ctx.Rng(NpcId).Next()       // stream do portador
resultado: sucesso | parcial | falha -> aplica efeito e/ou modo de falha
```

Modos de falha que valem a pena existir: efeito parcial, efeito no alvo errado, custo cobrado sem
resultado nenhum, exposição pública do portador, dano permanente a ele, atração de atenção hostil.
Falha que não muda o mundo é falha que não devia ter sido simulada.

## Consequência social

Quem vê reage: medo, culto, perseguição, recrutamento, poder político. A reação **sai da
cultura**, não do poder — uma vila medieval religiosa queima o que uma república moderna regula e
um império tecnológico recruta. Religiosidade, abertura, valorização da magia e autoritarismo de
`society.md` decidem qual delas acontece.

## Herança, escassez e materialização

Potência segue a regra do projeto: **habilidade nunca é herdada, só predisposição**. O filho do
mago nasce com potencial; sem mestre, sem tempo e sem material, morre sabendo nada — linhagem
importa sem virar garantia.

Escassez é decisão de design, não balanceamento: **se todo NPC voa, voar é caminhar**. Potência só
significa algo enquanto for rara o bastante para reorganizar uma sociedade ao aparecer. Um portador
é candidato natural a NPC detalhado no Simulation LOD — ocupa posição de impacto histórico por
definição —, mas no agregado ele ainda existe: uma região pode reportar "3 portadores conhecidos"
sem que nenhum tenha nome.

## Ver também
- [npc.md](npc.md) — atributos e habilidades que a capacidade consulta
- [society.md](society.md) — cultura decidindo a reação ao extraordinário
- [genetics-and-family.md](genetics-and-family.md) — predisposição, nunca habilidade pronta
- [divinity-and-belief.md](divinity-and-belief.md) — potência acoplada a crença
- [simulation-lod.md](simulation-lod.md) — quando um portador vira NPC detalhado
- [history.md](history.md) — uso de poder como evento com consequência
