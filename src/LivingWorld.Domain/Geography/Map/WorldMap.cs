using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Geography.Map;

/// <summary>Geografia do mundo (Fase 2): grid de células, regiões, catálogo, custo de
/// deslocamento e âncoras de assentamento. Value-object-like — imutável após construção.
/// Os índices de consulta (célula→região, região→vizinhas) são reconstruídos a cada
/// construção/rehidratação a partir dos dados canônicos abaixo; nunca são, eles próprios,
/// serializados — não há nada extra para classificar como volátil no hash (task 4).</summary>
public sealed class WorldMap
{
    public int Width { get; }
    public int Height { get; }
    public ulong Seed { get; }
    public GeographyCatalog Catalog { get; }
    public CostWeights Cost { get; }
    public IReadOnlyList<MapCell> Cells { get; }
    public IReadOnlyList<Region> Regions { get; }
    public IReadOnlyList<SettlementAnchor> Settlements { get; }

    private readonly Dictionary<CellCoord, MapCell> _cellByCoord;
    private readonly Dictionary<CellCoord, RegionId> _regionByCell;
    private readonly Dictionary<RegionId, List<RegionId>> _neighborsByRegion;

    public WorldMap(
        int width, int height, ulong seed, GeographyCatalog catalog, CostWeights cost,
        IReadOnlyList<MapCell> cells, IReadOnlyList<Region> regions, IReadOnlyList<SettlementAnchor> settlements)
    {
        Width = width;
        Height = height;
        Seed = seed;
        Catalog = catalog;
        Cost = cost;
        Cells = cells;
        Regions = regions;
        Settlements = settlements;

        _cellByCoord = cells.ToDictionary(c => c.Coord);
        _regionByCell = regions.SelectMany(r => r.Cells.Select(c => (c, r.Id))).ToDictionary(t => t.c, t => t.Id);
        _neighborsByRegion = BuildNeighbors(regions, _regionByCell);
    }

    public MapCell CellAt(CellCoord coord) => _cellByCoord[coord];

    public bool TryGetCell(CellCoord coord, out MapCell cell) => _cellByCoord.TryGetValue(coord, out cell!);

    public RegionId RegionOf(CellCoord coord) => _regionByCell[coord];

    public IReadOnlyList<CellCoord> CellsOf(RegionId region) =>
        Regions.Single(r => r.Id == region).Cells;

    public IReadOnlyList<RegionId> NeighborsOf(RegionId region) => _neighborsByRegion[region];

    /// <summary>Constrói e valida (task 5/6): falha rápido no primeiro campo inválido,
    /// nomeando o campo — nunca explode depois de o mundo já existir.</summary>
    public static Result<WorldMap> Create(
        int width, int height, ulong seed, GeographyCatalog catalog, CostWeights cost,
        IReadOnlyList<MapCell> cells, IReadOnlyList<Region> regions, IReadOnlyList<SettlementAnchor> settlements)
    {
        if (width <= 0 || height <= 0)
            return Result<WorldMap>.Fail($"Width/Height: dimensões devem ser positivas ({width}x{height})");

        if (cost.Base <= 0)
            return Result<WorldMap>.Fail("CostWeights.Base: deve ser positivo");
        if (cost.AltitudeWeight < 0)
            return Result<WorldMap>.Fail("CostWeights.AltitudeWeight: não pode ser negativo");
        foreach (var (terrainId, weight) in cost.TerrainWeight)
            if (weight <= 0)
                return Result<WorldMap>.Fail($"CostWeights.TerrainWeight[{terrainId}]: deve ser positivo");

        var expected = new HashSet<CellCoord>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                expected.Add(new CellCoord(x, y));

        var seen = new HashSet<CellCoord>();
        foreach (var cell in cells)
        {
            if (!expected.Contains(cell.Coord))
                return Result<WorldMap>.Fail($"Cells: célula {cell.Coord} fora do grid {width}x{height}");
            if (!seen.Add(cell.Coord))
                return Result<WorldMap>.Fail($"Cells: célula {cell.Coord} duplicada");
            if (!catalog.IsValidTerrain(cell.Terrain))
                return Result<WorldMap>.Fail($"Cells[{cell.Coord}].Terrain: id {cell.Terrain.Id} inválido ou Unset");
            if (!catalog.IsValidBiome(cell.Biome))
                return Result<WorldMap>.Fail($"Cells[{cell.Coord}].Biome: id {cell.Biome.Id} inválido");
            foreach (var resource in cell.Resources)
                if (!catalog.IsValidResource(resource))
                    return Result<WorldMap>.Fail($"Cells[{cell.Coord}].Resources: id {resource.Id} inválido");
        }

        if (seen.Count != expected.Count)
            return Result<WorldMap>.Fail($"Cells: grid {width}x{height} incompleto ({seen.Count}/{expected.Count} células)");

        var regionCells = new HashSet<CellCoord>();
        foreach (var region in regions)
            foreach (var coord in region.Cells)
                if (!regionCells.Add(coord))
                    return Result<WorldMap>.Fail($"Regions: célula {coord} pertence a mais de uma região");

        if (regionCells.Count != expected.Count || !regionCells.SetEquals(expected))
            return Result<WorldMap>.Fail("Regions: nem toda célula do grid pertence a exatamente uma região");

        foreach (var settlement in settlements)
        {
            if (!expected.Contains(settlement.Cell))
                return Result<WorldMap>.Fail($"Settlements[{settlement.Name}]: célula {settlement.Cell} fora do grid {width}x{height}");
            foreach (var street in settlement.Streets)
                if (!expected.Contains(street))
                    return Result<WorldMap>.Fail($"Settlements[{settlement.Name}].Streets: célula {street} fora do grid {width}x{height}");
        }

        return Result<WorldMap>.Ok(new WorldMap(width, height, seed, catalog, cost, cells, regions, settlements));
    }

    private static Dictionary<RegionId, List<RegionId>> BuildNeighbors(
        IReadOnlyList<Region> regions, Dictionary<CellCoord, RegionId> regionByCell)
    {
        var result = regions.ToDictionary(r => r.Id, _ => new HashSet<RegionId>());
        int[] dx = [1, -1, 0, 0];
        int[] dy = [0, 0, 1, -1];

        foreach (var region in regions)
        {
            foreach (var coord in region.Cells)
            {
                for (int i = 0; i < 4; i++)
                {
                    var neighborCoord = new CellCoord(coord.X + dx[i], coord.Y + dy[i]);
                    if (regionByCell.TryGetValue(neighborCoord, out var neighborRegion) && neighborRegion != region.Id)
                        result[region.Id].Add(neighborRegion);
                }
            }
        }

        return result.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
    }
}
