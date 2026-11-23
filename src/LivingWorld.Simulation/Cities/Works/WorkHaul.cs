using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>AD-195: quem traz o material anda de verdade (<see cref="LivingWorld.Domain.Cities.StreetRules.HaulersWalk"/>). Enquanto o
/// lote não tem material para uma jornada da turma inteira, alguém da obra (diarista ou ajudante, um por hora, vários ao mesmo tempo)
/// recebe uma viagem: a despensa da casa que encomenda se ela tem o material, senão a banca mais barata de quem vende (paga na hora da
/// carga, com o estoque de quem vende), senão a mata fora da cidade (as horas de cortar, trabalhando lá). O destino de trabalho dele
/// passa a ser a fonte (<see cref="TargetOf"/>, lido por <see cref="SiteWork.DestinationOf"/>): anda pela rota, pega nas mãos o que
/// aguenta pelo peso, volta pela rota e deposita no lote (<c>WorkMaterialArrived</c>). Fome, sede, noite e tempo ruim interrompem como
/// qualquer jornada: a carga fica nas mãos dele até voltar ao lote. Quem sai da obra com a carga a leva para casa.</summary>
[LivingCapability("WORK_HAUL", Events = [WorldEventKind.WorkMaterialArrived, WorldEventKind.WorkMaterialShort, WorldEventKind.WorkResumed],
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
        if (!world.StreetsOn || !world.StreetRules.HaulersWalk || world.Ground is not { Builds.Count: > 0 } ground) return;
        bool touched = false;
        foreach (var work in ground.Builds)
            if (work.MaterialNeeded > 0 && (work.Hauls.Count > 0 || work.MaterialPlaced + work.MaterialOnSite < work.MaterialNeeded))
            {
                WorkHaul.Tick(world, ctx, work);
                touched = true;
            }
        if (touched) world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
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
            if (trip.Stage == 0 && npc.CurrentLocation == new CellCoord(trip.X, trip.Y))
            {
                if (trip.Source == "natureza")
                {
                    if (npc.CurrentAction != LivingWorld.Domain.Behavior.ActionType.Work) continue;
                    if (++trip.GatherDone < trip.GatherHours) continue;
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
        if (work.MaterialOnSite + coming >= Math.Max(1, day)) return;
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
            if (w.City != city.Id || w.Closed || w.Stock.GetValueOrDefault(resource) < units || !w.Prices.TryGetValue(resource, out long p) || p <= 0) continue;
            if (seller is null || p < price || (p == price && w.Id.Value < seller.Id.Value)) { seller = w; price = p; }
        }
        if (seller is not null && WorkMaterial.CanPay(world, work, units * price))
            return new HaulTrip { Npc = who.Id.Value, X = seller.Location.X, Y = seller.Location.Y, Source = "banca", Seller = seller.Id.Value, Units = units };
        if (rules.HouseGatherHours <= 0) return null;
        var wood = WoodsFor(world, city, new CellCoord(work.X, work.Y));
        return new HaulTrip
        {
            Npc = who.Id.Value, X = wood.X, Y = wood.Y, Source = "natureza", Units = units,
            GatherHours = Math.Max(1, CeilDiv((long)rules.HouseGatherHours * units, Math.Max(1, rules.HouseMaterialUnits))),
        };
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
        return npc.PickUp(resource, units, Math.Max(units, npc.CarryCapacity)).IsSuccess ? units : 0;
    }

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

    /// <summary>A mata mais perto do lote: a célula logo fora dos limites da cidade, do lado mais perto.</summary>
    private static CellCoord WoodsFor(WorldState world, LivingWorld.Domain.Cities.City city, CellCoord site)
    {
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds;
        int left = site.X - bounds.Origin.X + 1, right = bounds.Origin.X + bounds.Width - site.X, top = site.Y - bounds.Origin.Y + 1,
            bottom = bounds.Origin.Y + bounds.Height - site.Y;
        int best = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        var cell = best == left ? new CellCoord(bounds.Origin.X - 1, site.Y)
            : best == right ? new CellCoord(bounds.Origin.X + bounds.Width, site.Y)
            : best == top ? new CellCoord(site.X, bounds.Origin.Y - 1)
            : new CellCoord(site.X, bounds.Origin.Y + bounds.Height);
        return new CellCoord(Math.Clamp(cell.X, 0, world.Map.Width - 1), Math.Clamp(cell.Y, 0, world.Map.Height - 1));
    }

    private static long HomeOf(WorldState world, BuildWork work) =>
        work.Kind == BuildKind.Workshop && world.FindNpc(new NpcId(work.Owner)) is { Household: { } h } ? h.Value : work.Household;

    /// <summary>Quem sai da obra com a carga a leva para a despensa de casa (ou a segura, sem casa).</summary>
    private static void KeepLoad(WorldState world, Npc npc, ResourceType resource)
    {
        if (npc.CarriedResourceId != resource.Id || npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        npc.GiveCarriedTo(home);
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
