using System.Globalization;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>A peça na economia (AD-143). Carga: quem atravessa água entre a casa e o mercado leva só o que cabe (a pé, o que uma
/// pessoa carrega; na peça da casa, a carga útil que o avaliador mediu). Venda: quem sabe fazer expõe no mercado; quem chega ao
/// mercado precisando atravessar água e sem ter com quê compra se o preço couber no bolso e no que a peça lhe pouparia; quem compra
/// não precisa saber fazer. O preço nasce do custo da hora de quem fez e anda com oferta e procura pela mesma regra do mercado de
/// recursos (<see cref="EconomyRules.PriceSensitivity"/>), sem tabela.</summary>
public static class InventionMarket
{
    public const string SourceSystem = "invention-market";

    /// <summary>Carga (g) que a casa leva numa ida ao mercado; <see cref="long.MaxValue"/> quando nada corta: sem água no caminho e
    /// com <see cref="InventionRules.LandCargo"/> desligado, ou com o mercado na porta de casa. AD-153: por terra também se leva só o
    /// que cabe nos braços, no recipiente ou no veículo de terra da casa (puxado por quem foi; ele vadeia como quem anda).</summary>
    /// AD-157: com <paramref name="buyer"/>, o recipiente é o de quem foi (o que levou, ou o que ninguém da casa levou).
    public static long CargoGrams(WorldState world, Household household, CellCoord market, Npc? buyer = null)
    {
        if (!world.InventionOn) return long.MaxValue;
        var rules = world.InventionRules!;
        int water = LivingWorld.Simulation.Cities.Streets.StreetRoutes.WaterCells(world, household.Location, market); // AD-176: a água da rota real
        if (water == 0 && (!rules.LandCargo || household.Location == market)) return long.MaxValue;
        long foot = rules.FootCarryKg * 1000L;
        // AD-147: o recipiente da casa: preso ao corpo soma ao que os braços levam; na mão, leva o que ele aguenta
        var carrier = buyer is null
            ? world.Invention!.BestCarrier(household.Id.Value, rules)
            : world.Invention!.CarrierFor(buyer.Id.Value, household.Id.Value, rules, out _);
        if (carrier.Exists)
        {
            foot = carrier.Worn ? foot + carrier.CapacityG : Math.Max(foot, carrier.CapacityG);
            world.InventionTally.CarrierCargoTrips++;
            world.InventionTally.CarrierCargoGrams += foot - rules.FootCarryKg * 1000L;
        }
        // AD-176: o veículo de terra pede caminho que o comporte; com mais terreno sem caminho na rota, a carga volta aos braços
        if (rules.LandVehicles && world.Invention.BestHauler(household.Id.Value, rules) is { Exists: true } hauler && hauler.CapacityG > foot)
        {
            if (LivingWorld.Simulation.Cities.Streets.StreetRoutes.CartFits(world, household.Location, market)) foot = hauler.CapacityG;
            else world.InventionTally.HaulerRouteBlocked++;
        }
        if (water == 0) return foot;
        var craft = world.Invention!.BestCraft(household.Id.Value, rules);
        if (craft.SpeedMmS > 0 && WaterCrossing.ExtraCost(world.Map, rules, water, craft.SpeedMmS) < WaterCrossing.ExtraCost(world.Map, rules, water, 0))
        {
            world.InventionTally.CraftCargoTrips++;
            return Math.Max(foot, craft.PayloadG);
        }
        return foot;
    }

    /// <summary>Corta a quantidade ao que cabe na carga que sobra e desconta o que foi levado.</summary>
    public static long Fit(WorldState world, int resourceId, long quantity, ref long cargoGrams)
    {
        long cut = 0;
        return Fit(world, resourceId, quantity, ref cargoGrams, ref cut);
    }

    /// <summary>Como <see cref="Fit(WorldState, int, long, ref long)"/>, somando em <paramref name="cutGrams"/> o que ficou (AD-147).</summary>
    public static long Fit(WorldState world, int resourceId, long quantity, ref long cargoGrams, ref long cutGrams)
    {
        if (cargoGrams == long.MaxValue) return quantity;
        int unit = world.InventionRules!.UnitGramsOf(resourceId);
        long fits = Math.Min(quantity, cargoGrams / unit);
        if (fits < quantity)
        {
            world.InventionTally.FootCargoCapped++;
            world.InventionTally.CargoUnitsCut += quantity - fits;
            cutGrams += (quantity - fits) * unit;
        }
        cargoGrams -= fits * unit;
        return fits;
    }

    /// <summary>Depois da compra (AD-153): conta o corte por terra e o que o veículo de terra da casa levou além do que o corpo leva
    /// (braços e recipiente preso a ele). Só medida: nenhuma decisão lê isto.</summary>
    public static void NoteTrip(WorldState world, Household household, CellCoord market, long cargoStart, long cargoLeft, long cutG)
    {
        world.TripProbe?.Invoke(household.Id.Value, cutG); // AD-163: só observa
        // AD-157b (backlog 08b item 77): o que a carga cortou ficou na banca; a casa volta por ele na próxima ida que puder pagar
        if (world.InventionOn) world.Invention!.LeaveAtMarket(household.Id.Value, cutG > 0);
        if (!world.InventionOn || cargoStart == long.MaxValue) return;
        var rules = world.InventionRules!;
        var tally = world.InventionTally;
        if (cutG > 0 && LivingWorld.Simulation.Cities.Streets.StreetRoutes.WaterCells(world, household.Location, market) == 0)
        {
            tally.LandCargoCapped++;
            tally.LandCargoGramsCut += cutG;
        }
        if (world.Invention!.BestHauler(household.Id.Value, rules) is not { Exists: true } hauler) return;
        var carrier = world.Invention.BestCarrier(household.Id.Value, rules);
        long body = rules.FootCarryKg * 1000L + (carrier.Exists && carrier.Worn ? carrier.CapacityG : 0);
        long carried = cargoStart - cargoLeft;
        if (carried <= body) return;
        tally.HaulTrips++;
        tally.HaulGramsBeyondBody += carried - body;
        if (hauler.Mode == GroundMode.Roll) tally.HaulRoll++;
        else if (hauler.Mode == GroundMode.Skid) tally.HaulSkid++;
        else tally.HaulDrag++;
    }

    /// <summary>Quem está no mercado: se a casa precisa atravessar água e não tem com quê, conta na procura do mês e, havendo peça
    /// exposta aqui que valha o preço e caiba no bolso (sobrando para a comida), compra a mais barata.</summary>
    public static void AtMarket(WorldState world, TickContext ctx, Npc buyer, Household household, Workplace market)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        if (ledger.Minds.Count > 0 && ledger.BestCraftSpeed(household.Id.Value, rules) == 0)
            Shop(world, ctx, buyer, household, market, ledger.Minds, 0);
        // AD-147: quem tem o problema de carregar e nenhum recipiente em casa olha os que estão expostos
        if (ledger.CarryMinds.Count > 0 && !ledger.BestCarrier(household.Id.Value, rules).Exists)
            Shop(world, ctx, buyer, household, market, ledger.CarryMinds, CarryInvention.Function);
        // AD-153: e quem segue cortado sem veículo de terra olha os que estão expostos
        if (rules.LandVehicles && ledger.CarryMinds.Count > 0 && ledger.HaulDesigns is { Count: > 0 } && !ledger.BestHauler(household.Id.Value, rules).Exists)
            Shop(world, ctx, buyer, household, market, ledger.CarryMinds, HaulInvention.Function);
    }

    private static void Shop(WorldState world, TickContext ctx, Npc buyer, Household household, Workplace market,
        List<InventionMind> minds, byte function)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        int frustration = 0;
        var members = household.Members;
        for (int m = 0; m < members.Count; m++) // índice, não foreach: a compra é caminho quente e a interface alocaria
        {
            int i = function == 0 ? ledger.MindIndex(members[m].Value) : ledger.CarryMindIndex(members[m].Value);
            if (i >= 0) frustration = Math.Max(frustration, minds[i].FrustrationMinutes);
        }
        if (frustration < rules.FrustrationThresholdMinutes) return;
        if (ledger.ListingsAt(market.Id.Value, function) == 0) { ledger.Want(household.Id.Value, household.City, false, function); return; }

        int best = -1;
        for (int i = 0; i < ledger.Items.Count; i++)
        {
            var item = ledger.Items[i];
            if (item.MarketId != market.Id.Value || ledger.FunctionOf(item.DesignId) != function) continue;
            if (best < 0 || item.Price < ledger.Items[best].Price) best = i;
        }
        var listing = ledger.Items[best];
        long now = ctx.CurrentTick;
        // o que a peça pouparia: as horas que a água tira por mês (a frustração guardada decai, então o que entra por mês é a parte
        // que sai) × o preço da hora de quem compra × os meses que a peça ainda dura
        double hoursPerMonth = frustration * (1000 - rules.FrustrationKeepPm) / 1000.0 / 60.0;
        double monthsLeft = Math.Max(0, listing.ExpiresTick - now) / (double)world.Calendar.HoursPerMonth;
        double worth = hoursPerMonth * HourlyWage(world, buyer) * monthsLeft;
        long foodReserve = household.Members.Count * 10L
            * Math.Max(1, market.Prices.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)));
        bool affordable = buyer.Wallet.Amount - listing.Price >= foodReserve;
        if (worth < listing.Price || !affordable) { ledger.Want(household.Id.Value, household.City, false, function); return; }

        var price = new Money(listing.Price);
        if (!buyer.TryDebitWallet(price).IsSuccess) return;
        PaySeller(world, listing, market, price);
        ledger.Items[best] = listing with { HouseholdId = household.Id.Value, City = household.City, SellerId = 0, MarketId = null, Price = 0 };
        ledger.Touch();
        ledger.Want(household.Id.Value, household.City, true, function);
        world.InventionTally.Sales++;
        if (function == CarryInvention.Function) world.InventionTally.CarrySales++;
        if (function == HaulInvention.Function) world.InventionTally.HaulSales++;
        world.InventionTally.SaleCoins += listing.Price;
        ctx.LogEvent(WorldEventKind.DesignSold, string.Create(CultureInfo.InvariantCulture,
            $"{buyer.Id.Value}|{listing.SellerId}|{listing.DesignId}|{listing.Price}"), SourceSystem);
    }

    /// <summary>Quem fez recebe; se morreu, alguém vivo da casa dele; sem ninguém, o caixa do mercado (a moeda não some).</summary>
    private static void PaySeller(WorldState world, CraftedItem listing, Workplace market, Money price)
    {
        if (world.FindNpc(new NpcId(listing.SellerId)) is { IsAlive: true } seller) { seller.CreditWallet(price); return; }
        if (world.FindHousehold(new HouseholdId(listing.HouseholdId)) is { } home)
            foreach (var member in home.Members.OrderBy(m => m.Value))
                if (world.FindNpc(member) is { IsAlive: true } heir) { heir.CreditWallet(price); return; }
        market.CreditTreasury(price);
    }

    /// <summary>Fecha o mês: a procura (casas distintas que quiseram) vira a demanda que se lê no mês seguinte, e o preço de cada
    /// peça exposta anda pela regra do mercado: fator = 1 + sensibilidade × (1 − oferta/procura), com a razão entre 0 e 2.</summary>
    internal static void CloseMonth(WorldState world)
    {
        var ledger = world.Invention!;
        var demand = ledger.Wants.GroupBy(w => (w.City, w.Function)).Select(g => new DesignDemand(g.Key.City, g.Count(), g.Key.Function))
            .OrderBy(d => d.City.Value).ThenBy(d => d.Function).ToList();
        bool changed = demand.Count != ledger.Demand.Count || !demand.SequenceEqual(ledger.Demand) || ledger.Wants.Count > 0;
        ledger.Demand.Clear();
        ledger.Demand.AddRange(demand);
        ledger.Wants.Clear();
        if (changed) ledger.Touch();
        if (ledger.Items.Count == 0 || !world.EconomyRules.Enabled) return;

        double sensitivity = world.EconomyRules.PriceSensitivity;
        var supply = new Dictionary<(CityId, byte), int>();
        foreach (var item in ledger.Items)
            if (item.ForSale) supply[(item.City, ledger.FunctionOf(item.DesignId))] = supply.GetValueOrDefault((item.City, ledger.FunctionOf(item.DesignId))) + 1;
        if (supply.Count == 0) return;
        for (int i = 0; i < ledger.Items.Count; i++)
        {
            var item = ledger.Items[i];
            if (!item.ForSale) continue;
            byte function = ledger.FunctionOf(item.DesignId);
            double ratio = Math.Clamp(supply[(item.City, function)] / Math.Max(ledger.DemandIn(item.City, function), 0.0001), 0, 2);
            double factor = 1 + sensitivity * (1 - ratio);
            long price = Math.Max(1, factor > 1 ? (long)Math.Ceiling(item.Price * factor) : (long)Math.Round(item.Price * factor));
            if (price == item.Price) continue;
            ledger.Items[i] = item with { Price = price };
            world.InventionTally.Repriced++;
        }
        ledger.Touch();
    }

    /// <summary>Preço da hora de alguém: o salário do ofício (ou a média dos ofícios, para quem não tem) pelas horas do mês.</summary>
    internal static double HourlyWage(WorldState world, Npc npc)
    {
        var wages = world.EconomyRules.WageByProfession;
        double monthly = wages.TryGetValue(npc.Profession.Id, out long w) ? w : wages.Count == 0 ? 0 : wages.Values.Average();
        return monthly / Math.Max(1, world.InventionRules!.WorkHoursPerMonth);
    }

    /// <summary>Mercado da cidade onde quem compra comida e água vai (o de comida ou água mais perto da casa de quem vende).</summary>
    internal static Workplace? MarketFor(WorldState world, Npc seller)
    {
        if (!world.EconomyRules.Enabled || seller.Household is not { } h || world.FindHousehold(h) is not { } home) return null;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        Workplace? best = null;
        long bestDistance = long.MaxValue;
        foreach (var w in world.Workplaces)
        {
            if (w.City != seller.City || !world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id)) continue;
            if (!w.Prices.ContainsKey(food) && !w.Prices.ContainsKey(water)) continue;
            long d = Math.Abs(w.Location.X - home.Location.X) + Math.Abs(w.Location.Y - home.Location.Y);
            if (d < bestDistance || (d == bestDistance && best is not null && w.Id.Value < best.Id.Value)) { best = w; bestDistance = d; }
        }
        return best;
    }
}
