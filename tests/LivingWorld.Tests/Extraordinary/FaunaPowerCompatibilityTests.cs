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
}
