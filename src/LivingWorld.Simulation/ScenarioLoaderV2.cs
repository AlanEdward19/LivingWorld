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

        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, definition.Map.Seed, definition.Map,
            population.Catalog, population.Rules,
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

        foreach (var city in definition.City.Cities)
            world.AddCity(new City(
                world.NextCityId(), city.Location, city.FoundedAtTick, foundedFromCityId: null, city.AggregatePool));

        return Result<(WorldState, WorldClock)>.Ok((world, new WorldClock(ScenarioRunner.DefaultSystems(), maxIterationsPerTick)));
    }
}
