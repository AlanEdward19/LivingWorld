# Postos de trabalho (#223)

Quem trabalha no prédio do ofício não fica empilhado no ladrilho dele: o motor dá a cada trabalhador
um **posto** (célula do térreo, em coordenadas do mundo) com lotação por célula.

## Regra
- Lotação por célula: `PlayerRules.CellCrowd` (3), a mesma que barra o jogador.
- **Lavoura** (oficina tipo fazenda): o posto é uma célula do **campo**, o chão livre em volta da planta
  (sem prédio, sem água; rua só se não sobra campo). Quem lavra não fica dentro do prédio.
- Outros ofícios: células de piso do térreo; quando enchem, o chão em volta.
- Escolha: a célula menos cheia (a mais perto no empate). Sem RNG; só depende da ordem dos NPCs.
- Sem vaga em nenhum posto: o NPC espera fora (sem `Interior`), nunca se empilha.

## Onde mora
- `Simulation/Cities/WorkStands.cs` (postos e escolha) e `BuildingOccupancySystem` (aplica, hora a hora).
- O posto é gravado em `Npc.Interior` (andar 0, célula local, pode cair fora da planta no campo).
- `NpcVisual.Location` leva o posto de quem trabalha; o cliente desenha onde o motor diz.
- `InteriorProjector` não lista como ocupante quem está no campo (fora da planta).
- `Npc.CurrentLocation` segue na célula do local (presença, rotas e decisão não mudam).
