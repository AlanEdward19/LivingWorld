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

/// <summary>REALISM-03 — reprodução por proximidade, energia e seed.</summary>
public sealed class FaunaLifecycleReproduceTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Eligible_pair_births_new_animal_nearby_same_species()
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 50, ReproduceRadius: 2,
            ReproduceProbability: 1.0, PredatorOf: null, PredationProbability: 0);
        var sink = new RecordingSink();
        var world = WorldWithPair(rules, seed: 7);
        var clock = new WorldClock([new FaunaLifecycleSystem()], sink: sink);

        clock.Tick(world);

        Assert.Equal(3, world.Fauna.Count(a => a.IsAlive));
        var child = world.Fauna.Single(a => a.Id.Value == 3);
        Assert.Equal("rabbit", child.Species);
        Assert.True(child.IsAlive);
        Assert.True(
            FaunaLifecycleSystem.Chebyshev(child.Position, new CellCoord(1, 1)) <= 1
            || FaunaLifecycleSystem.Chebyshev(child.Position, new CellCoord(2, 1)) <= 1);
        var birth = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Birth);
        Assert.Equal(FaunaLifecycleSystem.SystemName, birth.SourceSystem);
        Assert.Contains("3", birth.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_seed_produces_identical_births()
    {
        var first = RunBirths(seed: 99);
        var second = RunBirths(seed: 99);

        Assert.Equal(
            first.Select(a => (a.Id.Value, a.Species, a.Position, a.IsAlive)),
            second.Select(a => (a.Id.Value, a.Species, a.Position, a.IsAlive)));
    }

    [Fact]
    public void Different_seeds_can_diverge_on_birth_outcome()
    {
        // Probabilidade intermediária: seeds diferentes devem poder divergir (sensor de RNG).
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 50, ReproduceRadius: 2,
            ReproduceProbability: 0.5, PredatorOf: null, PredationProbability: 0);

        bool foundDivergence = false;
        for (ulong seed = 1; seed <= 40 && !foundDivergence; seed++)
        {
            var a = CountAliveAfter(rules, seed);
            var b = CountAliveAfter(rules, seed + 1000);
            if (a != b) foundDivergence = true;
        }

        Assert.True(foundDivergence, "reprodução deve depender da seed do mundo");
    }

    private static IReadOnlyList<Animal> RunBirths(ulong seed)
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 50, ReproduceRadius: 2,
            ReproduceProbability: 1.0, PredatorOf: null, PredationProbability: 0);
        var world = WorldWithPair(rules, seed);
        new WorldClock([new FaunaLifecycleSystem()]).Run(world, ticks: 3);
        return world.Fauna.OrderBy(a => a.Id.Value).ToList();
    }

    private static int CountAliveAfter(AnimalSpeciesRules rules, ulong seed)
    {
        var world = WorldWithPair(rules, seed);
        new WorldClock([new FaunaLifecycleSystem()]).Run(world, ticks: 5);
        return world.Fauna.Count(a => a.IsAlive);
    }

    private static WorldState WorldWithPair(AnimalSpeciesRules rules, ulong seed)
    {
        var energy = LazyNeed.Initial(100, 0, 0);
        var a = new Animal(new AnimalId(1), rules.Species, new CellCoord(1, 1), true, null, energy);
        var b = new Animal(new AnimalId(2), rules.Species, new CellCoord(2, 1), true, null, energy);
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [a, b],
            animalSpeciesRules: [rules],
            nextAnimalId: 3);
    }
}
