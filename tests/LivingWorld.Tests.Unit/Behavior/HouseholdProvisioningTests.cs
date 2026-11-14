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

/// <summary>Como a casa se abastece (AD-148): a banca vende o que tem, a compra completa a meta, uma pessoa da casa vai por todos,
/// a compra espera o fim do turno quando a despensa dura até lá, e quem não paga a comida não vai ao mercado por ela. Cada teste
/// roda também com <see cref="ProvisioningRules.Legacy"/> e mostra o comportamento antigo.</summary>
public class HouseholdProvisioningTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Market = new(1, 1);
    private static readonly CellCoord Home = new(1, 1);
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 9, [2] = 9 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Workplace Shop, Npc Buyer, Household Home, List<Npc> Others) Build(
        bool legacy, long wallet, long wheatAtShop = 1000, long pantryWheat = 0, long pantryWater = 0, int extraMembers = 0)
    {
        var needs = legacy ? ScenarioRunner.DefaultNeedsRules with { Provisioning = ProvisioningRules.Legacy } : ScenarioRunner.DefaultNeedsRules;
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, needs, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(),
            familyRules: legacy ? ScenarioRunner.DefaultFamilyRules with { Pressure = null } : ScenarioRunner.DefaultFamilyRules,
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>()));
        var shop = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Market, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Wheat] = wheatAtShop, [Water] = 1000 }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long> { [Wheat] = 4, [Water] = 3 });
        world.AddWorkplace(shop);

        var buyer = NewNpc(world, wallet);
        var others = new List<Npc>();
        for (int i = 0; i < extraMembers; i++) others.Add(NewNpc(world, 0));
        var home = new Household(new HouseholdId(1), Home, buyer.Id, [buyer.Id, .. others.Select(o => o.Id)]);
        world.AddHousehold(home);
        buyer.JoinHousehold(home.Id);
        foreach (var o in others) o.JoinHousehold(home.Id);
        if (pantryWheat > 0) home.Deposit(Wheat, pantryWheat);
        if (pantryWater > 0) home.Deposit(Water, pantryWater);
        return (world, shop, buyer, home, others);
    }

    private static Npc NewNpc(WorldState world, long wallet)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            Home, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: Home, wallet: new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    private static void Buy(WorldState world, Npc buyer) =>
        BehaviorDecisionSystem.ResolveFoodPlan(world, buyer, MarketIndex.BuildForTick(world), world.CurrentDate.TotalHours, ActionType.Buy);

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    public void A_stall_with_less_than_the_purchase_sells_what_it_has(bool legacy, long expected)
    {
        var (world, shop, buyer, home, _) = Build(legacy, wallet: 500, wheatAtShop: 3, pantryWater: 50);

        Buy(world, buyer);

        Assert.Equal(expected, home.Stock.GetValueOrDefault(Wheat));
        Assert.Equal(3 - expected, shop.Stock[Wheat]);
    }

    [Theory]
    [InlineData(false, 100)]
    [InlineData(true, 110)]
    public void The_purchase_completes_the_target_instead_of_piling_on_top(bool legacy, long expectedWater)
    {
        var (world, _, buyer, home, _) = Build(legacy, wallet: 500, pantryWater: 100);

        Buy(world, buyer);

        Assert.Equal(expectedWater, home.Stock.GetValueOrDefault(Water)); // a meta da casa de um é 10: já tem
        Assert.True(home.Stock.GetValueOrDefault(Wheat) >= 10);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void During_the_shift_the_purchase_waits_while_the_pantry_lasts(bool legacy, bool goes)
    {
        // 5 moradores com 4 de comida e água: 0,8 por morador, abaixo da metade da meta (1) e acima de meio dia (0,5)
        var (world, _, buyer, _, _) = Build(legacy, wallet: 500, pantryWheat: 4, pantryWater: 4, extraMembers: 4);
        var index = MarketIndex.BuildForTick(world);

        Assert.Equal(goes, BehaviorDecisionSystem.NeedsRestocking(world, buyer, index, duringWork: true));
        Assert.True(BehaviorDecisionSystem.NeedsRestocking(world, buyer, index, duringWork: false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void One_person_shops_for_the_household(bool legacy, bool alsoGoes)
    {
        var (world, _, buyer, _, others) = Build(legacy, wallet: 500, extraMembers: 1);
        others[0].SetCurrentAction(ActionType.Buy, world.CurrentDate.TotalHours);

        Assert.Equal(alsoGoes, BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void A_household_that_found_the_stall_empty_waits_longer_each_time(bool legacy, bool goesBackNow)
    {
        var (world, _, buyer, home, _) = Build(legacy, wallet: 500, wheatAtShop: 0, pantryWater: 50);
        long now = world.CurrentDate.TotalHours;

        Buy(world, buyer);

        Assert.Equal(goesBackNow, BehaviorDecisionSystem.NeedsRestocking(world, buyer, MarketIndex.BuildForTick(world)));
        if (legacy) { Assert.Equal(0, home.FoodStallRetryTick); return; }
        Assert.Equal(now + 6, home.FoodStallRetryTick);
        Buy(world, buyer);
        Assert.Equal(now + 12, home.FoodStallRetryTick); // a segunda ida vazia seguida dobra a espera
        home.BoughtFood();
        Assert.Equal(0, home.FoodStallEmptyVisits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hunger_without_the_price_of_food_does_not_pull_to_the_market(bool legacy)
    {
        // fome perto do zero, despensa sem comida e com água, carteira de 2 contra o trigo a 4
        var (world, _, buyer, _, _) = Build(legacy, wallet: 2, pantryWater: 20);
        long now = world.CurrentDate.TotalHours;
        buyer.SetHunger(5, now);
        var view = BehaviorDecisionSystem.ProvisionViewFor(world, buyer, MarketIndex.BuildForTick(world));
        var ctx = DecisionContextBuilder.Build(world, buyer, now, provisionTargetDays: view.TargetDays,
            foodUnitPrice: view.FoodPrice, shopperOut: view.ShopperOut);

        double buy = BehaviorDecisionSystem.UtilityBaseOf(ctx, ActionType.Buy, world.EconomyRules, default);

        if (legacy) Assert.True(buy > 100); // a fome inteira puxava ao mercado, onde nada se comprava
        else Assert.True(buy <= 50);
    }
}
