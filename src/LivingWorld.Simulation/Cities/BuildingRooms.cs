using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;

namespace LivingWorld.Simulation.Cities;

/// <summary>#226: os cômodos de cada andar de um prédio e as arestas fechadas (paredes de fora + de dentro), já em conjunto para consulta. Derivado e
/// imutável: função pura da planta e do catálogo de cômodos do cenário; nunca persistido nem lido pelo hash.</summary>
internal sealed class CachedRooms
{
    public CachedRooms(BuildingLayout layout, FloorRooms[] floors)
    {
        Floors = floors;
        Blocked = new HashSet<WallEdge>[floors.Length];
        for (int level = 0; level < floors.Length; level++)
            Blocked[level] = FurniturePlanner.WallsOf(layout.FloorLayouts[level], floors[level]);
    }

    public FloorRooms[] Floors { get; }

    /// <summary>Arestas fechadas do andar (coordenadas locais).</summary>
    public HashSet<WallEdge>[] Blocked { get; }

    /// <summary>O cômodo da célula no andar, ou nulo se a célula não é da planta.</summary>
    public Room? RoomAt(int floor, CellCoord cell)
    {
        foreach (var room in Floors[floor].Rooms)
            foreach (var c in room.Cells)
                if (c == cell) return room;
        return null;
    }
}

/// <summary>Cômodos por prédio e catálogo, guardados junto do prédio (a tabela fraca solta o cache com ele). Sem catálogo de cômodos o andar é um cômodo só e as
/// únicas paredes são as de fora.</summary>
internal static class BuildingRooms
{
    private sealed record Entry(RoomCatalog? Catalog, CachedBuildingLayout Layout, int Version, CachedRooms Rooms);

    private static readonly ConditionalWeakTable<Building, Entry> Cache = new();

    public static CachedRooms Of(Building building, ObjectCatalog? catalog)
    {
        var rooms = catalog?.Rooms;
        var cached = BuildingLayoutCache.Of(building);
        int version = building.Edit?.Version ?? -1;
        if (Cache.TryGetValue(building, out var known) && ReferenceEquals(known.Catalog, rooms) && ReferenceEquals(known.Layout, cached) && known.Version == version) return known.Rooms;
        var computed = Compute(building, cached, catalog);
        Cache.AddOrUpdate(building, new Entry(rooms, cached, version, computed));
        return computed;
    }

    /// <summary>Os cômodos de origem (o planejador na planta de origem, sem a obra do morador): é o que decide onde a mobília do catálogo fica (#264).</summary>
    public static CachedRooms BaseOf(Building building, ObjectCatalog? catalog)
    {
        var rooms = catalog?.Rooms;
        var cached = BuildingLayoutCache.BaseOf(building);
        if (BaseCache.TryGetValue(building, out var known) && ReferenceEquals(known.Catalog, rooms) && ReferenceEquals(known.Layout, cached)) return known.Rooms;
        var computed = Plan(building, cached, rooms);
        BaseCache.AddOrUpdate(building, new Entry(rooms, cached, 0, computed));
        return computed;
    }

    private static readonly ConditionalWeakTable<Building, Entry> BaseCache = new();

    private static CachedRooms Compute(Building building, CachedBuildingLayout cached, ObjectCatalog? catalog) =>
        building.Edit is null ? BaseOf(building, catalog) : Edited(building, cached, BaseOf(building, catalog));

    private static CachedRooms Plan(Building building, CachedBuildingLayout cached, RoomCatalog? catalog)
    {
        var layout = cached.Layout;
        long seed = building.Id.Value * 31 + building.BuildingTypeId;
        var wall = BuildingFootprintGenerator.WallMaterialOf(building.BuildingTypeId);
        var floors = new FloorRooms[layout.FloorLayouts.Count];
        for (int level = 0; level < floors.Length; level++)
        {
            var cells = layout.FloorLayouts[level].Cells;
            var all = cells.Select(c => c.Cell).ToArray();
            var plan = catalog?.PlanFor(building.BuildingTypeId, level) ?? [];
            var start = FurniturePlanner.StartOf(cells.ToDictionary(c => c.Cell, c => c.Material), level);
            floors[level] = plan.Count < 1 || start is not { } entry
                ? FloorRooms.Single(all)
                : RoomPlanner.Plan(all, entry, plan, wall, seed ^ (level * 0x5BD1L));
        }

        return new CachedRooms(layout, floors);
    }

    private static CachedRooms Edited(Building building, CachedBuildingLayout cached, CachedRooms origin)
    {
        var layout = cached.Layout;
        var wall = BuildingFootprintGenerator.WallMaterialOf(building.BuildingTypeId);
        var floors = new FloorRooms[layout.FloorLayouts.Count];
        for (int level = 0; level < floors.Length; level++)
            floors[level] = HomeEditor.ApplyRooms(origin.Floors[level], layout.FloorLayouts[level], building.Edit, level, wall);
        return new CachedRooms(layout, floors);
    }
}
