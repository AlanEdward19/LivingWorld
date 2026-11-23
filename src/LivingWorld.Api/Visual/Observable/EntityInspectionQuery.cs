using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>Vínculo navegável de uma ficha: relação, tipo e id da outra entidade (abre com a mesma rota) e o nome legível.</summary>
public sealed record EntityLink(string Relation, string Kind, string Id, string? Name);

/// <summary>Evento ligado à entidade, com rótulo, posição e envolvidos.</summary>
public sealed record EntityEventView(long Tick, long EventId, WorldEventKind Kind, string Label, CellCoord? Location, EventInvolved Involved);

/// <summary>Ficha genérica (ADR-0047): estado atual (forma por tipo), vínculos e história recente.</summary>
public sealed record EntityInspection(
    string Kind, string Id, string Name, object State, IReadOnlyList<EntityLink> Links, IReadOnlyList<EntityEventView> RecentEvents);

public sealed record NpcEntityState(NpcInspectionDto Npc, NpcObservable? Observable);
public sealed record BuildingEntityState(
    long Id, string CityId, int BuildingTypeId, string? TypeName, long CompletedAtTick, int Floors, BuildingStateVisual Occupancy,
    IReadOnlyList<StockLine> FullStock, long? TreasuryCents);
/// <summary>AD-193: a obra em andamento (o mesmo registro do escopo) e os nomes de quem encomenda e de quem está nela.</summary>
public sealed record WorkEntityState(WorkSiteVisual Work, string? OwnerName, string? HouseholdName, IReadOnlyList<EntityLink> Crew,
    IReadOnlyList<EntityLink> Helpers);
public sealed record CropEntityState(CropVisual Crop, long Barn, long SeedReserve, long LastHarvest, long WaterRequired, long WaterDelivered);
public sealed record AnimalEntityState(AnimalVisual Animal, bool IsAlive, long? LastBirthTick, int? RegionId);
public sealed record ObjectEntityState(ObjectVisual Object, long BuiltTick, long ExpiresTick, long SellerId, long Price);
public sealed record LeavingEntityState(LeavingVisual Leaving, IReadOnlyList<StockLine> Loads);
public sealed record HouseholdEntityState(
    long Id, string CityId, CellCoord Location, long HeadId, IReadOnlyList<long> MemberIds, long? DwellingId, bool Homeless,
    IReadOnlyList<StockLine> Stock);
public sealed record WorkplaceEntityState(
    long Id, int LocationTypeId, string? LocationTypeName, string CityId, CellCoord Location, long? BuildingId, long? OwnerId,
    IReadOnlyList<long> EmployeeIds, IReadOnlyList<StockLine> Stock, long TreasuryCents, bool Closed);

/// <summary>Resumo leve de cidade (sem camadas, portais nem moradores): o que a ficha mostra. AD-183: <see cref="FounderId"/> e
/// <see cref="FounderName"/> = quem liderou a saída e fundou a cidade-filha (nulos na cidade semeada ou fundada pelo pool).</summary>
public sealed record CityEntityState(
    string Id, string Name, CellCoord Location, long Population, long PooledResidents, long FoundedAtTick, string? FoundedFromCityId,
    string? MergedIntoCityId, CityIndicators Indicators, int Households, int Buildings, int Workplaces, ScopeClimateVisual Climate,
    long? FounderId = null, string? FounderName = null);

/// <summary>Rota genérica de inspeção (AD-170, <c>GET /entities/{kind}/{id}</c>). Leitura pura do estado do motor: reaproveita
/// <see cref="NpcInspectionQuery"/> para pessoa e os registros do escopo para o resto.</summary>
public static class EntityInspectionQuery
{
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["npc"] = "npc",
        ["pessoa"] = "npc",
        ["building"] = "building",
        ["predio"] = "building",
        ["prédio"] = "building",
        ["crop"] = "crop",
        ["lavoura"] = "crop",
        ["animal"] = "animal",
        ["object"] = "object",
        ["objeto"] = "object",
        ["leaving"] = "leaving",
        ["espolio"] = "leaving",
        ["espólio"] = "leaving",
        ["city"] = "city",
        ["cidade"] = "city",
        ["household"] = "household",
        ["casa"] = "household",
        ["workplace"] = "workplace",
        ["oficina"] = "workplace",
        ["event"] = "event",
        ["evento"] = "event",
        ["work"] = "work",
        ["obra"] = "work",
    };

    private const int RecentTake = 20;
    private const int ScanLimit = 200_000;

    /// <summary>A ficha de NPC com o nome legível do emprego (tipo de local do período).</summary>
    public static NpcInspectionDto Named(NpcInspectionDto dto, PresentationNames names) =>
        dto.Employment is { } employment
            ? dto with { Employment = employment with { Name = names.LocationType(employment.LocationTypeId) } }
            : dto;

    public static EntityInspection? Inspect(WorldState world, string kind, string id, PresentationNames names, BufferingWorldEventSink? sink)
    {
        if (!Kinds.TryGetValue(kind, out var canonical)) return null;
        var ctx = new ObservableContext(world, names);
        var reader = new Reader(world, ctx, sink);
        if (canonical == "city")
            return Guid.TryParse(id, out var guid) ? reader.City(new CityId(guid)) : null;
        if (!long.TryParse(id, out long value)) return null;
        return canonical switch
        {
            "npc" => reader.Npc(value),
            "building" => reader.Building(value),
            "crop" => reader.Crop(value),
            "animal" => reader.Animal(value),
            "object" => reader.Object(value),
            "leaving" => reader.Leaving(value),
            "household" => reader.Household(value),
            "workplace" => reader.Workplace(value),
            "event" => reader.Event(value),
            "work" => reader.Work(value),
            _ => null,
        };
    }

    private sealed class Reader(WorldState world, ObservableContext ctx, BufferingWorldEventSink? sink)
    {
        private string? NpcName(long id) => world.FindNpc(new NpcId(id))?.Name ?? world.ColdArchive.Lookup(id)?.Name;

        /// <summary>AD-178: a casa leva o nome de família do responsável ("casa dos Galmoren"); nome legado de uma palavra só
        /// (mundo antigo) cai no "casa de npc-…" de antes.</summary>
        private string? HouseName(long headId) => NpcName(headId) is not { } name ? null
            : name.LastIndexOf(' ') is var cut and > 0 ? $"casa dos {name[(cut + 1)..]}" : $"casa de {name}";
        private EntityLink Person(string relation, long id) => new(relation, "npc", id.ToString(), NpcName(id));
        private EntityLink? Person(string relation, long? id) => id is { } v && v >= 0 ? Person(relation, v) : null;
        private EntityLink Home(string relation, long id) =>
            new(relation, "household", id.ToString(), world.FindHousehold(new HouseholdId(id)) is { } h ? HouseName(h.Head.Value) : null);
        private EntityLink BuildingLink(string relation, long id) =>
            new(relation, "building", id.ToString(), world.FindBuilding(new BuildingId(id)) is { } b ? ctx.Names.BuildingType(b.BuildingTypeId) : null);
        private EntityLink WorkplaceLink(string relation, Workplace w) => new(relation, "workplace", w.Id.Value.ToString(), ctx.Names.LocationType(w.LocationType.Id));
        private EntityLink CityLink(string relation, CityId id) => new(relation, "city", id.ToString(), world.FindCity(id)?.Name);

        private IReadOnlyList<StockLine> Stock(IReadOnlyDictionary<ResourceType, long> stock) =>
            stock.Where(s => s.Value > 0).OrderBy(s => s.Key.Id).Select(s => new StockLine(s.Key.Id, s.Value, ctx.Names.Resource(s.Key.Id))).ToList();

        private IReadOnlyList<EntityEventView> Events(Func<EventInvolved, bool> match, long? factParticipant = null)
        {
            var views = new List<EntityEventView>();
            if (sink is not null)
                foreach (var evt in sink.NewestMatching(evt => match(EventInvolvementReader.Read(world, evt)), RecentTake, ScanLimit))
                    views.Add(View(evt));
            if (factParticipant is { } person)
                foreach (var fact in world.Facts.Reverse().Where(f => f.Participants.Contains(new NpcId(person))).Take(RecentTake))
                    views.Add(View(new WorldEvent(fact.Tick, fact.Kind, fact.Payload)));
            return views.DistinctBy(v => (v.Tick, v.Kind, v.Label)).OrderByDescending(v => v.Tick).ThenBy(v => v.Kind).Take(RecentTake).ToList();
        }

        private EntityEventView View(WorldEvent evt)
        {
            var involved = EventInvolvementReader.Read(world, evt);
            return new EntityEventView(evt.Tick, evt.EventId, evt.Kind,
                CrimeEventNarrator.Describe(world, evt) ?? LivingEventPresentationCatalog.Describe(evt.Kind),
                EventInvolvementReader.LocationOf(world, evt, involved), involved);
        }

        private static List<EntityLink> Links(params EntityLink?[] links) => links.OfType<EntityLink>().ToList();

        /// <summary>AD-172: nome do prédio — o tipo no período; senão o do local da oficina; senão "casa de ..." do primeiro morador
        /// responsável; só por último "prédio {id}".</summary>
        private string BuildingName(BuildingStateVisual occupancy, Workplace? workplace, long id)
        {
            if (occupancy.TypeName is { } typeName) return typeName;
            if (workplace is not null && ctx.Names.LocationType(workplace.LocationType.Id) is { } place) return place;
            if (occupancy.HouseholdIds.Count > 0 && world.FindHousehold(new HouseholdId(occupancy.HouseholdIds[0])) is { } home
                && HouseName(home.Head.Value) is { } house)
                return house;
            return occupancy.Function switch
            {
                "workplace" or "dwelling,workplace" or "lodging,workplace" => $"oficina {id}",
                "lodging" => $"hospedaria {id}",
                "dwelling" => $"moradia {id}",
                _ => $"prédio {id}",
            };
        }

        /// <summary>AD-172: ficha de um evento do buffer da API (pelo id do log): o evento e os vínculos dos envolvidos.</summary>
        public EntityInspection? Event(long id)
        {
            if (id <= 0 || sink?.NewestMatching(evt => evt.EventId == id, 1, ScanLimit) is not [var evt]) return null;
            var view = View(evt);
            var who = view.Involved;
            var links = Links(Person("subject", who.SubjectId), Person("other", who.OtherId), Person("author", who.AuthorId),
                Person("victim", who.VictimId), who.HouseholdId is { } h ? Home("household", h) : null,
                who.BuildingId is { } b ? BuildingLink("building", b) : null,
                who.CityId is { } c && Guid.TryParse(c, out var city) ? CityLink("city", new CityId(city)) : null);
            foreach (var related in who.RelatedIds ?? []) links.Add(Person("related", related));
            if (who.WorkplaceId is { } w && world.FindWorkplace(new WorkplaceId(w)) is { } workplace) links.Add(WorkplaceLink("workplace", workplace));
            if (who.LeavingId is { } l) links.Add(new EntityLink("leaving", "leaving", l.ToString(), null));
            return new EntityInspection("event", id.ToString(), view.Label, view, links, []);
        }

        public EntityInspection? Npc(long id)
        {
            var inspected = NpcInspectionQuery.Inspect(world, new NpcId(id));
            if (!inspected.IsSuccess) return null;
            var dto = Named(inspected.Value!, ctx.Names);
            var npc = world.FindNpc(new NpcId(id)) is { IsAlive: true } alive ? alive : null;
            var links = Links(Person("mother", dto.MotherId?.Value), Person("father", dto.FatherId?.Value),
                Person("spouse", dto.Spouse?.Value), Person("courting", npc?.CourtingWith?.Value));
            foreach (var child in dto.ChildIds ?? []) links.Add(Person("child", child.Value));
            if (npc is not null)
                foreach (var sibling in world.Npcs.Where(o => o.IsAlive && o.Id != npc.Id
                             && ((npc.MotherId is { } m && o.MotherId == m) || (npc.FatherId is { } f && o.FatherId == f))).OrderBy(o => o.Id.Value))
                    links.Add(Person("sibling", sibling.Id.Value));
            if (dto.Household is { } home) links.Add(Home("household", home.Value));
            if (dto.HouseholdBuildingId is { } dwelling) links.Add(BuildingLink("dwelling", dwelling.Value));
            if (dto.Employer is { } employer && world.FindWorkplace(employer) is { } workplace)
            {
                links.Add(WorkplaceLink("employer", workplace));
                if (workplace.Building is { } b) links.Add(BuildingLink("workplace-building", b.Value));
                if (workplace.Owner is { } owner) links.Add(Person("boss", owner.Value));
            }
            foreach (var owned in world.Workplaces.Where(w => w.Owner == new NpcId(id)).OrderBy(w => w.Id.Value))
                links.Add(WorkplaceLink("owns", owned));
            if (dto.City != default) links.Add(CityLink("city", dto.City));
            var observable = npc is null ? null : ObservableProjector.Npc(ctx, npc);
            return new EntityInspection("npc", id.ToString(), dto.Name, new NpcEntityState(dto, observable), links,
                Events(e => EventInvolvementReader.People(e).Contains(id), id));
        }

        public EntityInspection? Building(long id)
        {
            if (world.FindBuilding(new BuildingId(id)) is not { } building) return null;
            var occupancy = ObservableProjector.BuildingStates(ctx, [building])[0];
            var workplace = occupancy.WorkplaceId is { } wid ? world.FindWorkplace(new WorkplaceId(wid)) : null;
            var links = Links(CityLink("city", building.City), Person("owner", occupancy.OwnerId));
            if (building.UnderWork is { } underWork) links.Add(new EntityLink("work", "work", underWork.ToString(), "obra em andamento")); // AD-193
            if (workplace is not null) links.Add(WorkplaceLink("workplace", workplace));
            foreach (var h in occupancy.HouseholdIds) links.Add(Home("household", h));
            foreach (var r in occupancy.ResidentIds) links.Add(Person("resident", r));
            foreach (var w in occupancy.WorkerIds) links.Add(Person("worker", w));
            int floors = BuildingLayoutGenerator.Generate(building).Floors;
            return new EntityInspection("building", id.ToString(), BuildingName(occupancy, workplace, id),
                new BuildingEntityState(id, building.City.ToString(), building.BuildingTypeId, occupancy.TypeName, building.CompletedAtTick,
                    floors, occupancy, workplace is null ? [] : Stock(workplace.Stock), workplace?.Treasury.Amount),
                links, Events(e => e.BuildingId == id || (workplace is not null && e.WorkplaceId == workplace.Id.Value)));
        }

        /// <summary>AD-193: ficha da obra: quem encomenda (casa, fundador ou a cidade), o canteiro, a turma e os ajudantes, e os eventos dela.</summary>
        public EntityInspection? Work(long id)
        {
            if (WorkSiteProjector.Find(world, id, ctx.Names) is not { } work) return null;
            var crew = work.Crew.Select(n => Person("crew", n)).ToList();
            var helpers = work.Helpers.Select(n => Person("helper", n)).ToList();
            var links = Links(work.OwnerId is { } owner ? Person("owner", owner) : null,
                work.HouseholdId is { } h && world.FindHousehold(new HouseholdId(h)) is not null ? Home("household", h) : null,
                work.BuildingId is { } b ? BuildingLink("site", b) : null,
                Guid.TryParse(work.CityId, out var city) ? CityLink("city", new CityId(city)) : null);
            links.AddRange(crew);
            links.AddRange(helpers);
            string? ownerName = work.OwnerId is { } o ? NpcName(o) : null;
            string? homeName = work.HouseholdId is { } hh && world.FindHousehold(new HouseholdId(hh)) is { } home ? HouseName(home.Head.Value) : null;
            string what = work.Kind switch
            {
                "casa" => homeName is { } house ? $"obra da {house}" : "obra de uma casa",
                "oficina" => ownerName is { } founder ? $"obra da oficina de {founder}" : "obra de uma oficina",
                _ => $"obra da cidade: {work.TypeName ?? work.Kind}",
            };
            return new EntityInspection("work", id.ToString(), what, new WorkEntityState(work, ownerName, homeName, crew, helpers), links,
                Events(e => e.WorkId == id));
        }

        public EntityInspection? Crop(long id)
        {
            if (world.CropBatches.FirstOrDefault(c => c.Id.Value == id) is not { } crop) return null;
            var visual = ObservableProjector.Crop(ctx, crop);
            var farm = ctx.FarmAt(crop.Plot);
            var links = Links(Person("owner", visual.OwnerId));
            if (farm is not null)
            {
                links.Add(WorkplaceLink("farm", farm));
                if (farm.Building is { } b) links.Add(BuildingLink("farm-building", b.Value));
                links.Add(CityLink("city", farm.City));
            }
            return new EntityInspection("crop", id.ToString(), visual.ResourceName ?? $"lavoura {id}",
                new CropEntityState(visual, crop.Barn, crop.SeedReserve, crop.LastHarvest, crop.WaterRequired, crop.WaterDelivered),
                links, farm is null ? [] : Events(e => e.WorkplaceId == farm.Id.Value));
        }

        public EntityInspection? Animal(long id)
        {
            if (world.FindAnimal(new LivingWorld.Domain.Fauna.AnimalId(id)) is not { } animal) return null;
            string? predatorOf = world.AnimalSpeciesRules.FirstOrDefault(r => r.Species == animal.Species)?.PredatorOf;
            int? region = world.Map.TryGetCell(animal.Position, out _) ? world.Map.RegionOf(animal.Position).Value : null;
            return new EntityInspection("animal", id.ToString(), animal.Species,
                new AnimalEntityState(ObservableProjector.Animal(animal, ctx.Tick, predatorOf), animal.IsAlive, animal.LastBirthTick, region),
                [], []);
        }

        public EntityInspection? Object(long id)
        {
            if (ctx.Item(id) is not { } item) return null;
            var visual = ObservableProjector.Object(ctx, item);
            var links = Links(new EntityLink("design", "design", item.DesignId.ToString(), null), Home("household", item.HouseholdId),
                Person("holder", visual.HolderId), item.SellerId > 0 ? Person("maker", item.SellerId) : null, CityLink("city", item.City));
            if (item.MarketId is { } market && world.FindWorkplace(new WorkplaceId(market)) is { } shop) links.Add(WorkplaceLink("market", shop));
            return new EntityInspection("object", id.ToString(), visual.RootShape is { } shape ? $"{shape} #{item.DesignId}" : $"peça {id}",
                new ObjectEntityState(visual, item.BuiltTick, item.ExpiresTick, item.SellerId, item.Price), links,
                Events(e => e.DesignId == item.DesignId));
        }

        public EntityInspection? Leaving(long id)
        {
            if (world.Invention?.Leavings?.FirstOrDefault(l => l.Id == id) is not { Id: > 0 } leaving) return null;
            var visual = ObservableProjector.Leaving(ctx, leaving);
            var loads = world.Invention.LeftLoads?.Where(l => l.LeavingId == id).OrderBy(l => l.ResourceId)
                .Select(l => new StockLine(l.ResourceId, l.Quantity, ctx.Names.Resource(l.ResourceId))).ToList() ?? [];
            var links = Links(Person("dead", visual.DeadId), visual.HouseholdId is { } h ? Home("household", h) : null,
                visual.HeirHouseholdId is { } heir ? Home("heir-household", heir) : null, Person("fetcher", visual.FetcherId),
                Person("carrier", visual.CarrierId),
                visual.ItemId is { } item ? new EntityLink("object", "object", item.ToString(), null) : null);
            return new EntityInspection("leaving", id.ToString(), $"espólio de {NpcName(visual.DeadId)}",
                new LeavingEntityState(visual, loads), links, Events(e => e.LeavingId == id));
        }

        public EntityInspection? Household(long id)
        {
            if (world.FindHousehold(new HouseholdId(id)) is not { } home) return null;
            var links = Links(Person("head", home.Head.Value), CityLink("city", home.City),
                home.Dwelling is { } d ? BuildingLink("dwelling", d.Value) : null);
            foreach (var member in home.Members) links.Add(Person("member", member.Value));
            return new EntityInspection("household", id.ToString(), HouseName(home.Head.Value) ?? $"casa {id}",
                new HouseholdEntityState(id, home.City.ToString(), home.Location, home.Head.Value,
                    new ValueList<long>(home.Members.Select(m => m.Value).ToArray()), home.Dwelling?.Value, home.IsHomeless, Stock(home.Stock)),
                links, Events(e => e.HouseholdId == id));
        }

        public EntityInspection? Workplace(long id)
        {
            if (world.FindWorkplace(new WorkplaceId(id)) is not { } w) return null;
            var links = Links(CityLink("city", w.City), Person("owner", w.Owner?.Value), w.Building is { } b ? BuildingLink("building", b.Value) : null);
            foreach (var e in w.Employees) links.Add(Person("employee", e.Value));
            return new EntityInspection("workplace", id.ToString(), ctx.Names.LocationType(w.LocationType.Id) ?? $"oficina {id}",
                new WorkplaceEntityState(id, w.LocationType.Id, ctx.Names.LocationType(w.LocationType.Id), w.City.ToString(), w.Location,
                    w.Building?.Value, w.Owner?.Value, w.Employees.Select(e => e.Value).ToList(), Stock(w.Stock), w.Treasury.Amount, w.Closed),
                links, Events(e => e.WorkplaceId == id));
        }

        public EntityInspection? City(CityId id)
        {
            if (world.FindCity(id) is not { } city) return null;
            var links = Links(city.FoundedFromCityId is { } from ? CityLink("founded-from", from) : null,
                city.MergedIntoCityId is { } into ? CityLink("merged-into", into) : null, Person("founder", city.FounderId));
            foreach (var b in world.Buildings.Where(b => b.City == id).OrderBy(b => b.Id.Value)) links.Add(BuildingLink("building", b.Id.Value));
            var indicators = new CityIndicators(
                CityPopulationQuery.Population(world, id), CityPopulationQuery.Wealth(world, id), CityPopulationQuery.Health(world, id),
                CityPopulationQuery.Inequality(world, id), CityPopulationQuery.Economy(world, id), CityPopulationQuery.Housing(world, id));
            string key = id.ToString();
            return new EntityInspection("city", key, city.Name,
                new CityEntityState(key, city.Name, city.Location, indicators.Population, city.PoolNpcIds.Count, city.FoundedAtTick,
                    city.FoundedFromCityId?.ToString(), city.MergedIntoCityId?.ToString(), indicators,
                    world.Households.Count(h => h.City == id && !h.IsEmpty), world.Buildings.Count(b => b.City == id),
                    world.Workplaces.Count(w => w.City == id), ObservableProjector.Climate(world, city.Location),
                    city.FounderId, city.FounderId is { } founder ? NpcName(founder) : null),
                links, Events(e => e.CityId == key));
        }
    }
}
