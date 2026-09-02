using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

public sealed class AttributeStrengthProductionTests
{
    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly ResourceType Timber = new(1);

    [Fact]
    public void Strength_two_multiplies_production_by_skill_times_two_versus_control()
    {
        var skills = SkillsRules.Create(
            cap: 100,
            baseRateBySource: new Dictionary<SkillGainSource, double> { [SkillGainSource.Practice] = 1 },
            skillByProfession: new Dictionary<int, SkillType> { [1] = new SkillType(0) },
            teachingSkill: new SkillType(6)).Value!;
        var treated = Workshop(["attribute.strength:2"], manifested: true, workerSkill: 50, skills);
        var control = Workshop(["attribute.strength:2"], manifested: false, workerSkill: 50, skills);

        new ProductionSystem(skills).Tick(treated.World, treated.Ctx);
        new ProductionSystem(skills).Tick(control.World, control.Ctx);

        long controlOut = control.Workplace.Stock.GetValueOrDefault(new ResourceType(1));
        long treatedOut = treated.Workplace.Stock.GetValueOrDefault(new ResourceType(1));
        Assert.Equal(15, controlOut);
        Assert.Equal(30, treatedOut);
        Assert.Equal(controlOut * 2, treatedOut);
    }

    [Fact]
    public void Ceasing_strength_restores_production_to_skill_only()
    {
        var treated = Workshop(["attribute.strength:2"], manifested: true, workerSkill: 0, skills: null);
        new ProductionSystem().Tick(treated.World, treated.Ctx);
        Assert.Equal(20, treated.Workplace.Stock.GetValueOrDefault(new ResourceType(1)));

        treated.World.UpsertExtraordinaryCarrier(Carrier(treated.Worker.Id, manifested: false));
        treated.Workplace.Withdraw(new ResourceType(1), 20);
        new ProductionSystem().Tick(treated.World, treated.Ctx);

        Assert.Equal(10, treated.Workplace.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal(1.0, AttributeMechanic.StrengthMultiplier(treated.World, treated.Worker));
    }

    [Fact]
    public void Strength_production_still_clamps_to_the_existing_workplace_capacity()
    {
        var treated = Workshop(
            ["attribute.strength:2"], manifested: true, workerSkill: 0, skills: null,
            capacity: new Dictionary<(int, int), long> { [(1, 1)] = 12 });

        new ProductionSystem().Tick(treated.World, treated.Ctx);

        Assert.Equal(12, treated.Workplace.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal(20, treated.World.ResourceProduced.GetValueOrDefault(new ResourceType(1)));
    }

    [Fact]
    public void Strength_two_doubles_construction_resource_consumption_versus_control()
    {
        var treated = ConstructionSite(["attribute.strength:2"], manifested: true);
        var control = ConstructionSite(["attribute.strength:2"], manifested: false);

        new ConstructionSystem().Tick(treated.World, treated.Ctx);
        new ConstructionSystem().Tick(control.World, control.Ctx);

        Assert.Equal(8, control.City.Stock.GetValueOrDefault(Timber));
        Assert.Equal(4, control.City.ConstructionQueue[0].TicksRemaining);
        Assert.Equal(2, control.City.ConstructionQueue[0].Consumed.GetValueOrDefault(Timber));

        Assert.Equal(6, treated.City.Stock.GetValueOrDefault(Timber));
        Assert.Equal(3, treated.City.ConstructionQueue[0].TicksRemaining);
        Assert.Equal(4, treated.City.ConstructionQueue[0].Consumed.GetValueOrDefault(Timber));
    }

    [Fact]
    public void Ceasing_strength_restores_construction_to_one_tick_of_consumption()
    {
        var site = ConstructionSite(["attribute.strength:2"], manifested: true);
        new ConstructionSystem().Tick(site.World, site.Ctx);
        Assert.Equal(3, site.City.ConstructionQueue[0].TicksRemaining);

        site.World.UpsertExtraordinaryCarrier(Carrier(site.Worker.Id, manifested: false));
        new ConstructionSystem().Tick(site.World, site.Ctx);

        Assert.Equal(2, site.City.ConstructionQueue[0].TicksRemaining);
        Assert.Equal(6, site.City.ConstructionQueue[0].Consumed.GetValueOrDefault(Timber));
    }

    private static (WorldState World, TickContext Ctx, Domain.Economy.Workplace Workplace, Npc Worker) Workshop(
        IReadOnlyList<string> effects, bool manifested, double workerSkill, SkillsRules? skills,
        Dictionary<(int, int), long>? capacity = null)
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10 },
            requiresCellResource: null, maxWorkersPerCycle: 1).Value!;
        var catalog = new EconomyCatalog(
            new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var rules = EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: capacity ?? new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(),
            priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0,
            demandBaselinePerNpc: new Dictionary<int, double>()).Value!;
        var location = new CellCoord(1, 1);
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var state = Carrier(new NpcId(1), manifested);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            economyRules: rules, economyCatalog: catalog,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state]);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: 1,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        var worker = new Npc(
            new NpcId(1), "worker", Sex.Male, WorldDate.Epoch(world.Calendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, location, motherId: null, fatherId: null, household: null,
            health: 100, personality: Personality, profession: new ProfessionType(1),
            currentLocation: location,
            skills: SkillSet.Empty.WithGain(new SkillType(0), workerSkill, cap: 100));
        world.AddNpc(worker);
        workplace.Hire(worker.Id);
        worker.Hire(workplace.Id);
        return (world, new TickContext(world, world.Rng, world.Scheduler), workplace, worker);
    }

    private static (WorldState World, TickContext Ctx, Domain.Cities.City City, Npc Worker) ConstructionSite(
        IReadOnlyList<string> effects, bool manifested)
    {
        var cityRules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
            foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
            .Value!;
        var catalog = new CityCatalog(new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = 10 }, ticksToBuild: 5, housingCapacityProvided: 4).Value!,
        });
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 11, ScenarioRunner.DefaultMap(11),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            cityRules: cityRules, cityCatalog: catalog,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [Carrier(new NpcId(1), manifested)]);
        var city = new City(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0,
            foundedFromCityId: null, aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        city.DepositStock(Timber, 10);
        Assert.True(ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1).IsSuccess);

        var worker = new Npc(
            new NpcId(1), "builder", Sex.Male, WorldDate.Epoch(world.Calendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, city.Location, motherId: null, fatherId: null, household: null,
            health: 100, personality: Personality, profession: ProfessionType.None, currentLocation: city.Location,
            currentAction: ActionType.Work, city: city.Id);
        world.AddNpc(worker);
        return (world, new TickContext(world, world.Rng, world.Scheduler), city, worker);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId npcId, bool manifested) =>
        new(npcId, ["test-power"], manifested, manifested ? "active" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
