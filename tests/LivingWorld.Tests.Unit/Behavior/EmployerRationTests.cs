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
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>labor-allocation (issue 22): quem trabalha numa oficina que tem comida e agua, esta la e nao tem nada em casa
/// nem dinheiro, come e bebe do estoque da oficina (o poco e o celeiro da propriedade) em vez de morrer de sede ao lado
/// de mil de agua. AD-151: a comida é parte do pagamento (vai à caderneta e sai do salário); a água do poço segue livre.</summary>
public class EmployerRationTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Farm = new(1, 1);
    private static readonly CellCoord Elsewhere = new(5, 5);
    private static readonly ResourceType Wheat = new(1);
    private static readonly ResourceType Water = new(2);

    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules Economy(long wage) => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: wage > 0 ? new Dictionary<int, long> { [1] = wage } : new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 5, [2] = 5 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static (WorldState World, Npc Npc, Workplace Farm, Household? Home) Build(
        CellCoord npcAt, bool withHousehold = true, long farmWater = 10, long farmWheat = 10, long wage = 30, long wheatPrice = 0,
        bool freeRation = false)
    {
        var world = new WorldState(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(wage) with { FreeWorkRation = freeRation },
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()));
        var farm = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Farm, 5, employees: [],
            stock: new Dictionary<ResourceType, long> { [Wheat] = farmWheat, [Water] = farmWater }, treasury: Money.Zero,
            prices: wheatPrice > 0 ? new Dictionary<ResourceType, long> { [Wheat] = wheatPrice } : new Dictionary<ResourceType, long>());
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

    // AD-151: a ração da oficina era comida do estoque à venda sem ninguém pagar. Agora é a refeição "com comida" do salário.

    private static long Coin(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);

    [Fact]
    public void The_food_ration_goes_to_the_slate_at_the_stall_price_and_the_well_water_stays_free()
    {
        var (world, npc, farm, _) = Build(Farm, wheatPrice: 3);

        Eat(world, npc);

        Assert.Equal(100, npc.HungerAt(0));
        Assert.Equal(3, farm.BoardOwedBy(npc.Id)); // uma refeição ao preço da venda
        Assert.Equal(9, farm.Stock[Water]);        // a água do poço não entra na conta
        Assert.Equal(3, world.PurchaseTally.BoardCharged);
    }

    [Fact]
    public void The_payroll_withholds_the_meals_and_coin_and_food_are_conserved()
    {
        var (world, npc, farm, _) = Build(Farm, wheatPrice: 2);
        farm.CreditTreasury(new Money(100));
        long coinBefore = Coin(world);
        long foodBefore = farm.Stock[Wheat];

        Eat(world, npc);
        new LivingWorld.Simulation.Economy.Labor.WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(new Money(28), npc.Wallet);          // salário 30 menos a refeição de 2
        Assert.Equal(new Money(72), farm.Treasury);       // a refeição ficou no caixa
        Assert.Equal(0, farm.BoardOwedBy(npc.Id));
        Assert.Null(farm.Board);
        Assert.Equal(coinBefore, Coin(world));
        Assert.Equal(foodBefore, farm.Stock[Wheat] + world.ResourceConsumed[Wheat]);
    }

    [Fact]
    public void Without_a_wage_in_the_trade_the_workshop_gives_no_free_food()
    {
        var (world, npc, farm, _) = Build(Farm, wage: 0);

        Eat(world, npc);

        Assert.Equal(0, npc.HungerAt(0));          // antes: comia de graça do estoque à venda
        Assert.Equal(10, farm.Stock[Wheat]);
        Assert.Equal(100, npc.ThirstAt(0));         // a água do poço continua
        Assert.Equal(1, world.PurchaseTally.BoardRefused);
    }

    /// <summary>Na carestia o preço passa do salário: a refeição continua (é parte do pagamento) e o que o líquido do mês não cobre
    /// a oficina paga em espécie. A moeda segue conservada.</summary>
    [Fact]
    public void Meals_past_the_month_wage_are_paid_in_kind_by_the_workshop()
    {
        var (world, npc, farm, _) = Build(Farm, wage: 5, wheatPrice: 2);
        farm.CreditTreasury(new Money(100));
        long coinBefore = Coin(world);

        for (int i = 0; i < 4; i++) { npc.SetHunger(0, 0); Eat(world, npc); }
        Assert.Equal(8, farm.BoardOwedBy(npc.Id)); // quatro refeições, nenhuma negada
        Assert.Equal(6, farm.Stock[Wheat]);
        new LivingWorld.Simulation.Economy.Labor.WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(Money.Zero, npc.Wallet);             // o líquido inteiro foi para as refeições
        Assert.Equal(new Money(100), farm.Treasury);      // pagou 5 e reteve 5
        Assert.Null(farm.Board);
        Assert.Equal(3, world.PurchaseTally.BoardInKind); // o resto, em espécie
        Assert.Equal(coinBefore, Coin(world));
    }

    [Fact]
    public void Who_left_the_job_pays_the_slate_from_the_wallet_and_the_rest_is_lost_by_the_employer()
    {
        var (world, npc, farm, _) = Build(Farm, wheatPrice: 4);
        Eat(world, npc);
        npc.SetHunger(0, 0);
        Eat(world, npc);
        Assert.Equal(8, farm.BoardOwedBy(npc.Id));
        farm.Fire(npc.Id);
        npc.Fire();
        npc.CreditWallet(new Money(5));
        long coinBefore = Coin(world);

        new LivingWorld.Simulation.Economy.Labor.WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(Money.Zero, npc.Wallet);
        Assert.Equal(new Money(5), farm.Treasury);
        Assert.Null(farm.Board);
        Assert.Equal(3, world.PurchaseTally.BoardForgiven);
        Assert.Equal(coinBefore, Coin(world));
    }

    [Fact]
    public void The_measurement_switch_brings_back_the_free_ration()
    {
        var (world, npc, farm, _) = Build(Farm, wage: 0, freeRation: true);

        Eat(world, npc);

        Assert.Equal(100, npc.HungerAt(0));
        Assert.Null(farm.Board);
    }
}
