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
public sealed record InteriorOccupant(
    NpcId Npc, FloorLevel Floor, CellCoord LocalCell, ActionType? Action = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Domain.Population.Appearance? Appearance = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? UsingObject = null);

/// <summary><see
/// cref="Occupants"/> lista todo NPC vivo com <see cref="Npc.Interior"/> apontando para
/// este prédio — <see cref="OccupancyModeled"/> passa a <c>true</c> agora que existe dado real
/// por trás.</summary>
public sealed record InteriorSnapshot(BuildingId Id, CityId City, int BuildingTypeId, bool OccupancyModeled, IReadOnlyList<InteriorOccupant> Occupants,
    BuildingLayout Layout,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] InteriorObjects? Objects = null);

/// <summary>Peça de mobília: id do catálogo + índice de variação determinístico (ADR-0034). O cliente compartilha mesh/material por (Id, Variation).</summary>
public sealed record PlacedObject(string Id, int Variation);

/// <summary>Objetos do interior (#47 fatia 1): o catálogo do cenário e a mobília do tipo do prédio, em ordem. Ausente = mundo sem <c>Objects</c>
/// (o cliente cai no <c>assets.json</c>).</summary>
public sealed record InteriorObjects(int Format, IReadOnlyList<LivingWorld.Domain.Objects.ObjectDescriptor> Catalog, IReadOnlyList<PlacedObject> Furniture);

public static class InteriorProjector
{
    /// <summary>Só os ocupantes (sem a planta): o que o delta de interior compara a cada tick. Nulo se o prédio não existe.</summary>
    public static IReadOnlyList<InteriorOccupant>? BuildOccupants(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return null;
        return world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId)
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell, n.CurrentAction, n.Appearance, UsingObjectOf(world.ObjectCatalog, building.BuildingTypeId, n)))
            .ToArray();
    }

    public static Result<InteriorSnapshot> Build(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return Result<InteriorSnapshot>.Fail($"prédio {buildingId} não encontrado");

        var occupants = world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId)
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell, n.CurrentAction, n.Appearance, UsingObjectOf(world.ObjectCatalog, building.BuildingTypeId, n)))
            .ToArray();

        return Result<InteriorSnapshot>.Ok(new InteriorSnapshot(building.Id, building.City, building.BuildingTypeId, OccupancyModeled: true, occupants,
            BuildingLayoutGenerator.Generate(building), BuildObjects(world.ObjectCatalog, building.BuildingTypeId)));
    }

    /// <summary>Id do primeiro objeto da mobília do tipo de prédio que atende à ação do NPC e que ele pode usar (mesma regra do motor,
    /// <see cref="LivingWorld.Simulation.Objects.ObjectUse.Serving"/>). Nulo sem catálogo: o cliente fica com a pose da ação (ADR-0037).</summary>
    private static string? UsingObjectOf(LivingWorld.Domain.Objects.ObjectCatalog? catalog, int buildingTypeId, Npc npc) =>
        catalog is not null && npc.CurrentAction is { } action
            ? LivingWorld.Simulation.Objects.ObjectUse.Serving(catalog, buildingTypeId, action, npc)?.Id
            : null;

    private static InteriorObjects? BuildObjects(LivingWorld.Domain.Objects.ObjectCatalog? catalog, int buildingTypeId)
    {
        if (catalog is null) return null;
        var furniture = catalog.FurnishingFor(buildingTypeId)
            .Select((id, slot) => new PlacedObject(id, LivingWorld.Domain.Objects.ObjectVariation.Index(catalog.Find(id)?.VariationSeed ?? 0, slot)))
            .ToArray();
        return new InteriorObjects(catalog.Format, catalog.Objects, furniture);
    }
}
