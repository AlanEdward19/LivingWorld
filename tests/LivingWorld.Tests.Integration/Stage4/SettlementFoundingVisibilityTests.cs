using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T22 (LWV-04.6): fundação visível no mapa-múndi a partir de
/// uma única cidade-mãe — marcador novo em sítio distinto, transferência do pool e evento
/// de timeline. Não exige segunda cidade pré-existente.</summary>
public class SettlementFoundingVisibilityTests
{
    private static readonly VisualScope WorldScope = new(VisualScopeKind.World, "");

    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 0, migrationFoodWeight: 0,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold: 0.1,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: 1, materializationIdleTicksBeforeEligible: 5).Value!;

    private static WorldState MakeWorld(ulong seed = 22) => new(
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

    private static (WorldState World, City Mother, City Founded) FoundFromSingleCity()
    {
        var world = MakeWorld();
        var mother = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, new AggregatePopulationPool(20, 200, 200));
        world.AddCity(mother);
        Assert.Single(world.Cities);

        new SettlementFoundingSystem().Tick(world, Ctx(world));
        var scheduled = Assert.Single(world.PendingEvents);
        new SettlementFoundingSystem().HandleEvent(world, Ctx(world), scheduled);

        var founded = world.Cities.Single(c => c.Id != mother.Id);
        return (world, mother, founded);
    }

    [Fact]
    public void Founding_from_one_city_projects_a_second_city_upsert_at_a_distinct_site()
    {
        var (world, mother, founded) = FoundFromSingleCity();

        var cities = LivingScopeProjector.Build(world, WorldScope).Cities;
        Assert.Equal(2, cities.Count);
        var daughter = Assert.Single(cities, c => c.Id == founded.Id);
        Assert.NotEqual(mother.Location, daughter.Location);
        Assert.Equal(founded.Location, daughter.Location);
        Assert.Equal(mother.Id, daughter.FoundedFromCityId);
    }

    [Fact]
    public void Founding_pool_transfer_is_visible_on_projected_city_populations()
    {
        var (world, mother, founded) = FoundFromSingleCity();
        var cities = LivingScopeProjector.Build(world, WorldScope).Cities;
        var motherVisual = Assert.Single(cities, c => c.Id == mother.Id);
        var daughterVisual = Assert.Single(cities, c => c.Id == founded.Id);

        Assert.Equal(0, motherVisual.Population);
        Assert.Equal(20, daughterVisual.Population);
        Assert.Equal(20, motherVisual.Population + daughterVisual.Population);
    }

    [Fact]
    public void World_timeline_names_the_founding_without_requiring_a_preexisting_second_city()
    {
        var (world, _, _) = FoundFromSingleCity();
        var events = LivingScopeProjector.Build(world, WorldScope).Events;
        var founding = Assert.Single(events, e => e.Kind == WorldEventKind.SettlementFounded);

        Assert.Equal("Um novo assentamento foi fundado", founding.Label);
        Assert.DoesNotContain("payload", founding.Label, StringComparison.OrdinalIgnoreCase);
    }
}
