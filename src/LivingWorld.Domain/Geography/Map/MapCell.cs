namespace LivingWorld.Domain.Geography.Map;

/// <summary>Célula do grid (task 1): terreno, bioma, altitude, água e recursos disponíveis.
/// Value object imutável — sem referência a UI. <see cref="Temperature"/> é base gerada de
/// bioma/altitude (PWR-74), recomputável — o snapshot JSON omite o campo e reidrata via
/// <see cref="WithDerivedTemperature"/> (não é identidade canônica independente; overlays
/// causais vivem em <c>EnvironmentTemperatureAdjustment</c>).</summary>
public sealed record MapCell(
    CellCoord Coord,
    TerrainType Terrain,
    BiomeType Biome,
    int Altitude,
    bool HasWater,
    IReadOnlyList<ResourceType> Resources,
    float Temperature = 0)
{
    /// <summary>Constrói com temperatura base derivada de bioma/altitude.</summary>
    public static MapCell WithDerivedTemperature(
        CellCoord coord,
        TerrainType terrain,
        BiomeType biome,
        int altitude,
        bool hasWater,
        IReadOnlyList<ResourceType> resources) =>
        new(coord, terrain, biome, altitude, hasWater, resources, DeriveBase(biome, altitude));

    /// <summary>Base determinístico a partir de bioma/altitude — sem RNG extra.</summary>
    public static float DeriveBase(BiomeType biome, int altitude) =>
        10f + biome.Id * 2f - altitude;
}

/// <summary>Região: agrupa células e é a unidade de consulta (task 1/4).</summary>
public sealed record Region(RegionId Id, IReadOnlyList<CellCoord> Cells);

/// <summary>Âncora de assentamento (task 6): referência a uma célula do grid — crescer, migrar
/// e fundar é Fase 8. <see cref="Id"/>/<see cref="Orientation"/>/<see cref="Streets"/> (Fase
/// 15.1, T44) são autoria opcional do World Creator: sem eles, o assentamento ainda funciona
/// como âncora simples (id vazio, sem rotação, sem rua declarada).</summary>
public sealed record SettlementAnchor(
    string Name, CellCoord Cell, string Id = "", int Orientation = 0, IReadOnlyList<CellCoord>? Streets = null)
{
    public IReadOnlyList<CellCoord> Streets { get; init; } = Streets ?? [];
}
