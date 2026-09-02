using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>REALISM-19: reprodução para no teto — degrada, não trava o tick.</summary>
public sealed class EcologyPopulationCapTests
{
    [Fact]
    public void Fauna_reproduction_stops_at_max_alive_without_hanging()
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 0, ReproduceEnergyThreshold: 0, ReproduceRadius: 99,
            ReproduceProbability: 1.0, PredatorOf: null, PredationProbability: 0);
        var energy = LazyNeed.Initial(100, 0, 0);
        var fauna = Enumerable.Range(1, FaunaLifecycleSystem.MaxAliveFauna)
            .Select(i => new Animal(
                new AnimalId(i), "rabbit", new CellCoord(i % 8, i / 8), true, null, energy))
            .ToList();
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: fauna,
            animalSpeciesRules: [rules],
            nextAnimalId: FaunaLifecycleSystem.MaxAliveFauna + 1);

        new WorldClock([new FaunaLifecycleSystem()]).Tick(world);

        Assert.Equal(FaunaLifecycleSystem.MaxAliveFauna, world.Fauna.Count(a => a.IsAlive));
        Assert.True(world.Fauna.Count <= FaunaLifecycleSystem.MaxAliveFauna);
    }
}
