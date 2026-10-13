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

/// <summary>labor-allocation (issue 22): quem trabalha numa oficina que tem comida e agua, esta la e nao tem nada em casa
/// nem dinheiro, come e bebe do estoque da oficina (o poco e o celeiro da propriedade) em vez de morrer de sede ao lado
/// de mil de agua.</summary>
public class EmployerRationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Farm = new(1, 1);
    private static readonly CellCoord Elsewhere = new(5, 5);
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);

    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 5, [2] = 5 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Npc Npc, Workplace Farm, Household? Home) Build(
        CellCoord npcAt, bool withHousehold = true, long farmWater = 10, long farmWheat = 10)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(),
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()));
        var farm = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Farm, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Wheat] = farmWheat, [Water] = farmWater }, treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(farm);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            npcAt, motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: npcAt);
        world.AddNpc(npc);
        farm.Hire(npc.Id);
        npc.Hire(farm.Id);
        Household? home = null;
        if (withHousehold)
        {
            home = new Household(new HouseholdId(1), Elsewhere, npc.Id, [npc.Id]); // despensa vazia
            world.AddHousehold(home);
            npc.JoinHousehold(home.Id);
        }

        npc.SetThirst(0, 0);
        npc.SetHunger(0, 0);
        return (world, npc, farm, home);
    }

    private static void Eat(WorldState world, Npc npc) =>
        BehaviorDecisionSystem.ResolveFoodPlan(world, npc, MarketIndex.BuildForTick(world), tick: 0, ActionType.Eat);

    [Fact]
    public void Worker_at_the_farm_with_an_empty_pantry_eats_and_drinks_from_the_farm_stock()
    {
        var (world, npc, farm, _) = Build(Farm);

        Eat(world, npc);

        Assert.Equal(100, npc.ThirstAt(0));
        Assert.Equal(100, npc.HungerAt(0));
        Assert.Equal(9, farm.Stock[Water]);
        Assert.Equal(9, farm.Stock[Wheat]);
        Assert.Equal(1, world.ResourceConsumed[Water]);
        Assert.Equal(1, world.ResourceConsumed[Wheat]);
    }

    [Fact]
    public void Worker_away_from_the_workplace_gets_no_ration()
    {
        var (world, npc, farm, _) = Build(Elsewhere);

        Eat(world, npc);

        Assert.Equal(0, npc.ThirstAt(0));
        Assert.Equal(10, farm.Stock[Water]);
    }

    [Fact]
    public void A_pantry_with_water_is_used_before_the_ration()
    {
        var (world, npc, farm, home) = Build(Farm);
        home!.Deposit(Water, 3);

        Eat(world, npc);

        Assert.Equal(2, home.Stock[Water]);
        Assert.Equal(10, farm.Stock[Water]); // a agua veio de casa
        Assert.Equal(9, farm.Stock[Wheat]);  // sem trigo em casa, esse veio da ração
    }

    [Fact]
    public void An_empty_farm_gives_nothing_and_a_homeless_worker_still_eats_at_work()
    {
        var (emptyWorld, emptyNpc, _, _) = Build(Farm, farmWater: 0, farmWheat: 0);
        Eat(emptyWorld, emptyNpc);
        Assert.Equal(0, emptyNpc.ThirstAt(0));

        var (world, homeless, farm, _) = Build(Farm, withHousehold: false);
        Eat(world, homeless);
        Assert.Equal(100, homeless.ThirstAt(0));
        Assert.Equal(9, farm.Stock[Water]);
    }
}
