using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>AD-194: o material da casa e da oficina é estoque do canteiro (<see cref="StreetRules.MaterialOnSite"/>). As horas de quem
/// está na obra vão primeiro à parede enquanto há material no lote; o resto vai a trazer carga: comprada de quem vende na cidade, carga
/// por carga (a casa, ou o que o fundador guardou, paga cada uma), ou buscada na natureza; <c>BuildUnitsPerLoad</c> unidades por
/// viagem, a ida carregada e a volta pela rota. Sem material no lote, sem quem venda ou sem como pagar, e sem como buscar, a obra trava
/// (evento uma vez por episódio) e retoma quando a carga volta a chegar. O que sobra no lote volta a quem pagou.</summary>
internal static class WorkMaterial
{
    /// <summary>A obra que abre agora leva material de verdade.</summary>
    public static void Plan(WorldState world, BuildWork work)
    {
        var rules = world.StreetRules;
        if (!rules.MaterialOnSite || rules.HouseMaterialUnits <= 0) return;
        work.MaterialResource = rules.HouseMaterialResourceId;
        work.MaterialNeeded = rules.HouseMaterialUnits;
    }

    /// <summary>Horas de obra de alguém no lote: as que a parede pode usar com o material que há (devolvidas, já postas), o resto vai a
    /// trazer carga. Obra sem plano de material (aberta antes, ou regra desligada) usa todas.</summary>
    public static long Spend(WorldState world, TickContext ctx, BuildWork work, Npc npc, long done)
    {
        if (work.MaterialNeeded <= 0 || done <= 0) return done;
        long total = Math.Max(1, work.HoursTotal);
        long built = total - Math.Max(0, work.HoursLeft);
        long have = work.MaterialPlaced + work.MaterialOnSite;
        long cap = have >= work.MaterialNeeded ? total : have * total / work.MaterialNeeded;
        long build = Math.Clamp(cap - built, 0, done);
        if (build > 0)
        {
            long placed = Math.Min(work.MaterialNeeded, CeilDiv(work.MaterialNeeded * (built + build), total));
            long moved = Math.Clamp(placed - work.MaterialPlaced, 0, work.MaterialOnSite);
            work.MaterialPlaced += moved;
            work.MaterialOnSite -= moved;
        }
        long rest = done - build;
        if (rest > 0 && work.MaterialPlaced + work.MaterialOnSite < work.MaterialNeeded)
        {
            work.HaulBanked += rest;
            Haul(world, ctx, work, npc);
        }
        return build;
    }

    /// <summary>As viagens que as horas guardadas já pagam chegam ao lote; sem nenhuma carga possível, a obra trava.</summary>
    private static void Haul(WorldState world, TickContext ctx, BuildWork work, Npc npc)
    {
        if (world.FindActiveCity(new CityId(work.City)) is not { } city) return;
        while (work.MaterialPlaced + work.MaterialOnSite < work.MaterialNeeded)
        {
            var load = NextLoad(world, work, city);
            if (load is null)
            {
                if (!work.MaterialShort)
                {
                    work.MaterialShort = true;
                    long missing = work.MaterialNeeded - work.MaterialPlaced - work.MaterialOnSite;
                    WorkEvents.Log(ctx, WorldEventKind.WorkMaterialShort, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y,
                        $"{work.MaterialResource}|{missing}", cause: work.StartEventId);
                }
                work.HaulBanked = Math.Min(work.HaulBanked, world.StreetRules.CrewHoursPerDay); // horas sem carga não se acumulam
                return;
            }
            var (units, hours, seller, price) = load.Value;
            if (work.HaulBanked < hours) return; // a viagem segue na próxima jornada
            if (seller is not null)
            {
                long cost = units * price;
                if (!Pay(world, ctx, city, work, cost)) { work.HaulBanked = 0; return; }
                seller.Withdraw(new ResourceType(work.MaterialResource), units);
                seller.CreditTreasury(new Money(cost));
                world.RecordResourceConsumed(new ResourceType(work.MaterialResource), units);
                work.Spent += cost;
            }
            work.HaulBanked -= hours;
            work.MaterialOnSite += units;
            WorkEvents.Log(ctx, WorldEventKind.WorkMaterialArrived, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y,
                $"{work.MaterialResource}|{units}|{npc.Id.Value}|{(seller is null ? "natureza" : "banca")}", cause: work.StartEventId);
            if (work.MaterialShort)
            {
                work.MaterialShort = false;
                WorkEvents.Log(ctx, WorldEventKind.WorkResumed, work.Id, work.City, BuildWorks.TypeOf(work), work.X, work.Y,
                    "0|material", cause: work.StartEventId);
            }
        }
    }

    /// <summary>A próxima carga: de quem vende na cidade (o mais barato com estoque, desempate por id) se quem paga pode, pela rota ida
    /// carregada e volta; senão buscada na natureza (as horas de buscar da casa, por unidade); nula se nenhuma das duas.</summary>
    private static (long Units, long Hours, LivingWorld.Domain.Economy.Workplace? Seller, long Price)? NextLoad(WorldState world, BuildWork work, LivingWorld.Domain.Cities.City city)
    {
        var rules = world.StreetRules;
        long units = Math.Min(Math.Max(1, rules.BuildUnitsPerLoad), work.MaterialNeeded - work.MaterialPlaced - work.MaterialOnSite);
        var resource = new ResourceType(work.MaterialResource);
        var site = new CellCoord(work.X, work.Y);
        LivingWorld.Domain.Economy.Workplace? seller = null;
        long price = 0;
        foreach (var w in world.Workplaces)
        {
            if (w.City != city.Id || w.Closed || w.Stock.GetValueOrDefault(resource) < units || !w.Prices.TryGetValue(resource, out long p) || p <= 0) continue;
            if (seller is null || p < price || (p == price && w.Id.Value < seller.Id.Value)) { seller = w; price = p; }
        }
        if (seller is not null && CanPay(world, work, units * price))
        {
            double trip = StreetRoutes.CostOf(world, seller.Location, site).Cost
                * LivingWorld.Simulation.Emergence.Mobility.LoadFactorOfKg(rules, rules.SiteLoadKg)
                + StreetRoutes.CostOf(world, site, seller.Location).Cost;
            return (units, Math.Max(1, (long)Math.Ceiling(trip)), seller, price);
        }
        if (rules.HouseGatherHours <= 0) return null;
        return (units, Math.Max(1, CeilDiv((long)rules.HouseGatherHours * units, Math.Max(1, rules.HouseMaterialUnits))), null, 0);
    }

    private static bool CanPay(WorldState world, BuildWork work, long cost) =>
        work.Kind == BuildKind.Workshop ? work.Escrow >= cost : PavingDecision.MeansOf(world, work.Household) >= cost;

    private static bool Pay(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, BuildWork work, long cost)
    {
        if (work.Kind == BuildKind.Workshop)
        {
            if (work.Escrow < cost) return false;
            work.Escrow -= cost;
            return true;
        }
        return PavingDecision.PayFrom(world, ctx, city, [work.Household], cost);
    }

    /// <summary>O material que ficou no lote (obra pronta ou abandonada) volta à despensa da casa que encomendou, ou à do fundador.</summary>
    public static void ReturnLeftover(WorldState world, BuildWork work)
    {
        if (work.MaterialOnSite <= 0) return;
        long units = work.MaterialOnSite;
        work.MaterialOnSite = 0;
        long home = work.Household;
        if (work.Kind == BuildKind.Workshop && world.FindNpc(new NpcId(work.Owner)) is { Household: { } h }) home = h.Value;
        if (world.FindHousehold(new HouseholdId(home)) is { IsEmpty: false } house)
            house.Deposit(new ResourceType(work.MaterialResource), units);
        else if (world.FindActiveCity(new CityId(work.City)) is { } city)
            city.DepositStock(new ResourceType(work.MaterialResource), units);
    }

    /// <summary>Horas de carregar que ainda faltam, pela próxima carga (o cliente mostra "falta carregar").</summary>
    public static long HaulHoursLeft(WorldState world, BuildWork work)
    {
        long missing = work.MaterialNeeded - work.MaterialPlaced - work.MaterialOnSite;
        if (missing <= 0 || world.FindActiveCity(new CityId(work.City)) is not { } city || NextLoad(world, work, city) is not { } load) return 0;
        long trips = CeilDiv(missing, Math.Max(1, load.Units));
        return Math.Max(0, trips * load.Hours - work.HaulBanked);
    }

    private static long CeilDiv(long a, long b) => (a + b - 1) / b;
}
