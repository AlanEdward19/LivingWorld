# Autoria de período — bloco `Dynamics` (opcional)

Volta pro [índice](period-authoring.md). Único bloco realmente opcional do contrato. Ausente
= período sem viés declarado e sem evolução de conteúdo (só o sorteio uniforme padrão de
profissão).

```jsonc
"Dynamics": {
  "ProfessionBiases": [
    { "ProfessionId": 1, "Weight": 2.0 }       // Weight > 0
  ],
  "SkillBiases": [
    { "SkillId": 0, "Weight": 1.5 }  // SkillId é um int aberto — mesmo contrato de ProfessionId,
                                      // sem nome fechado (ver nota abaixo)
  ],
  "TransformationRules": [
    { "Kind": "Emerge", "TargetProfessionIds": [5], "TriggerTick": 1000 },
    { "Kind": "Disappear", "SourceProfessionIds": [2], "TriggerTick": 5000 },
    { "Kind": "Merge", "SourceProfessionIds": [1, 2], "TargetProfessionIds": [3] },
    { "Kind": "Split", "SourceProfessionIds": [3], "TargetProfessionIds": [1, 2] }
  ]
}
```

## Cardinalidade por `Kind`

Violar qualquer uma destas regras é rejeitado no validador (`400`, erro nomeia a regra):

| Kind | `SourceProfessionIds` | `TargetProfessionIds` |
|---|---|---|
| `Emerge` | vazio | exatamente 1 |
| `Disappear` | exatamente 1 | vazio |
| `Merge` | 2 ou mais | exatamente 1 |
| `Split` | exatamente 1 | 2 ou mais |

`TriggerTick` é opcional (long, `>= 0`) — ausente significa "sem gatilho de tick explícito
declarado neste momento da fase" (reserva de campo pra uso futuro de sistemas que disparam a
regra).

## Referência a profissão

**Toda profissão citada em `ProfessionBiases`/`TransformationRules` precisa existir em
`ProfessionIds`** (bloco População, ver [schema](period-authoring-schema.md)) — a menos que
`ProfessionIds` esteja vazio (sem restrição, qualquer id passa). Id fora do catálogo é
rejeitado, nomeando o id no erro.

## Habilidades — `SkillId` aberto (ainda sem efeito em runtime)

`SkillBiases[].SkillId` é um id inteiro qualquer, sem lista fechada de nomes — mesmo contrato
de `ProfessionId`. Nome (se houver) é dado de fora do motor, não deste contrato. Nota: o motor
ainda mapeia habilidade internamente por um enum fechado de 13 valores (Fase 6) — declarar um
`SkillId` aqui é aceito e persistido, mas ainda não influencia o sorteio/ganho de habilidade em
runtime (mesmo status de antes, só o contrato de entrada deixou de exigir nome fechado).
