using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>#266: onde uma peça está de fato: as células da pegada (<see cref="ObjectFootprint"/>), o giro e o tamanho. Serve ao planejador, à validação do modo comprar e aos testes.</summary>
public readonly record struct FurnitureBody(CellCoord Origin, int Width, int Depth, int Facing)
{
    public IEnumerable<CellCoord> Cells => ObjectFootprint.Cells(Origin.X, Origin.Y, Width, Depth, Facing).Select(c => new CellCoord(c.X, c.Y));

    /// <summary>As células da pegada de <paramref name="descriptor"/> (1×1 sem declaração) no lugar dado.</summary>
    public static FurnitureBody Of(ObjectDescriptor? descriptor, CellCoord origin, int facing)
    {
        var (w, d) = ObjectFootprint.SizeOf(descriptor);
        return new FurnitureBody(origin, w, d, facing);
    }
}

/// <summary>#266: as regras de geometria de uma peça no piso, as mesmas para o planejador inicial e para o comprar/mover: toda a pegada em piso (nem porta, nem escada), nenhuma célula de
/// outra peça, fora da passagem das portas, sem aresta de parede entre as células da pegada (nada atravessa parede), o fundo encostado na parede quando o móvel pede, e um lado livre
/// para usar (frente ou lados, nunca só o fundo). Funções puras.</summary>
public static class FurniturePlacement
{
    private static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    /// <summary>Por que a peça não pode ficar ali (ou nulo). <paramref name="floorCells"/> são as células de piso do andar; <paramref name="blocked"/> as de outras peças;
    /// <paramref name="doorways"/> as de passagem; <paramref name="walls"/> as arestas fechadas; <paramref name="walkable"/> as andáveis (para o lado livre).</summary>
    public static string? Refusal(FurnitureBody body, IReadOnlySet<CellCoord> floorCells, IReadOnlySet<CellCoord> blocked, IReadOnlySet<CellCoord> doorways,
        IReadOnlySet<WallEdge> walls, IReadOnlySet<CellCoord> walkable, bool needWall)
    {
        var cells = body.Cells.ToList();
        foreach (var cell in cells)
            if (!floorCells.Contains(cell)) return cells.Count > 1 ? "não cabe: a peça ocupa " + cells.Count + " células de piso" : "aqui não é piso da casa";
        foreach (var cell in cells)
            if (blocked.Contains(cell)) return "já tem um móvel aqui";
        foreach (var cell in cells)
            if (doorways.Contains(cell)) return "bloquearia a passagem da porta";
        var own = cells.ToHashSet();
        foreach (var cell in cells)
            foreach (var d in Around)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (own.Contains(next) && walls.Contains(WallEdge.Between(cell, next))) return "atravessaria uma parede";
            }

        if (needWall && !BackOnWall(body, walls)) return "precisa ficar encostado na parede (gire para o fundo ficar na parede)";
        if (!HasAccess(body, blocked, walls, walkable)) return "ficaria sem lado livre para usar";
        return null;
    }

    /// <summary>Toda célula do fundo da peça tem parede (de fora ou de dentro) logo atrás.</summary>
    public static bool BackOnWall(FurnitureBody body, IReadOnlySet<WallEdge> walls)
    {
        var (bx, by) = ObjectFootprint.Back(body.Facing);
        return BackRow(body).All(cell => walls.Contains(WallEdge.Between(cell, new CellCoord(cell.X + bx, cell.Y + by))));
    }

    /// <summary>Alguma célula vizinha da pegada (fora dela e fora do fundo) é andável, está livre de outras peças e não tem parede no meio.</summary>
    public static bool HasAccess(FurnitureBody body, IReadOnlySet<CellCoord> blocked, IReadOnlySet<WallEdge> walls, IReadOnlySet<CellCoord> walkable)
    {
        var own = body.Cells.ToHashSet();
        var back = BackRow(body).ToHashSet();
        var (bx, by) = ObjectFootprint.Back(body.Facing);
        foreach (var cell in own)
            foreach (var d in Around)
            {
                if (back.Contains(cell) && d.X == bx && d.Y == by) continue;
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (!own.Contains(next) && walkable.Contains(next) && !blocked.Contains(next) && !walls.Contains(WallEdge.Between(cell, next))) return true;
            }

        return false;
    }

    private static IEnumerable<CellCoord> BackRow(FurnitureBody body)
    {
        for (int i = 0; i < body.Width; i++)
        {
            var (dx, dy) = ObjectFootprint.Turn(i, 0, body.Facing);
            yield return new CellCoord(body.Origin.X + (int)dx, body.Origin.Y + (int)dy);
        }
    }
}
