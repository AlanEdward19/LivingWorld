# ADR-0026: rota `POST /npcs/{id}/decision-sandbox`

- **Status**: aceito
- **Data**: 2026-10-14
- **Decisores**: usuário (pediu implementar a fatia 2 da issue #58 sem supervisão)

## Contexto
A Fase 28 task 12 pede um sandbox de decisão isolado. `DecisionSandbox` existia só como classe, sem rota, então
o cliente Godot não consegue o "E se…?" do painel Cérebro. Rota nova é contrato público.

## Decisão
Criar `POST /npcs/{id}/decision-sandbox`. Corpo opcional: `needs` (hunger/thirst/sleep/social, 0–100, alto =
satisfeito, como na ficha), `personality` (qualquer dos 10 traços, 0–100) e `continuityAction` (ActionType).
Resposta `{ action, trace }`, com `trace` na mesma forma de `cognitionTrace[].trace`. O servidor monta o
`DecisionContext` real do NPC, aplica os overrides e roda `DecisionSandbox.Decide`. 404 para NPC inexistente,
morto, arquivado ou ainda no pool; 400 para valor fora da faixa ou traço desconhecido.

## Alternativas consideradas
- Calcular no cliente — viola `rules/llm-boundary.md` e duplicaria o motor.
- Contexto 100% sintético sem NPC — perde o contexto real (corpo, domicílio, memórias), que é o que importa.

## Consequências
- **Positivas**: mesma função de utilidade do motor; só leitura, hash do mundo intocado.
- **Negativas / trade-offs**: usa `ProvisionTargetDays` padrão (o motor usa o do mercado); sem foresight sintético.
- **Follow-ups**: se algum dia o sandbox precisar de memória/crença sintética, estender o corpo (compatível).
