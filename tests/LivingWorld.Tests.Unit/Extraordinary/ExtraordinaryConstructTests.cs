using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryConstructTests
{
    [Fact]
    public void Invocation_creates_a_canonical_costed_construct_with_causal_events_and_no_economic_leak()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        var sink = new RecordingSink();
        var moneyBefore = (world.MoneyMinted, world.MoneyDestroyed);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, Context(world, sink), new ExtraordinaryInvocation(70, carrier.Id, "construct", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        var construct = Assert.Single(world.ExtraordinaryConstructs);
        Assert.Equal(
            (0L, carrier.Id, "construct", 70L, new CellCoord(1, 0), 40, 40, 0L, 24L, "green-energy"),
            (construct.Id, construct.CreatorId, construct.PowerId, construct.SourceInvocationId,
                construct.Origin, construct.Durability, construct.MaxDurability,
                construct.CreatedAtTick, construct.ExpiresAtTick, construct.AppearanceToken));
        Assert.Equal([new CellCoord(1, 0), new CellCoord(2, 0)], construct.Footprint);
        Assert.Equal(90, carrier.SleepAt(0));
        Assert.Equal(moneyBefore, (world.MoneyMinted, world.MoneyDestroyed));
        Assert.Contains(sink.Events, item => item.Kind == WorldEventKind.ExtraordinaryConstructCreated
            && item.Payload == "1|70|construct|0");

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        var restoredConstruct = Assert.Single(restored.ExtraordinaryConstructs);
        Assert.Equal(
            (construct.Id, construct.CreatorId, construct.PowerId, construct.SourceInvocationId,
                construct.Origin, construct.Durability, construct.ExpiresAtTick, construct.AppearanceToken),
            (restoredConstruct.Id, restoredConstruct.CreatorId, restoredConstruct.PowerId,
                restoredConstruct.SourceInvocationId, restoredConstruct.Origin, restoredConstruct.Durability,
                restoredConstruct.ExpiresAtTick, restoredConstruct.AppearanceToken));
        Assert.Equal(construct.Footprint, restoredConstruct.Footprint);
    }

    [Fact]
    public void Durability_and_expiration_remove_constructs_without_producing_resources()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        var household = AttachStockedHousehold(world, carrier);
        var sink = new RecordingSink();
        var ctx = Context(world, sink);
        ExtraordinaryInvocationEngine.Invoke(
            world, ctx, new ExtraordinaryInvocation(71, carrier.Id, "construct", target.Id));

        var damaged = ExtraordinaryConstructOperations.Damage(world, ctx, 0, 15);
        Assert.True(damaged.IsSuccess, damaged.Error);
        Assert.Equal(25, Assert.Single(world.ExtraordinaryConstructs).Durability);
        ExtraordinaryConstructOperations.Damage(world, ctx, 0, 25);
        Assert.Empty(world.ExtraordinaryConstructs);
        Assert.Equal(12, household.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Single(household.Stock);

        var (expiringWorld, expiringCarrier, expiringTarget) = WorldWithConstructPower();
        var expiringHousehold = AttachStockedHousehold(expiringWorld, expiringCarrier);
        ExtraordinaryInvocationEngine.Invoke(
            expiringWorld, Context(expiringWorld, sink),
            new ExtraordinaryInvocation(72, expiringCarrier.Id, "construct", expiringTarget.Id));
        new WorldClock([new ExtraordinaryStateSystem()], sink: sink).Run(expiringWorld, 24);

        Assert.Empty(expiringWorld.ExtraordinaryConstructs);
        Assert.Equal(12, expiringHousehold.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.Single(expiringHousehold.Stock);
        Assert.Contains(sink.Events, item => item.Kind == WorldEventKind.ExtraordinaryConstructRemoved
            && item.Payload.EndsWith("|expired", StringComparison.Ordinal));
    }

    [Fact]
    public void Construct_footprint_blocks_authoritative_ground_travel()
    {
        var (world, carrier, target) = WorldWithConstructPower(includeSpeed: true);
        ExtraordinaryInvocationEngine.Invoke(
            world, Context(world), new ExtraordinaryInvocation(73, carrier.Id, "construct", target.Id));
        var household = new Household(new HouseholdId(1), new CellCoord(3, 0), carrier.Id, [carrier.Id]);
        carrier.JoinHousehold(household.Id);
        world.AddHousehold(household);
        carrier.SetCurrentAction(ActionType.Travel, -1);
        SimulationWakeTestHelper.Wake(world, carrier);

        new BehaviorDecisionSystem().Tick(world, Context(world));

        Assert.Equal(new CellCoord(0, 0), carrier.CurrentLocation);
        Assert.Equal(ActionType.Travel, carrier.CurrentAction);
    }

    [Fact]
    public void Invalid_footprint_is_transactional_and_does_not_charge_the_carrier()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        target.MoveTo(new CellCoord(world.Map.Width - 1, 0), 0);
        int sleepBefore = carrier.SleepAt(0);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, Context(world), new ExtraordinaryInvocation(74, carrier.Id, "construct", target.Id));

        Assert.False(result.IsSuccess);
        Assert.Empty(world.ExtraordinaryConstructs);
        Assert.Equal(sleepBefore, carrier.SleepAt(0));
        Assert.Equal(0, world.NextExtraordinaryConstructId);
    }

    [Fact]
    public void Authored_target_cell_places_construct_at_the_exact_validated_origin()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        var chosen = new CellCoord(6, 7);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, Context(world),
            new ExtraordinaryInvocation(75, carrier.Id, "construct", target.Id, TargetCell: chosen));

        Assert.True(result.IsSuccess, result.Error);
        var construct = Assert.Single(world.ExtraordinaryConstructs);
        Assert.Equal(chosen, construct.Origin);
        Assert.Equal([chosen, new CellCoord(7, 7)], construct.Footprint);
    }

    [Fact]
    public void Authored_target_cell_rejects_a_building_footprint_without_charging_cost()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        var chosen = new CellCoord(6, 7);
        world.AddBuilding(new Building(
            new BuildingId(1), new CityId(Guid.Empty), buildingTypeId: -1,
            completedAtTick: 0, position: chosen, orientation: 0));
        int sleepBefore = carrier.SleepAt(0);

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, Context(world),
            new ExtraordinaryInvocation(76, carrier.Id, "construct", target.Id, TargetCell: chosen));

        Assert.False(result.IsSuccess);
        Assert.Empty(world.ExtraordinaryConstructs);
        Assert.Equal(sleepBefore, carrier.SleepAt(0));
    }

    [Fact]
    public void Authored_target_cell_rejects_a_living_npc_without_charging_cost()
    {
        var (world, carrier, target) = WorldWithConstructPower();
        int sleepBefore = carrier.SleepAt(0);

        var result = ExtraordinaryInvocationEngine.InvokeAuthored(
            world, Context(world), carrier.Id, "construct", target.Id,
            targetCell: carrier.CurrentLocation);

        Assert.False(result.IsSuccess);
        Assert.Empty(world.ExtraordinaryConstructs);
        Assert.Equal(sleepBefore, carrier.SleepAt(0));
        Assert.Equal(0, world.NextEventId);
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithConstructPower(bool includeSpeed = false)
    {
        var effects = includeSpeed
            ? new[] { "construct.create:2x1:40:24:green-energy", "movement.speed-multiplier:3" }
            : ["construct.create:2x1:40:24:green-energy"];
        var descriptor = new PowerDescriptor(
            "construct", "artifact", effects, "Active", ["carrier.sleep:10"],
            "Guaranteed", [], [], ["green-aura"], []);
        var carrierState = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "green", "green-energy"), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrierState]);
        var carrier = AddNpc(world, 1);
        var target = AddNpc(world, 2);
        return (world, carrier, target);
    }

    private static Npc AddNpc(WorldState world, long id)
    {
        var npc = new Npc(
            new NpcId(id), $"npc-{id}", Sex.Male,
            WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static Household AttachStockedHousehold(WorldState world, Npc carrier)
    {
        var household = new Household(new HouseholdId(1), carrier.CurrentLocation, carrier.Id, [carrier.Id]);
        household.Deposit(new ResourceType(1), 12);
        carrier.JoinHousehold(household.Id);
        world.AddHousehold(household);
        return household;
    }

    private static TickContext Context(WorldState world, IWorldEventSink? sink = null) =>
        new(world, world.Rng, world.Scheduler, sink);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
