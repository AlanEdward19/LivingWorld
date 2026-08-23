using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T21 (LWV-04.7): rotas de migração entre cidades existentes
/// no mapa-múndi — deslocamento visível, membership só na chegada, commute intra-cidade
/// não é emigração.</summary>
public class InterCityMigrationVisibilityTests
{
    private static readonly ResourceType Food = new(1);
    private static readonly VisualScope WorldScope = new(VisualScopeKind.World, "");

    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 0, migrationFoodWeight: 1,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold: 0.1,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: 1, materializationIdleTicksBeforeEligible: 5).Value!;

    private static WorldState MakeWorld(ulong seed = 21) => new(
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
        cityRules: MakeRules());

    private static TickContext Ctx(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(WorldState world, long id, CityId city, CellCoord location) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        location, motherId: null, fatherId: null, household: null, health: 80,
        personality: Neutral, profession: ProfessionType.None, currentLocation: location, city: city);

    private static (WorldState World, City Origin, City Destination, Npc Head) MigratingHousehold()
    {
        var world = MakeWorld();
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(8, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);

        var head = MakeNpc(world, 1, origin.Id, origin.Location);
        world.AddNpc(head);
        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
        head.JoinHousehold(household.Id);
        household.Deposit(Food, 0);
        world.AddHousehold(household);
        var destinationNpc = MakeNpc(world, 2, destination.Id, destination.Location);
        world.AddNpc(destinationNpc);
        var destinationHousehold = new Household(
            new HouseholdId(2), destination.Location, destinationNpc.Id, [destinationNpc.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000);
        world.AddHousehold(destinationHousehold);

        Assert.True(world.Cities.Count >= 2);
        new MigrationSystem().Tick(world, Ctx(world));
        return (world, origin, destination, head);
    }

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
    public void World_projection_exposes_travel_route_without_changing_city_membership()
    {
        var (world, origin, destination, head) = MigratingHousehold();

        var visual = Assert.Single(LivingScopeProjector.Build(world, WorldScope).Npcs, n => n.Id == head.Id);

        Assert.Equal(origin.Id, visual.City);
        Assert.Equal(origin.Id, head.City);
        Assert.Equal(ActionType.Travel, visual.CurrentAction);
        Assert.Equal(destination.Location, visual.RelocationDestination);
    }

    [Fact]
    public void Intra_city_commute_is_not_projected_as_an_emigration_route()
    {
        var world = MakeWorld();
        var city = new City(world.NextCityId(), new CellCoord(2, 2), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var other = new City(world.NextCityId(), new CellCoord(9, 9), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(other);
        var npc = MakeNpc(world, 7, city.Id, city.Location);
        npc.SetCurrentAction(ActionType.Travel, 0);
        world.AddNpc(npc);

        Assert.DoesNotContain(
            LivingScopeProjector.Build(world, WorldScope).Npcs,
            n => n.Id == npc.Id && n.RelocationDestination is not null);

        var visual = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString())).Npcs,
            n => n.Id == npc.Id);
        Assert.Null(visual.RelocationDestination);
        Assert.Equal(city.Id, visual.City);
        Assert.Equal(ActionType.Travel, visual.CurrentAction);
    }

    [Fact]
    public void Arrival_applies_destination_membership_and_clears_the_travel_route()
    {
        var (world, origin, destination, head) = MigratingHousehold();
        RunUntilRelocationClears(world);

        Assert.Equal(destination.Id, head.City);
        Assert.NotEqual(origin.Id, head.City);

        var destScope = new VisualScope(VisualScopeKind.City, destination.Id.ToString());
        var arrived = Assert.Single(
            LivingScopeProjector.Build(world, destScope).Npcs, n => n.Id == head.Id);
        Assert.Equal(destination.Id, arrived.City);
        Assert.Null(arrived.RelocationDestination);

        Assert.DoesNotContain(
            LivingScopeProjector.Build(world, WorldScope).Npcs,
            n => n.Id == head.Id && n.RelocationDestination is not null);
    }
}
