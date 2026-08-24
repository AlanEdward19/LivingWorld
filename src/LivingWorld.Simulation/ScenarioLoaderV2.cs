using LivingWorld.Domain;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Simulation;

/// <summary>Monta um mundo completo a partir de um <c>periodDefinition</c> (Fase 13, T3):
/// substitui a integração parcial de <see cref="ScenarioLoader"/> (que ainda não plugava
/// comportamento/economia/cidades/dinâmica) por um pipeline único via
/// <see cref="PeriodDefinitionValidator"/> — mapa, população, comportamento, economia, cidades e
/// vieses/regras de transformação vêm todos do mesmo período, nenhum cai em default hardcoded
/// quando o campo está presente no JSON. <see cref="ScenarioLoader"/> continua existindo e
/// intocado — cenários legados (<c>scenarios/default.json</c>, <c>scenarios/test-scifi.json</c>)
/// não declaram Economy/Cities/Dynamics e continuam passando por ele.</summary>
public static class ScenarioLoaderV2
{
    public static Result<(WorldState World, WorldClock Clock)> LoadWorld(string json, int maxIterationsPerTick = 1000)
    {
        var definitionResult = PeriodDefinitionValidator.Validate(json);
        if (!definitionResult.IsSuccess)
            return Result<(WorldState, WorldClock)>.Fail(definitionResult.Error!);

        var definition = definitionResult.Value!;
        var population = definition.Population;

        // Fase 13, T10: viés declarado em Dynamics.ProfessionBiases vira peso real de sorteio
        // (PopulationCatalog.RollProfession) — sem bias declarado, catálogo original passa
        // adiante sem cópia, sorteio continua uniforme.
        var catalog = definition.Dynamics.ProfessionBiases.Count == 0
            ? population.Catalog
            : population.Catalog with
            {
                ProfessionWeights = definition.Dynamics.ProfessionBiases.ToDictionary(b => b.ProfessionId, b => b.Weight),
            };

        // Post-ship fix (bug real, "mesma seed, mundo diferente"): usava InitialMapForPopulation
        // pra silenciosamente REGERAR o mapa inteiro num tamanho maior sempre que a população
        // autorada não garantia espaço de sobra pra cada household+workplace (mesmo num mapa
        // 10x10 default com população modesta). MapGenerator consome um WorldRng(seed) sequencial
        // célula a célula -- mudar width/height pra mesma seed produz um terreno totalmente
        // diferente em toda célula, não só nas bordas novas, então a cidade autorada pelo usuário
        // acabava sobre um terreno que ele nunca viu/escolheu. O mapa autorado (definition.Map)
        // agora é usado exatamente como veio do JSON; escassez de espaço é responsabilidade do
        // placement (BuildingPlacementResolver/OverflowPlacer), que já resolve isso sem regerar
        // nada (célula livre nos bounds, senão anel de overflow, senão recusa/decline).
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, definition.Map.Seed, definition.Map,
            catalog, population.Rules,
            definition.Behavior.NeedsRules, definition.Behavior.ActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            economyRules: definition.Economy.Rules, economyCatalog: definition.Economy.Catalog,
            cityRules: definition.City.Rules, cityCatalog: definition.City.Catalog,
            restPlaceCatalog: definition.Behavior.RestPlaceCatalog,
            resourceCatalog: definition.ResourceCatalog,
            processRecipes: definition.ProcessRecipes,
            extraordinary: definition.Extraordinary);

        var createdCityIds = new List<CityId>(definition.City.Cities.Count);
        foreach (var city in definition.City.Cities)
        {
            string name = string.IsNullOrEmpty(city.Name) ? CityNameGenerator.Generate(world) : city.Name;
            // T50: cidade autorada com pool não-vazio reserva um NpcId estável por membro,
            // clicável/materializável individualmente (antes só existia contagem+somas).
            var poolNpcIds = world.ReserveNpcIdBlock(city.AggregatePool.Count);
            var createdCity = new City(
                world.NextCityId(), city.Location, city.FoundedAtTick, foundedFromCityId: null, city.AggregatePool,
                name: name, poolNpcIds: poolNpcIds);
            world.AddCity(createdCity);
            createdCityIds.Add(createdCity.Id);
        }

        var createdBuildingIds = new List<BuildingId>(definition.City.Buildings.Count);
        foreach (var building in definition.City.Buildings)
        {
            var createdBuilding = new Building(
                world.NextBuildingIdAndAdvance(), createdCityIds[building.CityIndex], building.BuildingTypeId,
                completedAtTick: 0, position: building.Position, orientation: building.Orientation);
            world.AddBuilding(createdBuilding);
            createdBuildingIds.Add(createdBuilding.Id);
        }

        // Bugfix real (usuário, 2026-08-13): a população inicial nunca era vinculada a nenhuma
        // cidade (Npc.City/Household.City ficavam no CityId default) — sumia de toda projeção
        // (World filtra NPC externo por cidade conhecida, City filtra por Npc.City == cityId),
        // então todo mundo criado (em branco ou por template) parecia sempre vazio, com a mesma
        // única cidade fixa do formulário (population 0) e nenhum morador visível em lugar
        // nenhum. Reusa a cidade autorada na mesma célula da vila inicial, se existir; senão
        // funda uma nova ali — mesmo nome de fallback de SettlementFoundingSystem.
        //
        // Bugfix real (usuário, 2026-08-14): com 2+ assentamentos autorados, só a vila inicial
        // (population.Village) ganhava população — as demais nasciam sempre com 0 moradores.
        // Distribui population.InitialPopulation por TODAS as cidades autoradas (resto pra vila
        // inicial), não precisa ser exatamente igual entre elas, só nenhuma ficar zerada à toa.
        if (population.InitialPopulation > 0)
        {
            int homeCityIndex = definition.City.Cities
                .Select((c, index) => (c.Location, index))
                .Where(c => c.Location == population.Village)
                .Select(c => c.index)
                .DefaultIfEmpty(-1)
                .First();

            var seedTargets = new List<(CellCoord Location, CityId Id)>();
            for (int i = 0; i < definition.City.Cities.Count; i++)
                seedTargets.Add((definition.City.Cities[i].Location, createdCityIds[i]));

            if (homeCityIndex < 0)
            {
                var homeCity = new City(
                    world.NextCityId(), population.Village, foundedAtTick: 0, foundedFromCityId: null,
                    new AggregatePopulationPool(0, 0, 0), name: CityNameGenerator.Generate(world));
                world.AddCity(homeCity);
                homeCityIndex = seedTargets.Count;
                seedTargets.Add((homeCity.Location, homeCity.Id));
            }

            // Cidade autorada com `InitialPopulation` explícito nasce com esse valor; o resto do
            // total continua dividido igualmente entre as demais (resto da divisão pra vila-sede,
            // como sempre) — dá controle de tamanho inicial por assentamento sem mexer na fórmula
            // de crescimento (CityBoundsResolver deriva o footprint da população atual, sempre).
            var explicitShares = new Dictionary<int, int>();
            for (int i = 0; i < definition.City.Cities.Count; i++)
            {
                var explicitPopulation = definition.City.Cities[i].InitialPopulation;
                if (explicitPopulation is int share) explicitShares[i] = share;
            }
            long explicitTotal = explicitShares.Values.Sum();
            var remainingTargets = Enumerable.Range(0, seedTargets.Count).Where(i => !explicitShares.ContainsKey(i)).ToList();
            long remainingPopulation = Math.Max(0, population.InitialPopulation - explicitTotal);
            int perCity = remainingTargets.Count > 0 ? (int)(remainingPopulation / remainingTargets.Count) : 0;
            int remainder = remainingTargets.Count > 0 ? (int)(remainingPopulation % remainingTargets.Count) : 0;
            int remainderTargetIndex = remainingTargets.Contains(homeCityIndex) ? homeCityIndex : remainingTargets.FirstOrDefault(-1);
            for (int i = 0; i < seedTargets.Count; i++)
            {
                int share = explicitShares.TryGetValue(i, out var explicitShare)
                    ? explicitShare
                    : perCity + (i == remainderTargetIndex ? remainder : 0);
                if (share <= 0) continue;
                try
                {
                    PopulationSeeder.SeedInitial(
                        world, share, population.Culture, seedTargets[i].Location, seedTargets[i].Id);
                }
                catch (InvalidOperationException ex)
                {
                    return Result<(WorldState, WorldClock)>.Fail($"Population: {ex.Message}");
                }
            }
        }

        foreach (var workplace in definition.Economy.Workplaces)
        {
            if (!world.ActiveCities().Any())
            {
                // SPEC_DEVIATION: um cenário autorado sem nenhuma cidade não oferece a entidade
                // proprietária exigida pelo placement. Mantém o workplace legado sem Building,
                // em vez de inventar uma cidade que o JSON não declarou.
                world.AddWorkplace(new Workplace(
                    world.NextWorkplaceIdAndAdvance(), workplace.LocationType, workplace.Location,
                    workplace.MaxVacancies, employees: [], workplace.Stock, workplace.Treasury, workplace.Prices));
                continue;
            }

            var city = NearestCity(world.ActiveCities(), workplace.Location);
            int buildingTypeId = workplace.LocationType.Id;
            var candidateId = new BuildingId(world.NextBuildingId);
            var candidateShape = BuildingFootprintGenerator.Generate(candidateId, buildingTypeId, orientation: 0)
                .Select(cell => cell.Cell)
                .ToList();
            long cityPopulation = CityPopulationQuery.Population(world, city.Id);
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, cityPopulation).Bounds;
            var authoredFootprint = candidateShape
                .Select(cell => new CellCoord(workplace.Location.X + cell.X, workplace.Location.Y + cell.Y))
                .ToList();

            CellCoord position;
            int orientation;
            if (CityOccupancy.IsFree(world, city, bounds, authoredFootprint, candidateId))
            {
                position = workplace.Location;
                orientation = 0;
            }
            else
            {
                var candidate = new Building(candidateId, city.Id, buildingTypeId, completedAtTick: 0);
                var resolved = BuildingPlacementResolver.Resolve(candidate, city, world, bounds);
                if (resolved is null)
                    return Result<(WorldState, WorldClock)>.Fail(
                        "Workplaces: nenhuma célula livre para posicionar o edifício autorado");
                position = resolved.Value.Position;
                orientation = resolved.Value.Orientation;
            }

            world.AddBuilding(new Building(
                world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId, completedAtTick: 0,
                position, orientation));
            world.AddWorkplace(new Workplace(
                world.NextWorkplaceIdAndAdvance(), workplace.LocationType, position, workplace.MaxVacancies,
                employees: [], workplace.Stock, workplace.Treasury, workplace.Prices, city.Id));
        }

        // Fase 15.1, T21: portal é dado descritivo — resolve RefIndex autorado pro RefId real
        // (mesmo padrão de createdCityIds[building.CityIndex] acima) depois que cidades/prédios
        // já têm id, e autora via AddPortal, único ponto de mutação da coleção.
        foreach (var portal in definition.City.Portals)
            world.AddPortal(new SpatialPortal(
                portal.Id, portal.Label,
                ResolvePortalEndpoint(portal.From, createdCityIds, createdBuildingIds),
                ResolvePortalEndpoint(portal.To, createdCityIds, createdBuildingIds)));

        // Fase 13, T13: PeriodEvolutionSystem primeiro na lista — regra de transformação muda o
        // catálogo antes de qualquer sistema do mesmo tick sortear profissão por ele
        // (NatalitySystem/MaterializationSystem/PopulationSeeder).
        IReadOnlyList<ISimulationSystem> systems =
            ScenarioRunner.DefaultSystems(
                definition.Dynamics.TransformationRules, extraordinary: definition.Extraordinary);

        return Result<(WorldState, WorldClock)>.Ok((world, new WorldClock(systems, maxIterationsPerTick)));
    }

    private static City NearestCity(IEnumerable<City> cities, CellCoord location) =>
        cities
            .OrderBy(city => Math.Max(
                Math.Abs(city.Location.X - location.X),
                Math.Abs(city.Location.Y - location.Y)))
            .ThenBy(city => city.Id.Value)
            .First();

    private static PortalEndpoint ResolvePortalEndpoint(
        AuthoredPortalEndpoint endpoint, IReadOnlyList<CityId> createdCityIds, IReadOnlyList<BuildingId> createdBuildingIds) =>
        endpoint.Space switch
        {
            PortalSpaceKind.World => new PortalEndpoint(PortalSpaceKind.World, "", endpoint.Cell),
            PortalSpaceKind.City => new PortalEndpoint(PortalSpaceKind.City, createdCityIds[endpoint.RefIndex].ToString(), endpoint.Cell),
            PortalSpaceKind.Building => new PortalEndpoint(PortalSpaceKind.Building, createdBuildingIds[endpoint.RefIndex].ToString(), endpoint.Cell),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
        };
}
