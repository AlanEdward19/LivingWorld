using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Api.Visual.Scope;

public sealed record NpcVisual(
    NpcId Id,
    CellCoord Location,
    ActionType? CurrentAction,
    CityId? City = null,
    CellCoord? RelocationDestination = null,
    ExtraordinaryNpcVisual? Extraordinary = null,
    // #46 fatia 2: genes que o cliente usa para vestir o boneco; nulo em mundo sem AppearanceRules
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Appearance? Appearance = null)
{
    /// <summary>AD-170: sinais visíveis (ação com alvo, idade, ofício, corpo, doença, emoção, carga). No delta vem omitido quando não
    /// mudou desde o tick anterior (o cliente mantém o que já tinha).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcObservable? Observable { get; init; }

    /// <summary>AD-170: tick em que a ação atual começou, o alvo dela e a intenção (o que foi fazer e onde).</summary>
    public long ActionStartedAtTick { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcActionTargetDto? ActionTarget { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ActionType? Intent { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? IntentTarget { get; init; }
}
public sealed record CityVisual(
    CityId Id, string Name, CellCoord Location, long Population, CellBounds Bounds,
    CityId? FoundedFromCityId = null)
{
    /// <summary>AD-170: densidade da cidade no escopo Mundo (quem está dentro, sem mandar cada morador).</summary>
    public CityDensityVisual? Density { get; init; }
}
public sealed record BuildingVisual(
    BuildingId Id, CityId CityId, int BuildingTypeId, CellCoord Location, int Orientation = 0)
{
    /// <summary>AD-170: planta completa do térreo e andares, para desenhar prédio novo vindo no delta sem recarregar.</summary>
    public int Floors { get; init; } = 1;
    public IReadOnlyList<FootprintCell> GroundFootprint { get; init; } = [];
    /// <summary>AD-170: valor do imóvel e hospedagem, os mesmos do marcador de cidade (só com <c>Pricing</c>/<c>Lodging</c> no cenário).</summary>
    public long Value { get; init; }
    public LodgingMarker? Lodging { get; init; }
}
public sealed record ProcessVisual(
    long Id, string Kind, long TargetId, double Progress, string DescriptorKey,
    double? Quality = null, long? RemainingHours = null, CellCoord? Location = null,
    IReadOnlyList<CellCoord>? Footprint = null, string? AppearanceToken = null)
{
    /// <summary>AD-170: recurso do processo de material (saída, cultura ou carga) e o nome dele no período.</summary>
    public int? ResourceId { get; init; }
    public string? ResourceName { get; init; }
}
public sealed record IndicatorUpdate(string Key, double Value);
public sealed record NotableVisualEvent(long Tick, WorldEventKind Kind, string Label, CellCoord? Location = null)
{
    /// <summary>AD-170: envolvidos estruturados (ids de pessoa, casa, prédio, caso, espólio) lidos do payload.</summary>
    public EventInvolved? Involved { get; init; }

    /// <summary>AD-170: id do evento no log do motor (0 = evento sem id, como a fundação derivada da cidade).</summary>
    public long EventId { get; init; }
}

public sealed record LivingScopeState(
    IReadOnlyList<NpcVisual> Npcs,
    IReadOnlyList<CityVisual> Cities,
    IReadOnlyList<BuildingVisual> Buildings,
    IReadOnlyList<ProcessVisual> Processes,
    IReadOnlyList<IndicatorUpdate> Indicators,
    IReadOnlyList<NotableVisualEvent> Events)
{
    public static LivingScopeState Empty { get; } = new([], [], [], [], [], []);

    // AD-170 (ADR-0047 etapa 2): o observável do escopo, por acréscimo. Prédio: função e ocupação; lavoura; fauna (indivíduos na
    // cidade, bandos no mundo); espólio no chão; objetos inventados; estação e clima.
    public IReadOnlyList<BuildingStateVisual> BuildingStates { get; init; } = [];
    public IReadOnlyList<CropVisual> Crops { get; init; } = [];
    public IReadOnlyList<AnimalVisual> Animals { get; init; } = [];
    public IReadOnlyList<FaunaGroupVisual> FaunaGroups { get; init; } = [];
    public IReadOnlyList<LeavingVisual> Leavings { get; init; } = [];
    public IReadOnlyList<ObjectVisual> Objects { get; init; } = [];
    public ScopeClimateVisual? Climate { get; init; }

    public bool Equals(LivingScopeState? other) =>
        other is not null
        && Npcs.SequenceEqual(other.Npcs)
        && Cities.SequenceEqual(other.Cities)
        && Buildings.SequenceEqual(other.Buildings)
        && Processes.SequenceEqual(other.Processes)
        && Indicators.SequenceEqual(other.Indicators)
        && Events.SequenceEqual(other.Events)
        && BuildingStates.SequenceEqual(other.BuildingStates)
        && Crops.SequenceEqual(other.Crops)
        && Animals.SequenceEqual(other.Animals)
        && FaunaGroups.SequenceEqual(other.FaunaGroups)
        && Leavings.SequenceEqual(other.Leavings)
        && Objects.SequenceEqual(other.Objects)
        && Equals(Climate, other.Climate);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        AddRange(ref hash, Npcs);
        AddRange(ref hash, Cities);
        AddRange(ref hash, Buildings);
        AddRange(ref hash, Processes);
        AddRange(ref hash, Indicators);
        AddRange(ref hash, Events);
        AddRange(ref hash, BuildingStates);
        AddRange(ref hash, Crops);
        AddRange(ref hash, Animals);
        AddRange(ref hash, FaunaGroups);
        AddRange(ref hash, Leavings);
        AddRange(ref hash, Objects);
        hash.Add(Climate);
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
        IReadOnlyList<WorldEvent>? events = null,
        PresentationNames? names = null)
    {
        var observable = new ObservableContext(world, names);
        // ResolveGrownBounds alimenta os boxes de overflow das próprias
        // buildings da cidade de volta pra Resolve, fazendo os bounds realmente crescerem,
        // não só no teste unitário.
        var cityBounds = world.ActiveCities().ToDictionary(
            city => city.Id,
            city => CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds);

        var npcs = world.Npcs
            .Where(npc => npc.IsAlive && IsNpcInScope(world, npc, scope, cityBounds))
            .OrderBy(npc => npc.Id.Value)
            .Select(npc => new NpcVisual(
                npc.Id, npc.CurrentLocation, npc.CurrentAction, npc.City, RelocationDestinationOf(world, npc),
                ExtraordinaryNpcVisualProjector.Build(world, npc.Id), npc.Appearance)
            {
                Observable = ObservableProjector.Npc(observable, npc),
                ActionStartedAtTick = npc.ActionStartedAtTick,
                ActionTarget = NpcInspectionQuery.TargetOf(npc),
                Intent = npc.CurrentIntent,
                IntentTarget = npc.IntentTarget,
            })
            .ToList();

        var densities = scope.Kind == VisualScopeKind.World ? ObservableProjector.Densities(world, cityBounds) : null;

        var cities = scope.Kind == VisualScopeKind.World
            ? world.ActiveCities().OrderBy(city => city.Id.Value).Select(city =>
            {
                var bounds = cityBounds[city.Id];
                return new CityVisual(
                    city.Id, city.Name, city.Location, CityPopulationQuery.Population(world, city.Id),
                    new CellBounds(bounds.Origin.X, bounds.Origin.Y, bounds.Width, bounds.Height),
                    city.FoundedFromCityId)
                { Density = densities!.GetValueOrDefault(city.Id) };
            }).ToList()
            : [];

        CityId? focusedCity = scope.Kind == VisualScopeKind.City && Guid.TryParse(scope.RefId, out var cityGuid)
            ? new CityId(cityGuid)
            : null;
        var visibleBuildings = new List<Building>();
        var recipes = world.CityCatalog.BuildingRecipes;
        bool pricesBuildings = focusedCity is not null && recipes.Values.Any(r => r.Pricing is not null || r.Lodging is not null);
        var valuation = pricesBuildings && world.FindActiveCity(focusedCity!.Value) is { } valuedCity ? new ValuationContext(world, valuedCity) : null;
        var tenants = valuation is not null ? DwellingCapacity.TenantsByBuilding(world) : null;
        var buildings = focusedCity is { } cityId
            ? world.Buildings.Where(building => building.City == cityId).OrderBy(building => building.Id.Value)
                .Select(building =>
                {
                    var city = world.FindActiveCity(cityId)!;
                    // Mesmos bounds já resolvidos acima (cityBounds) —
                    // Resolve precisa deles pra tentar uma célula livre antes do overflow.
                    // null = escassez de terra pra este prédio agora — excluído
                    // desta resposta em vez de derrubar o escopo inteiro; nunca persistido.
                    if (BuildingPlacementResolver.Resolve(building, city, world, cityBounds[cityId]) is not { } resolved)
                        return null;
                    var footprint = BuildingFootprintGenerator
                        .Generate(building.Id, building.BuildingTypeId, resolved.Orientation)
                        .Select(cell => new CellCoord(
                            resolved.Position.X + cell.Cell.X,
                            resolved.Position.Y + cell.Cell.Y));
                    if (!footprint.All(cityBounds[cityId].Contains)) return null;
                    visibleBuildings.Add(building);
                    var plan = ObservableProjector.Plan(building, resolved.Orientation);
                    long value = 0;
                    LodgingMarker? lodging = null;
                    if (valuation is not null && recipes.TryGetValue(building.BuildingTypeId, out var recipe))
                    {
                        value = BuildingValuation.ValueOf(valuation, building, recipe);
                        if (recipe.Lodging is not null)
                            lodging = new LodgingMarker(
                                DwellingCapacity.Of(world, building), tenants!.GetValueOrDefault(building.Id),
                                BuildingValuation.RentOf(valuation, building, recipe), BuildingValuation.NightlyOf(valuation, building, recipe));
                    }
                    return new BuildingVisual(
                        building.Id, building.City, building.BuildingTypeId, resolved.Position, resolved.Orientation)
                    { Floors = plan.Floors, GroundFootprint = plan.GroundFootprint, Value = value, Lodging = lodging };
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
                    Quality: null, snapshot.RemainingHours, snapshot.Location)
                { ResourceId = process.Output.Id, ResourceName = observable.Names.Resource(process.Output.Id) };
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
                    Quality: null, snapshot.RemainingHours, snapshot.Location)
                { ResourceId = npc.CarriedResourceId, ResourceName = observable.Names.Resource(npc.CarriedResourceId) };
            });

        var cropProcesses = world.CropBatches
            .Where(crop => crop.Status != CropStatus.Harvested)
            .OrderBy(crop => crop.Id.Value)
            .Select(crop =>
            {
                var snapshot = ResourceProcessPresentation.ToCrop(world, crop);
                return new ProcessVisual(
                    snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.RemainingHours, snapshot.Location)
                { ResourceId = crop.CropResourceId, ResourceName = observable.Names.Resource(crop.CropResourceId) };
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
            .Select(evt =>
            {
                var involved = EventInvolvementReader.Read(world, evt);
                return new NotableVisualEvent(
                    evt.Tick, evt.Kind, CrimeEventNarrator.Describe(world, evt) ?? LivingEventPresentationCatalog.Describe(evt.Kind),
                    EventInvolvementReader.LocationOf(world, evt, involved) ?? LocationOf(world, evt))
                { Involved = involved, EventId = evt.EventId };
            });
        var foundingEvents = world.ActiveCities()
            .Where(city => city.FoundedFromCityId is not null)
            .Select(city => new NotableVisualEvent(
                city.FoundedAtTick,
                WorldEventKind.SettlementFounded,
                LivingEventPresentationCatalog.Describe(WorldEventKind.SettlementFounded),
                city.Location)
            { Involved = new EventInvolved(CityId: city.Id.ToString()) });
        var visibleEvents = loggedEvents
            .Concat(foundingEvents)
            .DistinctBy(evt => (evt.Tick, evt.Kind, evt.Label))
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind)
            .ToList();

        bool InScope(CellCoord cell) => IsCellInScope(cell, scope, cityBounds);
        var climateCell = focusedCity is { } climateCity && world.FindCity(climateCity) is { } focused
            ? focused.Location
            : new CellCoord(world.Map.Width / 2, world.Map.Height / 2);
        return new LivingScopeState(npcs, cities, buildings, processes, indicators, visibleEvents)
        {
            BuildingStates = ObservableProjector.BuildingStates(observable, visibleBuildings),
            Crops = ObservableProjector.Crops(observable, InScope),
            Animals = focusedCity is not null ? ObservableProjector.Animals(observable, InScope) : [],
            FaunaGroups = scope.Kind == VisualScopeKind.World ? ObservableProjector.FaunaGroups(world) : [],
            Leavings = ObservableProjector.Leavings(observable, InScope),
            Objects = focusedCity is { } objectCity ? ObservableProjector.Objects(observable, objectCity) : [],
            Climate = ObservableProjector.Climate(world, climateCell),
        };
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

    // Mesmo critério geométrico de NpcScopeResolver (Domain) — cidade não encontrada nunca
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
