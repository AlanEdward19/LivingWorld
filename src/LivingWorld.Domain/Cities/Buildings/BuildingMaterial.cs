using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Material de uma célula do footprint de um prédio (Fase 15.1, T45) — mesmo vocabulário
/// do placeholder client-side (`web/src/map-engine/buildingFootprint.ts`, `BuildingMaterial`).</summary>
public enum BuildingMaterial
{
    StoneWall,
    WoodWall,
    Door,
    Floor,

    /// <summary>Célula que conecta andares (Fase 15.1, T46/ADR-0018) — contrato de
    /// caminhabilidade (<see cref="InteriorWalkability"/>), sem geração real de planta por
    /// andar ainda (nenhum <see cref="BuildingFootprintGenerator"/> a emite hoje).</summary>
    Stair,
}

/// <summary>Célula do footprint com material (Fase 15.1, T45).</summary>
public sealed record FootprintCell(CellCoord Cell, BuildingMaterial Material);
