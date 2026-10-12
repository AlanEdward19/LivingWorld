using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Ocupante projetado de um interior — id do NPC + andar/célula
/// locais, mesmo shape que <see cref="InteriorOccupancy"/> sem expor tipo de Domain cru.</summary>
public sealed record InteriorOccupant(NpcId Npc, FloorLevel Floor, CellCoord LocalCell, ActionType? Action = null);

/// <summary><see
/// cref="Occupants"/> lista todo NPC vivo com <see cref="Npc.Interior"/> apontando para
/// este prédio — <see cref="OccupancyModeled"/> passa a <c>true</c> agora que existe dado real
/// por trás.</summary>
public sealed record InteriorSnapshot(BuildingId Id, CityId City, int BuildingTypeId, bool OccupancyModeled, IReadOnlyList<InteriorOccupant> Occupants,
    BuildingLayout Layout);

/// <summary>Delta de um interior: quem entrou ou mudou (andar, célula ou ação) vem completo em
/// <see cref="Upserts"/>; quem saiu, em <see cref="Removed"/>. Um único caminho para o cliente aplicar.</summary>
public sealed record InteriorTickDelta(long Tick, IReadOnlyList<InteriorOccupant> Upserts, IReadOnlyList<NpcId> Removed)
{
    public static InteriorTickDelta Diff(long tick, IReadOnlyList<InteriorOccupant> before, IReadOnlyList<InteriorOccupant> after)
    {
        var beforeByNpc = before.ToDictionary(o => o.Npc);
        var afterIds = after.Select(o => o.Npc).ToHashSet();
        var upserts = after.Where(a => !beforeByNpc.TryGetValue(a.Npc, out var b) || b != a).ToArray();
        var removed = before.Where(b => !afterIds.Contains(b.Npc)).Select(b => b.Npc).ToArray();
        return new InteriorTickDelta(tick, upserts, removed);
    }
}

public static class InteriorProjector
{
    public static Result<InteriorSnapshot> Build(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return Result<InteriorSnapshot>.Fail($"prédio {buildingId} não encontrado");

        var occupants = world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId)
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell, n.CurrentAction))
            .ToArray();

        return Result<InteriorSnapshot>.Ok(new InteriorSnapshot(building.Id, building.City, building.BuildingTypeId, OccupancyModeled: true, occupants,
            BuildingLayoutGenerator.Generate(building)));
    }
}
