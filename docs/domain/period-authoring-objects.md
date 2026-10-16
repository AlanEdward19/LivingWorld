# Autoria de objetos (`Objects`, `Furnishings`)

Blocos opcionais do período (ADR-0034, #47 fatia 1). Ausentes = comportamento e hash de antes; o cliente usa o `assets.json`.

```json
"objectsFormat": 1,
"Objects": [{
  "id": "bed", "category": "bed", "variationSeed": 0,
  "shape": [{"shape": "box", "size": [0.5, 0.2, 0.9], "offset": [0, 0.1, 0], "color": "d8cfb8", "symmetry": "none"}],
  "usePoints": [{"name": "lie", "offset": [0, 0.2, 0], "facing": 0}]
}],
"Furnishings": {"default": ["table", "bench", "crate"], "3": ["bed", "table", "chest"]}
```

## Campos
| Campo | Regra |
|---|---|
| `objectsFormat` | Obrigatório com `Objects`; só `1` é aceito |
| `id` | Texto único; `Furnishings` só cita ids existentes |
| `category` | Texto livre do cenário (não é enum) |
| `shape[].shape` | `box` [x,y,z] · `cylinder` [raio,altura] · `sphere` [raio] · `capsule` [raio,altura]; tamanhos > 0 |
| `shape[].offset` | Centro da parte relativo à base (chão) do objeto |
| `shape[].symmetry` | `none`, `x`, `z` ou `xz`: espelha a parte nos eixos citados |
| `variationSeed` | 0 = forma exata; ≠ 0 = índice de variação (0–3) por posição, mesma seed, mesma variação |
| `usePoints[]` | `name`, `offset` (3 números, a partir da base), `facing` (graus), `action` (id de `ActionType`, opcional) e `pose` (`idle\|walk\|sleep\|work\|eat\|socialize\|buy\|power`, opcional). Com `action`, o servidor põe quem faz essa ação no objeto (`usingObject`); sem `pose` vale a pose da ação (ADR-0037) |
| `effects[]` | Tokens `alvo:magnitude`: `sleep:0.5`, `eat:1`. Magnitude > -1 e soma por alvo > -1; a duração de dormir/comer em casa é dividida por (1 + magnitude), mínimo 1 h (ADR-0038) |
| `interactions[]` | `{action, profession?, skill?:{id,min}}`. Sem a lista, serve a quem tem ponto de uso; com ela, só as ações listadas e quem cumpre os requisitos |
| `Furnishings` | Chave `default` ou id do tipo de prédio → lista ordenada de ids |

## Onde vive
`ObjectCatalogScenarioLoader` valida na borda; `WorldState.ObjectCatalog` guarda; `InteriorProjector` entrega `objects` no
snapshot de interior (`/visual/subscribe?scope=Interior`); `client/godot/Scenes/object_factory.gd` desenha com mesh/material compartilhados.
`DefaultObjects` traz a mobília que era do `assets.json`.
O efeito vale para a moradia (`Household.Dwelling`) cujo tipo de prédio tem o objeto em `Furnishings` (os lares semeados têm tipo `-1`); a mobília padrão não declara efeitos, então o comportamento do mundo só muda quando o cenário os liga.
