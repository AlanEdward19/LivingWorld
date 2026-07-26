# ADR-0010: Potência como modificador unificado, não subsistema paralelo

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O escopo extra traz mutantes, magos, deuses, aliens, conquistadores e artefatos. O caminho
óbvio — um subsistema por categoria — multiplicaria o custo de **toda fase futura**: cada
regra nova de economia, relação ou mortalidade precisaria ser escrita cinco vezes, e cada
teste também. É assim que um motor vira cinco motores mal costurados.

## Decisão
Vamos tratar tudo isso como um modelo só: **potência é a capacidade de violar uma regra
padrão do mundo, com preço declarado**. Um poder nunca é um caso especial dentro do motor
— é um **modificador sobre um sistema que já existe** (mortalidade, produção, relação,
aprendizado, deslocamento, tempo).

Todo poder declara cinco coisas: fonte (genética, divina, arcana, tecnológica, alienígena,
artefato), efeito, custo, probabilidade e modo de falha — mais a consequência social, que
sai da cultura e não do poder.

Aplicações diretas: um **deus** é potência altíssima acoplada a uma economia de crença, que
reusa a camada de crença do ADR-0007. **Aliens e conquistadores** são culturas num degrau
tecnológico diferente, chegando por contato — reusam cultura, tecnologia e guerra.

Duas regras herdadas valem sem exceção: a rolagem usa o **RNG semeado** do mundo, e
**habilidade nunca é herdada, só predisposição** — filho de mago nasce com potencial.

## Alternativas consideradas
- **Um subsistema por categoria** — modelagem mais fiel a cada fantasia e custo permanente
  em toda fase seguinte. Rejeitado pelo custo composto, não pela fidelidade.
- **Poder como conteúdo puro de cenário, sem modelo** — máximo de flexibilidade e nenhuma
  garantia: sem custo e sem rolagem declarados, nada impede um poder que simplesmente
  vence, e "tudo calculado, nada garantido" morre.

## Consequências
- **Positivas**: fase nova custa uma vez, não cinco; poderes herdam determinismo, teste de
  conservação e integridade referencial de graça; deus, mutante e alien viram configuração
  em vez de código.
- **Negativas / trade-offs**: alguma fantasia não vai caber no molde e vai exigir esticá-lo;
  o modelo genérico é menos expressivo que cinco específicos; e há o risco de virar um
  balde de modificadores sem coerência — mitigado por exigir os cinco eixos declarados.
- **Follow-ups**: escassez é decisão de balanceamento, não de arquitetura — se todo NPC
  voa, voar é caminhar. Fica para o cenário e para o julgamento humano, sem gate.
