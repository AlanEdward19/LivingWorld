using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Simulation.Cities;

/// <summary>Planta de um prédio com as células caminháveis de cada andar já separadas. Derivada e imutável:
/// nunca persistida nem lida pelo hash.</summary>
internal sealed class CachedBuildingLayout
{
    private readonly HashSet<CellCoord>[] _walkableSets;

    public CachedBuildingLayout(BuildingLayout layout)
    {
        Layout = layout;
        WalkableByFloor = new FootprintCell[layout.FloorLayouts.Count][];
        _walkableSets = new HashSet<CellCoord>[layout.FloorLayouts.Count];
        for (int level = 0; level < WalkableByFloor.Length; level++)
        {
            var walkable = new List<FootprintCell>();
            foreach (var cell in layout.FloorLayouts[level].Cells)
                if (InteriorWalkability.IsWalkable(cell.Material)) walkable.Add(cell);
            WalkableByFloor[level] = walkable.ToArray();
            _walkableSets[level] = [.. walkable.Select(c => c.Cell)];
        }
    }

    public BuildingLayout Layout { get; }

    /// <summary>Células caminháveis de cada andar, na ordem da planta (a escolha por hash depende dessa ordem).</summary>
    public FootprintCell[][] WalkableByFloor { get; }

    public bool IsWalkable(int floor, CellCoord localCell) => _walkableSets[floor].Contains(localCell);
}

/// <summary>Planta por prédio, gerada uma vez. A planta é função pura de <c>(Id, BuildingTypeId, Orientation)</c>, os três
/// fixos na construção de <see cref="Building"/>, então a entrada nunca fica velha; a tabela fraca solta a planta junto
/// com o prédio (demolido, ou mundo descartado). Gerar a planta a cada entrada de NPC era ~10% do tempo de tick (#154).</summary>
internal static class BuildingLayoutCache
{
    private static readonly ConditionalWeakTable<Building, CachedBuildingLayout> Cache = new();

    public static CachedBuildingLayout Of(Building building) =>
        Cache.GetValue(building, static b => new CachedBuildingLayout(BuildingLayoutGenerator.Generate(b)));
}
