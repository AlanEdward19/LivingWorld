# ADR-0026: Estado de desenvolvimento por NPC no snapshot (Fase 21)

- **Status**: aceito
- **Data**: 2026-10-14
- **Decisores**: Alan (escolheu o estado armazenado por NPC e a ontogenia ligada em todo cenário, issue #70)

## Contexto
A Fase 21 faz a capacidade de agir ser conquistada por marco. O plano marcava como porta de mão única o formato do
snapshot. Derivar o progresso só da idade dispensaria o campo, mas descartaria a variação individual que a 21b (exposição,
janela crítica) precisa gravar; trocar derivação por estado depois mudaria o snapshot duas vezes.

## Decisão
- `Npc.Development` (`DevelopmentState`: 6 doubles em [0,1], um por eixo) é canônico. `null` = desenvolvimento completo;
  todo adulto é `null` e o sistema fica inerte para ele. O `null` é gravado **explícito**: o delta binário só carrega
  campos presentes, e quem completa passa de objeto a nulo.
- `OntogenyRules` (marcos + o que cada ação exige) é `[Canonical]` em `WorldState`, serializado como dado e relido sem
  estado derivado. Snapshot antigo sem o bloco volta `Disabled` (nada muda para mundos já gravados).
- Cenário sem bloco `Ontogeny` usa os marcos humanos padrão (`OntogenyDefaults.Human`): **ligada por padrão**;
  `Enabled: false` desliga. O construtor de `WorldState` segue `Disabled` por padrão (testes e mundos montados à mão).
- Na 21a o progresso acompanha a idade escalada pelo `RateGene`; a 21b o troca por exposição acumulada.
- `ActionType.Cry` entra no catálogo com duração opcional (1 h), como `Steal`.

## Alternativas consideradas
- Derivar da idade sem campo — a 21b teria de mudar o snapshot de novo.
- Ligar só em cenários que declaram marcos — deixaria o realismo fora do mundo padrão.

## Consequências
- **Positivas**: variação individual desde já; adultos custam 1 campo nulo (~22 bytes) e nenhum tempo no sistema.
- **Negativas / trade-offs**: goldens e pins de hash regravados; `Cry` sem consumidor causal além da própria utility
  até a 21b (choro atendido conta como contato de exposição).
- **Follow-ups**: teto de janela crítica (21b) e `MilestoneProgressSum` do pool agregado (21c) mudam o snapshot de novo.
