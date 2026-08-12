namespace LivingWorld.Domain;

/// <summary>Material de uma célula do footprint de um prédio (Fase 15.1, T45) — mesmo vocabulário
/// do placeholder client-side (`web/src/map-engine/buildingFootprint.ts`, `BuildingMaterial`).</summary>
public enum BuildingMaterial
{
    StoneWall,
    WoodWall,
    Door,
    Floor,
}

/// <summary>Célula do footprint com material (Fase 15.1, T45).</summary>
public sealed record FootprintCell(CellCoord Cell, BuildingMaterial Material);
