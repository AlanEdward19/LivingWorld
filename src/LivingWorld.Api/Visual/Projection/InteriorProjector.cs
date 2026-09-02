using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Ocupante projetado de um interior (Fase 15.1, T47) — id do NPC + andar/célula
/// locais, mesmo shape que <see cref="InteriorOccupancy"/> sem expor tipo de Domain cru.</summary>
public sealed record InteriorOccupant(NpcId Npc, FloorLevel Floor, CellCoord LocalCell);

/// <summary>Fase 15, T5 (VTT-03); ocupação real desde Fase 15.1, T47 (G7): <see
/// cref="Occupants"/> lista todo NPC vivo com <see cref="Npc.Interior"/> apontando para
/// este prédio — <see cref="OccupancyModeled"/> passa a <c>true</c> agora que existe dado real
/// por trás.</summary>
public sealed record InteriorSnapshot(BuildingId Id, CityId City, int BuildingTypeId, bool OccupancyModeled, IReadOnlyList<InteriorOccupant> Occupants);

public static class InteriorProjector
{
    public static Result<InteriorSnapshot> Build(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return Result<InteriorSnapshot>.Fail($"prédio {buildingId} não encontrado");

        var occupants = world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId)
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell))
            .ToArray();

        return Result<InteriorSnapshot>.Ok(new InteriorSnapshot(building.Id, building.City, building.BuildingTypeId, OccupancyModeled: true, occupants));
    }
}
