using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Células de um andar do prédio, em coordenadas locais do footprint.</summary>
public sealed record FloorLayout(int Level, IReadOnlyList<FootprintCell> Cells);

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

    public static int FloorsOf(BuildingId id, int buildingTypeId) => Generate(id, buildingTypeId).Floors;

    public static BuildingLayout Generate(Building building) =>
        Generate(building.Id, building.BuildingTypeId, building.Orientation ?? BuildingFootprintGenerator.DerivedOrientation(building.Id, building.BuildingTypeId));

    public static BuildingLayout Generate(BuildingId id, int buildingTypeId, int? orientation = null)
    {
        var ground = BuildingFootprintGenerator.Generate(id, buildingTypeId, orientation);
        int stairIndex = IndexOfFirst(ground, BuildingMaterial.Floor);
        int floors = DesiredFloors(id, buildingTypeId);
        if (stairIndex < 0 || floors == 1)
            return new BuildingLayout(1, [new FloorLayout(0, ground)]);

        var groundWithStair = Replace(ground, stairIndex, BuildingMaterial.Stair);
        var wall = ground.FirstOrDefault(c => c.Material is BuildingMaterial.StoneWall or BuildingMaterial.WoodWall)?.Material
            ?? BuildingMaterial.StoneWall;
        var upper = groundWithStair
            .Select(c => c.Material == BuildingMaterial.Door ? c with { Material = wall } : c)
            .ToArray();

        var layouts = new List<FloorLayout> { new(0, groundWithStair) };
        for (int level = 1; level < floors; level++)
            layouts.Add(new FloorLayout(level, upper));
        return new BuildingLayout(floors, layouts);
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
