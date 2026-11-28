using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Um andar do prédio, em coordenadas locais do footprint. #226: toda célula é piso (ou a entrada, ou a escada); <c>Walls</c> são as paredes
/// de fora, nas arestas da borda (sem a abertura da porta), e <c>Doors</c> são as aberturas de porta de fora (só no térreo). As paredes de dentro
/// (cômodos) vêm de <see cref="RoomPlanner"/>, pelo catálogo do cenário. Nulos = planta montada à mão (sem arestas).</summary>
public sealed record FloorLayout(int Level, IReadOnlyList<FootprintCell> Cells,
    IReadOnlyList<WallSegment>? Walls = null, IReadOnlyList<WallSegment>? Doors = null);

/// <summary>Planta completa: andares do térreo (nível 0) para cima. Derivada, nunca persistida.</summary>
public sealed record BuildingLayout(int Floors, IReadOnlyList<FloorLayout> FloorLayouts);

/// <summary>Andares e escada de um prédio, derivados só de <c>(BuildingId, BuildingTypeId)</c>: função
/// pura sobre <see cref="BuildingFootprintGenerator"/>, sem tick nem estado do mundo. Com mais de um
/// andar, a escada ocupa a primeira célula <see cref="BuildingMaterial.Floor"/> do footprint e fica
/// no mesmo <c>(x,y)</c> em todos os andares; a porta só existe no térreo (acima vira parede).</summary>
public static class BuildingLayoutGenerator
{
    private const int MaxFloors = 3;
    private const int StartingHouseTypeId = -1;
    private static readonly CellCoord[] Sides = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    public static int FloorsOf(BuildingId id, int buildingTypeId) => Generate(id, buildingTypeId).Floors;

    public static BuildingLayout Generate(Building building) =>
        Generate(building.Id, building.BuildingTypeId, building.Orientation ?? BuildingFootprintGenerator.DerivedOrientation(building.Id, building.BuildingTypeId), building.Shape);

    public static BuildingLayout Generate(BuildingId id, int buildingTypeId, int? orientation = null, BuildingShape? shape = null)
    {
        var ground = BuildingFootprintGenerator.Generate(id, buildingTypeId, orientation, shape);
        var wall = BuildingFootprintGenerator.WallMaterialOf(buildingTypeId);
        var (outer, doors) = OuterWalls(ground, wall);
        int stairIndex = IndexOfFirst(ground, BuildingMaterial.Floor);
        int floors = shape?.Floors ?? DesiredFloors(id, buildingTypeId);
        if (stairIndex < 0 || floors == 1)
            return new BuildingLayout(1, [new FloorLayout(0, ground, outer, doors)]);

        var groundWithStair = Replace(ground, stairIndex, BuildingMaterial.Stair);
        // acima do térreo não há porta: a aresta dela vira parede e a entrada é só a escada
        var upperCells = groundWithStair
            .Select(c => c.Material == BuildingMaterial.Door ? c with { Material = BuildingMaterial.Floor } : c)
            .ToArray();
        var upperWalls = outer.Concat(doors.Select(d => d with { Material = wall })).ToArray();

        var layouts = new List<FloorLayout> { new(0, groundWithStair, outer, doors) };
        for (int level = 1; level < floors; level++)
            layouts.Add(new FloorLayout(level, upperCells, upperWalls, []));
        return new BuildingLayout(floors, layouts);
    }

    /// <summary>As paredes de fora: uma por aresta da borda da planta, menos a abertura da porta (devolvida à parte).</summary>
    private static (WallSegment[] Walls, WallSegment[] Doors) OuterWalls(IReadOnlyList<FootprintCell> ground, BuildingMaterial wall)
    {
        var cells = new HashSet<CellCoord>(ground.Count);
        foreach (var c in ground) cells.Add(c.Cell);
        var (door, facing) = BuildingFootprintGenerator.DoorOf(ground);
        var doorOutside = new CellCoord(door.X + facing.X, door.Y + facing.Y);
        var walls = new List<WallSegment>();
        var doors = new List<WallSegment>();
        foreach (var cell in ground)
            foreach (var d in Sides)
            {
                var outside = new CellCoord(cell.Cell.X + d.X, cell.Cell.Y + d.Y);
                if (cells.Contains(outside)) continue;
                var edge = WallEdge.Between(cell.Cell, outside);
                if (cell.Cell == door && outside == doorOutside) doors.Add(new WallSegment(edge.A, edge.B, BuildingMaterial.Door));
                else walls.Add(new WallSegment(edge.A, edge.B, wall));
            }
        return ([.. walls], [.. doors]);
    }

    private static int DesiredFloors(BuildingId id, int buildingTypeId) =>
        buildingTypeId == StartingHouseTypeId
            ? 2
            : 1 + (int)(StableHash.Mix(id.Value ^ ((long)buildingTypeId << 16) ^ 0x4F6C) % MaxFloors);

    private static int IndexOfFirst(IReadOnlyList<FootprintCell> cells, BuildingMaterial material)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i].Material == material) return i;
        return -1;
    }

    private static FootprintCell[] Replace(IReadOnlyList<FootprintCell> cells, int index, BuildingMaterial material)
    {
        var copy = cells.ToArray();
        copy[index] = copy[index] with { Material = material };
        return copy;
    }
}
