using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15, T5 (VTT-03): projeção de interior. <see cref="Building"/> não tem
/// <c>CellCoord</c> própria e nenhum <c>Npc</c> referencia "dentro de qual prédio está" — o
/// domínio ainda não modela ocupação por interior, só a associação prédio-cidade. <see
/// cref="InteriorSnapshot.OccupancyModeled"/> falso é o mesmo padrão de fallback de <see
/// cref="Layers.LayerBuildResult.NotYetModeled"/>: identidade do prédio é real, ocupação não.</summary>
public sealed record InteriorSnapshot(BuildingId Id, CityId City, int BuildingTypeId, bool OccupancyModeled);

public static class InteriorProjector
{
    public static Result<InteriorSnapshot> Build(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return Result<InteriorSnapshot>.Fail($"prédio {buildingId} não encontrado");

        return Result<InteriorSnapshot>.Ok(new InteriorSnapshot(building.Id, building.City, building.BuildingTypeId, OccupancyModeled: false));
    }
}
