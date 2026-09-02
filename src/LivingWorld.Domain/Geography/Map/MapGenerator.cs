using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Geography.Map;

/// <summary>Gerador procedural de mapa (task 5): mesma seed → mesmo mapa, sempre via
/// <see cref="WorldRng"/> (nunca System.Random). Garante zero células <see cref="TerrainType.Unset"/> —
/// todo terreno vem de <paramref name="catalog"/>.</summary>
public static class MapGenerator
{
    public static Result<WorldMap> Generate(
        ulong seed, int width, int height, int regionSize,
        GeographyCatalog catalog, CostWeights cost, IReadOnlyList<SettlementAnchor> settlements)
    {
        if (catalog.TerrainIds.Count == 0)
            return Result<WorldMap>.Fail("Catalog.TerrainIds: precisa de ao menos um terreno para gerar");

        var terrainIds = catalog.TerrainIds.OrderBy(id => id).ToArray();
        var biomeIds = catalog.BiomeIds.OrderBy(id => id).ToArray();
        var resourceIds = catalog.ResourceIds.OrderBy(id => id).ToArray();
        var rng = new WorldRng(seed);

        var cells = new List<MapCell>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var terrain = new TerrainType(terrainIds[(int)(rng.NextDouble() * terrainIds.Length)]);
                var biome = biomeIds.Length > 0
                    ? new BiomeType(biomeIds[(int)(rng.NextDouble() * biomeIds.Length)])
                    : default;
                int altitude = (int)(rng.NextDouble() * 10);
                bool hasWater = rng.NextDouble() < 0.1;

                var resources = resourceIds.Length > 0 && rng.NextDouble() < 0.3
                    ? new ResourceType[] { new(resourceIds[(int)(rng.NextDouble() * resourceIds.Length)]) }
                    : Array.Empty<ResourceType>();

                cells.Add(new MapCell(
                    new CellCoord(x, y), terrain, biome, altitude, hasWater, resources,
                    MapCell.DeriveBase(biome, altitude)));
            }
        }

        var regions = RegionGrid.Partition(width, height, regionSize);

        return WorldMap.Create(width, height, seed, catalog, cost, cells, regions, settlements);
    }
}
