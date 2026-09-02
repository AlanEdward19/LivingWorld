using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>REALISM-23 — transições inválidas de fauna/flora não passam.</summary>
public sealed class EcologyTransitionGuardTests
{
    [Fact]
    public void Dead_animals_do_not_reproduce()
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 0, ReproduceRadius: 5,
            ReproduceProbability: 1, PredatorOf: null, PredationProbability: 0);
        var a = new Animal(
            new AnimalId(1), "rabbit", new CellCoord(1, 1), false, null,
            LazyNeed.Initial(100, 0, 0), DeathTick: 0);
        var b = new Animal(
            new AnimalId(2), "rabbit", new CellCoord(1, 2), false, null,
            LazyNeed.Initial(100, 0, 0), DeathTick: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 9, ScenarioRunner.DefaultMap(9),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [a, b],
            animalSpeciesRules: [rules],
            nextAnimalId: 3);

        new WorldClock([new FaunaLifecycleSystem()]).Tick(world);

        Assert.Equal(2, world.Fauna.Count);
        Assert.DoesNotContain(world.Fauna, animal => animal.Id.Value >= 3);
        Assert.All(world.Fauna, animal => Assert.False(animal.IsAlive));
    }

    [Fact]
    public void Dead_predator_does_not_predate()
    {
        var wolfRules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 99, ReproduceRadius: 5,
            ReproduceProbability: 0, PredatorOf: "rabbit", PredationProbability: 1);
        var rabbitRules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 99, ReproduceRadius: 5,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var deadWolf = new Animal(
            new AnimalId(1), "wolf", new CellCoord(2, 2), false, null,
            LazyNeed.Initial(100, 0, 0), DeathTick: 0);
        var rabbit = new Animal(
            new AnimalId(2), "rabbit", new CellCoord(2, 3), true, null,
            LazyNeed.Initial(100, 0, 0));
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 13, ScenarioRunner.DefaultMap(13),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [deadWolf, rabbit],
            animalSpeciesRules: [wolfRules, rabbitRules]);

        new WorldClock([new FaunaLifecycleSystem()]).Tick(world);

        Assert.True(world.FindAnimal(rabbit.Id)!.IsAlive);
        Assert.False(world.FindAnimal(deadWolf.Id)!.IsAlive);
    }

    [Fact]
    public void Dead_plant_leaves_non_negative_cold_stage_and_does_not_reappear()
    {
        var rules = new PlantSpeciesRules(
            "wheat", MinToleratedTemp: 50, MaxToleratedTemp: 60, MaturityStage: 5,
            CropResourceId: 1, YieldPerMaturePlant: 1, ReproduceRadius: 1, ReproduceProbability: 0);
        var plant = new Plant(new PlantId(1), "wheat", new CellCoord(1, 1), GrowthStage: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 17, ScenarioRunner.DefaultMap(17),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            flora: [plant],
            plantSpeciesRules: [rules],
            nextPlantId: 2);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);

        Assert.Null(world.FindPlant(new PlantId(1)));
        var archived = world.ColdArchive.LookupPlant(1);
        Assert.NotNull(archived);
        Assert.True(archived.GrowthStage >= 0);
        Assert.Empty(world.Flora);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);
        Assert.Empty(world.Flora);
        Assert.Null(world.FindPlant(new PlantId(1)));
    }
}
