using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T10 (CITY-03): <see cref="ConstructionSystem"/> — iniciar sem insumo falha
/// sem mutar nada; obra concluída consome exatamente a receita; fila é FIFO.</summary>
public class ConstructionSystemTests
{
    private static readonly ResourceType Timber = new(1);

    private static WorldState MakeWorld(CityCatalog? catalog = null)
    {
        var rules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
            foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
            .Value!;

        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 11, ScenarioRunner.DefaultMap(11),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: rules, cityCatalog: catalog);
    }

    private static CityCatalog MakeCatalog(long timberCost = 10, long ticksToBuild = 5) => new(
        new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = timberCost }, ticksToBuild, housingCapacityProvided: 4).Value!,
        });

    private static City MakeCity(WorldState world) =>
        new(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void StartConstruction_fails_and_leaves_world_hash_unchanged_when_stock_is_insufficient()
    {
        var world = MakeWorld(MakeCatalog(timberCost: 10));
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 5); // insuficiente (receita pede 10)
        string hashBefore = WorldSnapshot.CanonicalHash(world);

        var result = ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);

        Assert.False(result.IsSuccess);
        Assert.Empty(city.ConstructionQueue);
        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void StartConstruction_fails_when_building_type_has_no_recipe_in_the_catalog()
    {
        var world = MakeWorld(MakeCatalog());
        var city = MakeCity(world);
        world.AddCity(city);

        var result = ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void StartConstruction_enqueues_a_project_when_stock_is_sufficient()
    {
        var world = MakeWorld(MakeCatalog(timberCost: 10, ticksToBuild: 5));
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 10);

        var result = ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);

        Assert.True(result.IsSuccess);
        var project = Assert.Single(city.ConstructionQueue);
        Assert.Equal(1, project.BuildingTypeId);
        Assert.Equal(5, project.TicksRemaining);
    }

    [Fact]
    public void Completed_project_has_total_consumption_equal_to_the_recipe_and_produces_a_building()
    {
        var world = MakeWorld(MakeCatalog(timberCost: 10, ticksToBuild: 5));
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 10);
        ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);
        var system = new ConstructionSystem();

        for (int i = 0; i < 5; i++)
            system.Tick(world, MakeCtx(world));

        Assert.Empty(city.ConstructionQueue);
        var building = Assert.Single(world.Buildings);
        Assert.Equal(city.Id, building.City);
        Assert.Equal(1, building.BuildingTypeId);
        Assert.Equal(0, city.Stock.GetValueOrDefault(Timber)); // consumo total == receita
    }

    [Fact]
    public void Queue_processes_only_the_head_project_leaving_the_second_untouched()
    {
        var world = MakeWorld(MakeCatalog(timberCost: 10, ticksToBuild: 5));
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 100);
        ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);
        ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);

        new ConstructionSystem().Tick(world, MakeCtx(world));

        Assert.Equal(2, city.ConstructionQueue.Count);
        Assert.Equal(4, city.ConstructionQueue[0].TicksRemaining); // avançou
        Assert.Equal(5, city.ConstructionQueue[1].TicksRemaining); // intocado (FIFO)
    }

    [Fact]
    public void Tick_pauses_without_reverting_progress_when_a_concurrent_consumer_drains_the_stock()
    {
        var world = MakeWorld(MakeCatalog(timberCost: 10, ticksToBuild: 5));
        var city = MakeCity(world);
        world.AddCity(city);
        city.DepositStock(Timber, 10);
        ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId: 1);
        var system = new ConstructionSystem();
        system.Tick(world, MakeCtx(world)); // consome 2/10, TicksRemaining 5->4

        long consumedSoFar = city.ConstructionQueue[0].Consumed.GetValueOrDefault(Timber);
        city.WithdrawStock(Timber, city.Stock.GetValueOrDefault(Timber)); // consumidor concorrente esvazia

        system.Tick(world, MakeCtx(world)); // sem insumo: pausa

        Assert.Equal(4, city.ConstructionQueue[0].TicksRemaining); // não regride nem avança
        Assert.Equal(consumedSoFar, city.ConstructionQueue[0].Consumed.GetValueOrDefault(Timber)); // progresso pago preservado
    }
}
