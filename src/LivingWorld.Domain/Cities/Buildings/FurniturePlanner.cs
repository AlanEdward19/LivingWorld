using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Onde fica a peça: andar, célula local da planta e giro em graus em torno de Y (0, 90, 180, 270) com o fundo da peça (-z) para a parede.</summary>
public readonly record struct FurnitureSpot(int Floor, CellCoord Cell, int Facing);

/// <summary>#237: posição da mobília na planta, só pela planta e uma semente (função pura, sem RNG, sem tick). Regras: só piso (nunca parede, porta
/// nem escada), nunca a célula de entrada (a de piso logo à frente da porta no térreo; a escada nos andares de cima) e sempre com caminho: depois de
/// posta a peça, toda célula andável que sobra continua ligada à porta (térreo) ou à escada (andares de cima) por células andáveis livres.
/// Prefere célula encostada em parede e vira o fundo da peça para ela. Peça que não cabe sem fechar o caminho fica sem lugar (nulo).</summary>
public static class FurniturePlanner
{
    private static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    /// <summary>Lugar de cada uma das <paramref name="count"/> peças do andar <paramref name="floor"/>, na ordem pedida; nulo = sem lugar.</summary>
    public static IReadOnlyList<FurnitureSpot?> Plan(BuildingLayout layout, int floor, int count, long seed)
    {
        var spots = new FurnitureSpot?[Math.Max(count, 0)];
        if (count <= 0 || floor < 0 || floor >= layout.FloorLayouts.Count) return spots;

        var materials = layout.FloorLayouts[floor].Cells.ToDictionary(c => c.Cell, c => c.Material);
        var walkable = materials.Where(kv => InteriorWalkability.IsWalkable(kv.Value)).Select(kv => kv.Key).ToHashSet();
        var start = StartOf(materials, floor);
        if (start is not { } from) return spots;

        var entry = EntryOf(materials, from, floor);
        var candidates = materials.Where(kv => kv.Value == BuildingMaterial.Floor && kv.Key != entry)
            .Select(kv => kv.Key)
            .OrderBy(cell => FacingOf(materials, cell) is null ? 1 : 0)
            .ThenBy(cell => StableHash.Mix(seed ^ ((long)cell.X << 24) ^ ((long)cell.Y << 8) ^ floor))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .ToList();

        var taken = new HashSet<CellCoord>();
        for (int i = 0; i < spots.Length; i++)
            foreach (var cell in candidates)
            {
                if (taken.Contains(cell)) continue;
                taken.Add(cell);
                if (!AllReachable(walkable, taken, from)) { taken.Remove(cell); continue; }
                spots[i] = new FurnitureSpot(floor, cell, FacingOf(materials, cell) ?? 0);
                break;
            }

        return spots;
    }

    /// <summary>A célula de piso logo à frente da porta (térreo) ou a escada (andares de cima), de onde se entra; nula se não há.</summary>
    public static CellCoord? EntryOf(IReadOnlyDictionary<CellCoord, BuildingMaterial> materials, CellCoord start, int floor)
    {
        if (floor > 0) return start;
        foreach (var d in Around)
        {
            var next = new CellCoord(start.X + d.X, start.Y + d.Y);
            if (materials.TryGetValue(next, out var m) && m == BuildingMaterial.Floor) return next;
        }

        return null;
    }

    /// <summary>De onde o caminho começa: a porta no térreo; a escada nos andares de cima. Nulo se a planta não tem.</summary>
    public static CellCoord? StartOf(IReadOnlyDictionary<CellCoord, BuildingMaterial> materials, int floor)
    {
        var wanted = floor == 0 ? BuildingMaterial.Door : BuildingMaterial.Stair;
        foreach (var kv in materials.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X))
            if (kv.Value == wanted) return kv.Key;
        return null;
    }

    /// <summary>Giro (graus) que põe o fundo da peça (-z) numa parede vizinha (norte, oeste, leste, sul, nessa ordem); nulo se nenhum vizinho é parede.</summary>
    public static int? FacingOf(IReadOnlyDictionary<CellCoord, BuildingMaterial> materials, CellCoord cell)
    {
        (CellCoord Side, int Facing)[] sides = [(new(0, -1), 0), (new(-1, 0), 90), (new(1, 0), 270), (new(0, 1), 180)];
        foreach (var (side, facing) in sides)
            if (materials.TryGetValue(new CellCoord(cell.X + side.X, cell.Y + side.Y), out var m)
                && m is BuildingMaterial.StoneWall or BuildingMaterial.WoodWall)
                return facing;
        return null;
    }

    /// <summary>Toda célula andável fora de <paramref name="blocked"/> é alcançada de <paramref name="from"/> sem passar por <paramref name="blocked"/>.</summary>
    public static bool AllReachable(HashSet<CellCoord> walkable, HashSet<CellCoord> blocked, CellCoord from)
    {
        var seen = new HashSet<CellCoord> { from };
        var queue = new Queue<CellCoord>([from]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in Around)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (walkable.Contains(next) && !blocked.Contains(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }

        return walkable.All(cell => blocked.Contains(cell) || seen.Contains(cell));
    }
}
