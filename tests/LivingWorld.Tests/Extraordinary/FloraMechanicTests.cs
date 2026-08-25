using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class FloraMechanicTests
{
    [Fact]
    public void Growth_rate_five_advances_plants_in_area_five_times_versus_control()
    {
        var setup = WorldWithGrowth(multiplier: 5, radius: 1);
        var ctx = new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler);

        new FloraGrowthSystem().Tick(setup.World, ctx);
        new FloraGrowthSystem().Tick(setup.World, ctx);

        Assert.Equal(
            (10, 2),
            (setup.World.FindPlant(setup.Treated.Id)!.GrowthStage,
                setup.World.FindPlant(setup.Control.Id)!.GrowthStage));
    }

    [Fact]
    public void Growth_rate_is_deterministic_for_the_same_seed()
    {
        var first = WorldWithGrowth(multiplier: 5, radius: 1);
        var second = WorldWithGrowth(multiplier: 5, radius: 1);

        for (int i = 0; i < 3; i++)
        {
            new FloraGrowthSystem().Tick(
                first.World, new TickContext(first.World, first.World.Rng, first.World.Scheduler));
            new FloraGrowthSystem().Tick(
                second.World, new TickContext(second.World, second.World.Rng, second.World.Scheduler));
        }

        Assert.Equal(
            first.World.Flora.Select(plant => (plant.Id, plant.GrowthStage)),
            second.World.Flora.Select(plant => (plant.Id, plant.GrowthStage)));
    }

    [Fact]
    public void Disabled_extraordinary_cannot_execute_flora_effects_or_grow()
    {
        var setup = WorldWithGrowth(multiplier: 5, radius: 1, enabled: false);
        var invoked = ExtraordinaryInvocationEngine.Invoke(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler),
            new ExtraordinaryInvocation(501, setup.Carrier.Id, "test-power", setup.Carrier.Id));
        new FloraGrowthSystem().Tick(
            setup.World, new TickContext(setup.World, setup.World.Rng, setup.World.Scheduler));

        Assert.False(invoked.IsSuccess);
        Assert.Contains("Enabled", invoked.Error, StringComparison.Ordinal);
        Assert.Equal(
            (0, 0),
            (setup.World.FindPlant(setup.Treated.Id)!.GrowthStage,
                setup.World.FindPlant(setup.Control.Id)!.GrowthStage));
    }

    [Fact]
    public void Default_registry_resolves_the_flora_prefix()
    {
        Assert.IsType<FloraMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("flora.growth-rate:5"));
    }

    private static FloraWorld WorldWithGrowth(int multiplier, int radius, bool enabled = true)
    {
        var treated = new Plant(new PlantId(1), "oak", new CellCoord(5, 5), 0);
        var control = new Plant(new PlantId(2), "oak", new CellCoord(9, 9), 0);
        var descriptor = new PowerDescriptor(
            "test-power", "test-source",
            [$"area:radius:{radius}", $"flora.growth-rate:{multiplier}"],
            "Active", [], "Guaranteed", [], [], [], []);
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
            flora: [treated, control]);
        var carrier = new Npc(
            new NpcId(1), "carrier", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(5, 5), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(5, 5));
        world.AddNpc(carrier);
        return new FloraWorld(world, carrier, treated, control);
    }

    private sealed record FloraWorld(WorldState World, Npc Carrier, Plant Treated, Plant Control);
}
