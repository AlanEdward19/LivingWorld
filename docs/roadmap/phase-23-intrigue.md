# Fase 23 — Intriga

**Objetivo**: intriga não é subsistema novo — é a camada de crença da Fase 10 e a memória
social da Fase 7 usadas **contra as pessoas**. Segredo é fato com propagação restrita;
chantagem é usar o segredo de outro; traição é agir contra um vínculo de confiança por
ganho; e a briga sai do primitivo do ADR-0011, não de um roteiro. Ninguém escreve um enredo:
o mundo passa a produzir rixa, escândalo e conspiração como subproduto do que já existe.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 10 fechar.

## Tasks
1. **Segredo = fato do esqueleto (Fase 10) com propagação de crença restrita**: dono,
   cúmplices, conjunto de quem sabe e risco de vazamento por salto de transmissão. Nenhuma
   tabela paralela de segredos — é atributo de propagação, não entidade nova.
2. **Motivo e oportunidade** como pré-condição de toda ação hostil: motivo (necessidade,
   rancor, ganho, ordem de facção) **e** oportunidade (proximidade, acesso, ausência de
   testemunha). Faltando um, a ação nem entra no conjunto candidato da utility.
3. **Chantagem** como modificador de negociação a partir de segredo alheio. Exige que o
   chantagista **acredite** no segredo — consulta de crença, nunca de verdade.
4. **Traição**: agir contra vínculo com confiança acima do limiar do cenário, por ganho
   mensurável. O efeito é o colapso do eixo de confiança da Fase 7 mais memória episódica de
   alta importância em quem viu — não um flag `traidor`.
5. **Pilha de humor**: modificadores transitórios com fonte, magnitude e decaimento
   declarados, somados numa pilha inspecionável que entra como peso na utility da Fase 4.
   Referência de mecânica: a pilha de pensamentos do RimWorld. Humor é derivado, nunca campo
   escrito à mão.
6. **Rancor de longo prazo**: memória episódica negativa que sobrevive à compactação virando
   crença sobre uma pessoa ou linhagem. Decai, **prescreve** no prazo do cenário e reacende
   com evento novo do mesmo alvo — a rixa cuja origem ninguém lembra direito, à moda do
   Dwarf Fortress, já sai de graça da distorção da Fase 10.
7. **Briga e violência pelo primitivo do ADR-0011**, perfil `Dramático`, com **sucesso
   parcial** de primeira classe: "você venceu, mas alguém viu" é o que alimenta a próxima
   intriga.
8. **Fofoca como transmissão enviesada**: os operadores de distorção da Fase 10, com
   probabilidade modulada pelos eixos de relação de quem conta com o alvo e com o ouvinte.
   Inimigo infla, aliado omite.
9. **Reputação por comunidade**: agregado das crenças daquela comunidade sobre um NPC,
   distinto da verdade e do que cada indivíduo acredita. Duas comunidades, dois retratos.
10. **Facção e conspiração**: organização com objetivo oculto (segredo de múltiplos donos),
    recrutamento por afinidade e rancor comum, e exposição pública como evento com
    consequência.

## Critérios de verificação
- **Ninguém sabe sem caminho**: enumeração por reflexão de **todo** acesso a crença,
  reprovando se algum caminho resolver para o fato direto **ou** se algum handler ficar sem
  cobertura; mais o assert, a cada tick em 10 anos, de que todo NPC que conhece um segredo
  tem cadeia de transmissão até o dono. Par de mutação igual ao da Fase 10: desligar a
  checagem por flag de teste tem de **fazer este critério falhar**.
- **Segredo causa traição**: par base/tratamento na mesma seed, tratamento = densidade de
  segredos maior no cenário. Taxa de traição maior no tratamento em **18/20 seeds**.
  Direção, não magnitude.
- **O chantagista acredita no que usa**: a cada tick em 10 anos, toda chantagem tem o
  segredo no conjunto de crenças do chantagista naquele tick. Uma única chantagem com
  segredo desconhecido reprova — é o mesmo vazamento de estado global que `memory.md`
  proíbe.
- **Rancor decai, prescreve e só reacende por evento**: sem evento novo do mesmo alvo,
  `rancor(t+1) ≤ rancor(t)` a cada tick, chegando a zero no prazo do cenário. A única
  subida permitida é no tick de um evento atribuído ao alvo; qualquer outra reprova.
- **Humor entrou na conta e diversificou a ação** — as duas metades, porque a primeira
  sozinha passa com uma pilha que só embaralha números: desligar a pilha por flag muda o
  hash canônico em 10 anos **e** reduz a contagem de ações distintas escolhidas na janela,
  par na mesma seed, em 18/20 seeds.
- **Reputação não é verdade**: no cenário pareado existe ao menos um NPC cuja reputação
  diverge entre duas comunidades **e** ambas divergem do fato, com cada comunidade agindo
  de forma coerente com a própria versão. Se nunca diverge, a fofoca não está enviesando
  nada.

## Fora do escopo
Prosa de escândalo, panfleto e crônica: Fase 12. Guerra entre estados e processo político
formal: Fase 10 e `society.md`. Segredo de culto e doutrina: Fase 17. Orientação e estado de
divulgação: Fase 22 — aqui só consumidos. Combate tático: fora do projeto; a briga resolve
numa rolagem do ADR-0011. O sistema de ferimento localizado (corpo e mente, com vício e
doença) e sua recuperação **é** parte do projeto — vive como saída da rolagem do ADR-0011,
não como sub-jogo tático, e é detalhado em fase própria de saúde/corpo, não aqui.

## Questões em aberto
- Segredo colide com o **cânone limitado** da Fase 10: despejado do cânone, o segredo deixa
  de existir ou vira fato que ninguém pode mais revelar — e a chantagem morre com ele?
- Reputação por comunidade é agregado recalculado por tick (caro) ou estado mantido (uma
  quarta camada de crença para conservar coerente)? Não dá para ter os dois.
- "Ausência de testemunha" obriga a saber quem vê quem a cada tick. Isso cabe no LOD
  agregado da Fase 8, ou intriga só existe em região materializada?
- Rancor que prescreve contradiz a rixa de linhagem que atravessa gerações. A prescrição é
  por indivíduo e a transmissão familiar reinicia o prazo, ou a linhagem tem prazo próprio?
- A utility da Fase 4 já soma necessidade × contexto × personalidade. Onde a pilha de humor
  entra sem virar o fator dominante, e que sensor barato avisa quando ela virou?

## Ver também
[memory.md](../domain/memory.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[behavior.md](../domain/behavior.md) · [society.md](../domain/society.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
