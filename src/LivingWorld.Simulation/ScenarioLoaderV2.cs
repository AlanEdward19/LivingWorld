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

        if (population.InitialPopulation > 0)
            PopulationSeeder.SeedInitial(world, population.InitialPopulation, population.Culture, population.Village);

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

        foreach (var building in definition.City.Buildings)
            world.AddBuilding(new Building(
                world.NextBuildingIdAndAdvance(), createdCityIds[building.CityIndex], building.BuildingTypeId,
                completedAtTick: 0, position: building.Position, orientation: building.Orientation));

        // Fase 13, T13: PeriodEvolutionSystem primeiro na lista — regra de transformação muda o
        // catálogo antes de qualquer sistema do mesmo tick sortear profissão por ele
        // (NatalitySystem/MaterializationSystem/PopulationSeeder).
        IReadOnlyList<ISimulationSystem> systems =
            [new PeriodEvolutionSystem(definition.Dynamics.TransformationRules), .. ScenarioRunner.DefaultSystems()];

        return Result<(WorldState, WorldClock)>.Ok((world, new WorldClock(systems, maxIterationsPerTick)));
    }
}
