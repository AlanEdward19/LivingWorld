# Casa de verdade: parede em aresta e cômodos (#226, M2 da fatia jogável #246)

Antes, a borda da planta inteira era parede-célula: a casa inicial 3×3 sobrava 1 célula de piso (a escada) e não cabia móvel.
Agora **toda célula da planta é piso** e a parede é uma **aresta entre duas células**. Uma casa 3×3 tem 9 pisos (4,5 × 4,5 m).
Código: `Domain/Cities/Buildings/` (`BuildingFootprintGenerator`, `BuildingLayoutGenerator`, `WallSegment`, `RoomPlanner`,
`RoomCatalog`, `FurniturePlanner`), `Simulation/Cities/BuildingRooms.cs`, `Simulation/Players/PlayerGround.cs`.

## Planta
- `FootprintCell.Material`: só `Floor`, `Door` (a célula de entrada: piso com a abertura na aresta de fora) e `Stair`.
  As células de parede (`StoneWall`/`WoodWall`) deixaram de existir; esses materiais ficam no **trecho de parede**.
- `FloorLayout.Walls`: uma `WallSegment {A, B, Material}` por aresta da borda (A antes de B por linha e coluna), sem a porta.
  `FloorLayout.Doors`: a abertura da porta da rua (só no térreo; nos andares de cima a aresta vira parede).
- A pegada não mudou (3×3 e 3–4 por lado): a vila não fica mais apertada, só o interior ganha espaço.

## Cômodos: dado do cenário, algoritmo no código
- O cenário declara em `Rooms` (ao lado de `Objects`/`Furnishings`): `kinds` (`id`, `uses` = ids de `ActionType` que o cômodo atende,
  `weight` = proporção da área, `minCells`) e `plans` (por tipo de prédio ou `"default"`; `"<tipo>@up"` para os andares de cima):
  a lista ordenada de cômodos. O primeiro é o que a porta da rua abre; os outros se encadeiam a partir dele.
- `RoomPlanner` (puro, sem RNG nem tick) divide o andar por cortes retos proporcionais ao peso, cada lado conexo, e liga
  os cômodos por **uma porta por ligação** de uma árvore que parte da entrada (a aresta de menor hash entre dois cômodos).
  Andar pequeno demais perde cômodos do fim do plano; com menos de dois fica um cômodo só. Sem `Rooms`, um cômodo só.
- Mobília: cada peça vai às células dos cômodos cujo `uses` casa com um `Action` dos pontos de uso do objeto (a cama no cômodo
  de dormir, a mesa no de comer); sem uso declarado ou sem cômodo que atenda, em qualquer um. Sempre mantém o caminho:
  toda célula livre continua ligada à porta atravessando só portas. Prefere encostar na parede (fundo da peça para ela).

## Passo e contrato
- `PlayerGround.TerrainRefusal` (e a máscara `GET /players/{id}/ground`, a mesma regra) barra o passo pela **aresta** fechada
  (de fora ou de cômodo); diagonal só sem tocar nenhuma aresta fechada; de fora para dentro só da soleira à célula da porta.
- Aditivo: `layout.floorLayouts[].walls/doors` (interior), `rooms[]` (`rooms`, `walls`, `doors` por andar) no snapshot do interior,
  `groundWalls`/`groundDoors` nos prédios da cidade e do escopo. O cliente desenha só onde o motor diz (`wall_panels.gd`).
- Célula de casa do domicílio (`DwellingCapacity.InteriorCellOf`): a de piso logo à frente da porta, por dentro.
- Sem compatibilidade com mundos salvos (decisão do dono, AD-215): a planta muda, goldens regravados.
