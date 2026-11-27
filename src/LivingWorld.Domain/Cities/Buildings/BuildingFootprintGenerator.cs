using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Planta determinística por prédio (porta canônica do placeholder
/// client-side em `web/src/map-engine/buildingFootprint.ts`): retângulo ou L (nunca aleatório —
/// mesmo <see cref="BuildingId"/>+<see cref="Building.BuildingTypeId"/> sempre a mesma planta),
/// parede com material por paridade de tipo, e exatamente uma porta que **sempre** pertence ao
/// footprint (diferente do placeholder client-side, que às vezes fica sem porta se a célula
/// preferida não for parede — aqui a porta cai no fallback determinístico mais próximo quando a
/// preferida não existe, nunca fica ausente). Sem parâmetro de andar: a função não lê tick nem
/// estado do mundo, então "estável entre snapshots, ticks e andares" é consequência direta
/// de ser pura.</summary>
public static class BuildingFootprintGenerator
{
    public static int DerivedOrientation(BuildingId buildingId, int buildingTypeId)
    {
        ulong h = StableHash.Mix(buildingId.Value ^ ((long)buildingTypeId << 32));
        return (int)(h % 4) * 90;
    }

    // Função pura de (id, tipo, orientação normalizada): memoizada, pois a planta de cada prédio é
    // relida a cada tick por vários sistemas (ocupação, colocação, locomoção). Teto evita crescer sem
    // limite em mundos com milhões de prédios.
    private const int MaxCachedFootprints = 100_000;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        (long Id, int Type, int Orientation), IReadOnlyList<FootprintCell>> Cache = new();

    public static IReadOnlyList<FootprintCell> Generate(
        BuildingId buildingId, int buildingTypeId, int? orientation = null)
    {
        var key = (buildingId.Value, buildingTypeId, NormalizeOrientation(orientation ?? 0));
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var footprint = Compute(buildingId, buildingTypeId, key.Item3);
        if (Cache.Count >= MaxCachedFootprints)
            Cache.Clear();
        Cache[key] = footprint;
        return footprint;
    }

    /// <summary>Material das paredes do prédio (paridade do tipo): a parede é aresta, o material mora no trecho (<see cref="WallSegment"/>).</summary>
    public static BuildingMaterial WallMaterialOf(int buildingTypeId) =>
        buildingTypeId % 2 == 0 ? BuildingMaterial.StoneWall : BuildingMaterial.WoodWall;

    /// <summary>A porta e o lado para onde ela abre (o primeiro vizinho de 4, na ordem baixo, direita, cima, esquerda, que não é da planta).</summary>
    public static (CellCoord Door, CellCoord Facing) DoorOf(IReadOnlyList<FootprintCell> footprint)
    {
        var cells = new HashSet<CellCoord>(footprint.Count);
        foreach (var c in footprint) cells.Add(c.Cell);
        var door = footprint.First(c => c.Material == BuildingMaterial.Door).Cell;
        foreach (var d in DoorDirections)
            if (!cells.Contains(new CellCoord(door.X + d.X, door.Y + d.Y))) return (door, d);
        return (door, DoorDirections[0]);
    }

    private static readonly CellCoord[] DoorDirections = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    private static IReadOnlyList<FootprintCell> Compute(
        BuildingId buildingId, int buildingTypeId, int normalizedOrientation)
    {
        // A casa inicial precisa de uma célula interna real, mas não pode dominar visualmente a
        // cidade: 3x3 é o menor footprint que satisfaz ambos. Outros prédios variam apenas entre
        // 3 e 4 células por eixo; a forma depende do tipo (compartilhável byte-a-byte com o web),
        // enquanto a orientação varia por identidade.
        long typeVariant = Math.Abs((long)buildingTypeId);
        int width = buildingTypeId == -1 ? 3 : 3 + (int)(typeVariant % 2);
        int height = width;
        bool isLShape = buildingTypeId != -1 && typeVariant > 0 && typeVariant % 7 == 0 && width == 4;

        bool InBase(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        bool InNotch(int x, int y) => isLShape && x >= width / 2 && y >= height / 2;
        bool InShape(int x, int y) => InBase(x, y) && !InNotch(x, y);

        // #226: a parede é aresta, não célula: toda célula da planta é piso; a da porta é a entrada (a abertura fica na aresta de fora).
        var cells = new List<(int X, int Y)>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (InShape(x, y)) cells.Add((x, y));

        int doorX = isLShape ? width / 4 : width / 2;
        int doorIndex = cells.FindIndex(c => c.X == doorX && c.Y == height - 1);
        if (doorIndex < 0)
            // Fallback determinístico (a porta SEMPRE existe): a primeira célula da borda inferior, em ordem estável (y desc, x asc).
            doorIndex = cells
                .Select((c, i) => (c, i))
                .OrderByDescending(t => t.c.Y)
                .ThenBy(t => t.c.X)
                .Select(t => t.i)
                .First();

        var result = new FootprintCell[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            var (x, y) = cells[i];
            result[i] = new FootprintCell(Rotate(x, y, width, height, normalizedOrientation), i == doorIndex ? BuildingMaterial.Door : BuildingMaterial.Floor);
        }
        return result;
    }

    /// <summary>Geometria efetiva de uma entidade: autoria vence; prédios sem orientação
    /// persistida recebem a rotação determinística derivada da identidade.</summary>
    public static IReadOnlyList<FootprintCell> Generate(Building building) =>
        Generate(
            building.Id,
            building.BuildingTypeId,
            building.Orientation ?? DerivedOrientation(building.Id, building.BuildingTypeId));

    private static int NormalizeOrientation(int orientation)
    {
        int normalized = (orientation % 360 + 360) % 360;
        return normalized is 0 or 90 or 180 or 270 ? normalized : 0;
    }

    private static CellCoord Rotate(int x, int y, int width, int height, int orientation) => orientation switch
    {
        90 => new CellCoord(height - 1 - y, x),
        180 => new CellCoord(width - 1 - x, height - 1 - y),
        270 => new CellCoord(y, width - 1 - x),
        _ => new CellCoord(x, y),
    };
}
