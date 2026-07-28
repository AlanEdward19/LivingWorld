using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T12 (CITY-07): <see cref="MigrationSystem"/> — household materializado migra
/// pesando emprego/comida/segurança/laços familiares (CityRules), movendo todo mundo pro destino
/// no mesmo tick e preservando <see cref="HouseholdId"/>.</summary>
public class MigrationSystemTests
{
    private static readonly ResourceType Food = new(1);

    private static CityRules MakeRules(
        double employmentWeight = 0, double foodWeight = 1, double securityWeight = 0, double familyTiesWeight = 0) =>
        CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, employmentWeight, foodWeight, securityWeight, familyTiesWeight,
            foundingConcentrationThreshold: 0.5, foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5,
            foundingDefensibilityThreshold: 0.5, foundingLeadershipThreshold: 0.5,
            organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!;

    private static WorldState MakeWorld(CityRules rules)
    {
        var economyRules = EconomyRules.Create(
            enabled: false, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long>(),
            priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 17, ScenarioRunner.DefaultMap(17),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: economyRules, cityRules: rules);
    }

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static (City Origin, City Destination) MakeTwoCities(WorldState world)
    {
        var origin = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        var destination = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(origin);
        world.AddCity(destination);
        return (origin, destination);
    }

    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary>Npc "cru" (sem passar por PopulationSeeder — que casa adultos automaticamente em
    /// households próprios e poluiria a lista que <see cref="MigrationSystem"/> itera).</summary>
    private static Npc MakeNpc(WorldState world, long id, CityId city) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        ScenarioRunner.DefaultVillageLocation, motherId: null, fatherId: null, household: null, health: 80,
        personality: NeutralPersonality, profession: ProfessionType.None, currentLocation: ScenarioRunner.DefaultVillageLocation,
        city: city);

    /// <summary>Duas populações separadas, uma por cidade — a origem é quem o teste observa; a
    /// do destino só existe pra dar ao candidato um estoque/nível real (nunca o mesmo Npc nas
    /// duas listas de membros).</summary>
    private static (Npc OriginHead, Household OriginHousehold) SeedTwoOneNpcHouseholds(
        WorldState world, CityId originCityId, CityId destinationCityId, long originFood, long destinationFood)
    {
        var originHead = MakeNpc(world, 1, originCityId);
        var destinationHead = MakeNpc(world, 2, destinationCityId);
        world.AddNpc(originHead);
        world.AddNpc(destinationHead);

        var originHousehold = new Household(new HouseholdId(1), ScenarioRunner.DefaultVillageLocation, originHead.Id, [originHead.Id], city: originCityId);
        originHousehold.Deposit(Food, originFood);
        world.AddHousehold(originHousehold);

        var destinationHousehold = new Household(new HouseholdId(2), ScenarioRunner.DefaultVillageLocation, destinationHead.Id, [destinationHead.Id], city: destinationCityId);
        destinationHousehold.Deposit(Food, destinationFood);
        world.AddHousehold(destinationHousehold);

        return (originHead, originHousehold);
    }

    [Fact]
    public void Household_migrates_to_the_city_with_strictly_better_food_when_only_food_is_weighted()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(destination.Id, npc.City);
        Assert.Equal(destination.Id, household.City);
    }

    [Fact]
    public void Migrating_npc_never_ends_up_with_no_city_it_moves_directly_to_the_destination()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, _) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.NotEqual(default, npc.City);
        Assert.Equal(destination.Id, npc.City);
    }

    [Fact]
    public void Household_id_is_preserved_across_migration()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (_, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(new HouseholdId(1), household.Id);
        Assert.Same(household, world.FindHousehold(new HouseholdId(1)));
    }

    [Fact]
    public void Household_does_not_migrate_when_the_current_city_already_scores_highest()
    {
        var world = MakeWorld(MakeRules(foodWeight: 1));
        var (origin, destination) = MakeTwoCities(world);
        var (npc, household) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 1000, destinationFood: 0);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
        Assert.Equal(origin.Id, household.City);
    }

    [Fact]
    public void Family_ties_can_keep_a_household_in_a_worse_city_when_weighted_heavily()
    {
        // Comida melhor no destino, mas o cônjuge mora na origem — laço familiar pesa mais.
        var world = MakeWorld(MakeRules(foodWeight: 1, familyTiesWeight: 100));
        var (origin, destination) = MakeTwoCities(world);
        var head = MakeNpc(world, 1, origin.Id);
        var spouseInOrigin = MakeNpc(world, 2, origin.Id);
        var unrelatedInDestination = MakeNpc(world, 3, destination.Id);
        head.Marry(spouseInOrigin.Id);
        world.AddNpc(head);
        world.AddNpc(spouseInOrigin);
        world.AddNpc(unrelatedInDestination);

        var household = new Household(new HouseholdId(1), origin.Location, head.Id, [head.Id], city: origin.Id);
        household.Deposit(Food, 0); // origem sem comida
        world.AddHousehold(household);

        var destinationHousehold = new Household(new HouseholdId(2), destination.Location, unrelatedInDestination.Id, [unrelatedInDestination.Id], city: destination.Id);
        destinationHousehold.Deposit(Food, 1000); // destino farto, mas sem ninguém da família
        world.AddHousehold(destinationHousehold);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, head.City); // ficou por causa do laço, apesar da comida pior
    }

    [Fact]
    public void Tick_is_a_no_op_when_city_rules_are_disabled()
    {
        var world = MakeWorld(CityRules.Disabled);
        var (origin, destination) = MakeTwoCities(world);
        var (npc, _) = SeedTwoOneNpcHouseholds(world, origin.Id, destination.Id, originFood: 0, destinationFood: 1000);

        new MigrationSystem().Tick(world, MakeCtx(world));

        Assert.Equal(origin.Id, npc.City);
    }
}
