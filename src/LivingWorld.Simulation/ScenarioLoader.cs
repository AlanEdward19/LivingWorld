using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Monta um mundo completo a partir de um único JSON de cenário (task 7): geografia
/// (<see cref="MapScenarioLoader"/>) e população (<see cref="PopulationScenarioLoader"/>) lidos
/// do mesmo arquivo — "cenário como dado desde o primeiro NPC". Sistemas são sempre os
/// <see cref="ScenarioRunner.DefaultSystems"/>; o que muda entre cenários é só o dado.</summary>
public static class ScenarioLoader
{
    public static Result<(WorldState World, WorldClock Clock)> LoadWorld(string json, int maxIterationsPerTick = 1000)
    {
        var mapResult = MapScenarioLoader.Load(json);
        if (!mapResult.IsSuccess)
            return Result<(WorldState, WorldClock)>.Fail(mapResult.Error!);

        var populationResult = PopulationScenarioLoader.Load(json);
        if (!populationResult.IsSuccess)
            return Result<(WorldState, WorldClock)>.Fail(populationResult.Error!);

        var population = populationResult.Value!;
        // ponytail: NeedsRules/ActionCatalog do cenário JSON ainda não plugados aqui (BehaviorScenarioLoader
        // existe mas a integração com ScenarioLoader é escopo de fase futura) — usa os defaults por ora.
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, mapResult.Value!.Seed, mapResult.Value!,
            population.Catalog, population.Rules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog);

        if (population.InitialPopulation > 0)
            PopulationSeeder.SeedInitial(world, population.InitialPopulation, population.Culture, population.Village);

        return Result<(WorldState, WorldClock)>.Ok((world, new WorldClock(ScenarioRunner.DefaultSystems(), maxIterationsPerTick)));
    }
}
