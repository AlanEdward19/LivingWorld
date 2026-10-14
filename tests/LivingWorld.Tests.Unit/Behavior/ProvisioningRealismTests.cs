using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>Mundo ao vivo que se sustenta (issue 23): comprar é estar no mercado, quem não tem moeda ainda leva água do
/// poço, e quem mora longe do mercado repõe a despensa antes de a sede apertar.</summary>
public class ProvisioningRealismTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Market = new(1, 1);
    private static readonly CellCoord Far = new(9, 9);
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 9, [2] = 9 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Workplace Shop, Npc Buyer, Household Home) Build(
        CellCoord npcAt, CellCoord homeAt, long wallet, long pantry = 0, int extraMembers = 0, bool shopProducesWater = true)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(), familyRules: ScenarioRunner.DefaultFamilyRules,
            economyCatalog: new EconomyCatalog(
                shopProducesWater
                    ? new Dictionary<int, ProductionRecipe>
                    {
                        [1] = ProductionRecipe.Create(
                            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10, [2] = 8 }, null, 10).Value!,
                    }
                    : new Dictionary<int, ProductionRecipe>(),
                [1], new Dictionary<int, int>()));
        var shop = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Market, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Wheat] = 1000, [Water] = 1000 }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [Wheat] = 4, [Water] = 3 });
        world.AddWorkplace(shop);

        var buyer = NewNpc(world, npcAt, wallet);
        var members = new List<NpcId> { buyer.Id };
        for (int i = 0; i < extraMembers; i++) members.Add(NewNpc(world, homeAt, 0).Id);
        var home = new Household(new HouseholdId(1), homeAt, buyer.Id, members);
        world.AddHousehold(home);
        foreach (var id in members) world.FindNpc(id)!.JoinHousehold(home.Id);
        if (pantry > 0)
        {
            home.Deposit(Wheat, pantry);
            home.Deposit(Water, pantry);
        }

        return (world, shop, buyer, home);
    }

    private static Npc NewNpc(WorldState world, CellCoord at, long wallet)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            at, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: at, wallet: new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    private static void Buy(WorldState world, Npc buyer) =>
        BehaviorDecisionSystem.ResolveFoodPlan(world, buyer, MarketIndex.BuildForTick(world), world.CurrentDate.TotalHours, ActionType.Buy);

    [Fact]
    public void Buying_from_a_market_the_buyer_is_not_at_changes_nothing()
    {
        var (world, shop, buyer, home) = Build(npcAt: new CellCoord(5, 5), homeAt: new CellCoord(5, 5), wallet: 500);

        Buy(world, buyer);

        Assert.Equal(500, buyer.Wallet.Amount);
        Assert.Equal(0, home.Stock.GetValueOrDefault(Water));
        Assert.Equal(1000, shop.Stock[Water]);
        Assert.Empty(world.TradeLedger);
    }

    [Fact]
    public void Without_a_coin_the_household_draws_water_from_the_well_but_not_food()
    {
        var (world, shop, buyer, home) = Build(npcAt: Market, homeAt: new CellCoord(5, 5), wallet: 0, extraMembers: 2);

        Buy(world, buyer);

        Assert.Equal(6, home.Stock.GetValueOrDefault(Water)); // 3 membros x 2 dias
        Assert.Equal(0, home.Stock.GetValueOrDefault(Wheat));
        Assert.Equal(994, shop.Stock[Water]);
        Assert.Equal(1000, shop.Stock[Wheat]);
        Assert.Equal(Money.Zero, shop.Treasury);
        Assert.Equal(0, buyer.Wallet.Amount);
        // não é venda: o poço não fatura nada, mas a retirada fica no livro com valor 0
        var entry = Assert.Single(world.TradeLedger);
        Assert.Equal(Water.Id, entry.ResourceId);
        Assert.Equal(0, entry.Value);
        Assert.Equal(6, entry.Quantity);
    }

    [Fact]
    public void The_well_never_gives_more_than_the_shop_holds()
    {
        var (world, shop, buyer, home) = Build(npcAt: Market, homeAt: new CellCoord(5, 5), wallet: 0);
        shop.Withdraw(Water, 998);

        Buy(world, buyer);

        Assert.Equal(2, home.Stock.GetValueOrDefault(Water));
        Assert.Equal(0, shop.Stock[Water]);
    }

    [Fact]
    public void The_target_pantry_grows_with_the_distance_to_the_market()
    {
        var (nearWorld, _, nearBuyer, _) = Build(npcAt: new CellCoord(2, 1), homeAt: new CellCoord(2, 1), wallet: 100);
        var (farWorld, _, farBuyer, _) = Build(npcAt: Far, homeAt: Far, wallet: 100);

        double near = BehaviorDecisionSystem.ProvisionTargetFor(nearWorld, nearBuyer, MarketIndex.BuildForTick(nearWorld));
        double far = BehaviorDecisionSystem.ProvisionTargetFor(farWorld, farBuyer, MarketIndex.BuildForTick(farWorld));

        Assert.True(near >= 2.0);
        Assert.True(far > near, $"meta longe {far} contra perto {near}");
    }

    [Fact]
    public void A_household_restocks_once_its_pantry_falls_below_half_the_target()
    {
        var (world, _, buyer, home) = Build(npcAt: Far, homeAt: Far, wallet: 100, pantry: 500);
        var index = MarketIndex.BuildForTick(world);
        double target = BehaviorDecisionSystem.ProvisionTargetFor(world, buyer, index);

        Assert.False(BehaviorDecisionSystem.NeedsRestocking(world, buyer, index));
        home.Withdraw(Water, 500);
        home.Withdraw(Wheat, 500);
        home.Deposit(Wheat, 1);
        home.Deposit(Water, 1);
        Assert.Equal(1 < target * 0.5, BehaviorDecisionSystem.NeedsRestocking(world, buyer, index));
    }

    [Fact]
    public void Restocking_needs_money_and_a_pantry_below_the_target()
    {
        var (brokeWorld, _, broke, _) = Build(npcAt: Far, homeAt: Far, wallet: 0, pantry: 1);
        var (fullWorld, _, full, _) = Build(npcAt: Far, homeAt: Far, wallet: 100, pantry: 500);

        Assert.False(BehaviorDecisionSystem.NeedsRestocking(brokeWorld, broke, MarketIndex.BuildForTick(brokeWorld)));
        Assert.False(BehaviorDecisionSystem.NeedsRestocking(fullWorld, full, MarketIndex.BuildForTick(fullWorld)));
    }

    [Fact]
    public void A_purchase_covers_more_days_the_farther_the_market_is_up_to_a_ceiling()
    {
        Assert.Equal(4, BehaviorDecisionSystem.PurchaseDaysFor(homeTicks: 0, hoursPerDay: 24));
        Assert.True(BehaviorDecisionSystem.PurchaseDaysFor(48, 24) > BehaviorDecisionSystem.PurchaseDaysFor(0, 24));
        Assert.True(BehaviorDecisionSystem.PurchaseDaysFor(96, 24) > BehaviorDecisionSystem.PurchaseDaysFor(48, 24));
        Assert.Equal(14, BehaviorDecisionSystem.PurchaseDaysFor(10_000, 24));
    }

    [Fact]
    public void Eating_does_not_beat_buying_when_the_pantry_has_food_but_no_water_and_thirst_is_critical()
    {
        var (world, _, buyer, home) = Build(npcAt: new CellCoord(5, 5), homeAt: new CellCoord(5, 5), wallet: 100, pantry: 0);
        home.Deposit(Wheat, 50);
        buyer.SetThirst(0, 0);
        buyer.SetHunger(100, 0);

        var decision = BehaviorDecisionSystem.SelectByUtility(
            DecisionContextBuilder.Build(world, buyer, 0), world.NeedsRules, world.EconomyRules, continuityAction: ActionType.Eat);

        Assert.Equal(ActionType.Buy, decision.Action);
    }

    [Fact]
    public void Restocking_needs_at_least_the_price_of_one_unit_in_the_wallet()
    {
        var (world, _, buyer, _) = Build(npcAt: Far, homeAt: Far, wallet: 2, pantry: 0); // preços 4 e 3
        Assert.False(BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));

        buyer.CreditWallet(new Money(1)); // 3: paga uma unidade de água
        Assert.True(BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));
    }

    [Fact]
    public void A_household_that_already_has_the_wells_days_of_water_draws_nothing()
    {
        var (world, shop, buyer, home) = Build(npcAt: Market, homeAt: new CellCoord(5, 5), wallet: 0, pantry: 0);
        home.Deposit(Water, 4);

        Buy(world, buyer);

        Assert.Equal(1000, shop.Stock[Water]); // o que a casa bebeu veio da despensa dela
        Assert.Equal(3, home.Stock.GetValueOrDefault(Water));
    }

    [Fact]
    public void The_two_resource_nearest_selling_overload_agrees_with_the_params_one()
    {
        var (world, shop, _, _) = Build(npcAt: Market, homeAt: new CellCoord(5, 5), wallet: 10);
        var index = MarketIndex.BuildForTick(world);
        var other = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(8, 8), 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Water] = 10 }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [Water] = 2 });
        world.AddWorkplace(other);
        var both = MarketIndex.BuildForTick(world);

        foreach (var origin in new[] { new CellCoord(0, 0), new CellCoord(5, 5), new CellCoord(9, 9), new CellCoord(8, 8) })
            foreach (var idx in new[] { index, both })
                Assert.Equal(idx.NearestSelling(origin, Wheat, Water)?.Id, idx.NearestSelling(origin, new[] { Wheat, Water })?.Id);

        shop.Withdraw(Wheat, 1000);
        shop.Withdraw(Water, 1000); // sem estoque: cai no mais próximo, igual nas duas formas
        var emptied = MarketIndex.BuildForTick(world);
        Assert.Equal(emptied.NearestSelling(new CellCoord(0, 0), Wheat, Water)?.Id, emptied.NearestSelling(new CellCoord(0, 0), new[] { Wheat, Water })?.Id);
    }

    [Fact]
    public void A_decision_without_trace_capture_picks_the_same_action_and_never_exposes_a_trace()
    {
        var (world, _, buyer, _) = Build(npcAt: new CellCoord(5, 5), homeAt: new CellCoord(5, 5), wallet: 100, pantry: 0);
        buyer.SetThirst(0, 0);
        var context = DecisionContextBuilder.Build(world, buyer, 0);

        var captured = BehaviorDecisionSystem.SelectByUtility(context, world.NeedsRules, world.EconomyRules, continuityAction: null);
        var plain = BehaviorDecisionSystem.SelectByUtility(
            context, world.NeedsRules, world.EconomyRules, continuityAction: null, captureTrace: false);

        Assert.Equal(captured.Action, plain.Action);
        Assert.Equal(captured.PendingPower, plain.PendingPower);
        Assert.NotNull(captured.Trace);
        Assert.Throws<InvalidOperationException>(() => plain.Trace);
    }

    [Fact]
    public void A_market_whose_recipe_does_not_produce_water_gives_no_free_water()
    {
        var (world, shop, buyer, home) = Build(
            npcAt: Market, homeAt: new CellCoord(5, 5), wallet: 0, shopProducesWater: false);

        Buy(world, buyer);

        Assert.Equal(0, home.Stock.GetValueOrDefault(Water));
        Assert.Equal(1000, shop.Stock[Water]);
        Assert.Empty(world.TradeLedger);
    }

    [Fact]
    public void A_wallet_that_covers_only_water_still_restocks_but_the_purchase_stays_within_the_wallet()
    {
        var (world, _, buyer, home) = Build(npcAt: Market, homeAt: Market, wallet: 3, pantry: 0); // água 3, trigo 4
        Assert.True(BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));

        Buy(world, buyer);

        Assert.Equal(0, buyer.Wallet.Amount);
        Assert.Equal(1, home.Stock.GetValueOrDefault(Water));
        Assert.Equal(0, home.Stock.GetValueOrDefault(Wheat));
    }

    [Theory]
    [InlineData(1, true)]   // 1 dia por pessoa, longe do mercado: abaixo da metade da meta
    [InlineData(2, false)]  // metade da meta (~3-4 dias) já está coberta
    [InlineData(14, false)] // despensa cheia para o mercado mais longe
    public void Restocking_at_a_fixed_distance_depends_only_on_the_days_in_the_pantry(int pantryPerMember, bool expected)
    {
        var (world, _, buyer, _) = Build(npcAt: Far, homeAt: Far, wallet: 100, pantry: pantryPerMember);

        Assert.Equal(expected, BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));
    }
}
