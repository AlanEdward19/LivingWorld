using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Simulation.Objects;

/// <summary>#264: a beleza de um cômodo, medida pelo motor: a soma da beleza dos móveis que estão nele (<c>Home.items[].beauty</c>) mais a média da beleza da pintura do piso e das
/// paredes dele (<c>Home.paints[].beauty</c>; o que não foi pintado vale a pintura padrão do tipo). Função pura do prédio e do catálogo, em cache por versão da obra.</summary>
public sealed record RoomBeautyView(int Floor, int Index, string? Kind, int Cells, double Beauty, double Items, double Paint, double Expected = 0);

public static class RoomBeauty
{
    private sealed record Entry(ObjectCatalog Catalog, int Version, IReadOnlyList<RoomBeautyView> Rooms, Dictionary<(int Floor, CellCoord Cell), double> ByCell, Dictionary<(int Floor, CellCoord Cell), double> Relative);

    private static readonly ConditionalWeakTable<Building, Entry> Cache = new();

    /// <summary>Os cômodos do prédio com a beleza de cada um; vazio sem catálogo de compra.</summary>
    public static IReadOnlyList<RoomBeautyView> Rooms(Building building, ObjectCatalog? catalog) => catalog?.Home is null ? [] : EntryOf(building, catalog).Rooms;

    /// <summary>A beleza do cômodo que tem a célula, ou nulo (sem catálogo de compra, célula fora da planta).</summary>
    public static double? At(Building building, ObjectCatalog? catalog, int floor, CellCoord cell) =>
        catalog?.Home is null ? null : EntryOf(building, catalog).ByCell.TryGetValue((floor, cell), out double beauty) ? beauty : null;

    /// <summary>#268: a beleza do cômodo que tem a célula menos a esperada para o tipo dele (<c>Home.rules.expectedBeauty</c>); é o que o humor mede.</summary>
    public static double? RelativeAt(Building building, ObjectCatalog? catalog, int floor, CellCoord cell) =>
        catalog?.Home is null ? null : EntryOf(building, catalog).Relative.TryGetValue((floor, cell), out double beauty) ? beauty : null;

    /// <summary>O gatilho de ambiente (<c>state</c> com <c>ambient</c>): a medida do cômodo onde a pessoa está agora (a beleza relativa ao que se espera do tipo de cômodo). Nulo fora de casa ou sem catálogo.</summary>
    public static double? Ambient(WorldState world, Npc npc, string metric)
    {
        if (metric != "beauty" || world.ObjectCatalog is not { Home: not null } catalog) return null;
        if (npc.Interior is { } inside)
            return world.FindBuilding(inside.Building) is { } building && IsDwelling(world, building) ? RelativeAt(building, catalog, inside.Floor.Value, inside.LocalCell) : null;
        // o corpo que alguém conduz anda pela pose fina e pode estar na planta sem `Interior`: o chão do passo diz em que casa e em que célula ele está
        int floor = PlayerGround.FloorOf(npc);
        var pose = PlayerGround.PoseOf(world, npc);
        return PlayerGround.At(world, pose, floor) is { } spot && IsDwelling(world, spot.Building)
            ? RelativeAt(spot.Building, catalog, floor, new CellCoord(pose.X - spot.Origin.X, pose.Y - spot.Origin.Y)) : null;
    }

    /// <summary>#269: "Cômodo feio" aparecia dentro do Mercado (a beleza esperada do salão da banca vinha da casa): o ambiente do gatilho é o de uma moradia, onde a decoração é da pessoa. Loja, mercado e escritório não têm a medida.</summary>
    private static bool IsDwelling(WorldState world, Building building)
    {
        foreach (var household in world.Households)
            if (household.Dwelling == building.Id) return true;
        return false;
    }

    private static Entry EntryOf(Building building, ObjectCatalog catalog)
    {
        int version = building.Edit?.Version ?? -1;
        if (Cache.TryGetValue(building, out var known) && ReferenceEquals(known.Catalog, catalog) && known.Version == version) return known;
        var computed = Compute(building, catalog, version);
        Cache.AddOrUpdate(building, computed);
        return computed;
    }

    private static Entry Compute(Building building, ObjectCatalog catalog, int version)
    {
        var home = catalog.Home!;
        var rooms = BuildingRooms.Of(building, catalog);
        var layout = BuildingLayoutCache.Of(building).Layout;
        var pieces = HomeFurniture.Of(building, catalog);
        var floorPaint = new Dictionary<(int, CellCoord), string>();
        var wallPaint = new Dictionary<(int, WallEdge), string>();
        if (building.Edit is { } edit)
            foreach (var mark in edit.Paints)
            {
                if (mark.Kind == "floor") floorPaint[(mark.Floor, new CellCoord(mark.X, mark.Y))] = mark.Paint;
                else wallPaint[(mark.Floor, WallEdge.Between(new CellCoord(mark.X, mark.Y), new CellCoord(mark.BX, mark.BY)))] = mark.Paint;
            }

        double defaultFloor = home.DefaultPaint("floor")?.Beauty ?? 0, defaultWall = home.DefaultPaint("wall")?.Beauty ?? 0;
        var views = new List<RoomBeautyView>();
        var byCell = new Dictionary<(int, CellCoord), double>();
        var relative = new Dictionary<(int, CellCoord), double>();
        for (int floor = 0; floor < rooms.Floors.Length; floor++)
        {
            var outer = (layout.FloorLayouts[floor].Walls ?? []).Select(w => w.Edge).ToHashSet();
            var inner = rooms.Floors[floor].Walls.Select(w => w.Edge).ToHashSet();
            for (int index = 0; index < rooms.Floors[floor].Rooms.Count; index++)
            {
                var room = rooms.Floors[floor].Rooms[index];
                var cells = room.Cells.ToHashSet();
                double items = 0;
                foreach (var piece in pieces)
                    if (piece.Spot is { } spot && spot.Floor == floor && cells.Contains(spot.Cell) && home.Item(piece.Id) is { } item) items += item.Beauty;

                double floors = 0;
                foreach (var cell in room.Cells)
                    floors += floorPaint.TryGetValue((floor, cell), out var id) && home.Paint(id) is { } painted ? painted.Beauty : defaultFloor;
                floors = room.Cells.Count > 0 ? floors / room.Cells.Count : 0;

                double walls = 0;
                int edges = 0;
                foreach (var cell in room.Cells)
                    foreach (var d in HomeEditor.Around)
                    {
                        var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                        if (cells.Contains(next)) continue;
                        var edge = WallEdge.Between(cell, next);
                        if (!outer.Contains(edge) && !inner.Contains(edge)) continue;
                        edges++;
                        walls += wallPaint.TryGetValue((floor, edge), out var id) && home.Paint(id) is { } painted ? painted.Beauty : defaultWall;
                    }

                walls = edges > 0 ? walls / edges : 0;
                double paint = Math.Round(floors + walls, 2);
                double total = Math.Round(items + paint, 2);
                double expected = room.Kind is { } kind && home.Rules.ExpectedBeauty is { } table && table.TryGetValue(kind, out double wanted) ? wanted : 0;
                views.Add(new RoomBeautyView(floor, index, room.Kind, room.Cells.Count, total, items, paint, expected));
                foreach (var cell in room.Cells) { byCell[(floor, cell)] = total; relative[(floor, cell)] = Math.Round(total - expected, 2); }
            }
        }

        return new Entry(catalog, version, views, byCell, relative);
    }
}
