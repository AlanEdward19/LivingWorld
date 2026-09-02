using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Ecology;

/// <summary>REALISM-01/02/06 — fauna consome energia e morre de fome sem poderes.</summary>
public sealed class FaunaLifecycleHungerTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    [Fact]
    public void Animal_energy_decays_per_species_hunger_rate_each_tick()
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 10, ReproduceEnergyThreshold: 50, ReproduceRadius: 2,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        // DecayRate 0 no animal — ApplyHunger aplica a taxa da espécie (REALISM-01).
        var (world, _) = WorldWithAnimal(
            "rabbit", LazyNeed.Initial(100, 0, 0), rules, enabledExtraordinary: false);
        var clock = new WorldClock([new FaunaLifecycleSystem()]);

        clock.Tick(world);
        Assert.Equal(100, world.Fauna.Single().Energy.ValueAt(world.CurrentDate.TotalHours));
        Assert.Equal(10, world.Fauna.Single().Energy.DecayRatePerTick);

        clock.Tick(world);
        Assert.Equal(90, world.Fauna.Single().Energy.ValueAt(world.CurrentDate.TotalHours));
        Assert.True(world.Fauna.Single().IsAlive);
    }

    [Fact]
    public void Energy_at_zero_kills_animal_and_logs_starvation_with_fauna_source()
    {
        var rules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 50, ReproduceEnergyThreshold: 50, ReproduceRadius: 2,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var sink = new RecordingSink();
        var (world, animalId) = WorldWithAnimal(
            "rabbit", LazyNeed.Initial(100, 0, rules.HungerDecayPerTick), rules, enabledExtraordinary: false);
        var clock = new WorldClock([new FaunaLifecycleSystem()], sink: sink);

        clock.Run(world, ticks: 3);

        Assert.False(world.FindAnimal(animalId)!.IsAlive);
        var death = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Starvation);
        Assert.Equal(animalId.Value.ToString(), death.Payload);
        Assert.Equal(FaunaLifecycleSystem.SystemName, death.SourceSystem);
    }

    [Fact]
    public void Hunger_and_starvation_run_when_extraordinary_is_disabled()
    {
        var rules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 100, ReproduceEnergyThreshold: 60, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var (world, animalId) = WorldWithAnimal(
            "wolf", LazyNeed.Initial(100, 0, rules.HungerDecayPerTick), rules, enabledExtraordinary: false);
        Assert.False(world.Extraordinary.Enabled);

        new WorldClock([new FaunaLifecycleSystem()]).Tick(world);

        Assert.False(world.FindAnimal(animalId)!.IsAlive);
        Assert.Equal(0, world.FindAnimal(animalId)!.Energy.ValueAt(world.CurrentDate.TotalHours));
    }

    private static (WorldState World, AnimalId Id) WorldWithAnimal(
        string species, LazyNeed energy, AnimalSpeciesRules rules, bool enabledExtraordinary)
    {
        var animal = new Animal(new AnimalId(1), species, new CellCoord(1, 1), true, null, energy);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabledExtraordinary, []),
            fauna: [animal],
            animalSpeciesRules: [rules]);
        return (world, animal.Id);
    }
}
