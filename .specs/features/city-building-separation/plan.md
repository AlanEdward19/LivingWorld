# City building separation [Estado: Implementado; verificação independente pendente]

## Problem

Prédios derivados (sem posição autorada) pousavam em qualquer célula livre: encostavam uns nos outros e
caíam em células de água. No cliente isso aparece como casas fundidas num bloco só e telhados sobre rio.
Evidência: `CityOccupancy.ScanForFreeOrigin` e `OverflowPlacer.FitsAt` só checavam sobreposição de célula.

Quando isto entrar, todo prédio derivado deixa 1 célula livre de qualquer outro e nunca pousa em água,
e quando falta terra ele relaxa a regra em degraus em vez de falhar ou colapsar.

## Flow

Só o servidor (`LivingWorld.Simulation`); nada persistido, a posição segue sendo função pura do mundo.

1. `PlacementPolicy` (new) - folga mínima + água do mapa, em 3 degraus de relaxamento
2. `CityOccupancy.ScanForFreeOriginFrom` (exists, changed) - varredura dos bounds com a política
3. `OverflowPlacer.ResolveOverflowPositionGiven` (exists, changed) - anéis com a política
4. `BuildingPlacementResolver.Resolve` (exists, changed) - desce de degrau só sem terra no mapa inteiro
5. `CityOccupancy.PlanFor` (exists, changed) - mesmo degrau para os vizinhos derivados (memoização)
6. `PopulationSeeder.PlaceHouseholdBuildings` (exists, changed) - sem terra, o domicílio ocupa um andar livre
   de uma casa existente (N andares = N domicílios) em vez de empilhar outro footprint

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: folga mínima, degrau de relaxamento |
| stored data | none - hash canônico muda onde há prédio derivado (posições novas) |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. degraus de relaxamento | 0: folga 1 + sem água; 1: folga 0 + sem água; 2: só sem sobreposição (regra antiga) | falhar sem posição: mapas pequenos (10x10 do cenário padrão) não comportam a folga |

- Extensão lateral (ala) não existe no motor: o footprint vem do id. A "extensão" desta fatia é vertical: sem
  terra para um footprint novo, o domicílio sobe para um andar livre de casa existente. Esgotados os andares,
  cai no anel legado (pode empilhar): limite conhecido em mapa muito apertado (medieval 10x10 com 45+ NPCs).

## Criteria

### S1: Separação e água (P1)

1. WHEN dois prédios derivados da mesma cidade são posicionados THEN the system SHALL deixar ≥ 1 célula livre
   entre os footprints (degrau 0)
2. The system SHALL NOT posicionar footprint em célula de água enquanto existir célula seca no mapa
3. WHEN o degrau 0 não acha célula em todo o mapa THEN the system SHALL tentar o degrau 1 e depois o 2

### S2: Determinismo e custo (P1)

4. WHEN não há terra para uma casa nova THEN the system SHALL abrigar o domicílio num andar livre de casa
   existente, nunca acima do número de andares dela
5. The system SHALL produzir as mesmas posições para a mesma seed e a mesma ordem de prédios
6. The system SHALL manter o custo de criar o mundo (6000 NPCs) em até +15% sobre o anterior

**Independent test:** `bash scripts/test.sh --filter BuildingSeparationTests`.

## Out of scope

| Excluded | Why |
| --- | --- |
| prédios autorados (`Position` informada) | autoria tem precedência |
| extensão lateral e andares por falta de terra | exigiria mudar footprint/layout (contrato visual) |
| `IsLandScarce` | segue medindo "nenhuma célula", sem folga nem água |
| tamanho dos bounds da cidade | o mapa-múndi cabe poucas casas com folga; retunar `SideFor` muda contrato visual |

## Sources

- `src/LivingWorld.Simulation/Cities/CityOccupancy.cs`, `.../Founding/OverflowPlacer.cs`
- `.specs/features/city-house-layout`, `dynamic-city-growth`, `directional-city-bounds-growth`
