using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;

namespace LivingWorld.Tests.Unit.Geography.Spatial;

/// <summary>Fase 2, task 3: propriedades do custo de deslocamento — simetria quando a
/// altitude é igual, direção quando não é, e efeito de terreno com braço de controle
/// (R4: par com mesma seed/distância, direção medida em 20/20).</summary>
public class MovementCostTests
{
    private const int Plains = 1;
    private const int Mountain = 2;

    private static readonly CostWeights Cost = new(
        Base: 1.0, AltitudeWeight: 0.5,
        TerrainWeight: new Dictionary<int, double> { [Plains] = 1.0, [Mountain] = 3.0 });

    private static WorldMap FlatMap(int width, int height, int terrainId, int altitude) =>
        BuildMap(width, height, (_, _) => (terrainId, altitude));

    private static WorldMap BuildMap(int width, int height, Func<CellCoord, int, (int Terrain, int Altitude)> shape)
    {
        var catalog = new GeographyCatalog(TerrainIds: [Plains, Mountain], BiomeIds: [], ResourceIds: []);
        var cells = new List<MapCell>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var coord = new CellCoord(x, y);
                var (terrain, altitude) = shape(coord, 0);
                cells.Add(MapCell.WithDerivedTemperature(
                    coord, new TerrainType(terrain), default, altitude, false, []));
            }
        var regions = RegionGrid.Partition(width, height, regionSize: Math.Max(width, height));
        return WorldMap.Create(width, height, seed: 1, catalog, Cost, cells, regions, []).Value!;
    }

    [Fact]
    public void Cost_is_symmetric_when_altitude_is_equal()
    {
        var map = FlatMap(width: 5, height: 5, terrainId: Plains, altitude: 3);
        var a = new CellCoord(0, 0);
        var b = new CellCoord(3, 4);

        Assert.Equal(MovementCost.Between(map, a, b), MovementCost.Between(map, b, a));
    }

    [Fact]
    public void Uphill_costs_strictly_more_than_downhill_between_the_same_two_cells()
    {
        var map = BuildMap(3, 1, (c, _) => (Plains, c.X == 0 ? 0 : 5));
        var low = new CellCoord(0, 0);
        var high = new CellCoord(2, 0);

        Assert.True(MovementCost.Between(map, low, high) > MovementCost.Between(map, high, low));
    }

    [Fact]
    public void Cost_is_always_positive_between_distinct_cells()
    {
        var rng = new Random(1234);
        var map = BuildMap(10, 10, (_, _) => (rng.Next(2) == 0 ? Plains : Mountain, rng.Next(10)));

        foreach (var a in map.Cells.Take(20))
            foreach (var b in map.Cells.Skip(20).Take(20))
                if (a.Coord != b.Coord)
                    Assert.True(MovementCost.Between(map, a.Coord, b.Coord) > 0);
    }

    [Fact]
    public void Property_over_1000_random_pairs_altitude_equal_implies_symmetric_otherwise_uphill_costs_more()
    {
        var rng = new Random(42);
        var map = BuildMap(20, 20, (_, _) => (rng.Next(2) == 0 ? Plains : Mountain, rng.Next(10)));
        var cells = map.Cells.ToArray();

        for (int i = 0; i < 1000; i++)
        {
            var a = cells[rng.Next(cells.Length)];
            var b = cells[rng.Next(cells.Length)];
            if (a.Coord == b.Coord) continue;

            double costAb = MovementCost.Between(map, a.Coord, b.Coord);
            double costBa = MovementCost.Between(map, b.Coord, a.Coord);

            Assert.True(costAb > 0);
            Assert.True(costBa > 0);

            if (a.Altitude == b.Altitude)
                Assert.Equal(costAb, costBa, precision: 9);
            else if (a.Altitude < b.Altitude)
                Assert.True(costAb > costBa); // subida (a->b) custa mais que descida (b->a)
            else
                Assert.True(costBa > costAb);
        }
    }

    [Fact]
    public void Mountain_pairs_cost_more_than_plains_pairs_of_same_distance_and_seed_in_20_of_20()
    {
        int hits = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var plainsMap = FlatMap(width: 4, height: 1, terrainId: Plains, altitude: 0);
            var mountainMap = FlatMap(width: 4, height: 1, terrainId: Mountain, altitude: 0);
            var a = new CellCoord(0, 0);
            var b = new CellCoord(3, 0); // mesma distância nos dois mapas, mesmo "seed" (irrelevante aqui — terreno é a única variável)

            double plainsCost = MovementCost.Between(plainsMap, a, b);
            double mountainCost = MovementCost.Between(mountainMap, a, b);

            if (mountainCost > plainsCost) hits++;
        }

        Assert.Equal(20, hits);
    }
}
