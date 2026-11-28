using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
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

public static class LivingScopeProjector
{
    public static LivingScopeState Build(
        WorldState world,
        VisualScope scope,
        IReadOnlyList<WorldEvent>? events = null,
        PresentationNames? names = null) =>
        Build(new ScopeProjectionFrame(world, names), scope, events);

    /// <summary>AD-172: projeta um escopo a partir da leitura partilhada do tick (o laço monta uma só para todos os escopos).</summary>
    public static LivingScopeState Build(ScopeProjectionFrame frame, VisualScope scope, IReadOnlyList<WorldEvent>? events = null)
    {
        var world = frame.World;
        var observable = frame.Observable;
        var cityBounds = frame.CityBounds;
        CityId? focusedCity = scope.Kind == VisualScopeKind.City && Guid.TryParse(scope.RefId, out var cityGuid)
            ? new CityId(cityGuid)
            : null;
        bool isWorld = scope.Kind == VisualScopeKind.World;

        var npcs = new List<NpcVisual>();
        var rest = new List<ProcessVisual>();
        var eat = new List<ProcessVisual>();
        var carry = new List<ProcessVisual>();
        foreach (var scoped in frame.Npcs)
        {
            if (!(focusedCity is { } id ? scoped.InCity(id) : isWorld && scoped.InWorld)) continue;
            var npc = scoped.Npc;
            npcs.Add(frame.NpcVisualOf(scoped, NpcVisualOf));
            if (npc.CurrentAction == ActionType.Sleep)
            {
                var snapshot = RestPresentation.ToProcess(world, npc);
                rest.Add(new ProcessVisual(
                    snapshot.Id, "rest", snapshot.ActorId, snapshot.Progress, snapshot.DescriptorKey,
                    snapshot.Status.Quality, snapshot.Status.RemainingHours, snapshot.Status.Location));
            }
            else if (npc.CurrentAction == ActionType.Eat)
            {
                var snapshot = FoodPresentation.ToProcess(world, npc);
                eat.Add(new ProcessVisual(
                    snapshot.Id, "food", snapshot.ActorId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.Status.RemainingHours, npc.CurrentLocation));
            }

            if (npc.IsCarrying && !frame.IsDeliveringWater(npc.Id))
            {
                var snapshot = ResourceProcessPresentation.CarryOf(npc)!;
                carry.Add(new ProcessVisual(
                    snapshot.Id, snapshot.Kind, snapshot.TargetId, snapshot.Progress, snapshot.DescriptorKey,
                    Quality: null, snapshot.RemainingHours, snapshot.Location)
                { ResourceId = npc.CarriedResourceId, ResourceName = observable.Names.Resource(npc.CarriedResourceId) });
            }
        }

        var cities = new List<CityVisual>();
        if (isWorld)
        {
            var densities = frame.Densities;
            foreach (var city in world.ActiveCities().OrderBy(city => city.Id.Value))
            {
                var bounds = cityBounds[city.Id];
                cities.Add(new CityVisual(
                    city.Id, city.Name, city.Location, frame.PopulationOf(city.Id),
                    new CellBounds(bounds.Origin.X, bounds.Origin.Y, bounds.Width, bounds.Height),
                    city.FoundedFromCityId)
                { Density = densities.GetValueOrDefault(city.Id) });
            }
        }

        var visibleBuildings = new List<Building>();
        var buildings = focusedCity is { } cityId ? Buildings(frame, cityId, visibleBuildings) : [];

        var processes = new List<ProcessVisual>(rest.Count + eat.Count + carry.Count + frame.ResourceProcesses.Count + frame.CropProcesses.Count);
        processes.AddRange(rest);
        processes.AddRange(eat);
        processes.AddRange(frame.ResourceProcesses);
        processes.AddRange(carry);
        processes.AddRange(frame.CropProcesses);
        if (focusedCity is { } processCityId && world.FindCity(processCityId) is { } processCity)
            for (int index = 0; index < processCity.ConstructionQueue.Count; index++)
            {
                var project = processCity.ConstructionQueue[index];
                long totalTicks = world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)
                    ? recipe.TicksToBuild
                    : Math.Max(1, project.TicksRemaining);
                double progress = 1.0 - project.TicksRemaining / (double)totalTicks;
                // AD-184: a obra com lote escolhido está no lote dela (antes, o anel fixo de sempre)
                var site = project.SiteX >= 0 ? new CellCoord(project.SiteX, project.SiteY) : BuildingPlacementResolver.ResolveQueuedSite(processCity, index);
                processes.Add(new ProcessVisual(index, "construction", project.BuildingTypeId, progress, "construction", Location: site));
            }
        if (focusedCity is { } worksCity) // AD-184: casa e oficina em obra, no lote, com o id da obra
            foreach (var work in LivingWorld.Simulation.Cities.Works.WorkView.InCity(world, worksCity))
                processes.Add(new ProcessVisual(-1_000_000 - work.Id, "build-work", work.Id, work.Progress, work.Kind, Location: work.Location));

        foreach (var construct in world.ExtraordinaryConstructs
            .Where(construct => IsCellInScope(construct.Origin, focusedCity, isWorld, cityBounds))
            .OrderBy(construct => construct.Id))
            processes.Add(new ProcessVisual(
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

        IReadOnlyList<IndicatorUpdate> indicators = focusedCity is { } indicatorCity
            ? BuildIndicators(CityPopulationQuery.Indicators(world, indicatorCity))
            : [];

        bool InScope(CellCoord cell) => IsCellInScope(cell, focusedCity, isWorld, cityBounds);
        var climateCell = focusedCity is { } climateCity && world.FindCity(climateCity) is { } focused
            ? focused.Location
            : new CellCoord(world.Map.Width / 2, world.Map.Height / 2);
        return new LivingScopeState(npcs, cities, buildings, processes, indicators, Events(world, events))
        {
            BuildingStates = frame.BuildingStates(visibleBuildings),
            Crops = ObservableProjector.Crops(observable, InScope),
            Animals = focusedCity is not null ? ObservableProjector.Animals(observable, InScope) : [],
            FaunaGroups = isWorld ? frame.FaunaGroups : [],
            Leavings = ObservableProjector.Leavings(observable, InScope),
            Objects = focusedCity is { } objectCity ? ObservableProjector.Objects(observable, objectCity) : [],
            Climate = ObservableProjector.Climate(world, climateCell),
            Roads = RoadsOf(world, focusedCity, isWorld),
            Works = focusedCity is { } workCity ? WorkSiteProjector.InCity(world, workCity, observable.Names) : [],
            Tick = frame.Tick,
        };
    }

    private static NpcVisual NpcVisualOf(ScopeProjectionFrame frame, ScopedNpc scoped, NpcVisual? previous)
    {
        var npc = scoped.Npc;
        var observable = ObservableProjector.Npc(frame.Observable, npc, previous?.Observable);
        var target = TargetOf(frame.World, npc, previous);
        var extraordinary = ExtraordinaryNpcVisualProjector.Build(frame.World, npc.Id);
        var stand = StandOf(frame.World, npc);
        if (previous is not null && frame.WeatherAsBefore && ReferenceEquals(previous.Observable, observable) && ReferenceEquals(previous.ActionTarget, target)
            && previous.Location == stand && previous.CurrentAction == npc.CurrentAction && previous.City == npc.City
            && previous.RelocationDestination == scoped.Relocation && Equals(previous.Extraordinary, extraordinary)
            && Equals(previous.Appearance, npc.Appearance) && previous.ActionStartedAtTick == npc.ActionStartedAtTick
            && previous.Intent == npc.CurrentIntent && previous.IntentKey == npc.IntentTarget)
            return previous; // AD-172: nada visível mudou desde o tick anterior
        var (route, arrival, over, intentTarget) = RouteOf(frame, npc, previous);
        return new NpcVisual(
            npc.Id, stand, npc.CurrentAction, npc.City, scoped.Relocation, extraordinary, npc.Appearance)
        {
            Route = route,
            RouteArrivalTick = arrival,
            RouteDepartedTick = route is null ? null : npc.ActionStartedAtTick, // AD-185: a saída da viagem, explícita
            RouteOver = over,
            Observable = observable,
            ActionStartedAtTick = npc.ActionStartedAtTick,
            ActionTarget = target,
            Intent = npc.CurrentIntent,
            IntentTarget = intentTarget,
            IntentKey = npc.IntentTarget,
        };
    }

    /// <summary>AD-176: a rota real de quem viaja, da célula onde está até a chegada, e o tick da chegada; o mesmo registro da
    /// viagem do tick anterior se ela não mudou (mesma ação, mesmo início, mesmo lugar).
    /// AD-189: e o alvo da viagem ("tipo:id", a mesma decisão do destino), calculado junto e guardado com a rota.</summary>
    private static (IReadOnlyList<CellCoord>? Route, long? Arrival, IReadOnlyList<int>? Over, string? IntentTarget) RouteOf(
        ScopeProjectionFrame frame, Npc npc, NpcVisual? previous)
    {
        if (npc.CurrentAction != ActionType.Travel) return (null, null, null, null);
        if (previous is { Route: not null } && frame.WeatherAsBefore && previous.CurrentAction == ActionType.Travel
            && previous.ActionStartedAtTick == npc.ActionStartedAtTick && previous.Location == npc.CurrentLocation
            && previous.IntentKey == npc.IntentTarget)
            return (previous.Route, previous.RouteArrivalTick, previous.RouteOver, previous.IntentTarget);
        var world = frame.World;
        if (LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.TravelDestinationFor(world, npc, frame.Markets) is not { } to
            || to == npc.CurrentLocation)
            return (null, null, null, null);
        string? intentTarget = LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.TravelIntentTargetFor(world, npc, frame.Markets);
        var cells = LivingWorld.Simulation.Cities.Streets.StreetRoutes.PathOf(world, npc.CurrentLocation, to);
        long? arrival = LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.TravelArrivalTick(world, npc, frame.Markets);
        // AD-185: as células "por cima" (escada, laje, telhado de casa colada), por índice na rota; nulo quando nenhuma
        var over = LivingWorld.Simulation.Cities.Streets.StreetRoutes.OverIndicesOf(world, cells);
        return (ValueList<CellCoord>.Of(cells), arrival, over.Count == 0 ? null : ValueList<int>.Of(over), intentTarget);
    }

    /// <summary>AD-176: as ruas do escopo, em ordem de célula (linha, coluna).</summary>
    private static IReadOnlyList<StreetCellView> RoadsOf(WorldState world, CityId? focusedCity, bool isWorld)
    {
        if (focusedCity is { } id) return world.FindActiveCity(id) is { } city ? Weathered(world, CityStreets.RoadsOf(world, city)) : [];
        if (!isWorld) return [];
        return Weathered(world, world.ActiveCities().SelectMany(c => CityStreets.RoadsOf(world, c)).OrderBy(r => r.Cell.Y).ThenBy(r => r.Cell.X).ToList());
    }

    /// <summary>AD-180: cada célula de rua com o chão do dia (molhado, lama, neve); sem o tempo, a lista como está.</summary>
    private static IReadOnlyList<StreetCellView> Weathered(WorldState world, IReadOnlyList<StreetCellView> roads)
    {
        if (world.Weather is null || roads.Count == 0) return roads;
        var result = new List<StreetCellView>(roads.Count);
        foreach (var road in roads) result.Add(road.WithWeather(world));
        return result;
    }

    /// <summary>O alvo do tick anterior ainda vale (mesmo tipo e id), sem montar o texto do id de novo. Espelha
    /// <c>NpcInspectionQuery.TargetOf</c>; o teste de cadência confere contra a projeção sem memória a cada tick.</summary>
    /// <summary>Alvo da ação. AD-193: quem trabalha no lote de uma obra (diarista ou ajudante, mesmo com emprego nas horas livres) tem a
    /// obra como alvo (<c>work:id</c>); senão o alvo de sempre. O registro anterior é reaproveitado quando o alvo não mudou.</summary>
    /// <summary>#223: onde o corpo fica desenhado. Quem trabalha no prédio do ofício fica no posto que o motor lhe deu (a célula do campo
    /// ou do piso, com lotação por célula) em vez de no ladrilho do prédio inteiro; o resto, no lugar de sempre.</summary>
    private static CellCoord StandOf(WorldState world, Npc npc) =>
        npc is { CurrentAction: ActionType.Work, Interior: { Floor.Value: 0 } inside } && world.FindBuilding(inside.Building) is { Position: { } origin }
            ? new CellCoord(origin.X + inside.LocalCell.X, origin.Y + inside.LocalCell.Y)
            : npc.CurrentLocation;

    private static NpcActionTargetDto? TargetOf(WorldState world, Npc npc, NpcVisual? previous)
    {
        if (WorkSiteProjector.SiteTargetOf(world, npc) is { } workId)
            return previous?.ActionTarget is { Kind: "work" } same && IsId(same.Id, workId)
                ? same
                : new NpcActionTargetDto("work", workId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return previous is not null && previous.ActionTarget is not { Kind: "work" } && SameTarget(previous.ActionTarget, npc)
            ? previous.ActionTarget
            : NpcInspectionQuery.TargetOf(npc);
    }

    private static bool SameTarget(NpcActionTargetDto? previous, Npc npc) => (npc.CurrentAction, previous) switch
    {
        (ActionType.Work, null) => npc.Employer is null,
        (ActionType.Sleep, null) => npc.Household is null,
        (ActionType.Socialize, null) => npc.Spouse is null,
        (_, null) => true,
        (ActionType.Work, { Kind: "workplace" }) when npc.Employer is { } workplace => IsId(previous.Id, workplace.Value),
        (ActionType.Sleep, { Kind: "household" }) when npc.Household is { } household => IsId(previous.Id, household.Value),
        (ActionType.Socialize, { Kind: "npc" }) when npc.Spouse is { } spouse => IsId(previous.Id, spouse.Value),
        _ => false,
    };

    private static bool IsId(string text, long id) => long.TryParse(text, out long parsed) && parsed == id;

    private static List<BuildingVisual> Buildings(ScopeProjectionFrame frame, CityId cityId, List<Building> visible)
    {
        var world = frame.World;
        var result = new List<BuildingVisual>();
        var city = world.FindActiveCity(cityId);
        if (city is null || !frame.CityBounds.TryGetValue(cityId, out var bounds)) return result;
        var recipes = world.CityCatalog.BuildingRecipes;
        bool pricesBuildings = recipes.Values.Any(r => r.Pricing is not null || r.Lodging is not null);
        var valuation = pricesBuildings ? new ValuationContext(world, city) : null;
        var tenants = valuation is not null ? DwellingCapacity.TenantsByBuilding(world) : null;
        foreach (var building in world.Buildings.Where(building => building.City == cityId).OrderBy(building => building.Id.Value))
        {
            // null = escassez de terra pra este prédio agora — excluído desta resposta em vez de derrubar o escopo; nunca persistido.
            if (BuildingPlacementResolver.ResolveLot(building, city, world, bounds) is not { } resolved) continue;
            if (!FitsInside(building, resolved.Position, resolved.Orientation, bounds)) continue;
            var plan = ObservableProjector.Plan(building, resolved.Orientation);
            visible.Add(building);
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

            result.Add(new BuildingVisual(building.Id, building.City, building.BuildingTypeId, resolved.Position, resolved.Orientation)
            {
                Floors = plan.Floors,
                GroundFootprint = plan.GroundFootprint,
                GroundWalls = plan.GroundWalls ?? [],
                GroundDoors = plan.GroundDoors ?? [],
                Value = value,
                Lodging = lodging,
                // AD-173: o lote de verdade (quintal, passagem da porta à rua, rua da frente)
                Lot = LotOf(world, city, building, resolved.Position, resolved.Croft),
                Work = LivingWorld.Simulation.Cities.Works.WorkView.Of(world, building), // AD-184: canteiro
            });
        }

        return result;
    }

    private static bool FitsInside(Building building, CellCoord position, int orientation, CityBounds bounds)
    {
        var footprint = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId, orientation, building.Shape);
        for (int i = 0; i < footprint.Count; i++)
            if (!bounds.Contains(new CellCoord(position.X + footprint[i].Cell.X, position.Y + footprint[i].Cell.Y)))
                return false;
        return true;
    }

    private static List<NotableVisualEvent> Events(WorldState world, IReadOnlyList<WorldEvent>? events)
    {
        var logged = (events ?? [])
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind).ThenBy(evt => evt.Payload, StringComparer.Ordinal)
            .Select(evt =>
            {
                var involved = EventInvolvementReader.Read(world, evt);
                return new NotableVisualEvent(
                    evt.Tick, evt.Kind, CrimeEventNarrator.Describe(world, evt) ?? LivingEventPresentationCatalog.Describe(evt.Kind),
                    EventInvolvementReader.LocationOf(world, evt, involved) ?? LocationOf(world, evt))
                { Involved = involved, EventId = evt.EventId };
            });
        var founding = world.ActiveCities()
            .Where(city => city.FoundedFromCityId is not null)
            .Select(city => new NotableVisualEvent(
                city.FoundedAtTick,
                WorldEventKind.SettlementFounded,
                LivingEventPresentationCatalog.Describe(WorldEventKind.SettlementFounded),
                city.Location)
            { Involved = new EventInvolved(SubjectId: city.FounderId, CityId: city.Id.ToString(), Motive: city.FounderId is null ? null : "fundador") });
        return logged
            .Concat(founding)
            .DistinctBy(evt => (evt.Tick, evt.Kind, evt.Label))
            .OrderBy(evt => evt.Tick).ThenBy(evt => evt.Kind)
            .ToList();
    }

    private static bool IsCellInScope(CellCoord cell, CityId? focusedCity, bool isWorld, IReadOnlyDictionary<CityId, CityBounds> cityBounds)
    {
        if (focusedCity is { } city) return cityBounds.TryGetValue(city, out var bounds) && bounds.Contains(cell);
        if (!isWorld) return false;
        foreach (var bounds in cityBounds.Values)
            if (bounds.Contains(cell)) return false;
        return true;
    }

    private static CellCoord? LocationOf(WorldState world, WorldEvent evt)
    {
        var first = evt.Payload.Split('|')[0];
        if (!long.TryParse(first, out var npcValue)) return null;
        return world.Npcs.FirstOrDefault(npc => npc.Id.Value == npcValue)?.CurrentLocation;
    }

    private static IReadOnlyList<IndicatorUpdate> BuildIndicators(CityIndicatorValues values) =>
    [
        new("population", values.Population),
        new("wealth", values.Wealth),
        new("health", values.Health),
        new("inequality", values.Inequality),
        new("economy", values.Economy),
        new("housing", values.Housing),
    ];

    /// <summary>AD-173: o lote com listas de igualdade por conteúdo, para o diff de escopo não reenviar o prédio a cada tick.</summary>
    private static LotView? LotOf(WorldState world, LivingWorld.Domain.Cities.City city, Building building, CellCoord position, int croft) =>
        CityStreets.LotOf(world, city, building, position, croft) is { } lot
            ? lot with { Croft = ValueList<CellCoord>.Of(lot.Croft), FrontPath = ValueList<CellCoord>.Of(lot.FrontPath), AccessCells = ValueList<CellCoord>.Of(lot.AccessCells) }
            : null;
}
