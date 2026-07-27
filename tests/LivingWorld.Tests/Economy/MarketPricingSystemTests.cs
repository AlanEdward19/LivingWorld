using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T16: <see cref="MarketPricingSystem"/> — preço sobe quando oferta/demanda
/// cai, cai quando sobra, nunca sai de [PriceFloor, PriceCeiling] (ECON-23/24).</summary>
public class MarketPricingSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules MakeRules(long floor, long ceiling, double sensitivity, double demandBaseline) =>
        EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long> { [1] = floor },
            priceCeiling: new Dictionary<int, long> { [1] = ceiling },
            priceSensitivity: sensitivity,
            demandBaselinePerNpc: new Dictionary<int, double> { [1] = demandBaseline }).Value!;

    private static (WorldState World, Workplace Market) BuildWorld(long stock, long initialPrice, EconomyRules rules)
    {
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>());
        var map = ScenarioRunner.DefaultMap(1);
        var world = new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: rules, economyCatalog: catalog);

        var market = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: 1,
            employees: [], stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = stock },
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = initialPrice });
        world.AddWorkplace(market);

        // Demanda = DemandBaselinePerNpc × população residente na região (design.md) — sem
        // morador nenhum, a demanda zera e o preço só reage à capacidade sobrar/faltar em si.
        var resident = new Npc(
            world.NextNpcIdAndAdvance(), "resident", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            new CellCoord(1, 1), motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: new CellCoord(1, 1));
        world.AddNpc(resident);

        return (world, market);
    }

    [Fact]
    public void Price_rises_when_stock_is_scarce_relative_to_demand()
    {
        var rules = MakeRules(floor: 1, ceiling: 1000, sensitivity: 1.0, demandBaseline: 10.0);
        var (world, market) = BuildWorld(stock: 1, initialPrice: 10, rules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new MarketPricingSystem().Tick(world, ctx);

        Assert.True(market.Prices[new ResourceType(1)] > 10);
    }

    [Fact]
    public void Price_falls_when_stock_is_abundant_relative_to_demand()
    {
        var rules = MakeRules(floor: 1, ceiling: 1000, sensitivity: 1.0, demandBaseline: 0.01);
        var (world, market) = BuildWorld(stock: 1000, initialPrice: 10, rules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new MarketPricingSystem().Tick(world, ctx);

        Assert.True(market.Prices[new ResourceType(1)] < 10);
    }

    [Fact]
    public void Price_never_leaves_the_declared_floor_ceiling_range()
    {
        var rules = MakeRules(floor: 5, ceiling: 20, sensitivity: 10.0, demandBaseline: 1000.0);
        var (world, market) = BuildWorld(stock: 0, initialPrice: 10, rules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new MarketPricingSystem().Tick(world, ctx);

        var price = market.Prices[new ResourceType(1)];
        Assert.InRange(price, 5, 20);
    }
}
