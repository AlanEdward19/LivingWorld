using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>AD-195: quem traz o material anda de verdade (<see cref="LivingWorld.Domain.Cities.StreetRules.HaulersWalk"/>). Enquanto o
/// lote não tem material para uma jornada da turma inteira, alguém da obra (diarista ou ajudante, um por hora, vários ao mesmo tempo)
/// recebe uma viagem: a despensa da casa que encomenda se ela tem o material, senão a banca mais barata de quem vende (paga na hora da
/// carga, com o estoque de quem vende), senão a mata fora da cidade (as horas de cortar, trabalhando lá). O destino de trabalho dele
/// passa a ser a fonte (<see cref="TargetOf"/>, lido por <see cref="SiteWork.DestinationOf"/>): anda pela rota, pega nas mãos o que
/// aguenta pelo peso, volta pela rota e deposita no lote (<c>WorkMaterialArrived</c>). Fome, sede, noite e tempo ruim interrompem como
/// qualquer jornada: a carga fica nas mãos dele até voltar ao lote. Quem sai da obra com a carga a leva para casa.</summary>
[LivingCapability("WORK_HAUL", Events = [], // os eventos de obra são declarados por BUILD_WORKS
    Consumers = ["behavior.work-destination", "economy.wallet", "economy.stock"])]
[RunsAfter(typeof(LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem))]
public sealed class WorkHaulSystem : ISimulationSystem
{
    public const string SystemName = "work-haul";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    /// <summary>AD-195: a cada hora as viagens de material andam (pegar na fonte, depositar no lote) e mais alguém sai para buscar.</summary>
    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.StreetsOn || !world.StreetRules.HaulersWalk || world.Ground is not { } ground) return;
        bool touched = false;
        foreach (var work in ground.Builds)
            if (work.MaterialNeeded > 0 && (work.Hauls.Count > 0 || work.MaterialPlaced + work.MaterialOnSite < work.MaterialNeeded))
            {
                WorkHaul.Tick(world, ctx, work);
                touched = true;
            }
        if (touched) world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
        if (!QueueHaul.Walking(world)) return;
        bool queued = false;
        foreach (var city in world.ActiveCities())
            if (city.ConstructionQueue.FirstOrDefault(p => p.TicksRemaining > 0) is { WorkId: not null, SiteX: >= 0 } project
                && world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe))
            {
                QueueHaul.Tick(world, ctx, city, project, recipe);
                queued = true;
            }
        if (queued) world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Cities));
    }
}

internal static class WorkHaul
{
    public static bool Walking(WorldState world, BuildWork work) => world.StreetRules.HaulersWalk && work.MaterialNeeded > 0;

    /// <summary>Para onde a viagem leva quem está nela agora: a fonte (indo) ou o lote (voltando com a carga); nulo fora de viagem.</summary>
    public static CellCoord? TargetOf(BuildWork work, Npc npc)
    {
        foreach (var trip in work.Hauls)
            if (trip.Npc == npc.Id.Value) return trip.Stage == 0 ? new CellCoord(trip.X, trip.Y) : new CellCoord(work.X, work.Y);
        return null;
    }

    /// <summary>Está na fonte da viagem dele (as horas ali são jornada de obra: o diarista recebe).</summary>
    public static bool AtSource(BuildWork work, Npc npc)
    {
        foreach (var trip in work.Hauls)
            if (trip.Npc == npc.Id.Value) return trip.Stage == 0 && npc.CurrentLocation == new CellCoord(trip.X, trip.Y);
        return false;
    }

    /// <summary>Uma hora da obra: as viagens andam (pegar na fonte, depositar no lote) e, se falta material para a próxima jornada, mais
    /// alguém da turma sai para buscar.</summary>
    public static void Tick(WorldState world, TickContext ctx, BuildWork work)
    {
        if (!Walking(world, work) || world.FindActiveCity(new CityId(work.City)) is not { } city) return;
        var site = new CellCoord(work.X, work.Y);
        var resource = new ResourceType(work.MaterialResource);
        for (int i = work.Hauls.Count - 1; i >= 0; i--)
        {
            var trip = work.Hauls[i];
            var npc = world.FindNpc(new NpcId(trip.Npc));
            if (npc is not { IsAlive: true } || !InWork(work, trip.Npc))
            {
                if (npc is { IsAlive: true } && trip.Stage == 1) KeepLoad(world, npc, resource); // saiu da obra: a carga vai com ele
                work.Hauls.RemoveAt(i);
                continue;
            }
            if (trip.Stage == 0 && !trip.Reached && npc.CurrentLocation != new CellCoord(trip.X, trip.Y)
                && ctx.CurrentTick - trip.Started > 3L * world.Calendar.HoursPerDay)
            {
                work.Hauls.RemoveAt(i); // três dias sem chegar à fonte (não pôde ir): a viagem passa a outro
                continue;
            }
            if (trip.Stage == 0 && npc.CurrentLocation == new CellCoord(trip.X, trip.Y))
            {
                trip.Reached = true;
                if (trip.Source == "natureza")
                {
                    // corta por hora de trabalho na mata e empilha lá (BuildWork.CutStock); leva da pilha a carga cheia quando há, ou o que
                    // tiver ao largar o trabalho (fim da jornada, fome, noite): quem chega encontra o que outro cortou (um corta enquanto o
                    // outro carrega) e ninguém volta de mãos vazias depois de um dia de corte
                    long perUnit = Math.Max(1, trip.GatherHours / Math.Max(1, trip.Units));
                    bool working = npc.CurrentAction == LivingWorld.Domain.Behavior.ActionType.Work;
                    if (working && ++trip.GatherDone >= perUnit)
                    {
                        // #243: cada unidade sai da madeira da árvore (a mesma que o jogador corta): a árvore esgota e vira toco
                        long ready = trip.GatherDone / perUnit;
                        long got = CutFrom(world, new CellCoord(trip.X, trip.Y), ready);
                        work.CutStock += got;
                        trip.GatherDone = got < ready ? 0 : trip.GatherDone % perUnit;
                        if (got < ready)
                        {
                            // a árvore acabou: segue para a árvore viva mais perto (a mata da obra muda), ou larga a viagem para planejar de novo
                            if (PlayerNature.NearestLive(world, new CellCoord(trip.X, trip.Y), NextTreeRadius) is { } next)
                            {
                                (trip.X, trip.Y, work.WoodsX, work.WoodsY) = (next.X, next.Y, next.X, next.Y);
                                trip.Reached = false;
                                trip.Started = ctx.CurrentTick;
                                continue;
                            }
                            work.WoodsX = -1;
                            if (work.CutStock <= 0) { work.Hauls.RemoveAt(i); continue; }
                        }
                    }
                    if (work.CutStock < trip.Units && (working || work.CutStock <= 0)) continue;
                    trip.Units = Math.Min(trip.Units, work.CutStock);
                    work.CutStock -= trip.Units;
                }
                long took = Take(world, ctx, city, work, trip, npc, resource);
                if (took <= 0) { work.Hauls.RemoveAt(i); continue; } // a fonte secou ou quem paga não pode: outra viagem depois
                trip.Units = took;
                trip.Stage = 1;
            }
            else if (trip.Stage == 1 && npc.CurrentLocation == site)
            {
                var (res, units) = npc.TakeCarried();
                work.Hauls.RemoveAt(i);
                if (res != work.MaterialResource || units <= 0) continue;
                work.MaterialOnSite += units;
                work.LastProgressTick = ctx.CurrentTick; // a carga que chega é andamento da obra
                WorkEvents.Log(ctx, WorldEventKind.WorkMaterialArrived, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y,
                    $"{work.MaterialResource}|{units}|{npc.Id.Value}|{trip.Source}", cause: work.StartEventId);
                Resume(ctx, work);
            }
        }
        Assign(world, ctx, city, work, resource);
    }

    private static bool InWork(BuildWork work, long npc) => work.Crew.Contains(npc) || work.Helpers.Contains(npc);

    /// <summary>Mais alguém sai para buscar, se o lote e o que já vem a caminho não dão para uma jornada da turma.</summary>
    private static void Assign(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, BuildWork work, ResourceType resource)
    {
        long coming = 0;
        foreach (var going in work.Hauls) coming += going.Units;
        long missing = work.MaterialNeeded - work.MaterialPlaced - work.MaterialOnSite - coming;
        if (missing <= 0) return;
        int team = Math.Max(1, work.Crew.Count + work.Helpers.Count);
        long day = CeilDiv(work.MaterialNeeded * world.StreetRules.CrewHoursPerDay * team, Math.Max(1, work.HoursTotal));
        // quem não tem material no lote para a jornada vai buscar (o que já vem a caminho ainda não serve à parede): no começo da obra a
        // turma toda carrega; com o lote abastecido, só o que falta (AD-195: antes um só carregava e os outros esperavam parados)
        if (work.MaterialOnSite >= Math.Max(1, day)) return;
        Npc? who = null;
        foreach (var list in (ReadOnlySpan<List<long>>)[work.Crew, work.Helpers])
        {
            foreach (long id in list)
                if (!Hauling(work, id) && world.FindNpc(new NpcId(id)) is { IsAlive: true, IsCarrying: false } npc && npc.City.Value == work.City)
                { who = npc; break; }
            if (who is not null) break;
        }
        if (who is null) return;
        long units = Math.Min(missing, CapacityUnits(world, who, resource));
        var trip = Plan(world, city, work, who, resource, units);
        if (trip is null)
        {
            if (!work.MaterialShort)
            {
                work.MaterialShort = true;
                WorkEvents.Log(ctx, WorldEventKind.WorkMaterialShort, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y,
                    $"{work.MaterialResource}|{missing + coming}", cause: work.StartEventId);
            }
            return;
        }
        trip.Started = ctx.CurrentTick;
        work.Hauls.Add(trip);
        SiteWork.Touch(world);
    }

    private static bool Hauling(BuildWork work, long npc)
    {
        foreach (var trip in work.Hauls) if (trip.Npc == npc) return true;
        return false;
    }

    /// <summary>A fonte da carga: a despensa da casa que encomenda (se tem), a banca mais barata com estoque que quem paga pode pagar,
    /// ou a mata fora da cidade; nula sem nenhuma.</summary>
    private static HaulTrip? Plan(WorldState world, LivingWorld.Domain.Cities.City city, BuildWork work, Npc who, ResourceType resource, long units)
    {
        var rules = world.StreetRules;
        long home = HomeOf(world, work);
        if (rules.MaterialFromHome && world.FindHousehold(new HouseholdId(home)) is { IsEmpty: false } house && house.Stock.GetValueOrDefault(resource) > 0)
            return new HaulTrip { Npc = who.Id.Value, X = house.Location.X, Y = house.Location.Y, Source = "casa", Units = Math.Min(units, house.Stock[resource]) };
        LivingWorld.Domain.Economy.Workplace? seller = null;
        long price = 0;
        foreach (var w in world.Workplaces)
        {
            if (w.City != city.Id || w.Closed || w.Stock.GetValueOrDefault(resource) < 1 || !w.Prices.TryGetValue(resource, out long p) || p <= 0) continue;
            if (seller is null || p < price || (p == price && w.Id.Value < seller.Id.Value)) { seller = w; price = p; }
        }
        // AD-195: com moeda e estoque na banca, compra o que pode desta carga (antes só comprava a carga cheia, e ia cortar na mata)
        if (seller is not null && rules.BuyPartialLoads)
        {
            long affordable = Math.Min(Math.Min(units, seller.Stock.GetValueOrDefault(resource)), PayableUnits(world, work, price));
            if (affordable > 0)
                return new HaulTrip { Npc = who.Id.Value, X = seller.Location.X, Y = seller.Location.Y, Source = "banca", Seller = seller.Id.Value, Units = affordable };
        }
        else if (seller is not null && seller.Stock.GetValueOrDefault(resource) >= units && WorkMaterial.CanPay(world, work, units * price))
            return new HaulTrip { Npc = who.Id.Value, X = seller.Location.X, Y = seller.Location.Y, Source = "banca", Seller = seller.Id.Value, Units = units };
        if (rules.HouseGatherHours <= 0) return null;
        if (work.WoodsX >= 0 && PlayerNature.TreeAt(world, new CellCoord(work.WoodsX, work.WoodsY)) is not null
            && PlayerNature.Left(world, new CellCoord(work.WoodsX, work.WoodsY), world.CurrentDate.TotalMilliseconds) <= 0)
            work.WoodsX = -1; // #243: a árvore da mata da obra virou toco: a próxima viagem acha outra
        if (work.WoodsX < 0) // a mata da obra é uma só: quem vai depois encontra a pilha de quem cortou antes
        {
            var found = WoodsFor(world, city, new CellCoord(work.X, work.Y), resource);
            work.WoodsX = found.X;
            work.WoodsY = found.Y;
        }
        var wood = new CellCoord(work.WoodsX, work.WoodsY);
        return new HaulTrip
        {
            Npc = who.Id.Value,
            X = wood.X,
            Y = wood.Y,
            Source = "natureza",
            Units = units,
            GatherHours = CutHours(world, new CellCoord(work.X, work.Y), wood, units),
        };
    }

    /// <summary>Horas de cortar e preparar <paramref name="units"/> na mata, sem a caminhada. AD-195: <c>HouseGatherHours</c> (AD-184) era o
    /// total de buscar o material da casa na natureza, com as viagens dentro: <c>HouseMaterialUnits / BuildUnitsPerLoad</c> idas e voltas
    /// carregadas. Agora a ida é andada de verdade, então o corte é o total menos essas viagens pela rota deste lote até esta mata
    /// (<c>StreetRoutes.CostOf</c>, a ida com o fator da carga). Sem a regra (<c>CutWithoutWalk</c> desligada), o total como antes.</summary>
    internal static long CutHours(WorldState world, CellCoord site, CellCoord wood, long units)
    {
        var rules = world.StreetRules;
        double total = rules.HouseGatherHours;
        if (rules.CutWithoutWalk)
        {
            long loads = CeilDiv(Math.Max(1, rules.HouseMaterialUnits), Math.Max(1, rules.BuildUnitsPerLoad));
            double trip = StreetRoutes.CostOf(world, site, wood).Cost
                + StreetRoutes.CostOf(world, wood, site).Cost * LivingWorld.Simulation.Emergence.Mobility.LoadFactorOfKg(rules, rules.SiteLoadKg);
            total = Math.Max(rules.HouseMaterialUnits, total - loads * trip); // ao menos uma hora por unidade
        }
        return Math.Max(1, (long)Math.Ceiling(total * units / Math.Max(1, rules.HouseMaterialUnits)));
    }

    private static long PayableUnits(WorldState world, BuildWork work, long price)
    {
        if (price <= 0) return long.MaxValue;
        long means = work.Kind == BuildKind.Workshop ? work.Escrow : PavingDecision.MeansOf(world, work.Household);
        return Math.Max(0, means / price);
    }

    /// <summary>Pega a carga na fonte: tira da despensa, compra da banca (paga quem encomenda) ou corta na mata; devolve o que pegou.</summary>
    private static long Take(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, BuildWork work, HaulTrip trip, Npc npc, ResourceType resource)
    {
        long units = Math.Min(trip.Units, CapacityUnits(world, npc, resource));
        switch (trip.Source)
        {
            case "casa":
                if (world.FindHousehold(new HouseholdId(HomeOf(world, work))) is not { } house) return 0;
                units = Math.Min(units, house.Stock.GetValueOrDefault(resource));
                if (units <= 0 || !house.Withdraw(resource, units).IsSuccess) return 0;
                world.RecordResourceConsumed(resource, units); // AD-195: da despensa para a obra (como a compra na banca)
                break;
            case "banca":
                if (world.FindWorkplace(new WorkplaceId(trip.Seller)) is not { Closed: false } seller
                    || seller.Stock.GetValueOrDefault(resource) < units || !seller.Prices.TryGetValue(resource, out long price) || price <= 0) return 0;
                long cost = units * price;
                if (!WorkMaterial.Pay(world, ctx, city, work, cost)) return 0;
                seller.Withdraw(resource, units);
                seller.CreditTreasury(new Money(cost));
                world.RecordResourceConsumed(resource, units);
                work.Spent += cost;
                break;
        }
        if (!npc.PickUp(resource, units, Math.Max(units, npc.CarryCapacity)).IsSuccess) return 0;
        if (trip.Source == "natureza")
        {
            // AD-195: o material cortado na mata entra na economia ao ser pego e vai direto para a obra
            world.RecordResourceProduced(resource, units);
            world.RecordResourceConsumed(resource, units);
        }
        return units;
    }

    /// <summary>Até onde, em células do lote, a obra procura a árvore viva mais perto (a mata fica fora do contorno da cidade).</summary>
    private const int TreeSearchRadius = 12;

    /// <summary>Até onde, em células, o corte procura outra árvore viva quando a da viagem acaba.</summary>
    private const int NextTreeRadius = 6;

    /// <summary>#243: tira <paramref name="units"/> da madeira da árvore da célula da viagem; devolve quanto saiu. Célula sem árvore do motor
    /// (a mata do mapa, a borda da cidade) corta sem limite, como antes.</summary>
    private static long CutFrom(WorldState world, CellCoord cell, long units) =>
        PlayerNature.TreeAt(world, cell) is null ? units : PlayerNature.Take(world, cell, world.CurrentDate.TotalMilliseconds, units);

    /// <summary>Quanto do material a pessoa aguenta numa viagem: o teto de carga dela e o peso que ainda deixa andar
    /// (<c>CarryMaxKg</c>); ao menos uma unidade.</summary>
    internal static long CapacityUnits(WorldState world, Npc npc, ResourceType resource)
    {
        var rules = world.StreetRules;
        double grams = world.InventionRules?.UnitGramsOf(resource.Id) is { } g && g > 0
            ? g
            : rules.SiteLoadKg * 1000.0 / Math.Max(1, rules.BuildUnitsPerLoad);
        long byWeight = (long)Math.Floor(rules.CarryMaxKg * 1000.0 / Math.Max(1, grams));
        long byHands = npc.CarryCapacity > 0 ? npc.CarryCapacity : Npc.DefaultCarryCapacity;
        return Math.Max(1, Math.Min(byHands, byWeight));
    }

    /// <summary>A mata mais perto do lote: a célula logo fora dos limites da cidade, do lado mais perto, que se alcança a pé do lote
    /// (água e terreno sem passagem ficam de fora); sem nenhuma, a borda mais perto.</summary>
    private static CellCoord WoodsFor(WorldState world, LivingWorld.Domain.Cities.City city, CellCoord site, ResourceType resource)
    {
        // #243: a árvore viva mais perto do lote (a mesma que o jogador vê e corta, com madeira que se esgota), alcançável a pé
        var map = world.Map;
        long now = world.CurrentDate.TotalMilliseconds;
        int tried = 0;
        for (int r = 1; r <= TreeSearchRadius && tried < 6; r++)
            for (int dy = -r; dy <= r && tried < 6; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var tree = new CellCoord(site.X + dx, site.Y + dy);
                    if (PlayerNature.TreeAt(world, tree) is null || PlayerNature.Left(world, tree, now) <= 0) continue;
                    tried++;
                    if (LivingWorld.Simulation.Behavior.Resolvers.RestPlaceResolver.IsReachable(map, site, tree)) return tree;
                }
        tried = 0;
        // a célula mais perto do lote onde o mapa tem o material (a mata de verdade), alcançável a pé
        for (int r = 1; r <= 24 && tried < 6; r++)
            for (int dy = -r; dy <= r && tried < 6; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var cell = new CellCoord(site.X + dx, site.Y + dy);
                    if (!map.TryGetCell(cell, out var mapCell) || !mapCell.Resources.Contains(resource)) continue;
                    tried++;
                    if (LivingWorld.Simulation.Behavior.Resolvers.RestPlaceResolver.IsReachable(map, site, cell)) return cell;
                    if (tried >= 6) break;
                }
        return EdgeWoods(world, city, site);
    }

    /// <summary>Sem mata no mapa por perto: a borda da cidade mais perto, alcançável.</summary>
    private static CellCoord EdgeWoods(WorldState world, LivingWorld.Domain.Cities.City city, CellCoord site)
    {
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds;
        var options = new (int Distance, CellCoord Cell)[]
        {
            (site.X - bounds.Origin.X + 1, new CellCoord(bounds.Origin.X - 1, site.Y)),
            (bounds.Origin.X + bounds.Width - site.X, new CellCoord(bounds.Origin.X + bounds.Width, site.Y)),
            (site.Y - bounds.Origin.Y + 1, new CellCoord(site.X, bounds.Origin.Y - 1)),
            (bounds.Origin.Y + bounds.Height - site.Y, new CellCoord(site.X, bounds.Origin.Y + bounds.Height)),
        };
        CellCoord? first = null;
        foreach (var (_, raw) in options.OrderBy(o => o.Distance).ThenBy(o => o.Cell.X).ThenBy(o => o.Cell.Y))
        {
            var cell = new CellCoord(Math.Clamp(raw.X, 0, world.Map.Width - 1), Math.Clamp(raw.Y, 0, world.Map.Height - 1));
            first ??= cell;
            if (LivingWorld.Simulation.Behavior.Resolvers.RestPlaceResolver.IsReachable(world.Map, site, cell)) return cell;
        }
        return first!.Value;
    }

    private static long HomeOf(WorldState world, BuildWork work) =>
        work.Kind == BuildKind.Workshop && world.FindNpc(new NpcId(work.Owner)) is { Household: { } h } ? h.Value : work.Household;

    /// <summary>Quem sai da obra com a carga a leva para a despensa de casa (ou a segura, sem casa).</summary>
    private static void KeepLoad(WorldState world, Npc npc, ResourceType resource)
    {
        if (npc.CarriedResourceId != resource.Id || npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        long units = npc.CarriedQuantity;
        npc.GiveCarriedTo(home);
        world.RecordResourceProduced(resource, units); // AD-195: o que era da obra volta à despensa
    }

    /// <summary>Obra pronta ou abandonada: quem estava com carga dela a leva para casa.</summary>
    public static void Release(WorldState world, BuildWork work)
    {
        var resource = new ResourceType(work.MaterialResource);
        foreach (var trip in work.Hauls)
            if (trip.Stage == 1 && world.FindNpc(new NpcId(trip.Npc)) is { IsAlive: true } npc) KeepLoad(world, npc, resource);
        work.Hauls.Clear();
    }

    private static void Resume(TickContext ctx, BuildWork work)
    {
        if (!work.MaterialShort) return;
        work.MaterialShort = false;
        WorkEvents.Log(ctx, WorldEventKind.WorkResumed, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y, "0|material",
            cause: work.StartEventId);
    }

    private static long CeilDiv(long a, long b) => (a + b - 1) / b;
}
