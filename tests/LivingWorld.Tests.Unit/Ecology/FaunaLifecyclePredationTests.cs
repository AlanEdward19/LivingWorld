using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>REALISM-04/05 — predação + poderes de fauna não substituem o ciclo base.</summary>
public sealed class FaunaLifecyclePredationTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Predator_consumes_prey_in_radius_gaining_energy_deterministically()
    {
        var wolfRules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: "rabbit", PredationProbability: 1.0);
        var rabbitRules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 99, ReproduceRadius: 2,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);

        var sink = new RecordingSink();
        var world = WorldWithPredatorPrey(wolfRules, rabbitRules, seed: 11);
        double energyBefore = world.FindAnimal(new AnimalId(1))!.Energy.ValueAt(0);
        var clock = new WorldClock([new FaunaLifecycleSystem()], sink: sink);

        clock.Tick(world);

        Assert.False(world.FindAnimal(new AnimalId(2))!.IsAlive);
        Assert.True(world.FindAnimal(new AnimalId(1))!.IsAlive);
        Assert.True(world.FindAnimal(new AnimalId(1))!.Energy.ValueAt(world.CurrentDate.TotalHours)
            > energyBefore);
        var death = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Death);
        Assert.Equal("2", death.Payload);
        Assert.Equal(FaunaLifecycleSystem.SystemName, death.SourceSystem);
    }

    [Fact]
    public void Species_without_PredatorOf_is_a_noop_without_error()
    {
        var rabbitRules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 99, ReproduceRadius: 2,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 1.0);
        var energy = LazyNeed.Initial(100, 0, 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 3, ScenarioRunner.DefaultMap(3),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna:
            [
                new Animal(new AnimalId(1), "rabbit", new CellCoord(1, 1), true, null, energy),
                new Animal(new AnimalId(2), "rabbit", new CellCoord(1, 2), true, null, energy),
            ],
            animalSpeciesRules: [rabbitRules]);

        new WorldClock([new FaunaLifecycleSystem()]).Tick(world);

        Assert.All(world.Fauna, a => Assert.True(a.IsAlive));
    }

    [Fact]
    public void Independent_two_species_zero_powers_population_changes_over_ticks()
    {
        var wolfRules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 0.2, ReproduceEnergyThreshold: 70, ReproduceRadius: 4,
            ReproduceProbability: 0.15, PredatorOf: "rabbit", PredationProbability: 0.35);
        var rabbitRules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0.15, ReproduceEnergyThreshold: 50, ReproduceRadius: 3,
            ReproduceProbability: 0.4, PredatorOf: null, PredationProbability: 0);

        var energy = LazyNeed.Initial(100, 0, 0);
        var fauna = new List<Animal>
        {
            new(new AnimalId(1), "wolf", new CellCoord(2, 2), true, null, energy),
            new(new AnimalId(2), "wolf", new CellCoord(3, 2), true, null, energy),
            new(new AnimalId(3), "rabbit", new CellCoord(2, 3), true, null, energy),
            new(new AnimalId(4), "rabbit", new CellCoord(3, 3), true, null, energy),
            new(new AnimalId(5), "rabbit", new CellCoord(4, 3), true, null, energy),
            new(new AnimalId(6), "rabbit", new CellCoord(2, 4), true, null, energy),
        };
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: fauna,
            animalSpeciesRules: [wolfRules, rabbitRules],
            nextAnimalId: 7);
        Assert.False(world.Extraordinary.Enabled);
        int startAlive = world.Fauna.Count(a => a.IsAlive);

        new WorldClock([new FaunaLifecycleSystem()]).Run(world, ticks: 80);

        int endAlive = world.Fauna.Count(a => a.IsAlive);
        int births = world.Fauna.Count(a => a.Id.Value >= 7);
        int deaths = world.Fauna.Count(a => !a.IsAlive);
        Assert.True(births > 0 || deaths > 0 || endAlive != startAlive,
            "população deve variar (nascimento, morte ou predação) sem poderes");
    }

    private static WorldState WorldWithPredatorPrey(
        AnimalSpeciesRules wolf, AnimalSpeciesRules rabbit, ulong seed)
    {
        var energy = LazyNeed.Initial(60, 0, 0);
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna:
            [
                new Animal(new AnimalId(1), "wolf", new CellCoord(2, 2), true, null, energy),
                new Animal(new AnimalId(2), "rabbit", new CellCoord(2, 3), true, null, energy),
            ],
            animalSpeciesRules: [wolf, rabbit]);
    }
}
