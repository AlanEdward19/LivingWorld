using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Api.Visual;

public sealed record NpcVisual(
    NpcId Id,
    CellCoord Location,
    ActionType? CurrentAction,
    CityId? City = null,
    CellCoord? RelocationDestination = null,
    ExtraordinaryNpcVisual? Extraordinary = null);
public sealed record CityVisual(
    CityId Id, string Name, CellCoord Location, long Population, CellBounds Bounds,
    CityId? FoundedFromCityId = null);
public sealed record BuildingVisual(
    BuildingId Id, CityId CityId, int BuildingTypeId, CellCoord Location, int Orientation = 0);
public sealed record ProcessVisual(
    long Id, string Kind, long TargetId, double Progress, string DescriptorKey,
    double? Quality = null, long? RemainingHours = null, CellCoord? Location = null,
    IReadOnlyList<CellCoord>? Footprint = null, string? AppearanceToken = null);
public sealed record IndicatorUpdate(string Key, double Value);
public sealed record NotableVisualEvent(long Tick, WorldEventKind Kind, string Label, CellCoord? Location = null);

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
        // dynamic-city-growth, T4b: ResolveGrownBounds alimenta os boxes de overflow das próprias
        // buildings da cidade de volta pra Resolve, fazendo os bounds realmente crescerem
        // (CITYGROW-03/05), não só no teste unitário de T4.
        var cityBounds = world.ActiveCities().ToDictionary(
            city => city.Id,
            city => CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds);

        var npcs = world.Npcs
            .Where(npc => npc.IsAlive && IsNpcInScope(world, npc, scope, cityBounds))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc => new NpcVisual(
                npc.Id, npc.CurrentLocation, npc.CurrentAction, npc.City, RelocationDestinationOf(world, npc),
                ExtraordinaryNpcVisualProjector.Build(world, npc.Id)))
            .ToList();

        var cities = scope.Kind == VisualScopeKind.World
            ? world.ActiveCities().OrderBy(city => city.Id.Value).Select(city =>
            {
                var bounds = cityBounds[city.Id];
                return new CityVisual(
                    city.Id, city.Name, city.Location, CityPopulationQuery.Population(world, city.Id),
                    new CellBounds(bounds.Origin.X, bounds.Origin.Y, bounds.Width, bounds.Height),
                    city.FoundedFromCityId);
            }).ToList()
            : [];

        CityId? focusedCity = scope.Kind == VisualScopeKind.City && Guid.TryParse(scope.RefId, out var cityGuid)
            ? new CityId(cityGuid)
            : null;
        var buildings = focusedCity is { } cityId
            ? world.Buildings.Where(building => building.City == cityId).OrderBy(building => building.Id.Value)
                .Select(building =>
                {
                    var city = world.FindActiveCity(cityId)!;
                    // dynamic-city-growth, T3: mesmos bounds já resolvidos acima (cityBounds) —
                    // Resolve precisa deles pra tentar uma célula livre antes do overflow.
                    // CITYGROW-02b: null = escassez de terra pra este prédio agora — excluído
                    // desta resposta em vez de derrubar o escopo inteiro; nunca persistido.
                    if (BuildingPlacementResolver.Resolve(building, city, world, cityBounds[cityId]) is not { } resolved)
                        return null;
                    var footprint = BuildingFootprintGenerator
                        .Generate(building.Id, building.BuildingTypeId, resolved.Orientation)
                        .Select(cell => new CellCoord(
                            resolved.Position.X + cell.Cell.X,
                            resolved.Position.Y + cell.Cell.Y));
                    if (!footprint.All(cityBounds[cityId].Contains)) return null;
                    return new BuildingVisual(
                        building.Id, building.City, building.BuildingTypeId, resolved.Position, resolved.Orientation);
                })
                .OfType<BuildingVisual>()
                .ToList()
            : [];

        var restProcesses = world.Npcs
            .Where(npc => npc.IsAlive && npc.CurrentAction == ActionType.Sleep && IsNpcInScope(world, npc, scope, cityBounds))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc =>
            {
                var snapshot = RestPresentation.ToProcess(world, npc);
                return new ProcessVisual(
                    snapshot.Id, "rest", snapshot.ActorId, snapshot.Progress, snapshot.DescriptorKey,
                    snapshot.Status.Quality, snapshot.Status.RemainingHours, snapshot.Status.Location);
            });

        var eatProcesses = world.Npcs
            .Where(npc => npc.IsAlive && npc.CurrentAction == ActionType.Eat && IsNpcInScope(world, npc, scope, cityBounds))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc =>
            {
                var snapshot = FoodPresentation.ToProcess(world, npc);
                return new ProcessVisual(
                    snapshot.Id, "food", snapshot.ActorId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.Status.RemainingHours, npc.CurrentLocation);
            });

        var resourceProcesses = world.ResourceProcesses
            .Where(process => process.Status == ProcessStatus.InProgress)
            .OrderBy(process => process.Id.Value)
            .Select(process =>
            {
                var snapshot = ResourceProcessPresentation.ToProcess(world, process);
                return new ProcessVisual(
                    snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.RemainingHours, snapshot.Location);
            });

        var carryProcesses = world.Npcs
            .Where(npc => npc.IsAlive && npc.IsCarrying && IsNpcInScope(world, npc, scope, cityBounds)
                && !world.ResourceProcesses.Any(process =>
                    process.ActorId == npc.Id && process.Status == ProcessStatus.InProgress && process.Kind == ProcessKind.DeliverWater))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc =>
            {
                var snapshot = ResourceProcessPresentation.CarryOf(npc)!;
                return new ProcessVisual(
                    snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.RemainingHours, snapshot.Location);
            });

        var cropProcesses = world.CropBatches
            .Where(crop => crop.Status != CropStatus.Harvested)
            .OrderBy(crop => crop.Id.Value)
            .Select(crop =>
            {
                var snapshot = ResourceProcessPresentation.ToCrop(world, crop);
                return new ProcessVisual(
                    snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.RemainingHours, snapshot.Location);
            });

        var constructionProcesses = focusedCity is { } processCityId && world.FindCity(processCityId) is { } processCity
            ? processCity.ConstructionQueue
                .Select((project, index) =>
                {
                    long totalTicks = world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)
                        ? recipe.TicksToBuild
                        : Math.Max(1, project.TicksRemaining);
                    double progress = 1.0 - project.TicksRemaining / (double)totalTicks;
                    var site = BuildingPlacementResolver.ResolveQueuedSite(processCity, index);
                    return new ProcessVisual(index, "construction", project.BuildingTypeId, progress, "construction",
                        Location: site);
                })
            : [];

        var extraordinaryConstructProcesses = world.ExtraordinaryConstructs
            .Where(construct => IsCellInScope(construct.Origin, scope, cityBounds))
            .OrderBy(construct => construct.Id)
            .Select(construct => new ProcessVisual(
                -construct.Id - 1,
                "extraordinary-construct",
                construct.CreatorId.Value,
                construct.Durability / (double)construct.MaxDurability,
                construct.AppearanceToken,
                Quality: construct.Durability / (double)construct.MaxDurability,
                RemainingHours: Math.Max(0, construct.ExpiresAtTick - world.CurrentDate.TotalHours),
                Location: construct.Origin,
                Footprint: construct.Footprint,
                AppearanceToken: construct.AppearanceToken));

        var processes = restProcesses
            .Concat(eatProcesses)
            .Concat(resourceProcesses)
            .Concat(carryProcesses)
            .Concat(cropProcesses)
            .Concat(constructionProcesses)
            .Concat(extraordinaryConstructProcesses)
            .ToList();

        IReadOnlyList<IndicatorUpdate> indicators = focusedCity is { } indicatorCity
            ? BuildIndicators(world, indicatorCity)
            : [];
        var loggedEvents = (events ?? [])
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind).ThenBy(evt => evt.Payload, StringComparer.Ordinal)
            .Select(evt => new NotableVisualEvent(
                evt.Tick, evt.Kind, LivingEventPresentationCatalog.Describe(evt.Kind), LocationOf(world, evt)));
        var foundingEvents = world.ActiveCities()
            .Where(city => city.FoundedFromCityId is not null)
            .Select(city => new NotableVisualEvent(
                city.FoundedAtTick,
                WorldEventKind.SettlementFounded,
                LivingEventPresentationCatalog.Describe(WorldEventKind.SettlementFounded),
                city.Location));
        var visibleEvents = loggedEvents
            .Concat(foundingEvents)
            .DistinctBy(evt => (evt.Tick, evt.Kind, evt.Label))
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind)
            .ToList();

        return new LivingScopeState(npcs, cities, buildings, processes, indicators, visibleEvents);
    }

    private static bool IsCellInScope(
        CellCoord cell,
        VisualScope scope,
        IReadOnlyDictionary<CityId, CityBounds> cityBounds) => scope.Kind switch
        {
            VisualScopeKind.City when Guid.TryParse(scope.RefId, out var cityGuid) =>
                cityBounds.TryGetValue(new CityId(cityGuid), out var bounds) && bounds.Contains(cell),
            VisualScopeKind.World => !cityBounds.Values.Any(bounds => bounds.Contains(cell)),
            _ => false,
        };

    // T50: mesmo critério geométrico de NpcScopeResolver (Domain) — cidade não encontrada nunca
    // deveria acontecer pra um NPC vivo, mas cai em "fora" (World) em vez de lançar.
    private static bool IsNpcInScope(
        WorldState world,
        Npc npc,
        VisualScope scope,
        IReadOnlyDictionary<CityId, CityBounds> cityBounds) =>
        scope.Kind switch
        {
            VisualScopeKind.City when Guid.TryParse(scope.RefId, out var cityGuid) =>
                npc.City == new CityId(cityGuid)
                && cityBounds.TryGetValue(npc.City, out var bounds)
                && NpcScopeResolver.Resolve(npc, bounds).Kind == NpcScopeKind.City,
            VisualScopeKind.World =>
                RelocationDestinationOf(world, npc) is not null
                || (cityBounds.TryGetValue(npc.City, out var bounds)
                    && NpcScopeResolver.Resolve(npc, bounds).Kind == NpcScopeKind.World),
            _ => false,
        };

    private static CellCoord? LocationOf(WorldState world, WorldEvent evt)
    {
        var first = evt.Payload.Split('|')[0];
        if (!long.TryParse(first, out var npcValue)) return null;
        return world.Npcs.FirstOrDefault(npc => npc.Id.Value == npcValue)?.CurrentLocation;
    }

    private static CellCoord? RelocationDestinationOf(WorldState world, Npc npc)
    {
        if (npc.Household is not { } householdId) return null;
        if (world.Households.FirstOrDefault(h => h.Id == householdId) is not { PendingRelocationCity: { } destinationId })
            return null;
        return world.FindCity(destinationId)?.Location;
    }

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
