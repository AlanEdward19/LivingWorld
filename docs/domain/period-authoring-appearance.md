# Autoria de período — aparência e habilidades da pessoa

Volta pro [índice](period-authoring.md). Decisão em [ADR-0032](../adr/ADR-0032-aparencia-herdada-e-autoria-de-pessoa.md).

## Bloco `Appearance` (opcional)

```jsonc
"Appearance": { "Heritability": 0.6, "AttractionWeight": 1.0 }
```

| Campo | Tipo | Obrigatório |
|---|---|---|
| `Heritability` | double em `[0,1]` | sim, se o bloco existir: 0 = sorteio independente, 1 = só os pais |
| `AttractionWeight` | double ≥ 0 | não (padrão 1,0; 0 desliga): peso de `AttractionFactor.Appearance` no cortejo |
| `Enabled` | bool | não: `false` equivale a omitir o bloco |

**Sem o bloco**: ninguém tem genes de aparência, o campo some do snapshot, do hash e das rotas, e o cliente sorteia a aparência como antes.
**Com o bloco**: todo NPC nasce com 4 genes em `[0,100]` (`SkinTone`, `HairTone`, `HairCurl`, `Build`); filhos herdam dos pais
(mesmo modelo da personalidade). O peso de atração só entra se o cenário já declara pesos de atração (cortejo ligado).

## Pessoa autorada (`AuthoredHouseholds[].Members[]`)

| Campo | Tipo | Nota |
|---|---|---|
| `Appearance` | `{ SkinTone, HairTone, HairCurl, Build }`, cada um `[0,100]` | exige o bloco `Appearance` no período; ausente = sorteado |
| `Skills` | objeto `{ "idHabilidade": nível }`, nível em `[0,100]` | id inteiro ≥ 0; ausente = sem habilidade inicial |

```jsonc
{ "Name": "Ana", "Sex": "Female", "AgeYears": 30, "ProfessionId": 1,
  "Appearance": { "SkinTone": 12, "HairTone": 34, "HairCurl": 56, "Build": 78 },
  "Skills": { "0": 40, "7": 15 } }
```

Erros nomeiam o campo (`...Members[0].Appearance.SkinTone: fora de [0,100]`). Mesma seed + mesmo bloco = mesmo mundo.

## Rotas

`GET /npcs/{id}` devolve `appearance` (omitido sem o bloco); as projeções de cidade e mundo trazem o mesmo objeto por NPC.
