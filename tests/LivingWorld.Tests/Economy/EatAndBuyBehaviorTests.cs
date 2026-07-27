using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T18/T19: <c>Eat</c> exige estoque do <see cref="Household"/> antes de
/// restaurar (ECON-16/17); <c>Buy</c> viaja ao mercado mais próximo e executa uma
/// <see cref="MarketTransaction"/> real (ECON-09).</summary>
public class EatAndBuyBehaviorTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules EnabledRules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static WorldState BuildWorld(EconomyCatalog? catalog = null)
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EnabledRules,
            economyCatalog: catalog ?? new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()));
    }

    private static (Npc Npc, Household Household) MakeResident(
        WorldState world, IReadOnlyDictionary<ResourceType, long>? stock = null, CellCoord? location = null)
    {
        var loc = location ?? new CellCoord(1, 1);
        var npcId = world.NextNpcIdAndAdvance();
        var household = new Household(world.NextHouseholdIdAndAdvance(), loc, npcId, [npcId], stock);
        world.AddHousehold(household);

        var npc = new Npc(
            npcId, "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), loc,
            motherId: null, fatherId: null, household: household.Id, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1),
            currentLocation: loc, hunger: 0, thirst: 0,
            currentAction: ActionType.Eat, actionStartedAtTick: 0);
        world.AddNpc(npc);
        return (npc, household);
    }

    [Fact]
    public void Eat_with_food_and_water_in_stock_restores_both_and_decrements_stock_by_one()
    {
        var world = BuildWorld();
        var (npc, household) = MakeResident(world, new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5, [new ResourceType(2)] = 5 });
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(world.ActionCatalog.MaxDurationHours[ActionType.Eat]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(100, npc.Hunger);
        Assert.Equal(100, npc.Thirst);
        Assert.Equal(4, household.Stock[new ResourceType(1)]);
        Assert.Equal(4, household.Stock[new ResourceType(2)]);
    }

    [Fact]
    public void Eat_with_no_food_in_stock_completes_without_restoring_hunger_and_without_exception()
    {
        var world = BuildWorld();
        var (npc, household) = MakeResident(world, new Dictionary<ResourceType, long> { [new ResourceType(2)] = 5 });
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(world.ActionCatalog.MaxDurationHours[ActionType.Eat]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(0, npc.Hunger);
        Assert.Equal(100, npc.Thirst);
        Assert.False(household.Stock.ContainsKey(new ResourceType(1)) && household.Stock[new ResourceType(1)] < 0);
    }

    [Fact]
    public void Npc_with_low_stock_and_sufficient_wallet_buys_from_nearest_market()
    {
        var location = new CellCoord(1, 1);
        var marketLocation = new CellCoord(1, 1);
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>());
        var world = BuildWorld(catalog);
        var market = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), marketLocation, maxVacancies: 0,
            employees: [], stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 50 },
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5 });
        world.AddWorkplace(market);

        var (npc, household) = MakeResident(world, new Dictionary<ResourceType, long>(), location);
        npc.CreditWallet(new Money(100));
        npc.SetCurrentAction(ActionType.Buy, 0);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(world.ActionCatalog.MaxDurationHours[ActionType.Buy]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(1, household.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal(new Money(95), npc.Wallet);
    }

    [Fact]
    public void Npc_with_insufficient_wallet_does_not_buy_and_stock_stays_unchanged()
    {
        var location = new CellCoord(1, 1);
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>());
        var world = BuildWorld(catalog);
        var market = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 0,
            employees: [], stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 50 },
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 500 });
        world.AddWorkplace(market);

        var (npc, household) = MakeResident(world, new Dictionary<ResourceType, long>(), location);
        npc.CreditWallet(new Money(1));
        npc.SetCurrentAction(ActionType.Buy, 0);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(world.ActionCatalog.MaxDurationHours[ActionType.Buy]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(0, household.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal(new Money(1), npc.Wallet);
    }
}
