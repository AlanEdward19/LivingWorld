using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Única porta de entrada de um NPC num prédio: valida prédio, andar e célula contra a
/// planta derivada antes de gravar <see cref="Npc.Interior"/>. Falha deixa o mundo intacto.</summary>
public static class InteriorEntry
{
    public static Result<Unit> TryEnter(WorldState world, Npc npc, BuildingId buildingId, FloorLevel floor, CellCoord localCell)
    {
        var building = world.FindBuilding(buildingId);
        if (building is null) return Result<Unit>.Fail($"prédio {buildingId} não encontrado");

        var cached = BuildingLayoutCache.Of(building);
        int floors = cached.Layout.Floors;
        if (floor.Value < 0 || floor.Value >= floors)
            return Result<Unit>.Fail($"andar {floor.Value} fora de [0, {floors - 1}]");

        if (!cached.IsWalkable(floor.Value, localCell))
            return Result<Unit>.Fail($"célula {localCell} não caminhável no andar {floor.Value}");

        npc.EnterBuilding(buildingId, floor, localCell);
        return Result<Unit>.Ok(Unit.Value);
    }
}
