namespace LivingWorld.Domain;

/// <summary>Planta determinística por prédio (Fase 15.1, T45; porta canônica do placeholder
/// client-side em `web/src/map-engine/buildingFootprint.ts`): retângulo ou L (nunca aleatório —
/// mesmo <see cref="BuildingId"/>+<see cref="Building.BuildingTypeId"/> sempre a mesma planta),
/// parede com material por paridade de tipo, e exatamente uma porta que **sempre** pertence ao
/// footprint (diferente do placeholder client-side, que às vezes fica sem porta se a célula
/// preferida não for parede — aqui a porta cai no fallback determinístico mais próximo quando a
/// preferida não existe, nunca fica ausente). Sem parâmetro de andar: a função não lê tick nem
/// estado do mundo, então "estável entre snapshots, ticks e andares" (T45) é consequência direta
/// de ser pura.</summary>
public static class BuildingFootprintGenerator
{
    public static IReadOnlyList<FootprintCell> Generate(BuildingId buildingId, int buildingTypeId)
    {
        ulong h = StableHash.Mix(buildingId.Value ^ ((long)buildingTypeId << 32));

        var wallMaterial = buildingTypeId % 2 == 0 ? BuildingMaterial.StoneWall : BuildingMaterial.WoodWall;
        int width = 4 + (int)(h % 3);
        int height = 3 + (int)((h >> 3) % 3);
        bool isLShape = h % 5 == 0;

        bool InBase(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        bool InNotch(int x, int y) => isLShape && x >= width / 2 && y >= height / 2;
        bool InShape(int x, int y) => InBase(x, y) && !InNotch(x, y);

        var cells = new List<(int X, int Y, bool IsWall)>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!InShape(x, y)) continue;
                bool isWall = !InShape(x - 1, y) || !InShape(x + 1, y) || !InShape(x, y - 1) || !InShape(x, y + 1);
                cells.Add((x, y, isWall));
            }

        int doorX = isLShape ? width / 4 : width / 2;
        int doorIndex = cells.FindIndex(c => c.IsWall && c.X == doorX && c.Y == height - 1);
        if (doorIndex < 0)
            // Fallback determinístico (garante que a porta SEMPRE existe, T45): primeira parede
            // na borda inferior, senão a primeira parede da planta, em ordem estável (y desc, x asc).
            doorIndex = cells
                .Select((c, i) => (c, i))
                .Where(t => t.c.IsWall)
                .OrderByDescending(t => t.c.Y)
                .ThenBy(t => t.c.X)
                .Select(t => t.i)
                .First();

        var result = new FootprintCell[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            var (x, y, isWall) = cells[i];
            var material = i == doorIndex ? BuildingMaterial.Door : isWall ? wallMaterial : BuildingMaterial.Floor;
            result[i] = new FootprintCell(new CellCoord(x, y), material);
        }
        return result;
    }
}
