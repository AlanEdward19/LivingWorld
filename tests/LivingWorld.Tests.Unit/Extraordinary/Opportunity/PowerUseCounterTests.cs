using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class PowerUseCounterTests
{
    [Fact]
    public void Successful_invocation_increments_use_count_exactly_once()
    {
        var (world, carrier, target, _) = WorldWithPower(["npc.health:5"], []);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var first = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(1, carrier.Id, "test-power", target.Id));
        var second = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(2, carrier.Id, "test-power", target.Id));

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(2, CarrierUseCount(world));
    }

    [Fact]
    public void Validation_failure_never_increments_use_count()
    {
        var (world, carrier, target, _) = WorldWithPower(
            ["npc.health:5"], ["household.resource.9:99"]);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(3, carrier.Id, "test-power", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, CarrierUseCount(world));
    }

    [Fact]
    public void Resolution_failure_still_pays_cost_but_never_increments_use_count()
    {
        var (world, carrier, target, _) = WorldWithPower(
            ["npc.health:5"], ["household.resource.9:1"], reliability: "ResolutionCheck");

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(
                4, carrier.Id, "test-power", target.Id, ResolutionResult.Failure));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, CarrierUseCount(world));
    }

    [Fact]
    public void Partial_success_increments_use_count_once()
    {
        var (world, carrier, target, _) = WorldWithPower(
            ["npc.health:5"], [], reliability: "ResolutionCheck");

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(
                5, carrier.Id, "test-power", target.Id, ResolutionResult.PartialSuccess));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, CarrierUseCount(world));
    }

    private static int CarrierUseCount(WorldState world) =>
        world.ExtraordinaryCarriers.Single(carrier => carrier.CarrierId == new NpcId(1)).UseCount;

    private static (WorldState World, Npc Carrier, Npc Target, Household Home) WorldWithPower(
        IReadOnlyList<string> effects, IReadOnlyList<string> costs, string reliability = "Guaranteed")
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", costs, reliability, [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", 100);
        var target = Npc(new NpcId(2), "target", 50);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        world.AddNpc(carrier);
        world.AddNpc(target);
        world.AddHousehold(home);
        return (world, carrier, target, home);
    }

    private static Npc Npc(NpcId id, string name, int health) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
