using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class AreaTargetResolverTests
{
    [Fact]
    public void Area_radius_applies_the_effect_only_inside_the_current_carrier_position()
    {
        var (world, carrier, inside, home) = WorldWithPower(
            effects: ["area:radius:3", "npc.health:15"], costs: ["household.resource.9:2"]);
        var outside = Npc(new NpcId(3), "outside", 50);
        outside.MoveTo(new CellCoord(8, 8), 0);
        world.AddNpc(outside);
        inside.MoveTo(new CellCoord(2, 2), 0);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(80, carrier.Id, "test-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((65, 50, 3L), (inside.Health, outside.Health, home.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Area_radius_is_remeasured_from_the_carrier_after_a_move()
    {
        var (world, carrier, inside, home) = WorldWithPower(
            effects: ["area:radius:3", "npc.health:15"], costs: ["household.resource.9:1"]);
        var far = Npc(new NpcId(3), "far", 50);
        far.MoveTo(new CellCoord(8, 8), 0);
        world.AddNpc(far);
        inside.MoveTo(new CellCoord(2, 2), 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(83, carrier.Id, "test-power", carrier.Id));
        carrier.MoveTo(new CellCoord(8, 8), 1);
        ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(84, carrier.Id, "test-power", carrier.Id));

        Assert.Equal((65, 65, 3L), (inside.Health, far.Health, home.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Area_with_zero_living_targets_succeeds_without_applying_effects()
    {
        var (world, carrier, other, home) = WorldWithPower(
            effects: ["area:region:99", "npc.health:15"], costs: ["household.resource.9:2"]);
        other.MoveTo(carrier.CurrentLocation, 0);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(81, carrier.Id, "test-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((50, 50, 3L), (carrier.Health, other.Health, home.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Area_cost_is_paid_once_regardless_of_how_many_targets_are_hit()
    {
        var (world, carrier, a, home) = WorldWithPower(
            effects: ["area:radius:3", "npc.health:1"], costs: ["household.resource.9:2"]);
        var b = Npc(new NpcId(3), "b", 50);
        b.MoveTo(new CellCoord(1, 0), 0);
        world.AddNpc(b);
        a.MoveTo(new CellCoord(0, 1), 0);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(82, carrier.Id, "test-power", carrier.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3L, home.Stock[new ResourceType(9)]);
        Assert.Equal(1, result.Value!.CostsPaid);
        Assert.True(result.Value.EffectsApplied >= 2);
    }

    private static (WorldState World, Npc Carrier, Npc Other, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", costs, "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 50);
        var other = Npc(new NpcId(2), "other", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(other);
        world.AddHousehold(home);
        return (world, carrier, other, home);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
