using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Venture;

/// <summary>A abertura em si (AD-152). Custo, todo pago com moeda que já existe: foro do lote ao tesouro da cidade; obra aos braços
/// sem trabalho da cidade (depois aos adultos da casa do fundador; sem ninguém, ele levanta sozinho e não paga obra); equipamento
/// comprado de quem o faz na cidade, que sai do estoque à venda e vira capital fixo (o que a cidade não tem pronto vem de fora pelo
/// mercado dela, ao preço de teto, e a moeda vai ao tesouro); insumo inicial das oficinas que usam insumo; e o capital de giro, que
/// é o caixa com que a oficina nasce. A oficina nasce com dono (o fundador), sócios com cota, o fundador trabalhando nela, e o
/// preço da conta (<see cref="VenturePlan"/>). Sem lote livre ou sem capital, nada muda.</summary>
internal static class VentureFounding
{
    public static Workplace? TryFound(WorldState world, TickContext ctx, VentureRules rules, VentureTrade trade, VenturePlan plan, Npc founder)
    {
        if (world.FindCity(founder.City) is not { } city || BuildingTypeOf(world, trade.LocationTypeId) is not { } buildingType) return null;

        var calendar = world.CurrentDate.Calendar;
        long minWage = world.EconomyRules.WageByProfession.Count == 0 ? 0 : world.EconomyRules.WageByProfession.Values.Min();
        long plotFee = (long)Math.Ceiling(rules.PlotFeeWageMonths * trade.Wage);
        var builders = Builders(world, rules, founder);
        long buildCost = builders.Count == 0 ? 0 : rules.BuildPersonDays * Math.Max(1, minWage / calendar.DaysPerMonth);
        var sellers = world.Workplaces.Where(w => w.City == city.Id).OrderBy(w => w.Id.Value).ToList();
        var equipment = Quote(world, sellers, rules.EquipmentResourceId, rules.EquipmentUnits);
        long imported = rules.EquipmentResourceId == 0 ? 0 : rules.EquipmentUnits - equipment.Sum(q => q.Units);
        long importCost = imported * Math.Max(1, world.EconomyRules.PriceCeiling.GetValueOrDefault(rules.EquipmentResourceId,
            world.EconomyRules.PriceFloor.GetValueOrDefault(rules.EquipmentResourceId, 1)));
        var supplies = new List<(Workplace Seller, ResourceType Resource, long Units, long Price)>();
        foreach (var (resourceId, perWorker) in trade.Recipe.Inputs.OrderBy(i => i.Key))
            supplies.AddRange(Quote(world, sellers, resourceId, perWorker * Math.Max(1, rules.StartCrew) * InputSupplySystem.TargetDays));
        long workingCapital = (long)Math.Ceiling(rules.WorkingCapitalMonths * plan.MonthlyPayroll);
        long needed = plotFee + buildCost + equipment.Sum(q => q.Units * q.Price) + importCost
            + supplies.Sum(q => q.Units * q.Price) + workingCapital;

        if (needed <= 0 || VentureCapital.Raise(world, rules, founder, needed, plan: plan, tradeSkill: trade.Skill) is not { } capital) return null;
        if (Place(world, city, buildingType) is not { } site)
        {
            world.VentureProbe?.Mark("sem_lote");
            return null;
        }

        long inherited = capital.Take(world, ctx);
        city.CreditTreasury(new Money(plotFee + importCost));
        world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: plotFee + importCost, spending: 0);
        // AD-184: com obra de verdade, a oficina não nasce no mês: o fundador guarda o resto do capital e a obra começa no lote
        bool asWork = world.StreetsOn && world.StreetRules.VentureIsWork;
        if (!asWork) PayBuilders(builders, buildCost);
        long spent = plotFee + importCost + (asWork ? 0 : buildCost);
        foreach (var (seller, resource, units, price) in equipment)
        {
            if (!seller.Withdraw(resource, units).IsSuccess) continue; // o que não veio fica no caixa da oficina
            seller.CreditTreasury(new Money(units * price));
            world.RecordResourceConsumed(resource, units); // vira bigorna e ferramenta da oficina: sai do que se vende
            spent += units * price;
        }
        if (asWork)
        {
            StartWorkshop(world, ctx, city, rules, trade, plan, founder, buildingType, site, capital, needed, spent, buildCost, inherited);
            return null;
        }

        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingType, ctx.CurrentTick, site.Position, site.Orientation);
        world.AddBuilding(building);
        LivingWorld.Simulation.Cities.Streets.StreetGroundSystem.Occupied(world, ctx.CurrentTick); // AD-192 (08d 182)
        var stakes = capital.Partners.Select(p => new WorkplaceStake(p.Npc.Id, (int)(p.Amount * (long)WorkplaceStake.Whole / needed))).ToList();
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(trade.LocationTypeId), site.Position, rules.MaxVacancies,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: plan.Prices,
            city: city.Id, building: building.Id, owner: founder.Id, partners: stakes);
        world.AddWorkplace(workplace);
        foreach (var (seller, resource, units, price) in supplies)
        {
            if (!seller.Withdraw(resource, units).IsSuccess) continue;
            seller.CreditTreasury(new Money(units * price));
            workplace.Deposit(resource, units, world.EconomyRules);
            spent += units * price;
        }

        workplace.CreditTreasury(new Money(needed - spent)); // capital de giro: o que sobrou do capital posto
        Employ(world, ctx, workplace, trade, founder);
        Pledge(world, ctx, capital, workplace);
        string origin = OriginOf(capital, inherited);
        string partners = capital.Partners.Count == 0 ? "-" : string.Join(',', capital.Partners.Select(p => $"{p.Npc.Id.Value}:{p.Amount}"));
        ctx.LogEvent(WorldEventKind.VentureFounded,
            $"{workplace.Id.Value}|{founder.Id.Value}|{origin}|{needed}|{capital.Own}|{inherited}|{partners}", sourceSystem: VentureSystem.SystemName);
        return workplace;
    }

    /// <summary>AD-184: a oficina vira obra. O foro e o equipamento já foram pagos; o resto do capital fica guardado na obra (paga os
    /// diaristas; o insumo inicial e o giro ficam de reserva) e o canteiro ocupa o lote. O empréstimo é assinado agora (as parcelas
    /// correm com a carência de sempre, mesmo sem a oficina abrir). A oficina abre em <see cref="OpenBuilt"/>.</summary>
    private static void StartWorkshop(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, VentureRules rules, VentureTrade trade,
        VenturePlan plan, Npc founder, int buildingType, (CellCoord Position, int Orientation) site, VentureCapital capital, long needed,
        long spent, long buildCost, long inherited)
    {
        long hours = (long)rules.BuildPersonDays * world.StreetRules.CrewHoursPerDay;
        string partners = capital.Partners.Count == 0 ? "-" : string.Join(',', capital.Partners.Select(p => $"{p.Npc.Id.Value}:{p.Amount}"));
        var work = LivingWorld.Simulation.Cities.Works.BuildWorks.Open(world, ctx, city, LivingWorld.Simulation.Cities.Works.BuildKind.Workshop,
            buildingType, site.Position, site.Orientation, 0, founder.Household?.Value ?? -1, founder.Id.Value, hours, spent,
            $"{founder.Id.Value}|{hours}|0|{spent}");
        work.Escrow = needed - spent;
        work.Reserve = Math.Max(0, needed - spent - buildCost);
        work.Capital = needed;
        work.LocationTypeId = trade.LocationTypeId;
        work.TradeProfession = trade.Profession.Id;
        foreach (var (resource, price) in plan.Prices.OrderBy(p => p.Key.Id)) work.Prices[resource.Id] = price;
        foreach (var (npc, amount) in capital.Partners) work.Partners.Add(new LivingWorld.Simulation.Cities.Works.BuildShare { Npc = npc.Id.Value, Amount = amount });
        work.Origin = $"{OriginOf(capital, inherited)}|{needed}|{capital.Own}|{inherited}";
        work.PartnersText = partners;
        if (capital.Granted is { } loan)
        {
            work.Loan = loan.Id;
            Credit.Lending.LogGrant(ctx, loan, capital.Loan!.TermMonths, capital.Loan.Kind);
        }
    }

    /// <summary>AD-184: a obra da oficina acabou. Ela abre no prédio pronto com o dono, os sócios com cota, o preço da conta, o insumo
    /// inicial comprado agora com o que ficou guardado e o resto como caixa; o fundador larga o que fazia e trabalha nela; o
    /// empréstimo fica com ela como garantia.</summary>
    internal static void OpenBuilt(WorldState world, TickContext ctx, LivingWorld.Simulation.Cities.Works.BuildWork work, Building building)
    {
        if (world.EconomyRules.Venture is not { } rules || world.FindNpc(new NpcId(work.Owner)) is not { IsAlive: true } founder
            || world.FindCity(building.City) is not { } city || !world.EconomyCatalog.Recipes.TryGetValue(work.LocationTypeId, out var recipe))
            return;
        var stakes = work.Partners.Select(p => new WorkplaceStake(new NpcId(p.Npc), (int)(p.Amount * (long)WorkplaceStake.Whole / Math.Max(1, work.Capital)))).ToList();
        var prices = work.Prices.OrderBy(p => p.Key).ToDictionary(p => new ResourceType(p.Key), p => p.Value);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(work.LocationTypeId), building.Position!.Value, rules.MaxVacancies,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: prices,
            city: city.Id, building: building.Id, owner: founder.Id, partners: stakes);
        world.AddWorkplace(workplace);
        var sellers = world.Workplaces.Where(w => w.City == city.Id && w != workplace).OrderBy(w => w.Id.Value).ToList();
        foreach (var (resourceId, perWorker) in recipe.Inputs.OrderBy(i => i.Key))
            foreach (var (seller, resource, units, price) in Quote(world, sellers, resourceId, perWorker * Math.Max(1, rules.StartCrew) * InputSupplySystem.TargetDays))
            {
                long take = Math.Min(units, work.Escrow / Math.Max(1, price));
                if (take <= 0 || !seller.Withdraw(resource, take).IsSuccess) continue;
                seller.CreditTreasury(new Money(take * price));
                workplace.Deposit(resource, take, world.EconomyRules);
                work.Escrow -= take * price;
            }
        workplace.CreditTreasury(new Money(work.Escrow));
        work.Escrow = 0;
        var profession = new ProfessionType(work.TradeProfession);
        if (founder.Employer is { } old) world.FindWorkplace(old)?.Fire(founder.Id);
        founder.Fire();
        workplace.Hire(founder.Id);
        founder.Hire(workplace.Id);
        if (founder.Profession != profession) founder.SwitchProfession(profession);
        founder.MarkJobChange(ctx.CurrentTick);
        ctx.LogEvent(WorldEventKind.Hired, $"{founder.Id.Value}|{workplace.Id.Value}", sourceSystem: VentureSystem.SystemName);
        if (work.Loan > 0 && world.CreditOn && world.CreditBook.Loans.FirstOrDefault(l => l.Id == work.Loan) is { } loan) loan.Workplace = workplace.Id.Value;
        var origin = (work.Origin ?? "poupanca|0|0|0").Split('|');
        ctx.LogEvent(WorldEventKind.VentureFounded,
            $"{workplace.Id.Value}|{founder.Id.Value}|{origin[0]}|{origin[1]}|{origin[2]}|{origin[3]}|{work.PartnersText ?? "-"}", sourceSystem: VentureSystem.SystemName);
    }

    /// <summary>O dono de uma oficina parada (a folha não fechou, ou ninguém trabalha nela) reabre a dele: põe o capital de giro
    /// do casal no caixa, cobra o preço da conta e volta a trabalhar nela. Sem sócio novo (as cotas são da abertura).</summary>
    public static Workplace? TryReopen(WorldState world, TickContext ctx, VentureRules rules, VentureTrade trade, VenturePlan plan, Npc owner, Workplace shop)
    {
        long needed = (long)Math.Ceiling(rules.WorkingCapitalMonths * plan.MonthlyPayroll);
        if (needed <= 0 || VentureCapital.Raise(world, rules, owner, needed, allowPartners: false, plan,
                Credit.LoanPurpose.Reopening) is not { } capital) return null;

        long inherited = capital.Take(world, ctx, Credit.LoanPurpose.Reopening);
        shop.CreditTreasury(new Money(needed));
        shop.SetClosed(false);
        shop.SetPayrollFailed(false);
        foreach (var (resource, price) in plan.Prices.OrderBy(p => p.Key.Id)) shop.SetPrice(resource, price);
        if (owner.Employer != shop.Id) Employ(world, ctx, shop, trade, owner);
        Pledge(world, ctx, capital, shop);
        string origin = OriginOf(capital, inherited);
        ctx.LogEvent(WorldEventKind.VentureFounded,
            $"{shop.Id.Value}|{owner.Id.Value}|{origin}|{needed}|{capital.Own}|{inherited}|-|reaberta", sourceSystem: VentureSystem.SystemName);
        return shop;
    }

    /// <summary>AD-155: quem quer abrir e acha na cidade uma oficina do ofício fechada (falida, tomada por um credor, ou largada) compra
    /// o prédio de quem ficou com ele, pelo que ele vale (foro, obra e equipamento), em vez de levantar outro; a moeda vai ao dono (ou
    /// ao tesouro, se é da cidade). Põe o capital de giro, os sócios entram com cota, e ela reabre com o dono novo.</summary>
    public static Workplace? TryBuyClosed(WorldState world, TickContext ctx, VentureRules rules, VentureTrade trade, VenturePlan plan, Npc founder)
    {
        if (!world.CreditOn) return null;
        Workplace? shop = null;
        foreach (var w in world.Workplaces)
            if (w.Closed && w.City == founder.City && w.LocationType.Id == trade.LocationTypeId && w.Owner != founder.Id
                && (shop is null || w.Id.Value < shop.Id.Value)) shop = w;
        if (shop is null) return null;

        long price = Credit.Debts.ShopValue(world, shop);
        long workingCapital = (long)Math.Ceiling(rules.WorkingCapitalMonths * plan.MonthlyPayroll);
        long needed = price + workingCapital;
        if (VentureCapital.Raise(world, rules, founder, needed, plan: plan, purpose: Credit.LoanPurpose.Reopening, tradeSkill: trade.Skill) is not { } capital) return null;

        long inherited = capital.Take(world, ctx, Credit.LoanPurpose.Reopening);
        if (shop.Owner is { } sellerId && world.FindNpc(sellerId) is { IsAlive: true } seller) seller.CreditWallet(new Money(price));
        else if (world.FindCity(shop.City) is { } city)
        {
            city.CreditTreasury(new Money(price));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: price, spending: 0);
        }
        else shop.CreditTreasury(new Money(price));

        shop.AssignOwner(founder.Id);
        shop.SetPartners(capital.Partners.Select(p => new WorkplaceStake(p.Npc.Id, (int)(p.Amount * (long)WorkplaceStake.Whole / needed))));
        shop.CreditTreasury(new Money(workingCapital));
        shop.SetClosed(false);
        shop.SetPayrollFailed(false);
        foreach (var (resource, p) in plan.Prices.OrderBy(x => x.Key.Id)) shop.SetPrice(resource, p);
        Employ(world, ctx, shop, trade, founder);
        Pledge(world, ctx, capital, shop);
        string partners = capital.Partners.Count == 0 ? "-" : string.Join(',', capital.Partners.Select(p => $"{p.Npc.Id.Value}:{p.Amount}"));
        ctx.LogEvent(WorldEventKind.VentureFounded,
            $"{shop.Id.Value}|{founder.Id.Value}|{OriginOf(capital, inherited)}|{needed}|{capital.Own}|{inherited}|{partners}|comprada", sourceSystem: VentureSystem.SystemName);
        return shop;
    }

    /// <summary>A oficina entra como garantia do empréstimo da abertura, e o contrato é anunciado.</summary>
    private static void Pledge(WorldState world, TickContext ctx, VentureCapital capital, Workplace shop)
    {
        if (capital.Granted is not { } loan) return;
        loan.Workplace = shop.Id.Value;
        Credit.Lending.LogGrant(ctx, loan, capital.Loan!.TermMonths, capital.Loan.Kind);
    }

    /// <summary>Origem contada: sociedade se houve sócio; empréstimo se houve credor; herança se metade ou mais do casal era herança.</summary>
    private static string OriginOf(VentureCapital capital, long inherited) =>
        capital.FromPartners > 0 ? "sociedade"
        : capital.Borrowed > 0 ? "emprestimo"
        : inherited * 2 >= capital.Own && inherited > 0 ? "heranca" : "poupanca";

    /// <summary>Tipo de prédio da oficina: o de uma que já existe no mundo, senão a receita de obra do catálogo da cidade.</summary>
    private static int? BuildingTypeOf(WorldState world, int locationTypeId)
    {
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
            if (workplace.LocationType.Id == locationTypeId && workplace.Building is { } id && world.FindBuilding(id) is { } b)
                return b.BuildingTypeId;
        return world.CityCatalog.BuildingRecipes.Where(r => r.Value.Workplace?.LocationTypeId == locationTypeId)
            .OrderBy(r => r.Key).Select(r => (int?)r.Key).FirstOrDefault();
    }

    private static (CellCoord Position, int Orientation)? Place(WorldState world, LivingWorld.Domain.Cities.City city, int buildingType)
    {
        var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, buildingType, world.CurrentDate.TotalHours);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;
        // AD-173: a oficina abre de frente para a rua (quem constrói escolhe a orientação)
        return BuildingPlacementResolver.ResolveLot(candidate, city, world, bounds, chooseOrientation: true) is { } r ? (r.Position, r.Orientation) : null;
    }

    /// <summary>Quem levanta a oficina: adultos sem trabalho da cidade, por id; sem eles, os adultos da casa do fundador.</summary>
    private static List<Npc> Builders(WorldState world, VentureRules rules, Npc founder)
    {
        var idle = new List<Npc>();
        var house = new List<Npc>();
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.City != founder.City || npc.Id == founder.Id) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            if (npc.Employer is null) idle.Add(npc);
            else if (npc.Household == founder.Household && founder.Household is not null) house.Add(npc);
        }

        var crew = idle.Count > 0 ? idle : house;
        crew.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return crew.Take(Math.Max(0, rules.BuildCrew)).ToList();
    }

    private static void PayBuilders(List<Npc> builders, long cost)
    {
        if (builders.Count == 0 || cost <= 0) return;
        long share = cost / builders.Count;
        for (int i = 0; i < builders.Count; i++)
            builders[i].CreditWallet(new Money(share + (i == 0 ? cost - share * builders.Count : 0)));
    }

    /// <summary>Compra de <paramref name="units"/> de quem faz o bem, do mais barato com estoque (desempate pelo menor id), até o que houver.</summary>
    private static List<(Workplace Seller, ResourceType Resource, long Units, long Price)> Quote(
        WorldState world, List<Workplace> sellers, int resourceId, long units)
    {
        var lines = new List<(Workplace, ResourceType, long, long)>();
        if (resourceId == 0 || units <= 0) return lines;
        var resource = new ResourceType(resourceId);
        long floor = Math.Max(1, world.EconomyRules.PriceFloor.GetValueOrDefault(resourceId, 1));
        // só vende quem faz o bem: a ferramenta da fazenda está em uso, não à venda
        foreach (var seller in sellers.Where(s => s.Stock.GetValueOrDefault(resource) > 0
                         && world.EconomyCatalog.Recipes.TryGetValue(s.LocationType.Id, out var r) && r.Outputs.GetValueOrDefault(resourceId) > 0)
                     .OrderBy(s => Math.Max(floor, s.Prices.GetValueOrDefault(resource, floor))).ThenBy(s => s.Id.Value))
        {
            if (units <= 0) break;
            long take = Math.Min(units, seller.Stock[resource]);
            lines.Add((seller, resource, take, Math.Max(floor, seller.Prices.GetValueOrDefault(resource, floor))));
            units -= take;
        }

        return lines;
    }

    private static void Employ(WorldState world, TickContext ctx, Workplace workplace, VentureTrade trade, Npc founder)
    {
        if (founder.Employer is { } old) world.FindWorkplace(old)?.Fire(founder.Id);
        founder.Fire();
        workplace.Hire(founder.Id);
        founder.Hire(workplace.Id);
        if (founder.Profession != trade.Profession) founder.SwitchProfession(trade.Profession);
        founder.MarkJobChange(ctx.CurrentTick);
        ctx.LogEvent(WorldEventKind.Hired, $"{founder.Id.Value}|{workplace.Id.Value}", sourceSystem: VentureSystem.SystemName);
    }
}
