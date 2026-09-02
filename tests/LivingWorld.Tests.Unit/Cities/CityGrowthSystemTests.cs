using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T11 (CITY-02): <see cref="CityGrowthSystem"/> — déficit de comida/moradia
/// reduz só <see cref="AggregatePopulationPool.Count"/>, nunca NPC materializado; taxa vem só de
/// <see cref="CityRules"/>.</summary>
public class CityGrowthSystemTests
{
    private static readonly ResourceType Food = new(1);

    private static CityRules MakeRules(
        double foodShortageThreshold = 20, double housingShortageThreshold = 20,
        double emigrationRatePerDeficitUnit = 0.5) => CityRules.Create(
        enabled: true, foodShortageThreshold, housingShortageThreshold, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
        foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
        foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
        .Value!;

    private static WorldState MakeWorld(CityRules rules)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 13, ScenarioRunner.DefaultMap(13),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EconomyRulesWithFood(), cityRules: rules);
        return world;
    }

    private static EconomyRules EconomyRulesWithFood() => EconomyRules.Create(
        enabled: false, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static City MakeCity(WorldState world, AggregatePopulationPool pool) =>
        new(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool, poolNpcIds: world.ReserveNpcIdBlock(pool.Count));

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Tick_reduces_aggregate_pool_count_when_food_stock_is_below_threshold_for_the_population()
    {
        var rules = MakeRules(foodShortageThreshold: 20, emigrationRatePerDeficitUnit: 0.5);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(100, 1000, 1000));
        world.AddCity(city);
        // 100 população agregada, 0 estoque de comida => déficit de comida 100%, excesso sobre o
        // limiar (20) é 80 => emigrantes esperados = floor(0.5 * 80) = 40.

        new CityGrowthSystem().Tick(world, MakeCtx(world));

        Assert.Equal(60, world.FindCity(city.Id)!.AggregatePool.Count);
    }

    [Fact]
    public void Tick_never_reduces_below_zero_even_when_the_computed_emigration_exceeds_the_pool()
    {
        var rules = MakeRules(foodShortageThreshold: 0, emigrationRatePerDeficitUnit: 10);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(3, 30, 30));
        world.AddCity(city);

        new CityGrowthSystem().Tick(world, MakeCtx(world));

        Assert.Equal(0, world.FindCity(city.Id)!.AggregatePool.Count);
    }

    [Fact]
    public void Tick_does_not_emigrate_when_deficit_stays_within_the_threshold()
    {
        var rules = MakeRules(foodShortageThreshold: 100, housingShortageThreshold: 100);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 500));
        world.AddCity(city);

        new CityGrowthSystem().Tick(world, MakeCtx(world));

        Assert.Equal(50, world.FindCity(city.Id)!.AggregatePool.Count);
    }

    [Fact]
    public void Tick_is_a_no_op_when_city_rules_are_disabled()
    {
        var world = MakeWorld(CityRules.Disabled);
        var city = MakeCity(world, new AggregatePopulationPool(100, 1000, 1000));
        world.AddCity(city);

        new CityGrowthSystem().Tick(world, MakeCtx(world));

        Assert.Equal(100, world.FindCity(city.Id)!.AggregatePool.Count);
    }

    [Fact]
    public void Tick_never_touches_a_materialized_npc_even_under_severe_shortage()
    {
        var rules = MakeRules(foodShortageThreshold: 0, emigrationRatePerDeficitUnit: 1);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(10, 100, 100));
        world.AddCity(city);
        PopulationSeeder.SeedInitial(world, 1, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        var npc = world.Npcs.First();
        npc.JoinCity(city.Id);
        int npcCountBefore = world.Npcs.Count;

        new CityGrowthSystem().Tick(world, MakeCtx(world));

        Assert.Equal(npcCountBefore, world.Npcs.Count);
        Assert.NotNull(world.FindNpc(npc.Id));
    }
}
