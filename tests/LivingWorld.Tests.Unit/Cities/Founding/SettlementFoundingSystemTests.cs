using LivingWorld.Domain.Cities;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities.Founding;

/// <summary>Fase 8, T13 (CITY-08): <see cref="SettlementFoundingSystem"/> — todos os limiares
/// batidos agenda a fundação em exatamente <see cref="CityRules.OrganizationTicks"/>; soma de
/// população antes/depois do split é idêntica.</summary>
public class SettlementFoundingSystemTests
{
    private static CityRules MakeRules(
        double foundingConcentrationThreshold = 0.5, long organizationTicks = 10) => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
        emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks, materializationIdleTicksBeforeEligible: 5)
        .Value!;

    private static WorldState MakeWorld(CityRules rules) => new(
        ScenarioRunner.DefaultCalendar, seed: 23, ScenarioRunner.DefaultMap(23),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
        cityRules: rules);

    private static City MakeCity(WorldState world, AggregatePopulationPool pool) =>
        new(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool);

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Tick_schedules_founding_in_exactly_organization_ticks_when_all_thresholds_are_met()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 400)); // concentração alta
        world.AddCity(city);

        new SettlementFoundingSystem().Tick(world, MakeCtx(world));

        var pending = Assert.Single(world.PendingEvents);
        Assert.Equal(world.CurrentDate.TotalHours + 10, pending.TargetTick);
        Assert.Equal(city.Id.Value.ToString(), pending.Payload);
        Assert.NotNull(world.FindCity(city.Id)!.FoundingScheduledAtTick);
    }

    [Fact]
    public void Tick_does_not_schedule_when_concentration_threshold_is_not_met()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.999);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(1, 10, 10)); // concentração baixa (1/2=0.5 < 0.999)
        world.AddCity(city);

        new SettlementFoundingSystem().Tick(world, MakeCtx(world));

        Assert.Empty(world.PendingEvents);
        Assert.Null(world.FindCity(city.Id)!.FoundingScheduledAtTick);
    }

    [Fact]
    public void Tick_never_reschedules_a_city_that_already_has_a_founding_pending()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 400));
        world.AddCity(city);
        var system = new SettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));

        system.Tick(world, MakeCtx(world)); // segundo mês, limiares continuam batidos

        Assert.Single(world.PendingEvents); // não duplicou o evento
    }

    [Fact]
    public void HandleEvent_founds_a_new_city_and_preserves_total_population_across_the_split()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5, organizationTicks: 10);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 400));
        world.AddCity(city);
        var system = new SettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        long populationBefore = world.Cities.Sum(c => CityPopulationQuery.Population(world, c.Id));

        var evt = Assert.Single(world.PendingEvents);
        system.HandleEvent(world, MakeCtx(world), evt);

        Assert.Equal(2, world.Cities.Count);
        var newCity = world.Cities.Single(c => c.Id != city.Id);
        Assert.Equal(city.Id, newCity.FoundedFromCityId);
        Assert.Equal(0, world.FindCity(city.Id)!.AggregatePool.Count); // cidade-mãe perdeu o pool inteiro
        long populationAfter = world.Cities.Sum(c => CityPopulationQuery.Population(world, c.Id));
        Assert.Equal(populationBefore, populationAfter);
    }

    [Fact]
    public void HandleEvent_transfers_wealth_and_health_sums_along_with_the_headcount()
    {
        var rules = MakeRules(foundingConcentrationThreshold: 0.5);
        var world = MakeWorld(rules);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 400));
        world.AddCity(city);
        var system = new SettlementFoundingSystem();
        system.Tick(world, MakeCtx(world));
        var evt = Assert.Single(world.PendingEvents);

        system.HandleEvent(world, MakeCtx(world), evt);

        var newCity = world.Cities.Single(c => c.Id != city.Id);
        Assert.Equal(new AggregatePopulationPool(50, 500, 400), newCity.AggregatePool);
    }

    [Fact]
    public void Tick_is_a_no_op_when_city_rules_are_disabled()
    {
        var world = MakeWorld(CityRules.Disabled);
        var city = MakeCity(world, new AggregatePopulationPool(50, 500, 400));
        world.AddCity(city);

        new SettlementFoundingSystem().Tick(world, MakeCtx(world));

        Assert.Empty(world.PendingEvents);
    }
}
