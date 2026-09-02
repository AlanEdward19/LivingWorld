using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

public sealed class ExtraordinaryStateTransitionTests
{
    [Fact]
    public void Authored_event_acquires_exact_power_once_and_records_the_cause()
    {
        var world = World([Descriptor("artifact", "event:item-bond", "carrier:action:Work")]);
        var npc = AddNpc(world, 1, ActionType.Work);
        var sink = new RecordingSink();
        var system = new ExtraordinaryStateSystem();
        Schedule(world, 1, "acquire|1|artifact|wrong-trigger");
        Schedule(world, 1, "acquire|1|artifact|item-bond");
        Schedule(world, 1, "acquire|1|artifact|item-bond");

        new WorldClock([system], sink: sink).Tick(world);

        var carrier = Assert.Single(world.ExtraordinaryCarriers);
        Assert.Equal((npc.Id, "artifact", true, "manifested"),
            (carrier.CarrierId, Assert.Single(carrier.PowerIds), carrier.IsManifested, carrier.ManifestationState));
        Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.ExtraordinaryAcquired);
        Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.ExtraordinaryManifested);
        Assert.Single(sink.Events, evt => evt.Kind == WorldEventKind.ExtraordinaryAcquisitionFailed);
    }

    [Fact]
    public void Night_condition_changes_appearance_metabolism_and_senescence_only_during_its_window()
    {
        var descriptor = Descriptor("night-change", "event:exposure", "world:is-night") with
        {
            Appearance = new ExtraordinaryAppearanceDescriptor(1.1, "pale", "mist"),
            NeedSubstitution = new NeedSubstitutionDescriptor("hunger", new ResourceType(9), 2),
            SenescenceRateMultiplier = 0,
        };
        var world = World([descriptor]);
        AddNpc(world, 1);
        var sink = new RecordingSink();
        Schedule(world, 1, "acquire|1|night-change|exposure");
        var clock = new WorldClock([new ExtraordinaryStateSystem()], sink: sink);

        clock.Tick(world); // hour 1: night
        var active = Assert.Single(world.ExtraordinaryCarriers);
        clock.Run(world, 6); // hour 7: day
        var dormant = Assert.Single(world.ExtraordinaryCarriers);

        Assert.Equal((true, 1.1, "pale", "mist", "hunger", 9, 0.0),
            (active.IsManifested, active.Appearance.ScaleMultiplier, active.Appearance.SkinTint,
                active.Appearance.MovementTrail, active.NeedSubstitution!.ReplacesNeed,
                active.NeedSubstitution.Resource.Id, active.SenescenceRateMultiplier));
        Assert.Equal((false, 1.0, "", "", null, 1.0),
            (dormant.IsManifested, dormant.Appearance.ScaleMultiplier, dormant.Appearance.SkinTint,
                dormant.Appearance.MovementTrail, dormant.NeedSubstitution, dormant.SenescenceRateMultiplier));
        Assert.Contains(sink.Events, evt => evt.Kind == WorldEventKind.ExtraordinaryDormant);
    }

    [Fact]
    public void Tick_cycle_manifestation_is_deterministic_and_uses_the_same_descriptor_engine()
    {
        var descriptor = Descriptor("cycle-change", "event:birth", "world:tick-cycle:24:6:6") with
        {
            Appearance = new ExtraordinaryAppearanceDescriptor(1.4, "fur", "dust"),
        };
        var first = World([descriptor]);
        var second = World([descriptor]);
        AddNpc(first, 1);
        AddNpc(second, 1);
        Schedule(first, 1, "acquire|1|cycle-change|birth");
        Schedule(second, 1, "acquire|1|cycle-change|birth");
        var firstClock = new WorldClock([new ExtraordinaryStateSystem()]);
        var secondClock = new WorldClock([new ExtraordinaryStateSystem()]);

        firstClock.Run(first, 6);
        secondClock.Run(second, 6);
        Assert.True(Assert.Single(first.ExtraordinaryCarriers).IsManifested);
        Assert.Equal(WorldSnapshot.CanonicalHash(first), WorldSnapshot.CanonicalHash(second));

        firstClock.Run(first, 6);
        secondClock.Run(second, 6);
        Assert.False(Assert.Single(first.ExtraordinaryCarriers).IsManifested);
        Assert.Equal(WorldSnapshot.CanonicalHash(first), WorldSnapshot.CanonicalHash(second));
    }

    [Fact]
    public void Same_manifestation_produces_distinct_authored_responses_by_observer_culture()
    {
        var descriptor = Descriptor("visible-power", "event:witnessed", "world:is-night");
        var responses = new[]
        {
            new ExtraordinaryCulturalResponseRule(1, "visible-change", "revere"),
            new ExtraordinaryCulturalResponseRule(2, "visible-change", "fear"),
            new ExtraordinaryCulturalResponseRule(3, "visible-change", "study"),
        };
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var world = World([descriptor], responses, seed: seed);
            AddNpc(world, 1, culture: 1);
            AddNpc(world, 2, culture: 2);
            var sink = new RecordingSink();
            Schedule(world, 1, "acquire|1|visible-power|witnessed");

            new WorldClock([new ExtraordinaryStateSystem()], sink: sink).Tick(world);

            Assert.Equal(
                ["1|1|visible-change|revere", "1|2|visible-change|fear"],
                sink.Events.Where(evt => evt.Kind == WorldEventKind.ExtraordinaryCulturalReaction)
                    .Select(evt => evt.Payload));
        }
    }

    [Fact]
    public void Known_carrier_is_retained_as_a_detailed_npc_by_lod_policy()
    {
        var descriptor = Descriptor("known-power", "event:known", "world:is-night");
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = World([descriptor], carriers: [carrier]);
        var npc = AddNpc(world, 1, materializedAtTick: 0);

        var result = MaterializationSystem.Dematerialize(world, npc.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("papel formal", result.Error, StringComparison.Ordinal);
        Assert.Contains(world.Npcs, candidate => candidate.Id == npc.Id);
    }

    private static PowerDescriptor Descriptor(string id, string acquisition, string condition) => new(
        id, "scenario-source", ["npc.health:1"], "Conditional", [], "Guaranteed", [], [],
        ["visible-change"], [acquisition], ManifestationCondition: condition);

    private static WorldState World(
        IReadOnlyList<PowerDescriptor> descriptors,
        IReadOnlyList<ExtraordinaryCulturalResponseRule>? responses = null,
        IReadOnlyList<ExtraordinaryCarrierState>? carriers = null,
        ulong seed = 42) => new(
        ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules,
        extraordinary: new ExtraordinaryScenarioData(true, descriptors, responses),
        extraordinaryCarriers: carriers);

    private static Npc AddNpc(
        WorldState world, long id, ActionType action = ActionType.Idle, int culture = 1,
        long? materializedAtTick = null)
    {
        var npc = new Npc(
            new NpcId(id), $"npc-{id}", Sex.Female, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            new CultureId(culture), new CellCoord(0, 0), motherId: null, fatherId: null,
            household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0),
            materializedAtTick: materializedAtTick);
        npc.SetCurrentAction(action, 0);
        world.AddNpc(npc);
        return npc;
    }

    private static void Schedule(WorldState world, long tick, string payload) =>
        new TickContext(world, world.Rng, world.Scheduler)
            .ScheduleEvent(tick, ExtraordinaryStateSystem.SystemName, payload);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
