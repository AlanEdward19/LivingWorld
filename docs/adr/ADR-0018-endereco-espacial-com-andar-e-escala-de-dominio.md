# ADR-0018: Endereço espacial com andar, e escala World/City/Building como dado de domínio

- **Status**: aceito
- **Data**: 2026-08-12
- **Decisores**: Alan

## Contexto

`.specs/features/phase-15.1-vtt-frontend-redesign/backend-gaps.md` (G5) registra que a escala
entre `WorldSpace`/`CitySpace`/`BuildingSpace` (`web/src/map-engine/space.ts:20-25`, `SCALE`) só
existe como constante do **cliente**, e que `CellCoord` (`src/LivingWorld.Domain/Geography/GeographyIds.cs:5`)
não representa andar/Z — o comentário em `space.ts:1-12` já documentava isso como
`SPEC_DEVIATION` esperando este ADR. G6 registra que o interior de um prédio não expõe pisos,
paredes, portas, escadas ou células caminháveis — só a identidade do prédio
(`InteriorProjector.cs`, `OccupancyModeled: false`).

`SpatialPortal` (T21, já planejada em `tasks.md:826`) depende explicitamente do "endereço
espacial/andares definido em T46" — este ADR é o que T21 vai importar.

## Decisão

### Escala vira dado do domínio, não do cliente
`SpaceScale` (`LivingWorld.Domain`) declara as mesmas duas constantes que hoje só existem em
`space.ts` (`WorldTilesPerCityTile = 20`, `CityTilesPerBuildingTile = 6`) e as duas
transformações (`ToParent`/`ToChild`). Valores preservados de propósito — não há unidade física
real de onde derivá-los (mesmo comentário do cliente: "valores de produto até o domínio expor
algo mensurável"), então mover a constante sem mudar o valor não altera nada visualmente quando
o cliente trocar de fonte (mesmo espírito do fallback de `CityBoundsResolver`, T45).

### Bounds de World/City/Building reaproveitam o mesmo shape
`SpatialBoundsResolver` devolve `CityBounds` (T45 — `Origin`/`Width`/`Height`, nome herdado, uso
generalizado) para os três níveis:
- **World**: direto de `WorldMap.Width/Height`.
- **City**: delega a `CityBoundsResolver.Resolve` (T45), sem duplicar a fórmula.
- **Building**: dimensões do próprio `BuildingFootprintGenerator.Generate` (T45) — o footprint
  **é** o bounds do interior; não existe um segundo número de tamanho de prédio no domínio, e
  criar um agora duplicaria a fonte de verdade sem necessidade.

### Andar é um inteiro sem unidade, endereço espacial é a tupla completa
```csharp
public readonly record struct FloorLevel(int Value) { public static readonly FloorLevel Ground = new(0); }
public readonly record struct SpatialAddress(SpaceKind Kind, Guid RefId, FloorLevel Floor, CellCoord Cell);
```
`RefId` é ignorado para `SpaceKind.World` (mesma convenção de `VisualScope.RefId`,
`src/LivingWorld.Api/Visual/VisualScope.cs:9-19`). Andar nunca é `[Canonical]` sozinho — é
componente de um endereço que outra estrutura (ex. `SpatialPortal`, `Npc`, T47) decide se
persiste; este ADR não adiciona nenhum campo canônico novo.

### Parede/porta/escada/caminhabilidade estendem o vocabulário de T45, não duplicam
`BuildingMaterial` (T45) ganha `Stair`. `InteriorWalkability.IsWalkable(material)` é o contrato de
caminhabilidade: `Floor`/`Door`/`Stair` são caminháveis, `StoneWall`/`WoodWall` não. Nenhum novo
tipo de célula paralelo a `FootprintCell` — escada é só outro material na mesma planta.
`FloorNavigator.Up`/`Down` são aritmética pura (`+1`/`-1`) — reversibilidade é a prova de que não
há estado escondido na navegação vertical.

## Fora do escopo (deliberado)

Este ADR define o **endereço** e os **contratos** (tipos), não gera pisos/paredes/escadas reais
para os prédios existentes — isso exigiria um algoritmo de planta-por-andar que nenhuma task
pediu ainda (G6/G7 continuam abertos para consumo real; T47 é o próximo a usar este endereço
para ocupação de NPC). `BuildingFootprintGenerator` (T45) continua sem parâmetro de andar de
propósito: a planta é a mesma em qualquer `FloorLevel` até uma task futura pedir variação real
por andar — isso preserva a garantia de estabilidade já testada em T45.

## Alternativas consideradas

- **`CellCoord` ganha um terceiro campo `Z`** — rejeitada: `CellCoord` é usado em todo o domínio
  (mapa-múndi, cidade) onde "andar" não faz sentido nenhum; forçar todo `CellCoord` existente a
  carregar `Z=0` é ruído em milhares de usos para resolver um problema só do `BuildingSpace`.
- **Enum de espaço próprio duplicado por task (`PortalSpaceKind` em T21, outro em T46)** —
  rejeitada aqui: `SpaceKind` (`World`/`City`/`Building`) é definido uma vez neste ADR; T21 reusa
  em vez de declarar o seu.

## Consequências

- **Positivas**: cliente pode, no futuro, ler escala e bounds do endereço real em vez de
  constante própria, sem mudar comportamento visual (mesmos valores); T21/T47 têm endereço
  espacial pronto para importar; nenhum campo canônico novo, nenhum golden regravado.
- **Negativas / trade-offs**: `SpatialAddress`/`FloorLevel` ficam sem consumidor real até T21/T47
  landarem — é contrato adiantado, não funcionalidade visível ainda.
- **Follow-ups**: T47 usa `SpatialAddress` para ocupação de NPC; T21 usa `SpaceKind` para
  `PortalEndpoint`; geração real de planta por andar fica para quando uma task pedir.
