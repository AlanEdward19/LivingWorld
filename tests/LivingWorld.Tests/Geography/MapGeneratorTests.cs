using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Geography;

/// <summary>Fase 2, task 2/4/5: geração procedural determinística, detecção de
/// <see cref="TerrainType.Unset"/> e cobertura de região por enumeração completa.</summary>
public class MapGeneratorTests
{
    private static readonly GeographyCatalog Catalog = new(
        TerrainIds: [1, 2, 3], BiomeIds: [1, 2], ResourceIds: [1]);

    private static readonly CostWeights Cost = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0, [2] = 1.5, [3] = 3.0 });

    private static WorldMap GenerateOrThrow(ulong seed) =>
        MapGenerator.Generate(seed, width: 10, height: 10, regionSize: 4, Catalog, Cost, []).Value!;

    [Theory]
    [MemberData(nameof(TwentySeeds))]
    public void Generated_grid_has_zero_unset_cells_and_at_least_two_distinct_terrains(ulong seed)
    {
        var map = GenerateOrThrow(seed);

        Assert.DoesNotContain(map.Cells, c => c.Terrain == TerrainType.Unset);
        Assert.True(map.Cells.Select(c => c.Terrain).Distinct().Count() >= 2,
            $"seed {seed}: apenas um terreno distinto apareceu no grid");
    }

    [Theory]
    [MemberData(nameof(TwentySeeds))]
    public void Same_seed_generates_byte_identical_map_twice(ulong seed)
    {
        var a = GenerateOrThrow(seed);
        var b = GenerateOrThrow(seed);

        Assert.Equal(
            a.Cells.Select(c => (c.Coord, c.Terrain, c.Biome, c.Altitude, c.HasWater, c.Temperature)),
            b.Cells.Select(c => (c.Coord, c.Terrain, c.Biome, c.Altitude, c.HasWater, c.Temperature)));
    }

    [Fact]
    public void Different_seed_generates_a_different_map()
    {
        var a = GenerateOrThrow(1);
        var b = GenerateOrThrow(2);

        Assert.NotEqual(
            a.Cells.Select(c => c.Terrain),
            b.Cells.Select(c => c.Terrain));
    }

    [Fact]
    public void Every_cell_belongs_to_exactly_one_region_and_round_trips_via_region_query()
    {
        var map = GenerateOrThrow(seed: 7);

        foreach (var cell in map.Cells)
        {
            var region = map.RegionOf(cell.Coord);
            Assert.Contains(cell.Coord, map.CellsOf(region));
        }

        var coveredByRegions = map.Regions.SelectMany(r => r.Cells).ToHashSet();
        Assert.Equal(map.Cells.Count, coveredByRegions.Count);
        foreach (var cell in map.Cells)
            Assert.Contains(cell.Coord, coveredByRegions);
    }

    [Fact]
    public void Every_cell_temperature_is_derived_from_biome_and_altitude_without_extra_rng()
    {
        var map = GenerateOrThrow(seed: 11);

        Assert.All(map.Cells, cell =>
            Assert.Equal(MapCell.DeriveBase(cell.Biome, cell.Altitude), cell.Temperature));
    }

    [Fact]
    public void Snapshot_json_omits_cell_temperature_and_rehydrates_the_derived_base()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 0);
        var json = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        var firstCell = json["Map"]!["Cells"]![0]!.AsObject();
        Assert.False(firstCell.ContainsKey("Temperature"));

        var rehydrated = WorldSnapshot.Deserialize(json.ToJsonString());
        Assert.All(rehydrated.Map.Cells, cell =>
            Assert.Equal(MapCell.DeriveBase(cell.Biome, cell.Altitude), cell.Temperature));
    }

    public static IEnumerable<object[]> TwentySeeds() =>
        Enumerable.Range(1, 20).Select(i => new object[] { (ulong)i });
}
