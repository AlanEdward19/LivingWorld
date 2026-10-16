using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class DimensionMechanicTests
{
    [Fact]
    public void Default_registry_resolves_the_dimension_prefix()
    {
        Assert.IsType<DimensionMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("dimension.pocket-store"));
        Assert.IsType<DimensionMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("dimension.portal:1:1:2:2"));
    }

    [Fact]
    public void Pocket_store_removes_household_stock_without_Destroyed_and_keeps_the_item_in_the_pocket()
    {
        var (world, carrier, target, home) = WorldWithPower(["dimension.pocket-store"]);
        var sink = new RecordingSink();
        long before = home.Stock[new ResourceType(1)];

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(401, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        var pocket = Assert.Single(
            world.ExtraordinaryCarriers.Single(item => item.CarrierId == carrier.Id).DimensionalPocket!);
        Assert.Equal(before - 1, home.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Equal((1, 1L), (pocket.ResourceId, pocket.Quantity));
        Assert.DoesNotContain(sink.Events, evt => evt.Kind == WorldEventKind.Destroyed);
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.ExtraordinaryEffectApplied);
    }

    [Fact]
    public void Pocket_store_retrieves_the_item_back_into_household_stock_on_the_next_invoke()
    {
        var (world, carrier, target, home) = WorldWithPower(["dimension.pocket-store"]);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(402, carrier.Id, "test-power", target.Id)).IsSuccess);
        long afterStore = home.Stock.GetValueOrDefault(new ResourceType(1));

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(403, carrier.Id, "test-power", target.Id));

        var carrierState = world.ExtraordinaryCarriers.Single(item => item.CarrierId == carrier.Id);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(afterStore + 1, home.Stock[new ResourceType(1)]);
        Assert.Empty(carrierState.DimensionalPocket ?? []);
    }

    [Fact]
    public void Npc_entering_cellA_appears_in_cellB_on_the_same_tick()
    {
        var cellA = new CellCoord(2, 2);
        var cellB = new CellCoord(4, 4);
        var (world, carrier, walker, _) = WorldWithPower([$"dimension.portal:{cellA.X}:{cellA.Y}:{cellB.X}:{cellB.Y}"]);
        walker.MoveTo(new CellCoord(1, 1), 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var invoked = ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(410, carrier.Id, "test-power", walker.Id));
        walker.MoveTo(cellA, ctx.CurrentTick);
        new DimensionPortalSystem().Tick(world, ctx);

        Assert.True(invoked.IsSuccess, invoked.Error);
        Assert.Equal(cellB, walker.CurrentLocation);
    }

    [Fact]
    public void Portal_is_bidirectional_on_the_same_tick()
    {
        var cellA = new CellCoord(2, 2);
        var cellB = new CellCoord(4, 4);
        var (world, carrier, walker, _) = WorldWithPower([$"dimension.portal:{cellA.X}:{cellA.Y}:{cellB.X}:{cellB.Y}"]);
        walker.MoveTo(new CellCoord(1, 1), 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(411, carrier.Id, "test-power", walker.Id)).IsSuccess);

        walker.MoveTo(cellB, ctx.CurrentTick);
        new DimensionPortalSystem().Tick(world, ctx);

        Assert.Equal(cellA, walker.CurrentLocation);
    }

    [Fact]
    public void After_the_creating_power_ceases_the_cell_no_longer_teleports()
    {
        var cellA = new CellCoord(2, 2);
        var cellB = new CellCoord(4, 4);
        var (world, carrier, walker, _) = WorldWithPower([$"dimension.portal:{cellA.X}:{cellA.Y}:{cellB.X}:{cellB.Y}"]);
        walker.MoveTo(new CellCoord(1, 1), 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        Assert.True(ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(412, carrier.Id, "test-power", walker.Id)).IsSuccess);

        var revoked = ExtraordinaryStateSystem.RevokeAuthored(world, ctx, carrier.Id, "test-power");
        walker.MoveTo(cellA, ctx.CurrentTick);
        new DimensionPortalSystem().Tick(world, ctx);

        Assert.True(revoked.IsSuccess, revoked.Error);
        Assert.Equal(cellA, walker.CurrentLocation);
        Assert.Null(world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == carrier.Id));
    }

    private static (WorldState World, Npc Carrier, Npc Other, Household Home) WorldWithPower(
        IReadOnlyList<string> effects)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
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
        var carrier = Npc(new NpcId(1), "carrier", 100, new CellCoord(0, 0));
        var other = Npc(new NpcId(2), "other", 50, new CellCoord(0, 1));
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), carrier.Id, [carrier.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 10 });
        world.AddNpc(carrier);
        world.AddNpc(other);
        world.AddHousehold(home);
        return (world, carrier, other, home);
    }

    private static Npc Npc(NpcId id, string name, int health, CellCoord location) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, location, motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: location);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
