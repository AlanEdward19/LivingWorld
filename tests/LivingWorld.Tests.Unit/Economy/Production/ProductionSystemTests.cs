using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy.Production;

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

    // --- Fase 6, T10 (SKILL-10/11): multiplicador de habilidade sobre produced ---

    private static SkillsRules MakeSkillsRules(double cap = 100) => SkillsRules.Create(
        cap, baseRateBySource: new Dictionary<SkillGainSource, double>(),
        skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0) },
        teachingSkill: new SkillType(6)).Value!;

    private static Npc MakeWorkerWithSkill(WorldState world, CellCoord location, double skillValue)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location,
            skills: SkillSet.Empty.WithGain(new SkillType(0), skillValue, cap: 100));
        world.AddNpc(npc);
        return npc;
    }

    private static Workplace BuildWorkplaceWithWorker(WorldState world, CellCoord location, double skillValue, out Npc worker)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 3,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        worker = MakeWorkerWithSkill(world, location, skillValue);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        return workplace;
    }

    [Fact]
    public void Without_skills_rules_higher_worker_skill_does_not_change_production_baseline()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10 }, requiresCellResource: null, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = BuildWorld(catalog);
        var workplace = BuildWorkplaceWithWorker(world, location, skillValue: 90, out _);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem(skillsRules: null).Tick(world, ctx);

        Assert.Equal(10, workplace.Stock[new ResourceType(1)]); // mesmo produced da Fase 5, sem multiplicador
    }

    [Fact]
    public void With_skills_rules_higher_average_worker_skill_produces_more_than_lower_skill_same_seed_and_input()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10 }, requiresCellResource: null, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var skillsRules = MakeSkillsRules();

        var lowWorld = BuildWorld(catalog);
        BuildWorkplaceWithWorker(lowWorld, location, skillValue: 0, out _);
        var lowWorkplace = lowWorld.Workplaces.Single();
        new ProductionSystem(skillsRules).Tick(lowWorld, new TickContext(lowWorld, lowWorld.Rng, lowWorld.Scheduler));

        var highWorld = BuildWorld(catalog);
        BuildWorkplaceWithWorker(highWorld, location, skillValue: 80, out _);
        var highWorkplace = highWorld.Workplaces.Single();
        new ProductionSystem(skillsRules).Tick(highWorld, new TickContext(highWorld, highWorld.Rng, highWorld.Scheduler));

        Assert.True(highWorkplace.Stock[new ResourceType(1)] > lowWorkplace.Stock[new ResourceType(1)]);
    }

    [Fact]
    public void Worker_with_unmapped_profession_contributes_neutral_multiplier()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10 }, requiresCellResource: null, maxWorkersPerCycle: 3).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = BuildWorld(catalog);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 3,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(999), currentLocation: location);
        world.AddNpc(npc);
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem(MakeSkillsRules()).Tick(world, ctx);

        Assert.Equal(10, workplace.Stock[new ResourceType(1)]); // sem mapeamento -> multiplicador 1.0
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

    // --- Fase 16.3, T9 (COH-22): WorkCapacityMultiplier como 4º fator ---

    private static Npc MakeWorkerWithMuscle(WorldState world, CellCoord location, double muscleMass)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            location, motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location,
            muscleMass: muscleMass);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void WorkCapacityMultiplier_is_applied_as_fourth_production_factor()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 100 }, requiresCellResource: null, maxWorkersPerCycle: 1).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = BuildWorld(catalog);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 1,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        // MuscleMassMean default = 28 → multiplier 1.0 at mean; 56 → 1.5
        var worker = MakeWorkerWithMuscle(world, location, muscleMass: 56);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ProductionSystem().Tick(world, ctx);

        Assert.Equal(150, workplace.Stock[new ResourceType(1)]);
    }

    [Fact]
    public void Two_npcs_same_job_different_MuscleMass_produce_different_output()
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 100 }, requiresCellResource: null, maxWorkersPerCycle: 1).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);

        long ProduceWithMuscle(double muscleMass)
        {
            var world = BuildWorld(catalog);
            var workplace = new Workplace(
                world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 1,
                employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
            world.AddWorkplace(workplace);
            var worker = MakeWorkerWithMuscle(world, location, muscleMass);
            workplace.Hire(worker.Id);
            worker.Hire(workplace.Id);
            new ProductionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
            return workplace.Stock.GetValueOrDefault(new ResourceType(1));
        }

        long weak = ProduceWithMuscle(14);
        long strong = ProduceWithMuscle(42);

        Assert.True(strong > weak, $"strong={strong} should exceed weak={weak}");
    }
}
