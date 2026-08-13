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

        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, definition.Map.Seed, definition.Map,
            catalog, population.Rules,
            definition.Behavior.NeedsRules, definition.Behavior.ActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            economyRules: definition.Economy.Rules, economyCatalog: definition.Economy.Catalog,
            cityRules: definition.City.Rules, cityCatalog: definition.City.Catalog);

        foreach (var workplace in definition.Economy.Workplaces)
            world.AddWorkplace(new Workplace(
                world.NextWorkplaceIdAndAdvance(), workplace.LocationType, workplace.Location, workplace.MaxVacancies,
                employees: [], workplace.Stock, workplace.Treasury, workplace.Prices));

        var createdCityIds = new List<CityId>(definition.City.Cities.Count);
        foreach (var city in definition.City.Cities)
        {
            string name = string.IsNullOrEmpty(city.Name) ? CityNameGenerator.Generate(world) : city.Name;
            var createdCity = new City(world.NextCityId(), city.Location, city.FoundedAtTick, foundedFromCityId: null, city.AggregatePool, name: name);
            world.AddCity(createdCity);
            createdCityIds.Add(createdCity.Id);
        }

        // Bugfix real (usuário, 2026-08-13): a população inicial nunca era vinculada a nenhuma
        // cidade (Npc.City/Household.City ficavam no CityId default) — sumia de toda projeção
        // (World filtra NPC externo por cidade conhecida, City filtra por Npc.City == cityId),
        // então todo mundo criado (em branco ou por template) parecia sempre vazio, com a mesma
        // única cidade fixa do formulário (population 0) e nenhum morador visível em lugar
        // nenhum. Reusa a cidade autorada na mesma célula da vila inicial, se existir; senão
        // funda uma nova ali — mesmo nome de fallback de SettlementFoundingSystem.
        if (population.InitialPopulation > 0)
        {
            int homeCityIndex = definition.City.Cities
                .Select((c, index) => (c.Location, index))
                .Where(c => c.Location == population.Village)
                .Select(c => c.index)
                .DefaultIfEmpty(-1)
                .First();

            CityId homeCityId;
            if (homeCityIndex >= 0)
            {
                homeCityId = createdCityIds[homeCityIndex];
            }
            else
            {
                var homeCity = new City(
                    world.NextCityId(), population.Village, foundedAtTick: 0, foundedFromCityId: null,
                    new AggregatePopulationPool(0, 0, 0), name: CityNameGenerator.Generate(world));
                world.AddCity(homeCity);
                homeCityId = homeCity.Id;
            }

            PopulationSeeder.SeedInitial(world, population.InitialPopulation, population.Culture, population.Village, homeCityId);
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
            [new PeriodEvolutionSystem(definition.Dynamics.TransformationRules), .. ScenarioRunner.DefaultSystems()];

        return Result<(WorldState, WorldClock)>.Ok((world, new WorldClock(systems, maxIterationsPerTick)));
    }

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
