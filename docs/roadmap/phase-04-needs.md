# Fase 4 — Necessidades e rotina

**Objetivo**: o NPC deixa de ser um registro que envelhece e passa a **decidir**: sente
fome, sono e sede, escolhe a ação de maior utilidade e cumpre uma rotina diária que a
urgência sabe interromper.

## Tasks
1. **Medidores de necessidade** (0-100) que decaem por tick: fome, sede, sono e o mínimo
   social. Taxa de decaimento vem do cenário, não de constante em código.
2. **Necessidade → objetivo**: acima de um limiar, a necessidade gera um objetivo ativo.
   Objetivo é dado no mundo, inspecionável, não estado interno de um método. Necessidade
   em 0 **dispara a consequência** — o clamp não pode virar silêncio.
3. **Personalidade numérica 0-100** com os 10 traços, sorteada por NPC na criação. Entra
   aqui porque só existe para modular pesos de decisão.
4. **Utility AI**: cada ação candidata recebe pontuação = necessidade × contexto (horário,
   distância, disponibilidade do local) × multiplicador de personalidade. Maior pontuação
   vence; empate desempata por ID de ação, nunca por ordem de iteração.
5. **Terminação da seleção de ação**: a seleção converge em número limitado de passos, com
   teto declarado no cenário; ao fim do tick nenhum NPC vivo fica sem ação escolhida.
   Utilidade cíclica ou empate patológico aborta com erro claro, não laça.
6. **Rotina diária** por profissão e estágio de vida (criança, adulto, idoso): sequência
   padrão de ações por hora, **com duração máxima declarada por ação**. Utility sobrepõe a
   rotina quando algo urgente aparece.
7. **Histerese/inércia**: a ação atual ganha bônus de continuidade e há custo de troca,
   para o NPC não alternar de ação a cada tick. É ligável/desligável por flag de teste —
   sem o braço desligado não dá para provar que ela faz efeito.
8. **Deslocamento com custo de tempo**: mover-se entre locais consome ticks segundo o custo
   da Fase 2; enquanto se desloca, o NPC não executa a ação de destino.
9. **Moradia**: todo NPC tem residência; dormir exige estar nela (ou num substituto
   declarado). Sem residência é um estado explícito, não `null` silencioso.

## Critérios de verificação
- **Fome vence trabalho, com controle**: mesmo cenário e mesma seed, fome 90 vs fome 10,
  comida a 1 local de distância e turno aberto — escolhe **comer** no braço 90 e
  **trabalhar** no braço 10, em 10/10 seeds.
- **Histerese com braço de controle**: par com/sem histerese, mesma seed, 20 seeds —
  `trocas_com < trocas_sem` em **20/20**. O teto absoluto de trocas por dia é o percentil
  99 das 20 seeds, gravado em `tests/baselines/action-switches.json`; número mágico no
  texto do critério é proibido.
- **Fome mata em prazo derivado**: `X = ceil(100 / taxaDecaimentoFome)` lido do cenário em
  runtime. NPC sem acesso a comida morre em `[X, X+1]` ticks, com `causa == Starvation`
  registrada no event log — datável, não "em algum momento".
- **Direção por traço, tabela de casos**: `[traço, cenário, açãoEsperadaBaixo,
  açãoEsperadaAlto]`, uma linha por traço. Para cada linha, mesma seed com o traço em 20 e
  em 80 produz as ações previstas em 10/10 seeds. O teste **falha se algum dos 10 traços
  não tiver linha** na tabela — traço novo sem caso reprova.
- **Clamp com consequência**: unit test do clamp com valores extremos (abaixo de 0, acima
  de 100, decaimento maior que o medidor) mais o assert de que chegar a 0 ativa o objetivo
  de fome no mesmo tick. Varrer 100 anos atrás de valor fora de faixa é caro e tautológico.
- **Sem deadlock de rotina**: nenhum NPC permanece na mesma ação além da duração máxima
  declarada dela, em 10 anos com o assert a cada tick; e o teste reprova se alguma ação do
  catálogo não declarar duração máxima.
- **Terminação da seleção**: nenhum tick excede o teto declarado de passos de seleção (a
  folga medida vai para o baseline); no cenário adversarial de utilidades cíclicas, o
  motor aborta nomeando o NPC e as ações empatadas.
- Deslocamento entre locais distintos consome >= 1 tick; nenhum NPC muda de local no mesmo
  tick em que decidiu ir.
- Desligar o utility AI muda o hash canônico em 10 anos (prova que a decisão entra na conta).

## Fora do escopo
Produção, salário, preço e compra de comida (Fase 5) — aqui comida é recurso de cenário
disponível ou não. Aprender e melhorar em ações (Fase 6) e relações sociais reais (Fase 7).

## Ver também
[behavior.md](../domain/behavior.md) ·
[npc.md](../domain/npc.md) ·
[time-and-ticks.md](../domain/time-and-ticks.md) ·
[world-map.md](../domain/world-map.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
