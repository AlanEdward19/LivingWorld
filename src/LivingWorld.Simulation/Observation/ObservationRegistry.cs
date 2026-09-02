using LivingWorld.Domain;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

public enum ScopeKind { World, City, Building }

/// <summary>Espelha <c>SpaceId</c> do cliente (World / City / Building) — mesmo vocabulário,
/// sem tradução na API (Fase 28, LOD-04).</summary>
public readonly record struct SpaceScope(ScopeKind Kind, CityId? CityId = null, BuildingId? BuildingId = null)
{
    public static SpaceScope World() => new(ScopeKind.World);

    public static SpaceScope City(CityId cityId) => new(ScopeKind.City, cityId);

    public static SpaceScope Building(CityId cityId, BuildingId buildingId) =>
        new(ScopeKind.Building, cityId, buildingId);
}

/// <summary>União dos escopos de toda fonte de observação ativa — não-canônico, não entra no
/// hash do mundo (Fase 28, T2, LOD-04).</summary>
public sealed class ObservationRegistry
{
    private readonly Dictionary<string, SpaceScope> _scopesBySource = new(StringComparer.Ordinal);

    public void SetScope(string sourceId, SpaceScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        _scopesBySource[sourceId] = scope;
    }

    public void ClearScope(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        _scopesBySource.Remove(sourceId);
    }

    /// <summary>Verdadeiro se qualquer fonte ativa enquadra o lugar do <paramref name="npc"/>
    /// (união, LOD-04).</summary>
    public bool IsObserved(Npc npc, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(world);

        foreach (var scope in _scopesBySource.Values)
        {
            if (ScopeCoversNpc(scope, npc, world))
                return true;
        }

        return false;
    }

    private static bool ScopeCoversNpc(SpaceScope scope, Npc npc, WorldState world) => scope.Kind switch
    {
        ScopeKind.World => true,
        ScopeKind.City => scope.CityId is { } cityId && npc.City == cityId,
        ScopeKind.Building => scope.BuildingId is { } buildingId
            && scope.CityId is { } cityId
            && npc.Interior is { Building: var occupiedBuilding }
            && occupiedBuilding == buildingId
            && world.FindBuilding(buildingId) is { City: var buildingCity }
            && buildingCity == cityId,
        _ => false,
    };
}
