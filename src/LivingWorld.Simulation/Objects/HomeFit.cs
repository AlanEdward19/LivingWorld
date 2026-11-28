using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Simulation.Cities;

namespace LivingWorld.Simulation.Objects;

/// <summary>#269: onde o móvel cabe. Um lugar válido (andar, célula local da casa e giro) que o motor aceitaria em <c>place</c>.</summary>
public sealed record HomeFitSpot(int Floor, int X, int Y, int Facing);

/// <summary>#269 (rodada 6, "comprar até o fim"): o motor responde ONDE o móvel cabe, em vez de o cliente tentar célula por célula no escuro (o sofá no depósito dava "vermelho" em todo clique e a
/// sala ficava sem ele). Procura o lugar mais perto de uma âncora (célula pedida ou o centro do térreo), preferindo o cômodo pedido (<c>sala</c>, qualquer id do catálogo de cômodos) e o andar da
/// âncora, e dentro de cada célula tenta os quatro giros a partir do pedido. A regra é a mesma de <see cref="HomeCommands.Placement"/>: nada aqui duplica a validação.</summary>
public static class HomeFit
{
    /// <summary>O lugar válido mais perto, ou nulo se a casa não tem onde pôr o móvel.</summary>
    public static HomeFitSpot? Nearest(Building building, ObjectCatalog catalog, HomeCatalog home, string objectId, int floor, CellCoord? anchor, int facing, string? roomKind = null, int ignore = -1)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var rooms = BuildingRooms.Of(building, catalog);
        var item = home.Item(objectId) ?? new HomeItem(objectId, "", 0, 0, -1);
        var pieces = HomeFurniture.Of(building, catalog);
        floor = Math.Clamp(floor, 0, Math.Max(0, layout.FloorLayouts.Count - 1));
        var center = anchor ?? Center(layout.FloorLayouts[floor]);
        var ranked = new List<(long Rank, int Floor, CellCoord Cell)>();
        for (int f = 0; f < layout.FloorLayouts.Count; f++)
            foreach (var c in layout.FloorLayouts[f].Cells)
            {
                if (c.Material != BuildingMaterial.Floor) continue;
                bool inRoom = roomKind is null || rooms.RoomAt(f, c.Cell)?.Kind == roomKind;
                long dx = c.Cell.X - center.X, dy = c.Cell.Y - center.Y;
                ranked.Add(((inRoom ? 0 : 10_000_000L) + (f == floor ? 0 : 1_000_000L) + dx * dx + dy * dy, f, c.Cell));
            }

        ranked.Sort((a, b) =>
        {
            int by = a.Rank.CompareTo(b.Rank);
            if (by != 0) return by;
            by = a.Floor.CompareTo(b.Floor);
            if (by != 0) return by;
            by = a.Cell.Y.CompareTo(b.Cell.Y);
            return by != 0 ? by : a.Cell.X.CompareTo(b.Cell.X);
        });
        foreach (var (_, f, cell) in ranked)
            for (int turn = 0; turn < 4; turn++)
            {
                int giro = (facing + 90 * turn) % 360;
                if (HomeCommands.Placement(building, catalog, home, pieces, ignore, item, f, cell, giro) is null) return new HomeFitSpot(f, cell.X, cell.Y, giro);
            }

        return null;
    }

    private static CellCoord Center(FloorLayout floor)
    {
        long x = 0, y = 0;
        int n = 0;
        foreach (var c in floor.Cells)
            if (c.Material == BuildingMaterial.Floor) { x += c.Cell.X; y += c.Cell.Y; n++; }
        return n == 0 ? new CellCoord(0, 0) : new CellCoord((int)(x / n), (int)(y / n));
    }
}
