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
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] InteriorObjects? Objects = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FloorRooms>? Rooms = null);

/// <summary>Peça de mobília: id do catálogo + índice de variação determinístico (ADR-0034). O cliente compartilha mesh/material por (Id, Variation).</summary>
/// <summary>#237: <c>Floor</c>, <c>Cell</c> (local da planta) e <c>Facing</c> (graus em torno de Y, fundo da peça para a parede) são o lugar que o motor deu à peça
/// (<see cref="LivingWorld.Simulation.Objects.HomeFurniture"/>); o cliente desenha ali. Sem <c>Cell</c> a peça não coube sem fechar o caminho (não se desenha).</summary>
public sealed record PlacedObject(string Id, int Variation,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Floor = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CellCoord? Cell = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Facing = null);

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
        var plan = BuildingLayoutGenerator.Generate(building);
        return world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId && IsInside(plan, interior))
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell, n.CurrentAction, n.Appearance, UsingObjectOf(world.ObjectCatalog, building.BuildingTypeId, n)))
            .ToArray();
    }

    /// <summary>#223: quem trabalha no campo em volta do prédio (posto fora da planta) não é ocupante do interior.</summary>
    private static bool IsInside(BuildingLayout layout, LivingWorld.Domain.Population.InteriorOccupancy interior) =>
        interior.Floor.Value >= 0 && interior.Floor.Value < layout.FloorLayouts.Count
        && layout.FloorLayouts[interior.Floor.Value].Cells.Any(c => c.Cell == interior.LocalCell);

    public static Result<InteriorSnapshot> Build(WorldState world, BuildingId buildingId)
    {
        var building = world.Buildings.FirstOrDefault(b => b.Id == buildingId);
        if (building is null) return Result<InteriorSnapshot>.Fail($"prédio {buildingId} não encontrado");

        var layout = BuildingLayoutGenerator.Generate(building);
        var occupants = world.Npcs
            .Where(n => n.IsAlive && n.Interior is { } interior && interior.Building == buildingId && IsInside(layout, interior))
            .Select(n => new InteriorOccupant(n.Id, n.Interior!.Floor, n.Interior!.LocalCell, n.CurrentAction, n.Appearance, UsingObjectOf(world.ObjectCatalog, building.BuildingTypeId, n)))
            .ToArray();

        return Result<InteriorSnapshot>.Ok(new InteriorSnapshot(building.Id, building.City, building.BuildingTypeId, OccupancyModeled: true, occupants,
            layout, BuildObjects(world, building), LivingWorld.Simulation.Cities.BuildingRooms.Of(building, world.ObjectCatalog).Floors));
    }

    /// <summary>Id do primeiro objeto da mobília do tipo de prédio que atende à ação do NPC e que ele pode usar (mesma regra do motor,
    /// <see cref="LivingWorld.Simulation.Objects.ObjectUse.Serving"/>). Nulo sem catálogo: o cliente fica com a pose da ação (ADR-0037).</summary>
    private static string? UsingObjectOf(LivingWorld.Domain.Objects.ObjectCatalog? catalog, int buildingTypeId, Npc npc) =>
        catalog is not null && npc.CurrentAction is { } action
            ? LivingWorld.Simulation.Objects.ObjectUse.Serving(catalog, buildingTypeId, action, npc)?.Id
            : null;

    private static InteriorObjects? BuildObjects(WorldState world, Building building)
    {
        if (world.ObjectCatalog is not { } catalog) return null;
        // camas de hospedagem: uma por vaga de moradia, só em prédio com receita de hospedagem (o mesmo número do marcador da cidade)
        int lodgingBeds = world.CityCatalog.BuildingRecipes.TryGetValue(building.BuildingTypeId, out var recipe) && recipe.Lodging is not null
            ? LivingWorld.Simulation.Cities.DwellingCapacity.Of(world, building) : 0;
        var furniture = LivingWorld.Simulation.Objects.HomeFurniture.Of(building, catalog, lodgingBeds)
            .Select(piece => piece.Spot is { } spot
                ? new PlacedObject(piece.Id, piece.Variation, spot.Floor, spot.Cell, spot.Facing)
                : new PlacedObject(piece.Id, piece.Variation))
            .ToArray();
        return new InteriorObjects(catalog.Format, catalog.Objects, furniture);
    }
}
