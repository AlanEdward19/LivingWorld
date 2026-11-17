using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Simulation.Cities.Layout;

/// <summary>Geometria do lote de uma planta (AD-173): porta, para onde ela abre, o recuo em volta e o quintal de trás.
/// Tudo em coordenadas locais da planta (some a origem).</summary>
internal sealed class LotShape
{
    public CellCoord[] Cells { get; }
    public CellCoord Door { get; }
    public CellCoord Facing { get; }
    public CellCoord[] Halo { get; }
    public CellCoord[] Croft { get; }
    public int Setback { get; }

    private LotShape(CellCoord[] cells, CellCoord door, CellCoord facing, CellCoord[] halo, CellCoord[] croft, int setback)
    {
        Cells = cells;
        Door = door;
        Facing = facing;
        Halo = halo;
        Croft = croft;
        Setback = setback;
    }

    public static LotShape Of(IReadOnlyList<FootprintCell> footprint, int setback, int croftDepth)
    {
        var cells = footprint.Select(c => c.Cell).ToArray();
        var set = cells.ToHashSet();
        var (door, facing) = LotGeometry.DoorOf(footprint, set);

        var halo = new List<CellCoord>();
        var seen = new HashSet<CellCoord>();
        if (setback > 0)
            foreach (var cell in cells)
                for (int dy = -setback; dy <= setback; dy++)
                    for (int dx = -setback; dx <= setback; dx++)
                    {
                        var n = new CellCoord(cell.X + dx, cell.Y + dy);
                        if (!set.Contains(n) && seen.Add(n)) halo.Add(n);
                    }

        return new LotShape(cells, door, facing, [.. halo], LotGeometry.CroftOf(cells, set, facing, croftDepth), setback);
    }
}

internal static class LotGeometry
{
    private static readonly CellCoord[] Directions = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    /// <summary>A porta e o lado para onde ela abre (o vizinho de 4 que não é da planta).</summary>
    public static (CellCoord Door, CellCoord Facing) DoorOf(IReadOnlyList<FootprintCell> footprint, HashSet<CellCoord>? cells = null)
    {
        cells ??= footprint.Select(c => c.Cell).ToHashSet();
        var door = footprint.First(c => c.Material == BuildingMaterial.Door).Cell;
        foreach (var d in Directions)
            if (!cells.Contains(new CellCoord(door.X + d.X, door.Y + d.Y))) return (door, d);
        return (door, Directions[0]);
    }

    /// <summary>Faixa de <paramref name="depth"/> células atrás da planta (do lado oposto à porta), sem repetição.</summary>
    public static CellCoord[] CroftOf(CellCoord[] cells, HashSet<CellCoord> set, CellCoord facing, int depth)
    {
        if (depth <= 0) return [];
        var croft = new List<CellCoord>();
        var seen = new HashSet<CellCoord>();
        foreach (var cell in cells)
            for (int j = 1; j <= depth; j++)
            {
                var back = new CellCoord(cell.X - facing.X * j, cell.Y - facing.Y * j);
                if (!set.Contains(back) && seen.Add(back)) croft.Add(back);
            }
        return [.. croft];
    }

    /// <summary>A célula de rua para onde a porta dá, andando reto para fora até <paramref name="reach"/> passos; nulo se não há.</summary>
    public static CellCoord? FrontStreetCell(StreetNetwork network, IReadOnlyList<FootprintCell> footprint, CellCoord origin, int reach)
    {
        var (door, facing) = DoorOf(footprint);
        for (int j = 1; j <= reach; j++)
        {
            var cell = new CellCoord(origin.X + door.X + facing.X * j, origin.Y + door.Y + facing.Y * j);
            if (network.Contains(cell)) return cell;
        }
        return null;
    }

    public static CellCoord? FrontStreetCell(StreetNetwork network, Building building, CellCoord origin) =>
        FrontStreetCell(network, BuildingFootprintGenerator.Generate(building), origin, MaxReach);

    /// <summary>Até onde se procura a rua saindo da porta: o maior quintal da frente previsto, mais a própria rua.</summary>
    public const int MaxReach = 3;
}
