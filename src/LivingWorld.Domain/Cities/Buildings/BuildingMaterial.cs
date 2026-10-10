using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Material de uma célula do footprint de um prédio — mesmo vocabulário
/// do placeholder client-side (`web/src/map-engine/buildingFootprint.ts`, `BuildingMaterial`).</summary>
public enum BuildingMaterial
{
    StoneWall,
    WoodWall,
    Door,
    Floor,

    /// <summary>Célula que conecta andares — contrato de
    /// caminhabilidade (<see cref="InteriorWalkability"/>), sem geração real de planta por
    /// andar ainda (nenhum <see cref="BuildingFootprintGenerator"/> a emite hoje).</summary>
    Stair,
}

public sealed record FootprintCell(CellCoord Cell, BuildingMaterial Material);
