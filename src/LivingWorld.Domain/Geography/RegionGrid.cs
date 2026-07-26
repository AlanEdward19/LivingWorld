namespace LivingWorld.Domain;

/// <summary>Particionamento determinístico de um grid width×height em regiões de blocos
/// regionSize×regionSize (task 1/4). Não depende de RNG — mesma entrada, mesma partição
/// sempre. Reusado pelo gerador procedural e pelo carregamento de mapa autoral.</summary>
public static class RegionGrid
{
    public static IReadOnlyList<Region> Partition(int width, int height, int regionSize)
    {
        var blocks = new Dictionary<RegionId, List<CellCoord>>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int blockX = x / regionSize;
                int blockY = y / regionSize;
                int blocksPerRow = (width + regionSize - 1) / regionSize;
                var id = new RegionId(blockY * blocksPerRow + blockX);

                if (!blocks.TryGetValue(id, out var cells))
                    blocks[id] = cells = [];
                cells.Add(new CellCoord(x, y));
            }
        }

        return blocks
            .OrderBy(kv => kv.Key.Value)
            .Select(kv => new Region(kv.Key, kv.Value))
            .ToArray();
    }
}
