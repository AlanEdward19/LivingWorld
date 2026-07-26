# ADR-0008: Ramificação como único modelo de viagem no tempo

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O escopo extra pede máquina do tempo, reescrita de fatos, artefatos temporais e raças
capazes de manipular tempo. Isso colide de frente com três pilares já decididos: motor
determinístico (ADR-0005), log append-only (ADR-0006) e hash de mundo como eval gate.

Reescrever o passado atacaria os três de uma vez. Precisávamos de um modelo que entregasse
a fantasia sem desmontar a fundação.

## Decisão
Vamos adotar **ramificação como o único modelo**: um salto ao tick T cria uma linha nova a
partir do snapshot de T. A linha-mãe segue existindo, intocada e indiferente. Não existe
reescrita, não existe paradoxo e não existe fusão de branches.

- O salto é **um evento anexado** — append-only preservado, nada de `UPDATE` no passado.
- `seed_B = H(seed_A, tick_divergência, id_intervenção)` — determinismo preservado por
  linhagem de seed.
- Branch é **copy-on-write** sobre o snapshot da mãe: custa a divergência, não o mundo.
- Todo salto é uma **rolagem** contra a inércia histórica do alvo, que o modelo de história
  degradável (ADR-0007) já calcula. Nada garantido.
- Branch precisa de **âncora** (habitante, viajante, artefato, consequência pendente) para
  persistir. Sem âncora, é coletado.

## Alternativas consideradas
- **Passado mutável com re-simulação** — exato e caríssimo: re-simular 100 anos por
  intervenção, e todo NPC conhecido pelo jogador pode deixar de existir sem aviso.
- **Retcon de crença** (mudar o fato e deixar livros e memórias se reescreverem) — o mais
  barato e o mais alinhado ao ADR-0007, mas o estado físico do mundo nunca mudaria: viagem
  no tempo que não muda nada além do que se acredita.
- **Ponto fixo (Novikov)** — custo zero, determinismo perfeito, agência zero. A tentativa
  sempre esteve no log e nada pode ser mudado.
- **Híbrido por força da intervenção** — os três acima combinados por rolagem. Mais rico e
  bem mais caro: três máquinas para manter coerentes em vez de uma.

## Consequências
- **Positivas**: zero paradoxo por construção; reusa snapshot e replay que a Fase 1 e a
  Fase 3 já entregam; a fantasia de conquistador entre linhas, artefato que ramifica e
  culto a uma linha perdida sai toda do mesmo mecanismo; o hash continua sendo gate.
- **Negativas / trade-offs**: mudar o passado **não afeta a linha de origem** — o payoff
  narrativo tem de vir de outro lugar (conhecimento e objetos atravessando, perda
  permanente, comparação entre linhas). Sem coleta, branches crescem sem teto. E uma
  dimensão a mais atravessa consulta, cache e ferramenta de debug para sempre.
- **Follow-ups**: ADR-0009 leva `BranchId` para o esquema já na Fase 3, porque retrofit
  depois é migração em todas as tabelas. A política de âncora e coleta é spec da fase
  temporal, não desta decisão.
