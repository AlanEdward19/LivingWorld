using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-143, fatia 2: a peça na economia e na vida — carga por viagem, venda com preço de mercado, produção pela receita do
/// design, material de estoque, pesca, cópia por ter visto, lápide apontada pelos relatos e a pessoa 0.</summary>
public class InventionEconomyTests
{
    private static readonly InventionRules Clean = InventionRules.Default with { ManufacturingNoisePm = 0 };

    private static (WorldState World, TickContext Ctx) NewWorld(InventionRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: rules ?? InventionRules.Default);
        return (world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    private static List<Npc> Adults(WorldState world) => world.Npcs
        .Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value).ToList();

    private static void Frustrate(WorldState world, Npc npc, int minutes) =>
        world.Invention!.PutMind(new InventionMind(npc.Id.Value, minutes, 0, 0, 0, FailureMode.None, null, 0));

    /// <summary>Uma célula do outro lado da água a partir da casa.</summary>
    private static CellCoord AcrossWater(WorldState world, Household home) =>
        world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => WaterCrossing.CellsBetween(world.Map, home.Location, c) >= 3);

    [Fact]
    public void Cargo_across_water_on_foot_is_what_a_person_carries_and_a_craft_carries_its_measured_payload()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var market = AcrossWater(world, home);
        long foot = InventionMarket.CargoGrams(world, home, market);
        InventionWorldTests.GiveCraft(world, npc);
        long craft = InventionMarket.CargoGrams(world, home, market);
        long payload = world.Invention!.BestCraft(home.Id.Value, world.InventionRules!).PayloadG;

        Assert.True(foot == world.InventionRules!.FootCarryKg * 1000L && craft == Math.Max(foot, payload), $"a pé {foot}, embarcado {craft}, carga {payload}");
    }

    [Fact]
    public void A_purchase_is_cut_to_what_fits_in_the_cargo()
    {
        var (world, _) = NewWorld();
        long cargo = 30_000;
        long food = InventionMarket.Fit(world, 1, 100, ref cargo); // 100 rações de 700 g não cabem em 30 kg

        Assert.Equal(30_000 / world.InventionRules!.UnitGramsOf(1), food);
    }

    [Fact]
    public void No_water_on_the_way_leaves_the_purchase_as_before()
    {
        var (world, _) = NewWorld();
        var home = HomeOf(world, Adults(world)[0]);

        Assert.Equal(long.MaxValue, InventionMarket.CargoGrams(world, home, home.Location));
    }

    private static (Npc Seller, Npc Buyer, CraftedItem Listing, LivingWorld.Domain.Economy.Workplace Market) Listed(WorldState world, long price)
    {
        var adults = Adults(world);
        var seller = adults[0];
        var buyer = adults.First(a => a.Household != seller.Household);
        var market = InventionMarket.MarketFor(world, buyer)!;
        var design = InventionWorldTests.GiveCraft(world, seller);
        var ledger = world.Invention!;
        ledger.Items.Clear(); // a peça da casa do vendedor some: só a exposta conta
        var item = new CraftedItem(ledger.NextItemId++, design.Id, seller.Household!.Value.Value, seller.City, 0,
            120L * world.Calendar.HoursPerMonth, 1000, seller.Id.Value, market.Id.Value, price);
        ledger.Items.Add(item);
        ledger.Touch();
        return (seller, buyer, item, market);
    }

    [Fact]
    public void Someone_who_needs_to_cross_water_buys_the_exposed_craft_without_knowing_how_to_make_it_and_the_coins_go_to_the_maker()
    {
        var (world, ctx) = NewWorld();
        var (seller, buyer, listing, market) = Listed(world, 40);
        Frustrate(world, buyer, 6000);
        buyer.CreditWallet(new Money(5000));
        long before = buyer.Wallet.Amount + seller.Wallet.Amount;

        InventionMarket.AtMarket(world, ctx, buyer, HomeOf(world, buyer), market);

        var item = world.Invention!.Items.Single(i => i.Id == listing.Id);
        Assert.True(item.HouseholdId == buyer.Household!.Value.Value && !item.ForSale && !world.Invention.Knows(buyer.Id.Value, item.DesignId)
                    && buyer.Wallet.Amount + seller.Wallet.Amount == before && world.InventionTally.Sales == 1);
    }

    [Fact]
    public void Nobody_buys_what_the_crossing_does_not_pay_for()
    {
        var (world, ctx) = NewWorld();
        var (_, buyer, listing, market) = Listed(world, 1_000_000);
        Frustrate(world, buyer, 6000);
        buyer.CreditWallet(new Money(2_000_000));

        InventionMarket.AtMarket(world, ctx, buyer, HomeOf(world, buyer), market);

        Assert.True(world.Invention!.Items.Single(i => i.Id == listing.Id).ForSale);
    }

    [Fact]
    public void The_price_falls_without_buyers_and_rises_when_more_want_than_there_is()
    {
        var (world, _) = NewWorld();
        var (_, buyer, listing, _) = Listed(world, 100);
        InventionMarket.CloseMonth(world);
        long alone = world.Invention!.Items.Single(i => i.Id == listing.Id).Price;
        foreach (var h in world.Households.Where(h => h.City == buyer.City).Take(5)) world.Invention.Want(h.Id.Value, h.City, false);
        InventionMarket.CloseMonth(world);
        long wanted = world.Invention.Items.Single(i => i.Id == listing.Id).Price;

        Assert.True(alone < 100 && wanted > alone, $"sem procura {alone}, com procura {wanted}");
    }

    [Fact]
    public void Material_kept_at_home_spares_the_gathering_of_the_order()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var design = InventionWorldTests.GiveCraft(world, npc);
        var rules = world.InventionRules!;
        long full = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg).BuildMinutes;
        foreach (var (material, grams) in PhysicsEvaluator.Recipe(design.Parts, rules)) world.Invention!.Store(npc.Household!.Value.Value, material, grams);

        long minutes = DesignWorkshop.Open(world, npc, design, 1000, null, 0);

        Assert.True(minutes < full && world.Invention!.Stored.Count == 0, $"com estoque {minutes} min, sem {full}");
    }

    [Fact]
    public void Material_that_circulates_in_the_economy_comes_from_the_pantry_before_nature()
    {
        var rules = InventionRules.Default with { Materials = InventionRules.DefaultMaterials.Select(m => m.Id == 1 ? m with { ResourceId = 4 } : m).ToList() };
        var (world, _) = NewWorld(rules);
        var npc = Adults(world)[0];
        var design = InventionWorldTests.GiveCraft(world, npc);
        long full = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg).BuildMinutes;
        HomeOf(world, npc).Deposit(new ResourceType(4), 100_000);

        long minutes = DesignWorkshop.Open(world, npc, design, 1000, null, 0);

        Assert.True(minutes < full && HomeOf(world, npc).Stock.GetValueOrDefault(new ResourceType(4)) < 100_000);
    }

    [Fact]
    public void An_order_for_sale_advances_in_the_daily_production_and_ends_exposed_at_the_market_with_a_price()
    {
        var (world, ctx) = NewWorld(Clean);
        var npc = Adults(world)[0];
        var design = InventionWorldTests.GiveCraft(world, npc);
        var market = InventionMarket.MarketFor(world, npc)!;
        DesignWorkshop.Open(world, npc, design, 1000, market.Id.Value, 0);

        for (int day = 0; day < 400 && world.Invention!.Orders.Count > 0; day++) DesignWorkshop.Advance(world, ctx);

        Assert.Contains(world.Invention!.Items, i => i.ForSale && i.MarketId == market.Id.Value && i.SellerId == npc.Id.Value && i.Price > 0);
    }

    private static Household? WaterSideHome(WorldState world) => world.Households.OrderBy(h => h.Id.Value)
        .FirstOrDefault(h => Fishing.WaterCellsNear(world.Map, h.Location, world.InventionRules!.WaterObservationRadius) > 0
            && h.Members.Any(m => world.FindNpc(m) is { IsAlive: true } n && n.AgeYears(world.CurrentDate) >= 20));

    private static long FishedFood(bool craft)
    {
        var (world, ctx) = NewWorld();
        var home = WaterSideHome(world)!;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        home.Withdraw(food, home.Stock.GetValueOrDefault(food));
        var adult = home.Members.Select(m => world.FindNpc(m)!).First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 20);
        if (craft) InventionWorldTests.GiveCraft(world, adult);
        else InventionWorldTests.GiveCraft(world, Adults(world).First(a => a.Household != home.Id)); // há peça no mundo, não nesta casa

        Fishing.Month(world, ctx);
        return home.Stock.GetValueOrDefault(food);
    }

    [Fact]
    public void A_household_with_a_working_craft_by_the_water_brings_food_from_the_water_and_one_without_does_not()
    {
        long with = FishedFood(craft: true), without = FishedFood(craft: false);

        Assert.True(with > 0 && without == 0, $"com peça {with}, sem {without}");
    }

    [Fact]
    public void Fish_taken_leaves_the_water_poorer_and_it_grows_back()
    {
        var (world, ctx) = NewWorld();
        var home = WaterSideHome(world)!;
        var adult = home.Members.Select(m => world.FindNpc(m)!).First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 20);
        InventionWorldTests.GiveCraft(world, adult);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        home.Withdraw(food, home.Stock.GetValueOrDefault(food));
        Fishing.Month(world, ctx);
        long after = world.Invention!.Fish.Single().Grams;
        world.Invention.Items.Clear();
        world.Invention.Touch();
        Fishing.Month(world, ctx);

        Assert.True(world.Invention.Fish.Single().Grams > after);
    }

    [Fact]
    public void Someone_with_the_water_problem_who_sees_the_craft_arrive_keeps_it_to_copy()
    {
        var (world, _) = NewWorld();
        var adults = Adults(world);
        var rower = adults[0];
        var watcher = adults.First(a => a.Household != rower.Household && a.City == rower.City);
        var design = InventionWorldTests.GiveCraft(world, rower);
        Frustrate(world, watcher, 600);

        InventionIntake.Sighted(world, rower, watcher.CurrentLocation, design.Id);

        Assert.Equal(design.Id, world.Invention!.Minds[world.Invention.MindIndex(watcher.Id.Value)].SeenDesignId);
    }

    [Fact]
    public void A_lost_design_still_told_by_history_keeps_a_tombstone_the_accounts_point_to()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var design = InventionWorldTests.GiveCraft(world, npc);
        var fact = new Fact(world.NextFactIdAndAdvance(), 1, WorldEventKind.DesignPrototyped, [npc.Id], npc.City, 0.6,
            $"{npc.Id.Value}|{design.Id}|0|0|0");
        world.AddFact(fact);
        world.Invention!.Items.Clear();

        InventionIntake.Forget(world, npc.Id.Value, 50);

        var view = DesignQuery.OfFact(world, fact.Id);
        Assert.True(view.IsSuccess && view.Value!.Lost && view.Value.Accounts == 1 && view.Value.LostTick == 50);
    }

    [Fact]
    public void A_tombstone_with_accounts_makes_trying_more_likely_in_its_city()
    {
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: InventionWorldTests.Eager with { BaseAttemptChancePm = 0 });
        var npc = Adults(world)[0];
        world.Invention!.Tombstones.Add(new DesignTombstone(7, npc.Id.Value, 0, 0, 1, npc.City, 3));
        Frustrate(world, npc, 6000);

        clock.Run(world, world.Calendar.HoursPerMonth + 1);

        Assert.True(world.InventionTally.RumorBoosts > 0);
    }

    [Fact]
    public void The_person_with_id_zero_remembers_what_happens_to_her()
    {
        var (world, _) = NewWorld();
        var first = world.Npcs.Single(n => n.Id.Value == 0);

        LifeEventIntake.OnEvent(world, WorldEventKind.Hired, "0|1", "employment", 1);

        Assert.True(first.Psyche is { Memories.Count: > 0 });
    }

    [Fact]
    public void A_world_with_orders_listings_stock_and_fish_reopens_from_the_snapshot_and_goes_on_identically()
    {
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: Clean);
        var (seller, _, _, market) = Listed(world, 50);
        var design = InventionWorldTests.GiveCraft(world, seller);
        world.Invention!.Store(seller.Household!.Value.Value, 2, 1234);
        DesignWorkshop.Open(world, seller, design, 900, market.Id.Value, 0);
        world.Invention.Fish.Add(new FishStock(seller.City, 99_000));
        world.Invention.Want(seller.Household!.Value.Value, seller.City, false);
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));

        clock.Run(world, 2 * world.Calendar.HoursPerMonth);
        new WorldClock(ScenarioRunner.DefaultSystems(), clock.MaxIterationsPerTick).Run(reopened, 2 * world.Calendar.HoursPerMonth);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));
    }
}
