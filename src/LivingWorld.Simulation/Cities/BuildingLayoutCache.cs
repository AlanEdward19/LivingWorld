using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Simulation.Cities;

/// <summary>Planta de um prédio com as células caminháveis de cada andar já separadas. Derivada e imutável:
/// nunca persistida nem lida pelo hash.</summary>
internal sealed class CachedBuildingLayout
{
    private readonly HashSet<CellCoord>[] _walkableSets;
    private readonly HashSet<CellCoord> _ground;

    public CachedBuildingLayout(BuildingLayout layout)
    {
        Layout = layout;
        _ground = [.. layout.FloorLayouts[0].Cells.Select(c => c.Cell)];
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

    /// <summary>A célula (local) é da planta do térreo (parede ou piso); fora dela é chão aberto.</summary>
    public bool CoversGround(CellCoord localCell) => _ground.Contains(localCell);
}

/// <summary>Planta por prédio, gerada uma vez. A planta de origem é função pura de <c>(Id, BuildingTypeId, Orientation, Shape)</c>, fixos na construção de
/// <see cref="Building"/>; a tabela fraca solta a planta junto com o prédio. #264: a obra do morador (<see cref="Building.Edit"/>) acrescenta células por cima; a
/// planta de agora é refeita quando a versão da edição muda, e a de origem (<see cref="BaseOf"/>) nunca muda (o planejador de cômodos e de móveis trabalha nela).
/// Gerar a planta a cada entrada de NPC era ~10% do tempo de tick (#154).</summary>
internal static class BuildingLayoutCache
{
    private sealed class Holder
    {
        public int Version = int.MinValue;
        public CachedBuildingLayout? Layout;
    }

    private static readonly ConditionalWeakTable<Building, Holder> Cache = new();
    private static readonly ConditionalWeakTable<Building, CachedBuildingLayout> Bases = new();

    public static CachedBuildingLayout Of(Building building)
    {
        var holder = Cache.GetOrCreateValue(building);
        int version = building.Edit?.Version ?? -1;
        lock (holder)
        {
            if (holder.Layout is null || holder.Version != version)
            {
                holder.Layout = building.Edit is null ? BaseOf(building) : new CachedBuildingLayout(BuildingLayoutGenerator.Generate(building));
                holder.Version = version;
            }

            return holder.Layout;
        }
    }

    /// <summary>A planta de origem do prédio, sem a obra do morador.</summary>
    public static CachedBuildingLayout BaseOf(Building building) =>
        Bases.GetValue(building, static b => new CachedBuildingLayout(BuildingLayoutGenerator.GenerateBase(b)));
}
