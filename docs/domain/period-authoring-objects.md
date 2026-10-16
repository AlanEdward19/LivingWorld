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
| `usePoints` | Onde o corpo encosta; lido a partir da fatia 2 |
| `Furnishings` | Chave `default` ou id do tipo de prédio → lista ordenada de ids |

## Onde vive
`ObjectCatalogScenarioLoader` valida na borda; `WorldState.ObjectCatalog` guarda; `InteriorProjector` entrega `objects` no
snapshot de interior (`/visual/subscribe?scope=Interior`); `client/godot/Scenes/object_factory.gd` desenha com mesh/material compartilhados.
`DefaultObjects` traz a mobília que era do `assets.json`.
