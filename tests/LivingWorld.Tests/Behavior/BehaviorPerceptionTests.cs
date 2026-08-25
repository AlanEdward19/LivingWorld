using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Behavior;

public sealed class BehaviorPerceptionTests
{
    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void Perception_radius_eight_reacts_to_danger_six_tiles_away_control_does_not()
    {
        var treated = Scene(["attribute.perception:8"], observer: new CellCoord(0, 0), threat: new CellCoord(6, 0));
        var control = Scene([], observer: new CellCoord(0, 0), threat: new CellCoord(6, 0));

        Decide(treated);
        Decide(control);

        Assert.Equal(ActionType.Travel, treated.Observer.CurrentAction);
        Assert.Equal(ActionType.Work, control.Observer.CurrentAction);
    }

    [Fact]
    public void Without_perception_power_control_reacts_only_at_adjacency()
    {
        var distant = Scene([], observer: new CellCoord(0, 0), threat: new CellCoord(6, 0));
        var adjacent = Scene([], observer: new CellCoord(0, 0), threat: new CellCoord(1, 0));

        Decide(distant);
        Decide(adjacent);

        Assert.Equal(ActionType.Work, distant.Observer.CurrentAction);
        Assert.Equal(ActionType.Travel, adjacent.Observer.CurrentAction);
    }

    [Fact]
    public void Perception_radius_is_per_carrier_never_global()
    {
        var world = DualObservers(new CellCoord(6, 0));
        SimulationWakeTestHelper.WakeAllAlive(world.World);
        new BehaviorDecisionSystem().Tick(world.World, world.Ctx);

        Assert.Equal(ActionType.Work, world.ShortSight.CurrentAction);
        Assert.Equal(ActionType.Travel, world.LongSight.CurrentAction);
    }

    [Fact]
    public void Reaction_speed_two_halves_the_wake_interval_versus_control()
    {
        var treated = Scene(["attribute.reaction-speed:2"], observer: new CellCoord(0, 0), threat: new CellCoord(9, 9),
            threatHealth: 100);
        var control = Scene([], observer: new CellCoord(0, 0), threat: new CellCoord(9, 9), threatHealth: 100);
        treated.Observer.SetCurrentAction(ActionType.Work, 0);
        control.Observer.SetCurrentAction(ActionType.Work, 0);
        long now = 0;

        long treatedWake = NpcWakeScheduler.ComputeNextWakeTick(
            treated.Observer, treated.World.NeedsRules, treated.World.ActionCatalog, now, treated.World);
        long controlWake = NpcWakeScheduler.ComputeNextWakeTick(
            control.Observer, control.World.NeedsRules, control.World.ActionCatalog, now, control.World);

        Assert.Equal(8, controlWake);
        Assert.Equal(4, treatedWake);
    }

    [Fact]
    public void Perception_plus_reaction_speed_flees_before_the_control_npc()
    {
        var world = ReactionRace();
        var clock = new WorldClock([new BehaviorDecisionSystem()]);

        NpcWakeScheduler.ScheduleWake(world.World, world.Ctx, world.Treated.Id.Value, 4);
        NpcWakeScheduler.ScheduleWake(world.World, world.Ctx, world.Control.Id.Value, 8);

        for (int i = 0; i < 4; i++)
            clock.Tick(world.World);

        Assert.Equal(ActionType.Travel, world.Treated.CurrentAction);
        Assert.Equal(ActionType.Work, world.Control.CurrentAction);
    }

    [Fact]
    public void Ceasing_reaction_speed_restores_the_default_wake_interval()
    {
        var scene = Scene(["attribute.reaction-speed:2"], observer: new CellCoord(0, 0), threat: new CellCoord(9, 9),
            threatHealth: 100);
        scene.Observer.SetCurrentAction(ActionType.Work, 0);
        Assert.Equal(4, NpcWakeScheduler.ComputeNextWakeTick(
            scene.Observer, scene.World.NeedsRules, scene.World.ActionCatalog, 0, scene.World));

        scene.World.UpsertExtraordinaryCarrier(Carrier(scene.Observer.Id, ["speed"], manifested: false));
        Assert.Equal(1.0, AttributeMechanic.ReactionSpeedMultiplier(scene.World, scene.Observer));
        Assert.Equal(8, NpcWakeScheduler.ComputeNextWakeTick(
            scene.Observer, scene.World.NeedsRules, scene.World.ActionCatalog, 0, scene.World));
    }

    private static void Decide((WorldState World, TickContext Ctx, Npc Observer, Npc Threat) scene)
    {
        SimulationWakeTestHelper.Wake(scene.World, scene.Observer);
        new BehaviorDecisionSystem().Tick(scene.World, scene.Ctx);
    }

    private static (WorldState World, TickContext Ctx, Npc Observer, Npc Threat) Scene(
        IReadOnlyList<string> observerEffects, CellCoord observer, CellCoord threat, int threatHealth = 1)
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
            },
            routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
            defaultAction: ActionType.Idle).Value!;

        PowerDescriptor[] descriptors = [];
        ExtraordinaryCarrierState[] carriers = [];
        if (observerEffects.Count > 0)
        {
            string id = observerEffects[0].Contains("reaction", StringComparison.Ordinal) ? "speed" : "sight";
            descriptors = [new PowerDescriptor(id, "test", observerEffects, "Active", [], "Guaranteed", [], [], [], [])];
            carriers = [Carrier(new NpcId(1), [id], manifested: true)];
        }

        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors), extraordinaryCarriers: carriers);

        var watcher = Npc(new NpcId(1), "watcher", observer, 100, ActionType.Work);
        var danger = Npc(new NpcId(2), "danger", threat, threatHealth, ActionType.Idle);
        world.AddNpc(watcher);
        world.AddNpc(danger);
        return (world, new TickContext(world, world.Rng, world.Scheduler), watcher, danger);
    }

    private static (WorldState World, TickContext Ctx, Npc ShortSight, Npc LongSight) DualObservers(CellCoord threat)
    {
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            maxDurationHours: new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2, [ActionType.Sleep] = 8, [ActionType.Work] = 8,
                [ActionType.Socialize] = 3, [ActionType.Travel] = 4, [ActionType.Idle] = 2, [ActionType.Buy] = 2,
            },
            routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
            defaultAction: ActionType.Idle).Value!;
        var shortPower = new PowerDescriptor("short", "test", ["attribute.perception:3"], "Active", [], "Guaranteed", [], [], [], []);
        var longPower = new PowerDescriptor("long", "test", ["attribute.perception:8"], "Active", [], "Guaranteed", [], [], [], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [shortPower, longPower]),
            extraordinaryCarriers:
            [
                Carrier(new NpcId(1), ["short"], manifested: true),
                Carrier(new NpcId(2), ["long"], manifested: true),
            ]);
        var shortSight = Npc(new NpcId(1), "short", new CellCoord(0, 0), 100, ActionType.Work);
        var longSight = Npc(new NpcId(2), "long", new CellCoord(9, 0), 100, ActionType.Work);
        var danger = Npc(new NpcId(3), "danger", threat, 1, ActionType.Idle);
        world.AddNpc(shortSight);
        world.AddNpc(longSight);
        world.AddNpc(danger);
        return (world, new TickContext(world, world.Rng, world.Scheduler), shortSight, longSight);
    }

    private static (WorldState World, TickContext Ctx, Npc Treated, Npc Control) ReactionRace()
    {
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var catalog = ActionCatalog.Create(
            maxDurationHours: new Dictionary<ActionType, int>
            {
                [ActionType.Eat] = 2, [ActionType.Sleep] = 8, [ActionType.Work] = 8,
                [ActionType.Socialize] = 3, [ActionType.Travel] = 4, [ActionType.Idle] = 2, [ActionType.Buy] = 2,
            },
            routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
            defaultAction: ActionType.Idle).Value!;
        var treatedPower = new PowerDescriptor(
            "fast", "test", ["attribute.perception:8", "attribute.reaction-speed:2"], "Active", [], "Guaranteed", [], [], [], []);
        var controlPower = new PowerDescriptor(
            "slow", "test", ["attribute.perception:8"], "Active", [], "Guaranteed", [], [], [], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [treatedPower, controlPower]),
            extraordinaryCarriers:
            [
                Carrier(new NpcId(1), ["fast"], manifested: true),
                Carrier(new NpcId(2), ["slow"], manifested: true),
            ]);
        var treated = Npc(new NpcId(1), "fast", new CellCoord(0, 5), 100, ActionType.Work);
        var control = Npc(new NpcId(2), "slow", new CellCoord(9, 5), 100, ActionType.Work);
        var danger = Npc(new NpcId(3), "danger", new CellCoord(5, 5), 1, ActionType.Idle);
        world.AddNpc(treated);
        world.AddNpc(control);
        world.AddNpc(danger);
        return (world, new TickContext(world, world.Rng, world.Scheduler), treated, control);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId npcId, IReadOnlyList<string> powerIds, bool manifested) =>
        new(npcId, powerIds.ToList(), manifested, manifested ? "active" : "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private static Npc Npc(NpcId id, string name, CellCoord at, int health, ActionType action) => new(
        id, name, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30),
        ScenarioRunner.DefaultCulture, at, motherId: null, fatherId: null, household: null, health: health,
        personality: Personality, profession: ProfessionType.None, currentLocation: at,
        currentAction: action);
}
