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

## Atualização (2026-08-12) — generalização, sem mudar a decisão
A decisão continua a mesma: potência é modificador unificado, não subsistema paralelo. O
que muda é que os cinco eixos deixavam a decisão mais rígida do que o problema pede — custo
e rolagem eram tratados como obrigatórios em todo poder, o que não é verdade para uma
invulnerabilidade passiva ou uma força inata. Generalizando:

- **Extraordinário é opcional por mundo**: `Extraordinary.Enabled = false` significa zero
  portadores, zero aquisição, zero manifestação, zero custo de sistema no caminho quente —
  não um `if realisticWorld` espalhado pelo motor.
- **Custo é opcional**, não um eixo fixo. `Costs = []` é um poder válido.
- **Rolagem é opcional**: `ReliabilityMode.Guaranteed` executa determinístico sem consumir
  RNG de resolução; `ResolutionCheck` usa o primitivo do ADR-0011. Ambos são o mesmo
  modificador, só muda o modo.
- **Transformação (manifestação em estado) é opcional**, nunca fundamento de potência —
  super-humano permanente não precisa de estado alternativo.
- **Vulnerabilidade intrínseca é opcional.** Quando existe, é dado do fenômeno, distinta de
  **contramedida** — que é criada, descoberta ou inventada por alguém, não uma fraqueza
  secreta plantada de origem.
- **Aquisição de potência é regra declarativa de primeira classe** (`PowerAcquisitionRule`):
  nascimento, trauma, quase-morte, item, ritual, exposição — o motor não conhece "cristal
  que dá poder", só a regra que qualquer cenário pode declarar.
- **Herói/vilão/monstro/divindade continuam fora da ontologia física do NPC.** São
  interpretação social (crença, cultura, história), nunca campo do personagem — o ADR já
  recusava isso implicitamente ao não modelar alinhamento; agora fica explícito.

Não é uma nova decisão arquitetural — é o mesmo modificador unificado, com os eixos que
antes pareciam obrigatórios reclassificados como configuráveis por poder e por mundo. Ver
[powers.md](../domain/powers.md) para o modelo atualizado e Fase 16/23/24 para onde cada
peça entra no roadmap.
