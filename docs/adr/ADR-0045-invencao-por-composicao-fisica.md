# ADR-0045: Invenção por composição física, concebida pelo NPC

- **Status**: proposto
- **Data**: 2026-11-12
- **Decisores**: dono do projeto (Alan), pendente; rascunho por agente

## Contexto
O dono pediu que NPCs inventem tecnologia, veículos (terra, água, ar), robôs, contracepção e remédio,
"em nada escrito": o NPC concebe a ideia, ela pode ser reproduzida depois, e a aparência e a animação
saem do material e da forma. Hoje a emergência é da cidade, não do NPC; o vetor de eixos (`pace, load,
reach`) é declarado no cenário e funciona por construção, sem física (`src/LivingWorld.Simulation/Emergence/TechDiscoverySystem.cs:39-120`,
`src/LivingWorld.Domain/Emergence/EmergenceRules.cs:22-25`), e o saber nunca se perde. A Fase 24 pede
pré-requisitos declarados (`docs/roadmap/phase-24-emergence.md:16`). Desenho completo:
`.specs/features/emergent-invention/design.md`.

## Decisão
Vamos representar toda invenção como um `Design`: até 16 partes primitivas com dimensão, material ou
processo e junção. O cenário declara só a natureza (propriedades físicas e químicas dos materiais
brutos). O motor declara leis simples (massa, flutuação, estabilidade, resistência, atrito, propulsão,
arrasto, química por produto escalar) num avaliador em aritmética inteira. O NPC concebe por busca
local a partir do que conhece, movido por frustração vivida; o protótipo gasta material e tempo e o
avaliador real decide se funciona. O saber vive em `KnownDesign` por NPC: ensina, copia com perda, morre
com o último que sabia. A função é medida contra consumidores que o motor já tem (deslocamento, travessia,
carga, duração de ação, fecundabilidade, doença, produção). O cliente desenha e anima a partir das partes,
materiais e junções (estende `client/godot/Scenes/object_factory.gd`). A LLM só nomeia (ADR-0013).
O `TechDiscoverySystem` fica atrás da flag até a primeira fatia (transporte sobre água) cobrir mobilidade
e então é substituído. O critério de pré-requisito da Fase 24 passa a ser "emerge do avaliador".

## Alternativas consideradas
- Estender o vetor de eixos atual com mais categorias: continua sendo lista escrita de eixos com sentido
  e "funciona" sem física; não gera aparência.
- Árvore tecnológica declarada no cenário: é exatamente o "escrito" que o dono recusou.
- Simulação física contínua por tick (corpo rígido): custo por NPC-tick inaceitável e difícil de manter
  determinística entre CPUs.
- Gerar invenções pela LLM: viola `rules/llm-boundary.md` (a LLM criaria fato).

## Consequências
- **Positivas**: seeds diferentes inventam coisas diferentes; pré-requisito emerge da física; aparência e
  animação sem arte por invenção; contracepção e remédio só existem se alguém inventar.
- **Negativas / trade-offs**: as leis físicas e as sondas de função são código e precisam de manutenção;
  propriedades de material entram como dado a conferir em fonte; equilíbrio sem gate (ADR-0013);
  goldens e baselines de emergência regravados; +~1,2% de snapshot estimado.
- **Follow-ups**: robô como agente que decide pede ADR próprio; ajuste do `ROADMAP.md` (Fase 24) é do dono;
  limiar de novidade entre designs e coleta de designs sem quem saiba.
