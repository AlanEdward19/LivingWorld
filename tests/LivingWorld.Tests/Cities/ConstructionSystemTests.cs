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

    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    /// <summary>Mesmo helper de <see cref="CityOccupancyTests"/>/<see
    /// cref="BuildingFootprintAndPlacementTests"/>: um <see cref="WorldState"/> com um mapa real
    /// de dimensões controladas (round-3 fix D) -- necessário pra forçar escassez de terra
    /// genuína (mapa do tamanho exato de um prédio) ou espaço real de sobra, sem depender do mapa
    /// padrão 10x10 de <see cref="ScenarioRunner.DefaultMap"/>.</summary>
    private static WorldState MakeWorldWithMap(int width, int height, CityCatalog catalog)
    {
        var rules = CityRules.Create(
            enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
            emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
            migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
            foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
            foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
            .Value!;
        var map = MapGenerator.Generate(seed: 12, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido — bug no teste, não no gerador");

        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 12, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, cityRules: rules, cityCatalog: catalog);
    }

    /// <summary>Mesmo truque de <see cref="CityOccupancyTests"/>: procura um footprint sem o
    /// entalhe do formato L, pra poder ocupar exatamente uma bounding box conhecida (e, aqui,
    /// tilar um mapa do exato tamanho do footprint pra escassez de terra genuína).</summary>
    private static (BuildingId Id, int Width, int Height) FindRectangularFootprint(int typeId)
    {
        for (long i = 1; i < 200; i++)
        {
            var id = new BuildingId(i);
            var cells = BuildingFootprintGenerator.Generate(id, typeId);
            int width = cells.Max(c => c.Cell.X) + 1;
            int height = cells.Max(c => c.Cell.Y) + 1;
            if (cells.Count == width * height)
                return (id, width, height);
        }
        throw new InvalidOperationException("nenhum footprint rectangular encontrado no intervalo testado");
    }

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

    // --- dynamic-city-growth, round-3 fix D (CITYGROW-02b): escassez de terra não pode
    // desaparecer o projeto ---

    /// <summary>Bug real (round-2 Verifier, Gap B): antes deste fix, um projeto de workplace que
    /// chegava ao tick de conclusão era desenfileirado incondicionalmente, mesmo quando
    /// <see cref="BuildingPlacementResolver.Resolve"/> não achava posição (escassez de terra) --
    /// o projeto (e o insumo já pago) simplesmente desaparecia, sem workplace nem retry, ao
    /// contrário do "fica na fila, tenta de novo depois" que design.md pede (Error Handling
    /// Strategy). Este teste prova as duas pontas: (1) num mapa sem espaço nenhum, o projeto
    /// continua na fila após o tick de conclusão -- não desaparece nem cria um Building órfão; (2)
    /// o MESMO cenário, mas com espaço real no mapa, completa normalmente e cria o Workplace.</summary>
    [Fact]
    public void Completing_project_leaves_a_land_scarce_workplace_queued_and_completes_once_land_is_available()
    {
        var catalogWithWorkplace = new CityCatalog(new Dictionary<int, BuildingRecipe>
        {
            [1] = BuildingRecipe.Create(
                new Dictionary<ResourceType, long> { [Timber] = 10 }, ticksToBuild: 1, housingCapacityProvided: 0,
                workplace: new WorkplaceProvision(LocationTypeId: 1, MaxVacancies: 1)).Value!,
        });
        var (rectId, w, h) = FindRectangularFootprint(typeId: 1);

        // Escassez de terra genuína: mapa do tamanho EXATO do único prédio existente -- nenhuma
        // célula livre em lugar nenhum, nem nos bounds nem no anel de overflow.
        var scarceWorld = MakeWorldWithMap(w, h, catalogWithWorkplace);
        // Localização no meio do mapa (não (0,0)) -- os bounds da cidade (população 0, lado 3)
        // precisam cair inteiramente dentro do mapa real, senão o scan "livre" encontraria uma
        // célula fora do mapa (que não é escassez de verdade, é um bounds mal-posicionado).
        var scarceCity = new City(scarceWorld.NextCityId(), new CellCoord(w / 2, h / 2), 0, null, AggregatePopulationPool.Empty);
        scarceWorld.AddCity(scarceCity);
        scarceWorld.AddBuilding(new Building(rectId, scarceCity.Id, buildingTypeId: 1, completedAtTick: 0, position: new CellCoord(0, 0), orientation: 0));
        scarceCity.DepositStock(Timber, 10);
        ConstructionSystem.StartConstruction(scarceWorld, scarceCity.Id, buildingTypeId: 1);

        new ConstructionSystem().Tick(scarceWorld, MakeCtx(scarceWorld)); // ticksToBuild=1 -> tenta concluir neste tick

        var queuedProject = Assert.Single(scarceCity.ConstructionQueue); // NÃO desapareceu -- ainda na fila pra retry
        Assert.Equal(0, queuedProject.TicksRemaining);
        Assert.Empty(scarceWorld.Workplaces); // workplace não foi criado
        Assert.DoesNotContain(scarceWorld.Buildings, b => b.Id.Value != rectId.Value); // nenhum Building órfão

        // Mesmo cenário/receita, mas com espaço real de sobra (mapa bem maior, sem nenhum
        // prédio bloqueando) -- o mesmo projeto completa normalmente e cria o Workplace, provando
        // que não é a mudança em si que impede a conclusão, só a escassez de terra genuína.
        var freeWorld = MakeWorldWithMap(200, 200, catalogWithWorkplace);
        var freeCity = new City(freeWorld.NextCityId(), new CellCoord(100, 100), 0, null, AggregatePopulationPool.Empty);
        freeWorld.AddCity(freeCity);
        freeCity.DepositStock(Timber, 10);
        ConstructionSystem.StartConstruction(freeWorld, freeCity.Id, buildingTypeId: 1);

        new ConstructionSystem().Tick(freeWorld, MakeCtx(freeWorld));

        Assert.Empty(freeCity.ConstructionQueue); // completou e desenfileirou
        var workplace = Assert.Single(freeWorld.Workplaces);
        Assert.Equal(1, workplace.MaxVacancies);
    }
}
