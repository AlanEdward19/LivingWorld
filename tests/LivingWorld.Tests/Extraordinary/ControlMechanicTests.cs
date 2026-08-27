using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ControlMechanicTests
{
    private static readonly Personality CarrierPersonality =
        Personality.Create(10, 20, 30, 40, 50, 60, 70, 80, 90, 100).Value!;
    private static readonly Personality TargetPersonality =
        Personality.Create(100, 90, 80, 70, 60, 50, 40, 30, 20, 10).Value!;

    [Fact]
    public void Possessed_npc_executes_carrier_declared_sequence_and_log_attributes_actions_to_possessed()
    {
        var (world, carrier, target) = WorldWithPower(
            ["control.possess:Sleep"], carrierAction: ActionType.Work, targetAction: ActionType.Idle);
        var sink = new RecordingSink();

        var invoked = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(401, carrier.Id, "test-power", target.Id));
        Assert.True(invoked.IsSuccess, invoked.Error);

        SimulationWakeTestHelper.WakeAllAlive(world);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal(ActionType.Sleep, target.CurrentAction);
        Assert.Equal(ActionType.Work, carrier.CurrentAction);
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
            && evt.Payload == $"{target.Id.Value}|possessed-action|{ActionType.Sleep}");
        Assert.DoesNotContain(sink.Events, evt =>
            evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
            && evt.Payload == $"{carrier.Id.Value}|possessed-action|{ActionType.Sleep}");
        Assert.DoesNotContain(sink.Events, evt => evt.Kind == WorldEventKind.IdentityChanged);
    }

    [Fact]
    public void After_possess_ceases_possessed_decides_via_normal_behavior_decision_system()
    {
        var (world, carrier, target) = WorldWithPower(
            ["control.possess:Sleep"], carrierAction: ActionType.Work, targetAction: ActionType.Idle,
            condition: "carrier:action:Work");

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(402, carrier.Id, "test-power", target.Id));
        SimulationWakeTestHelper.WakeAllAlive(world);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        Assert.Equal(ActionType.Sleep, target.CurrentAction);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        Assert.Null(TargetState(world, target).PossessedBy);

        SimulationWakeTestHelper.WakeAllAlive(world);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
        Assert.Equal(ActionType.Work, target.CurrentAction);
    }

    [Fact]
    public void Body_swap_exchanges_personality_and_observable_identity_not_location_or_household()
    {
        var (world, carrier, target) = WorldWithPower(["control.body-swap"]);
        var sink = new RecordingSink();
        var carrierHome = carrier.Household;
        var targetHome = target.Household;
        var carrierAt = carrier.CurrentLocation;
        var targetAt = target.CurrentLocation;

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(403, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(TargetPersonality, carrier.Personality);
        Assert.Equal(CarrierPersonality, target.Personality);
        Assert.Equal((carrierHome, targetHome), (carrier.Household, target.Household));
        Assert.Equal((carrierAt, targetAt), (carrier.CurrentLocation, target.CurrentLocation));
        Assert.Equal(target.Id, CarrierState(world, carrier).ImpersonatingId);
        Assert.Equal(carrier.Id, TargetState(world, target).ImpersonatingId);
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.IdentityChanged
            && evt.Payload == $"{carrier.Id.Value}|{target.Id.Value}|body-swap");
    }

    [Fact]
    public void Body_swap_is_reversible_when_manifestation_ceases()
    {
        var (world, carrier, target) = WorldWithPower(
            ["control.body-swap"], condition: "carrier:action:Work");
        carrier.SetCurrentAction(ActionType.Work, 0);

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(404, carrier.Id, "test-power", target.Id));
        Assert.Equal(TargetPersonality, carrier.Personality);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(CarrierPersonality, carrier.Personality);
        Assert.Equal(TargetPersonality, target.Personality);
        Assert.Null(CarrierState(world, carrier).BodySwapPartner);
        Assert.Null(CarrierState(world, carrier).ImpersonatingId);
        Assert.Null(TargetState(world, target).BodySwapPartner);
        Assert.Null(TargetState(world, target).ImpersonatingId);
    }

    [Fact]
    public void Impersonate_is_cosmetic_and_does_not_change_NpcId()
    {
        var (world, carrier, target) = WorldWithPower(["appearance.impersonate:2"]);
        var originalId = carrier.Id;
        var sink = new RecordingSink();

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(405, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(originalId, carrier.Id);
        Assert.Equal(target.Id, CarrierState(world, carrier).ImpersonatingId);
        Assert.NotEqual(carrier.Id, target.Id);
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.IdentityChanged
            && evt.Payload == $"{carrier.Id.Value}|{target.Id.Value}|impersonate");
    }

    [Fact]
    public void Impersonate_identity_overlay_clears_without_residue_when_manifestation_ceases()
    {
        var (world, carrier, target) = WorldWithPower(
            ["appearance.impersonate:2"], condition: "carrier:action:Work");
        carrier.SetCurrentAction(ActionType.Work, 0);

        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(406, carrier.Id, "test-power", target.Id));
        Assert.Equal(target.Id, CarrierState(world, carrier).ImpersonatingId);

        carrier.SetCurrentAction(ActionType.Idle, 0);
        new ExtraordinaryStateSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(carrier.Id, carrier.Id);
        Assert.Null(CarrierState(world, carrier).ImpersonatingId);
    }

    [Fact]
    public void Body_swap_and_impersonate_log_IdentityChanged_not_per_possessed_action()
    {
        var (world, carrier, target) = WorldWithPower(
            ["control.possess:Sleep"], targetAction: ActionType.Work);
        var sink = new RecordingSink();
        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(407, carrier.Id, "test-power", target.Id));
        SimulationWakeTestHelper.WakeAllAlive(world);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal(0, sink.Events.Count(evt => evt.Kind == WorldEventKind.IdentityChanged));
        Assert.Equal(1, sink.Events.Count(evt =>
            evt.Kind == WorldEventKind.ExtraordinaryEffectApplied
            && evt.Payload == $"{target.Id.Value}|possessed-action|{ActionType.Sleep}"));
    }

    [Fact]
    public void Control_and_impersonate_are_unreachable_when_extraordinary_is_disabled()
    {
        var possess = WorldWithPower(["control.possess:Sleep"], enabled: false);
        var swap = WorldWithPower(["control.body-swap"], enabled: false);
        var mask = WorldWithPower(["appearance.impersonate:2"], enabled: false);

        Assert.Equal("Extraordinary.Enabled: false", Invoke(possess).Error);
        Assert.Equal("Extraordinary.Enabled: false", Invoke(swap).Error);
        Assert.Equal("Extraordinary.Enabled: false", Invoke(mask).Error);
        Assert.Equal(ActionType.Idle, possess.Target.CurrentAction);
        Assert.Equal(CarrierPersonality, swap.Carrier.Personality);
        Assert.Null(CarrierState(mask.World, mask.Carrier).ImpersonatingId);
    }

    [Fact]
    public void Default_registry_resolves_control_and_appearance_prefixes()
    {
        Assert.IsType<ControlMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("control.possess:Sleep"));
        Assert.IsType<ControlMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("control.body-swap"));
        Assert.IsType<AppearanceMechanic>(
            ExtraordinaryMechanicRegistry.Default.Resolve("appearance.impersonate:2"));
    }

    private static Result<ExtraordinaryInvocationResult> Invoke(
        (WorldState World, Npc Carrier, Npc Target) scene) =>
        ExtraordinaryInvocationEngine.Invoke(
            scene.World, new TickContext(scene.World, scene.World.Rng, scene.World.Scheduler),
            new ExtraordinaryInvocation(499, scene.Carrier.Id, "test-power", scene.Target.Id));

    private static ExtraordinaryCarrierState CarrierState(WorldState world, Npc npc) =>
        world.ExtraordinaryCarriers.First(item => item.CarrierId == npc.Id);

    private static ExtraordinaryCarrierState TargetState(WorldState world, Npc npc) =>
        world.ExtraordinaryCarriers.First(item => item.CarrierId == npc.Id);

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPower(
        IReadOnlyList<string> effects,
        ActionType carrierAction = ActionType.Work,
        ActionType targetAction = ActionType.Idle,
        string? condition = null,
        bool enabled = true)
    {
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            maxDurationHours: new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2,
                [ActionType.Sleep] = 8,
                [ActionType.Work] = 8,
                [ActionType.Socialize] = 3,
                [ActionType.Travel] = 4,
                [ActionType.Idle] = 2,
                [ActionType.Buy] = 2,
                [ActionType.UsePower] = 1,
            },
            routineSlots:
            [
                new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23,
                    Action: ActionType.Work),
            ],
            defaultAction: ActionType.Idle).Value!;
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], [], ManifestationCondition: condition);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(enabled, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), "carrier", new CellCoord(0, 0), new HouseholdId(1),
            CarrierPersonality, carrierAction);
        var target = Npc(new NpcId(2), "target", new CellCoord(5, 0), new HouseholdId(2),
            TargetPersonality, targetAction);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc Npc(
        NpcId id, string name, CellCoord at, HouseholdId household, Personality personality, ActionType action) =>
        new(
            id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30),
            ScenarioRunner.DefaultCulture, at, motherId: null, fatherId: null,
            household: household, health: 100, personality: personality,
            profession: ProfessionType.None, currentLocation: at, currentAction: action);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
