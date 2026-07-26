using LivingWorld.Domain;

namespace LivingWorld.Tests.Geography;

/// <summary>Fase 2, task 3: pathfinding mínimo entre locais — base de rota comercial
/// (Fase 5) e migração (Fase 8).</summary>
public class MapPathfinderTests
{
    private static readonly GeographyCatalog Catalog = new(TerrainIds: [1, 2], BiomeIds: [], ResourceIds: []);
    private static readonly CostWeights Cost = new(
        Base: 1.0, AltitudeWeight: 0.0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0, [2] = 5.0 });

    [Fact]
    public void Shortest_cost_between_adjacent_cells_equals_the_direct_movement_cost()
    {
        var map = FlatMap(3, 3, terrainId: 1);
        var a = new CellCoord(0, 0);
        var b = new CellCoord(1, 0);

        var result = MapPathfinder.ShortestCost(map, a, b);

        Assert.True(result.IsSuccess);
        Assert.Equal(MovementCost.Between(map, a, b), result.Value, precision: 9);
    }

    [Fact]
    public void Shortest_cost_routes_around_expensive_terrain_instead_of_through_it()
    {
        // coluna do meio (x=1) é toda terreno caro; o caminho ótimo desvia por y=2.
        var map = BuildMap(3, 3, coord => coord.X == 1 && coord.Y < 2 ? 2 : 1);
        var start = new CellCoord(0, 0);
        var goal = new CellCoord(2, 0);

        var direct = MovementCost.Between(map, start, new CellCoord(1, 0)) + MovementCost.Between(map, new CellCoord(1, 0), goal);
        var result = MapPathfinder.ShortestCost(map, start, goal);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value < direct);
    }

    [Fact]
    public void Start_outside_the_grid_fails_naming_the_field()
    {
        var map = FlatMap(2, 2, terrainId: 1);

        var result = MapPathfinder.ShortestCost(map, new CellCoord(-1, -1), new CellCoord(0, 0));

        Assert.False(result.IsSuccess);
        Assert.Contains("start", result.Error);
    }

    private static WorldMap FlatMap(int width, int height, int terrainId) => BuildMap(width, height, _ => terrainId);

    private static WorldMap BuildMap(int width, int height, Func<CellCoord, int> terrainOf)
    {
        var cells = new List<MapCell>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var coord = new CellCoord(x, y);
                cells.Add(new MapCell(coord, new TerrainType(terrainOf(coord)), default, Altitude: 0, HasWater: false, []));
            }
        var regions = RegionGrid.Partition(width, height, regionSize: Math.Max(width, height));
        return WorldMap.Create(width, height, seed: 1, Catalog, Cost, cells, regions, []).Value!;
    }
}
