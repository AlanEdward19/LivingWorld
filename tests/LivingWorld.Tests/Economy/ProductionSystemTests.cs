using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T15: <see cref="ProductionSystem"/> — sem trabalhador presente ou sem
/// recurso de célula exigido, produção é 0 (ECON-07/08); spoilage reduz estoque pela taxa
/// declarada.</summary>
public class ProductionSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static EconomyRules MakeRules(
        Dictionary<int, double>? spoilage = null, Dictionary<(int, int), long>? capacity = null) =>
        EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: capacity ?? new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: spoilage ?? new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(),
            priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0,
            demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static WorldState BuildWorld(EconomyCatalog catalog, EconomyRules? rules = null)
    {
        var map = ScenarioRunner.DefaultMap(1);
        return new WorldState(
            Calendar, 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: rules ?? MakeRules(), economyCatalog: catalog);
    }

    private static Npc MakeWorker(WorldState world, CellCoord location)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Workplace_with_worker_present_and_no_required_resource_produces_more_than_zero()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 5 }, requiresCellResource: null, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = BuildWorld(catalog);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 3,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var worker = MakeWorker(world, location);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem().Tick(world, ctx);

        Assert.True(workplace.Stock.GetValueOrDefault(new ResourceType(1)) > 0);
    }

    [Fact]
    public void Workplace_with_zero_workers_present_produces_exactly_zero()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 5 }, requiresCellResource: null, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var world = BuildWorld(catalog);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: 3,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem().Tick(world, ctx);

        Assert.Equal(0, workplace.Stock.GetValueOrDefault(new ResourceType(1)));
    }

    [Fact]
    public void Workplace_requiring_absent_cell_resource_produces_zero_even_with_worker_present()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 5 }, requiresCellResource: 999, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = BuildWorld(catalog);
        // DefaultMap não declara recurso 999 em nenhuma célula.
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 3,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var worker = MakeWorker(world, location);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem().Tick(world, ctx);

        Assert.Equal(0, workplace.Stock.GetValueOrDefault(new ResourceType(1)));
    }

    [Fact]
    public void Spoilage_reduces_stock_by_declared_rate_and_zero_rate_leaves_stock_untouched()
    {
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>());
        var rules = MakeRules(spoilage: new Dictionary<int, double> { [1] = 0.1, [2] = 0 });
        var world = BuildWorld(catalog, rules);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: 1,
            employees: [],
            stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 100, [new ResourceType(2)] = 100 },
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem().Tick(world, ctx);

        Assert.Equal(90, workplace.Stock[new ResourceType(1)]);
        Assert.Equal(100, workplace.Stock[new ResourceType(2)]);
    }
}
