using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T11 (LWV-04.2/LWV-06): migração/fundação com deslocamento real,
/// membership só na chegada, sítio fundador distinto, conservação e replay de projeção viva.</summary>
public class LiveSettlementEvolutionTests
{
    private static readonly ResourceType Food = new(1);

    private static CityRules MakeRules(double foodWeight = 1) => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 0, migrationFoodWeight: foodWeight,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold: 0.1,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: 1, materializationIdleTicksBeforeEligible: 5).Value!;

    private static WorldState MakeWorld(CityRules rules, ulong seed = 51) => new(
        ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
        economyRules: EconomyRules.Create(
            enabled: false, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!,
        cityRules: rules);

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(WorldState world, long id, CityId city, CellCoord location) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        location, motherId: null, fatherId: null, household: null, health: 80,
        personality: Neutral, profession: ProfessionType.None, currentLocation: location, city: city);

    private static void ScheduleHourlyWakes(WorldState world, TickContext ctx)
    {
        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
    }

    private static void RunUntilRelocationClears(WorldState world, int maxHours = 300)
    {
        var clock = new WorldClock([new BehaviorDecisionSystem(), new RelocationArrivalSystem()]);
        var ctx = Ctx(world);
        for (int i = 0; i < maxHours; i++)
        {
            clock.Tick(world);
            ScheduleHourlyWakes(world, ctx);
            if (world.Households.All(h => h.PendingRelocationCity is null))
                return;
        }
    }

    [Fact]
    public void Migration_starts_travel_without_changing_city_membership_on_the_same_tick()
    {
        var world = MakeWorld(MakeRules());
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(3, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);

        var head = MakeNpc(world, 1, origin.Id, origin.Location);
        world.AddNpc(head);
        var household = new Household(new HouseholdId(1), new CellCoord(9, 9), head.Id, [head.Id], city: origin.Id);
        head.JoinHousehold(household.Id);
        household.Deposit(Food, 0);
        world.AddHousehold(household);
        var destinationNpc = MakeNpc(world, 2, destination.Id, destination.Location);
        world.AddNpc(destinationNpc);
        var destinationHousehold = new Household(new HouseholdId(2), destination.Location, destinationNpc.Id, [destinationNpc.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000);
        world.AddHousehold(destinationHousehold);

        new MigrationSystem().Tick(world, Ctx(world));

        Assert.Equal(origin.Id, head.City);
        Assert.Equal(destination.Id, household.PendingRelocationCity);
        Assert.Equal(ActionType.Travel, head.CurrentAction);
    }

    [Fact]
    public void Migration_changes_membership_only_after_arrival()
    {
        var world = MakeWorld(MakeRules());
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(2, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);

        var head = MakeNpc(world, 1, origin.Id, origin.Location);
        var member = MakeNpc(world, 2, origin.Id, origin.Location);
        world.AddNpc(head);
        world.AddNpc(member);
        var household = new Household(new HouseholdId(1), new CellCoord(9, 9), head.Id, [head.Id, member.Id], city: origin.Id);
        head.JoinHousehold(household.Id);
        member.JoinHousehold(household.Id);
        household.Deposit(Food, 0);
        world.AddHousehold(household);
        var destinationNpc = MakeNpc(world, 3, destination.Id, destination.Location);
        world.AddNpc(destinationNpc);
        var destinationHousehold = new Household(new HouseholdId(2), destination.Location, destinationNpc.Id, [destinationNpc.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000);
        world.AddHousehold(destinationHousehold);

        new MigrationSystem().Tick(world, Ctx(world));
        RunUntilRelocationClears(world);

        Assert.Equal(destination.Id, head.City);
        Assert.Equal(destination.Id, member.City);
        Assert.Equal(destination.Id, household.City);
        Assert.Null(household.PendingRelocationCity);
    }

    [Fact]
    public void Founding_creates_a_new_city_at_a_distinct_seeded_location()
    {
        var world = MakeWorld(MakeRules());
        var mother = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, new AggregatePopulationPool(20, 200, 200));
        world.AddCity(mother);

        new SettlementFoundingSystem().Tick(world, Ctx(world));
        var scheduled = Assert.Single(world.PendingEvents);
        new SettlementFoundingSystem().HandleEvent(world, Ctx(world), scheduled);

        var founded = world.Cities.Single(c => c.Id != mother.Id);
        Assert.NotEqual(mother.Location, founded.Location);
        Assert.Equal(20, founded.AggregatePool.Count);
        Assert.Equal(0, mother.AggregatePool.Count);
    }

    [Fact]
    public void Population_is_conserved_when_founding_moves_the_entire_aggregate_pool()
    {
        var world = MakeWorld(MakeRules());
        var mother = new City(world.NextCityId(), new CellCoord(4, 4), 0, null, new AggregatePopulationPool(15, 150, 150));
        world.AddCity(mother);
        long before = CityPopulationQuery.Population(world, mother.Id);

        new SettlementFoundingSystem().Tick(world, Ctx(world));
        var scheduled = Assert.Single(world.PendingEvents);
        new SettlementFoundingSystem().HandleEvent(world, Ctx(world), scheduled);

        long after = world.Cities.Sum(c => CityPopulationQuery.Population(world, c.Id));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Live_projection_replays_building_upserts_after_construction_completes()
    {
        var catalog = new CityCatalog(new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5 }, ticksToBuild: 1, housingCapacityProvided: 2).Value!,
        });
        var world = MakeWorld(MakeRules());
        world = new WorldState(
            world.Calendar, world.Seed, world.Map, world.PopulationCatalog, world.PopulationRules,
            world.NeedsRules, world.ActionCatalog, world.LifeStageRules,
            economyRules: world.EconomyRules, cityRules: world.CityRules, cityCatalog: catalog);
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, new AggregatePopulationPool(3, 30, 30));
        world.AddCity(city);
        city.DepositStock(new ResourceType(1), 10);

        var before = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        new ConstructionDemandSystem().Tick(world, Ctx(world));
        var queued = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        new ConstructionSystem().Tick(world, Ctx(world));
        var after = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));

        Assert.Empty(before.Buildings);
        Assert.Single(queued.Processes);
        Assert.Single(after.Buildings);
        Assert.Empty(after.Processes);
    }

    [Fact]
    public void Applying_scope_deltas_after_construction_matches_a_fresh_projection()
    {
        var catalog = new CityCatalog(new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5 }, ticksToBuild: 1, housingCapacityProvided: 2).Value!,
        });
        var world = MakeWorld(MakeRules());
        world = new WorldState(
            world.Calendar, world.Seed, world.Map, world.PopulationCatalog, world.PopulationRules,
            world.NeedsRules, world.ActionCatalog, world.LifeStageRules,
            economyRules: world.EconomyRules, cityRules: world.CityRules, cityCatalog: catalog);
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, new AggregatePopulationPool(3, 30, 30));
        world.AddCity(city);
        city.DepositStock(new ResourceType(1), 10);

        new ConstructionDemandSystem().Tick(world, Ctx(world));
        var after = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        var replayed = LivingDeltaReducer.Apply(
            LivingScopeState.Empty,
            ScopeDeltaBuilder.Diff(world.CurrentDate.TotalHours, LivingScopeState.Empty, after));
        Assert.Equal(after, replayed);
    }

    [Fact]
    public void Same_seed_migration_and_founding_path_is_deterministic()
    {
        static string Run(ulong seed)
        {
            var rules = MakeRules();
            var world = new WorldState(
                ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
                ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
                ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
                economyRules: EconomyRules.Create(
                    enabled: false, foodResourceId: 1, waterResourceId: 2,
                    capacityByResourceLocation: new Dictionary<(int, int), long>(),
                    spoilagePerDayByResource: new Dictionary<int, double>(),
                    wageByProfession: new Dictionary<int, long>(),
                    priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
                    priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!,
                cityRules: rules);
            var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
            var destination = new City(world.NextCityId(), new CellCoord(2, 0), 0, null, AggregatePopulationPool.Empty);
            world.AddCity(origin);
            world.AddCity(destination);
            var head = MakeNpc(world, 1, origin.Id, origin.Location);
            world.AddNpc(head);
            var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
            household.Deposit(Food, 0);
            world.AddHousehold(household);
            var destinationNpc = MakeNpc(world, 2, destination.Id, destination.Location);
            world.AddNpc(destinationNpc);
            var destinationHousehold = new Household(new HouseholdId(2), destination.Location, destinationNpc.Id, [destinationNpc.Id], city: destination.Id);
            destinationHousehold.Deposit(Food, 1000);
            world.AddHousehold(destinationHousehold);

            new MigrationSystem().Tick(world, Ctx(world));
            RunUntilRelocationClears(world);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(12), Run(12));
        Assert.NotEqual(Run(12), Run(13));
    }
}
