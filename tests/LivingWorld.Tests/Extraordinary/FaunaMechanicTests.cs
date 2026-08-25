using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class FaunaMechanicTests
{
    [Fact]
    public void Dominate_moves_animals_in_radius_toward_the_carrier_across_ticks()
    {
        var setup = WorldWithDominate(radius: 4, animalAt: new CellCoord(2, 2), outsiderAt: new CellCoord(9, 0));
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        new FaunaDominateSystem().Tick(setup.World, ctx);
        new FaunaDominateSystem().Tick(setup.World, ctx);

        Assert.Equal(
            (new CellCoord(4, 4), new CellCoord(9, 0)),
            (setup.World.FindAnimal(setup.Near.Id)!.Position, setup.World.FindAnimal(setup.Far.Id)!.Position));
    }

    [Fact]
    public void Dominate_is_deterministic_for_the_same_seed()
    {
        var first = WorldWithDominate(radius: 8, animalAt: new CellCoord(0, 0), outsiderAt: new CellCoord(9, 9));
        var second = WorldWithDominate(radius: 8, animalAt: new CellCoord(0, 0), outsiderAt: new CellCoord(9, 9));
        var firstCtx = new TickContext(first.World, first.World.Rng, first.World.Scheduler);
        var secondCtx = new TickContext(second.World, second.World.Rng, second.World.Scheduler);

        for (int i = 0; i < 3; i++)
        {
            new FaunaDominateSystem().Tick(first.World, firstCtx);
            new FaunaDominateSystem().Tick(second.World, secondCtx);
        }

        Assert.Equal(
            first.World.Fauna.Select(animal => (animal.Id, animal.Position)),
            second.World.Fauna.Select(animal => (animal.Id, animal.Position)));
    }

    [Fact]
    public void Infect_vector_marks_animals_in_contact_radius_without_touching_the_far_one()
    {
        var setup = WorldWithInfect("plague");
        new FaunaDominateSystem().Tick(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler));

        Assert.Equal(
            ("plague", "plague", (string?)null),
            (setup.World.FindAnimal(setup.Near.Id)!.VectorDisease,
                setup.World.FindAnimal(setup.Adjacent.Id)!.VectorDisease,
                setup.World.FindAnimal(setup.Far.Id)!.VectorDisease));
    }

    [Fact]
    public void Disabled_extraordinary_cannot_execute_fauna_effects_or_follow()
    {
        var setup = WorldWithDominate(radius: 8, animalAt: new CellCoord(0, 0), outsiderAt: new CellCoord(9, 9),
            enabled: false);
        var origin = setup.Near.Position;
        var invoked = ExtraordinaryInvocationEngine.Invoke(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler),
            new ExtraordinaryInvocation(402, setup.Carrier.Id, "test-power", setup.Carrier.Id));
        new FaunaDominateSystem().Tick(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler));

        Assert.False(invoked.IsSuccess);
        Assert.Contains("Enabled", invoked.Error, StringComparison.Ordinal);
        Assert.Equal(origin, setup.World.FindAnimal(setup.Near.Id)!.Position);
    }

    [Fact]
    public void Default_registry_resolves_the_fauna_prefix()
    {
        Assert.IsType<FaunaMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("fauna.dominate:3"));
        Assert.IsType<FaunaMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("fauna.infect-vector:plague"));
    }

    private static FaunaWorld WorldWithDominate(
        int radius, CellCoord animalAt, CellCoord outsiderAt, bool enabled = true)
    {
        var near = new Animal(new AnimalId(1), "wolf", animalAt, true);
        var far = new Animal(new AnimalId(2), "deer", outsiderAt, true);
        var (world, carrier) = World(
            [$"fauna.dominate:{radius}"], [near, far], enabled);
        return new FaunaWorld(world, carrier, near, far, far);
    }

    private static FaunaWorld WorldWithInfect(string disease)
    {
        var near = new Animal(new AnimalId(1), "rat", new CellCoord(5, 5), true);
        var adjacent = new Animal(new AnimalId(2), "rat", new CellCoord(6, 5), true);
        var far = new Animal(new AnimalId(3), "rat", new CellCoord(0, 0), true);
        var (world, carrier) = World([$"fauna.infect-vector:{disease}"], [near, adjacent, far]);
        return new FaunaWorld(world, carrier, near, far, adjacent);
    }

    private static (WorldState World, Npc Carrier) World(
        IReadOnlyList<string> effects, IReadOnlyList<Animal> animals, bool enabled = true)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, [descriptor]),
            extraordinaryCarriers: [state],
            fauna: animals);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(5, 5), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(5, 5));
        world.AddNpc(carrier);
        return (world, carrier);
    }

    private sealed record FaunaWorld(
        WorldState World, Npc Carrier, Animal Near, Animal Far, Animal Adjacent);
}
