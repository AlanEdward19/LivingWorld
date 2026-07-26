namespace LivingWorld.Domain;

/// <summary>Célula do grid (task 1): terreno, bioma, altitude, água e recursos disponíveis.
/// Value object imutável — sem referência a UI.</summary>
public sealed record MapCell(
    CellCoord Coord,
    TerrainType Terrain,
    BiomeType Biome,
    int Altitude,
    bool HasWater,
    IReadOnlyList<ResourceType> Resources);

/// <summary>Região: agrupa células e é a unidade de consulta (task 1/4).</summary>
public sealed record Region(RegionId Id, IReadOnlyList<CellCoord> Cells);

/// <summary>Âncora de assentamento (task 6): só a referência para uma célula do grid —
/// crescer, migrar e fundar é Fase 8.</summary>
public sealed record SettlementAnchor(string Name, CellCoord Cell);
