# ADR-0012: Catch-up preguiçoso de branch dormente

- **Status**: aceito
- **Data**: 2026-07-26
- **Decisores**: Alan

## Contexto
O ADR-0008 fez de cada salto temporal uma linha nova, e o usuário levou a ideia adiante:
branch é dimensão. Quem desenvolver artefato, tecnologia ou poder de trânsito
interdimensional consegue **voltar à linha de origem**.

Isso cria um problema que a ramificação sozinha não tinha. Se o viajante passa 5 anos na
linha B e volta para A, o que aconteceu em A nesses 5 anos? A linha estava parada. Alguém
precisa simular aquele intervalo — e simular todas as linhas o tempo todo é inviável.

## Decisão
Vamos deixar branches dormentes **congelados** e simular sob demanda:

- Cada branch guarda `simuladoAté` — o tick até onde já foi calculado.
- Voltar a um branch no tick T dispara **catch-up** de `simuladoAté` até T. Se
  `T <= simuladoAté`, não há trabalho nenhum: já foi calculado antes.
- Cada branch tem **relógio próprio**. "Agora" é por linha; elas não andam juntas.
- A resolução **não é escolha do catch-up, é definição do mundo**: `LOD(branch, tick)` é
  função pura do registro de presença até aquele tick, e esse registro é append-only. Um
  branch que ninguém observou *genuinamente rodou* em baixa resolução — isso é a verdade
  daquela linha, não uma aproximação dela. Simulado um tick em `L`, é `L` para sempre: não
  existe re-rodar em fidelidade maior. É o que mantém `preguiçoso == eager` de pé mesmo com
  degradação, e casa com o ADR-0007 — o detalhe que ninguém viu nunca existiu.
- O resultado é **cacheado e append-only**: catch-up já feito nunca é refeito.

A justificativa é o determinismo. Como o mundo é função de `(seed, estado, ticks)` e o
ADR-0009 dá seed por linhagem de branch, **simular tarde produz exatamente o mesmo mundo
que simular na hora**. Preguiça não é aproximação aqui: é o mesmo resultado, mais barato.
Determinismo comprou preguiça.

## Alternativas consideradas
- **Simular todos os branches sempre** — coerente e caro sem teto: N linhas vivas custam N
  vezes um mundo, para que quase nenhuma seja visitada.
- **Congelar de verdade (tempo não passa no branch abandonado)** — custo zero e mata o
  ponto: voltar para casa depois de 5 anos e encontrar tudo igual não é voltar para casa.
- **Resumo estatístico sem simular** (sortear "o que teria acontecido") — barato e quebra o
  determinismo e a auditoria: o resumo não bate com o que o replay produziria.

## Consequências
- **Positivas**: custo proporcional ao que é **visitado**, não ao que existe; a primeira
  visita paga, as seguintes não; reusa Simulation LOD e replay que já existem; o mesmo
  mecanismo serve para mundo ocioso sem jogador nenhum.
- **Negativas / trade-offs**: a primeira volta a um branch muito antigo é uma espera longa
  e visível — precisa de progresso na tela e provavelmente de catch-up em background; e um
  branch com muitos anos de atraso pode custar mais que o jogador tem paciência, o que
  empurra para pré-aquecer branches ancorados.
- **Consequência de aceitar o LOD como definição**: fidelidade perdida é perdida. Um jogador
  que volta a um período que ninguém observou não consegue ampliar o detalhe, porque ele
  nunca foi calculado. Em troca, o pré-aquecimento é bit-idêntico ao catch-up sob demanda —
  os dois seguem a mesma escala.
- **Follow-ups**: teto de branches vivos e política de âncora continuam valendo (ADR-0008);
  a fase de trânsito interdimensional especifica pré-aquecimento e orçamento de catch-up.
  O critério da Fase 20 passa a ser `preguiçoso == eager` **dado o mesmo registro de
  presença** — sem essa cláusula ele é falso assim que houver degradação.
