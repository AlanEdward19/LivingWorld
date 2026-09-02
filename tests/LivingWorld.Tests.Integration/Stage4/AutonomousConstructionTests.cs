using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T10 (LWV-04.1): construção autônoma por demanda — déficit de
/// moradia/vaga vira fila→obra→edifício/workplace autoritativo; sem capacidade real não há
/// trabalho fingido.</summary>
public class AutonomousConstructionTests
{
    private static readonly ResourceType Timber = new(1);

    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 1,
        foundingResourceThreshold: 1, foundingRouteThreshold: 1, foundingDefensibilityThreshold: 1,
        foundingLeadershipThreshold: 1, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;

    private static CityCatalog HousingCatalog(long timberCost = 10, long ticks = 3, long capacity = 4) => new(
        new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = timberCost }, ticks, housingCapacityProvided: capacity).Value!,
        });

    private static CityCatalog WorkplaceCatalog() => new(
        new Dictionary<int, BuildingRecipe>
        {
            [2] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = 8 }, ticksToBuild: 2, housingCapacityProvided: 0,
                workplace: new WorkplaceProvision(1, 2)).Value!,
        });

    private static EconomyRules EnabledEconomy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long> { [1] = 10 },
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static EconomyCatalog FarmerCatalog() => new(
        new Dictionary<int, ProductionRecipe>(),
        MarketLocationTypeIds: [],
        LocationTypeByProfession: new Dictionary<int, int> { [1] = 1 });

    private static WorldState MakeWorld(CityCatalog catalog, EconomyRules? economyRules = null, EconomyCatalog? economyCatalog = null)
    {
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 41, ScenarioRunner.DefaultMap(41),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: economyRules ?? EconomyRules.Create(
                enabled: false, foodResourceId: 1, waterResourceId: 2,
                capacityByResourceLocation: new Dictionary<(int, int), long>(),
                spoilagePerDayByResource: new Dictionary<int, double>(),
                wageByProfession: new Dictionary<int, long>(),
                priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
                priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!,
            economyCatalog: economyCatalog ?? EconomyCatalog.Empty,
            cityRules: MakeRules(), cityCatalog: catalog);
    }

    private static City MakeCity(WorldState world, long poolCount = 0) =>
        new(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null,
            new AggregatePopulationPool(poolCount, poolCount * 10, poolCount * 10),
            poolNpcIds: poolCount > 0 ? world.ReserveNpcIdBlock(poolCount) : []);

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Housing_deficit_enqueues_construction_when_stock_is_sufficient()
    {
        var world = MakeWorld(HousingCatalog());
        var city = MakeCity(world, poolCount: 5);
        world.AddCity(city);
        city.DepositStock(Timber, 20);

        new ConstructionDemandSystem().Tick(world, Ctx(world));

        var project = Assert.Single(city.ConstructionQueue);
        Assert.Equal(1, project.BuildingTypeId);
    }

    [Fact]
    public void Housing_deficit_does_not_enqueue_when_stock_is_insufficient()
    {
        var world = MakeWorld(HousingCatalog(timberCost: 50));
        var city = MakeCity(world, poolCount: 5);
        world.AddCity(city);
        city.DepositStock(Timber, 5);

        new ConstructionDemandSystem().Tick(world, Ctx(world));

        Assert.Empty(city.ConstructionQueue);
    }

    [Fact]
    public void Completed_housing_project_increases_authoritative_housing_capacity()
    {
        var world = MakeWorld(HousingCatalog(timberCost: 10, ticks: 2, capacity: 6));
        var city = MakeCity(world, poolCount: 4);
        world.AddCity(city);
        city.DepositStock(Timber, 20);
        Assert.Equal(0, CityPopulationQuery.Housing(world, city.Id));

        new ConstructionDemandSystem().Tick(world, Ctx(world));
        var construction = new ConstructionSystem();
        construction.Tick(world, Ctx(world));
        construction.Tick(world, Ctx(world));

        Assert.Empty(city.ConstructionQueue);
        Assert.Single(world.Buildings);
        Assert.Equal(6, CityPopulationQuery.Housing(world, city.Id));
    }

    [Fact]
    public void Employment_deficit_enqueues_workplace_construction_and_completion_creates_vacancies()
    {
        var world = MakeWorld(WorkplaceCatalog(), EnabledEconomy(), FarmerCatalog());
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 20);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "farmer", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
            city.Location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: city.Location, city: city.Id);
        world.AddNpc(npc);

        new ConstructionDemandSystem().Tick(world, Ctx(world));
        Assert.Equal(2, Assert.Single(city.ConstructionQueue).BuildingTypeId);

        var construction = new ConstructionSystem();
        construction.Tick(world, Ctx(world));
        construction.Tick(world, Ctx(world));

        var workplace = Assert.Single(world.Workplaces);
        Assert.Equal(1, workplace.LocationType.Id);
        Assert.Equal(2, workplace.MaxVacancies);
    }

    [Fact]
    public void Unemployed_npc_stays_blocked_until_workplace_exists_then_can_be_hired()
    {
        var world = MakeWorld(WorkplaceCatalog(), EnabledEconomy(), FarmerCatalog());
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 20);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "farmer", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
            city.Location, motherId: null, fatherId: null, household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: new ProfessionType(1), currentLocation: city.Location, city: city.Id);
        world.AddNpc(npc);

        new EmploymentSystem().Tick(world, Ctx(world));
        Assert.Null(npc.Employer);

        new ConstructionDemandSystem().Tick(world, Ctx(world));
        var construction = new ConstructionSystem();
        construction.Tick(world, Ctx(world));
        construction.Tick(world, Ctx(world));

        new EmploymentSystem().Tick(world, Ctx(world));
        Assert.NotNull(npc.Employer);
    }

    [Fact]
    public void Same_seed_produces_the_same_demand_and_completion_path()
    {
        static string Run(ulong seed)
        {
            var world = MakeWorld(HousingCatalog(timberCost: 10, ticks: 2, capacity: 3));
            world = new WorldState(
                world.Calendar, seed, world.Map, world.PopulationCatalog, world.PopulationRules,
                world.NeedsRules, world.ActionCatalog, world.LifeStageRules,
                economyRules: world.EconomyRules, cityRules: world.CityRules, cityCatalog: world.CityCatalog);
            var city = MakeCity(world, poolCount: 3);
            world.AddCity(city);
            city.DepositStock(Timber, 30);
            var systems = new WorldClock([new ConstructionDemandSystem(), new ConstructionSystem()]);
            systems.Run(world, ticks: world.Calendar.HoursPerDay * 3);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(77), Run(77));
        Assert.NotEqual(Run(77), Run(78));
    }
}
