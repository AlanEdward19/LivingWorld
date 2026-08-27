using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>REALISM-05 — fauna.dominate/infect-vector modulam; ciclo base continua.</summary>
public sealed class FaunaPowerCompatibilityTests
{
    [Fact]
    public void Dominate_active_does_not_stop_base_hunger_decay()
    {
        var rules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 10, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var animal = new Animal(
            new AnimalId(1), "wolf", new CellCoord(2, 2), true, null, LazyNeed.Initial(100, 0, 0));
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", ["fauna.dominate:8"], "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state],
            fauna: [animal],
            animalSpeciesRules: [rules]);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(5, 5), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(5, 5));
        world.AddNpc(carrier);

        var clock = new WorldClock([new FaunaLifecycleSystem(), new FaunaDominateSystem()]);
        clock.Tick(world);
        clock.Tick(world);

        var after = world.FindAnimal(animal.Id)!;
        Assert.True(after.IsAlive);
        Assert.Equal(10, after.Energy.DecayRatePerTick);
        Assert.Equal(90, after.Energy.ValueAt(world.CurrentDate.TotalHours));
        Assert.NotEqual(new CellCoord(2, 2), after.Position);
    }

    [Fact]
    public void Infect_vector_active_does_not_stop_base_hunger_reproduce_or_predation()
    {
        var deerRules = new AnimalSpeciesRules(
            "deer", HungerDecayPerTick: 10, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var wolfRules = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 1, ReproduceEnergyThreshold: 50, ReproduceRadius: 3,
            ReproduceProbability: 1, PredatorOf: "rabbit", PredationProbability: 1);
        var rabbitRules = new AnimalSpeciesRules(
            "rabbit", HungerDecayPerTick: 1, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0);
        var deer = new Animal(
            new AnimalId(1), "deer", new CellCoord(5, 5), true, null, LazyNeed.Initial(100, 0, 0));
        var wolfA = new Animal(
            new AnimalId(2), "wolf", new CellCoord(2, 2), true, null, LazyNeed.Initial(100, 0, 0));
        var wolfB = new Animal(
            new AnimalId(3), "wolf", new CellCoord(2, 3), true, null, LazyNeed.Initial(100, 0, 0));
        var rabbit = new Animal(
            new AnimalId(4), "rabbit", new CellCoord(3, 2), true, null, LazyNeed.Initial(100, 0, 0));
        var descriptor = new PowerDescriptor(
            "infect-power", "test-source", ["fauna.infect-vector:plague"], "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 7, ScenarioRunner.DefaultMap(7),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state],
            fauna: [deer, wolfA, wolfB, rabbit],
            animalSpeciesRules: [deerRules, wolfRules, rabbitRules],
            nextAnimalId: 5);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(5, 5), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(5, 5));
        world.AddNpc(carrier);

        var clock = new WorldClock([new FaunaLifecycleSystem(), new FaunaDominateSystem()]);
        clock.Tick(world);
        clock.Tick(world);

        long tick = world.CurrentDate.TotalHours;
        var infectedDeer = world.FindAnimal(deer.Id)!;
        Assert.Equal("plague", infectedDeer.VectorDisease);
        Assert.Equal(10, infectedDeer.Energy.DecayRatePerTick);
        Assert.Equal(90, infectedDeer.Energy.ValueAt(tick));
        Assert.Contains(world.Fauna, a => a.Species == "wolf" && a.IsAlive && a.Id.Value >= 5);
        Assert.False(world.FindAnimal(rabbit.Id)!.IsAlive);
    }
}
