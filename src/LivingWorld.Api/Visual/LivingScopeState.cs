using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Visual;

public sealed record NpcVisual(NpcId Id, CellCoord Location, ActionType? CurrentAction);
public sealed record CityVisual(CityId Id, CellCoord Location, long Population, CellBounds Bounds);
public sealed record BuildingVisual(BuildingId Id, CityId CityId, int BuildingTypeId, CellCoord Location);
public sealed record ProcessVisual(long Id, string Kind, long TargetId, double Progress, string DescriptorKey);
public sealed record IndicatorUpdate(string Key, double Value);
public sealed record NotableVisualEvent(long Tick, WorldEventKind Kind, string Label);

public sealed record LivingScopeState(
    IReadOnlyList<NpcVisual> Npcs,
    IReadOnlyList<CityVisual> Cities,
    IReadOnlyList<BuildingVisual> Buildings,
    IReadOnlyList<ProcessVisual> Processes,
    IReadOnlyList<IndicatorUpdate> Indicators,
    IReadOnlyList<NotableVisualEvent> Events)
{
    public static LivingScopeState Empty { get; } = new([], [], [], [], [], []);

    public bool Equals(LivingScopeState? other) =>
        other is not null
        && Npcs.SequenceEqual(other.Npcs)
        && Cities.SequenceEqual(other.Cities)
        && Buildings.SequenceEqual(other.Buildings)
        && Processes.SequenceEqual(other.Processes)
        && Indicators.SequenceEqual(other.Indicators)
        && Events.SequenceEqual(other.Events);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        AddRange(ref hash, Npcs);
        AddRange(ref hash, Cities);
        AddRange(ref hash, Buildings);
        AddRange(ref hash, Processes);
        AddRange(ref hash, Indicators);
        AddRange(ref hash, Events);
        return hash.ToHashCode();
    }

    private static void AddRange<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (var value in values)
            hash.Add(value);
    }
}

public static class LivingScopeProjector
{
    public static LivingScopeState Build(
        WorldState world,
        VisualScope scope,
        IReadOnlyList<WorldEvent>? events = null)
    {
        var cityBounds = world.Cities.ToDictionary(
            city => city.Id,
            city => SpatialBoundsResolver.ResolveCity(
                city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height).Bounds);

        var npcs = world.Npcs
            .Where(npc => npc.IsAlive && IsNpcInScope(npc, scope, cityBounds))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc => new NpcVisual(npc.Id, npc.CurrentLocation, npc.CurrentAction))
            .ToList();

        var cities = scope.Kind == VisualScopeKind.World
            ? world.Cities.OrderBy(city => city.Id.Value).Select(city =>
            {
                var bounds = cityBounds[city.Id];
                return new CityVisual(
                    city.Id, city.Location, CityPopulationQuery.Population(world, city.Id),
                    new CellBounds(bounds.Origin.X, bounds.Origin.Y, bounds.Width, bounds.Height));
            }).ToList()
            : [];

        CityId? focusedCity = scope.Kind == VisualScopeKind.City && Guid.TryParse(scope.RefId, out var cityGuid)
            ? new CityId(cityGuid)
            : null;
        var buildings = focusedCity is { } cityId
            ? world.Buildings.Where(building => building.City == cityId).OrderBy(building => building.Id.Value)
                .Select(building =>
                {
                    var city = world.Cities.Single(candidate => candidate.Id == cityId);
                    var (location, _, _) = BuildingPlacementResolver.Resolve(building, city);
                    return new BuildingVisual(building.Id, building.City, building.BuildingTypeId, location);
                }).ToList()
            : [];

        IReadOnlyList<IndicatorUpdate> indicators = focusedCity is { } indicatorCity
            ? BuildIndicators(world, indicatorCity)
            : [];
        var visibleEvents = (events ?? [])
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind).ThenBy(evt => evt.Payload, StringComparer.Ordinal)
            .Select(evt => new NotableVisualEvent(evt.Tick, evt.Kind, LivingEventPresentationCatalog.Describe(evt.Kind)))
            .ToList();

        return new LivingScopeState(npcs, cities, buildings, [], indicators, visibleEvents);
    }

    private static bool IsNpcInScope(
        Npc npc,
        VisualScope scope,
        IReadOnlyDictionary<CityId, CityBounds> cityBounds) =>
        scope.Kind switch
        {
            VisualScopeKind.City when Guid.TryParse(scope.RefId, out var cityGuid) =>
                npc.City == new CityId(cityGuid)
                && cityBounds.TryGetValue(npc.City, out var bounds)
                && bounds.Contains(npc.CurrentLocation),
            VisualScopeKind.World => cityBounds.TryGetValue(npc.City, out var bounds) && !bounds.Contains(npc.CurrentLocation),
            _ => false,
        };

    private static IReadOnlyList<IndicatorUpdate> BuildIndicators(WorldState world, CityId cityId) =>
    [
        new("population", CityPopulationQuery.Population(world, cityId)),
        new("wealth", CityPopulationQuery.Wealth(world, cityId)),
        new("health", CityPopulationQuery.Health(world, cityId)),
        new("inequality", CityPopulationQuery.Inequality(world, cityId)),
        new("economy", CityPopulationQuery.Economy(world, cityId)),
        new("housing", CityPopulationQuery.Housing(world, cityId)),
    ];
}
