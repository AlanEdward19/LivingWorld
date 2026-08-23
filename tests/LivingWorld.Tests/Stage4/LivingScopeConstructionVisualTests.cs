using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, Stage 4, T19 (LWV-04.4): obras na fila projetam <c>ProcessVisual</c>
/// com progresso e coordenada de canteiro <em>antes</em> do prédio autoritativo existir.</summary>
public class LivingScopeConstructionVisualTests
{
    private static readonly ResourceType Timber = new(1);

    private static CityCatalog Catalog(long ticks = 4) => new(
        new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = 8 }, ticks, housingCapacityProvided: 2).Value!,
        });

    private static WorldState MakeWorld() =>
        new(
            ScenarioRunner.DefaultCalendar, seed: 19, ScenarioRunner.DefaultMap(19),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: EconomyRules.Create(
                enabled: false, foodResourceId: 1, waterResourceId: 2,
                capacityByResourceLocation: new Dictionary<(int, int), long>(),
                spoilagePerDayByResource: new Dictionary<int, double>(),
                wageByProfession: new Dictionary<int, long>(),
                priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
                priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!,
            economyCatalog: EconomyCatalog.Empty,
            cityRules: CityRules.Create(
                enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
                emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
                migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 1,
                foundingResourceThreshold: 1, foundingRouteThreshold: 1, foundingDefensibilityThreshold: 1,
                foundingLeadershipThreshold: 1, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5).Value!,
            cityCatalog: Catalog());

    private static (WorldState World, City City) QueuedCity()
    {
        var world = MakeWorld();
        var city = new City(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        city.DepositStock(Timber, 40);
        Assert.True(ConstructionSystem.StartConstruction(world, city.Id, 1).IsSuccess);
        return (world, city);
    }

    [Fact]
    public void Queued_project_is_a_construction_process_before_any_building_exists()
    {
        var (world, city) = QueuedCity();

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        var process = Assert.Single(state.Processes, p => p.Kind == "construction");

        Assert.Empty(world.Buildings);
        Assert.Equal("construction", process.DescriptorKey);
        Assert.Equal(1, process.TargetId);
        Assert.Equal(0.0, process.Progress);
    }

    [Fact]
    public void Construction_process_includes_a_queued_site_location()
    {
        var (world, city) = QueuedCity();

        var process = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString())).Processes,
            p => p.Kind == "construction");

        Assert.Equal(BuildingPlacementResolver.ResolveQueuedSite(city, 0), process.Location);
    }

    [Fact]
    public void Construction_progress_rises_while_the_project_is_still_queued()
    {
        var (world, city) = QueuedCity();
        city.ConstructionQueue[0].Advance();

        var process = Assert.Single(
            LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString())).Processes,
            p => p.Kind == "construction");

        Assert.Equal(0.25, process.Progress);
        Assert.Empty(world.Buildings);
    }

    [Fact]
    public void Two_queued_projects_each_have_a_distinct_site()
    {
        var (world, city) = QueuedCity();
        Assert.True(ConstructionSystem.StartConstruction(world, city.Id, 1).IsSuccess);

        var sites = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()))
            .Processes.Where(p => p.Kind == "construction").Select(p => p.Location).ToList();

        Assert.Equal(2, sites.Count);
        Assert.NotEqual(sites[0], sites[1]);
    }
}
