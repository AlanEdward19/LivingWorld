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
    { "Skill": "Agriculture", "Weight": 1.5 }  // Skill é um dos 13 nomes fechados do motor —
                                                // ver lista abaixo, nunca invente um nome novo
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

## Habilidades — catálogo fechado

`SkillBiases[].Skill` só aceita um destes 13 nomes (habilidade ainda não é catálogo aberto por
dado, só o peso inicial é):

`Agriculture`, `Hunting`, `Trade`, `Construction`, `Medicine`, `Combat`, `Teaching`, `Craft`,
`Politics`, `Leadership`, `Research`, `Technology`, `Magic`.
