using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Authoring;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Authoring;

public sealed class WorldAuthoringCommandsTests
{
    [Fact]
    public void Rewrite_personality_is_atomic_and_force_action_uses_current_world_tick()
    {
        var (world, first, _) = World();
        world.CurrentDate = world.CurrentDate.AddHours(37);
        var ctx = Context(world);
        var invalid = WorldAuthoringCommands.RewritePersonality(
            world, ctx, first.Id, new PersonalityValues(101, 1, 2, 3, 4, 5, 6, 7, 8, 9));
        Assert.False(invalid.IsSuccess);
        Assert.Equal(50, first.Personality.Extroversion);

        var expected = Personality.Create(90, 10, 20, 30, 40, 50, 60, 70, 80, 90).Value!;
        var valid = WorldAuthoringCommands.RewritePersonality(
            world, ctx, first.Id, new PersonalityValues(90, 10, 20, 30, 40, 50, 60, 70, 80, 90));
        var action = WorldAuthoringCommands.ForceAction(world, ctx, first.Id, ActionType.Work);

        Assert.True(valid.IsSuccess, valid.Error);
        Assert.True(action.IsSuccess, action.Error);
        Assert.Equal(expected, first.Personality);
        Assert.Equal((ActionType.Work, 37L), (first.CurrentAction, first.ActionStartedAtTick));
    }

    [Fact]
    public void Break_relationships_removes_only_both_directions_of_selected_pair()
    {
        var (world, first, second) = World();
        var third = AddNpc(world, 3);
        first.Marry(third.Id);
        var familyBefore = (first.MotherId, first.FatherId, first.Spouse);
        world.GetOrCreateRelationship(new RelationshipKey(first.Id, second.Id), 0);
        world.GetOrCreateRelationship(new RelationshipKey(second.Id, first.Id), 0);
        world.GetOrCreateRelationship(new RelationshipKey(first.Id, third.Id), 0);

        var result = WorldAuthoringCommands.BreakRelationships(world, Context(world), first.Id, second.Id);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value);
        Assert.Single(world.Relationships);
        Assert.True(world.Relationships.ContainsKey(new RelationshipKey(first.Id, third.Id)));
        Assert.Equal(familyBefore, (first.MotherId, first.FatherId, first.Spouse));
    }

    [Fact]
    public void Force_action_rejects_values_outside_the_catalog_without_mutating_the_npc()
    {
        var (world, first, _) = World();
        first.SetCurrentAction(ActionType.Idle, 3);

        var result = WorldAuthoringCommands.ForceAction(
            world, Context(world), first.Id, (ActionType)999);

        Assert.False(result.IsSuccess);
        Assert.Equal((ActionType.Idle, 3L), (first.CurrentAction, first.ActionStartedAtTick));
    }

    [Fact]
    public void Invalid_authored_invocation_preserves_next_event_id_and_records_rejection()
    {
        var (world, first, _) = World();
        var sink = new RecordingSink();
        var service = new WorldAuthoringService(sink);
        long nextBefore = world.NextEventId;

        var result = service.Invoke(world, first.Id, "missing", first.Id, null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(nextBefore, world.NextEventId);
        Assert.Contains(sink.Events, item => item.Kind == WorldEventKind.AuthoringCommandRejected);
    }

    private static (WorldState World, Npc First, Npc Second) World()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        return (world, AddNpc(world, 1, new NpcId(90), new NpcId(91)), AddNpc(world, 2));
    }

    private static Npc AddNpc(WorldState world, long id, NpcId? motherId = null, NpcId? fatherId = null)
    {
        var npc = new Npc(
            new NpcId(id), $"npc-{id}", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(id == 1 ? 0 : 5, 0), motherId, fatherId, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, new CellCoord(id == 1 ? 0 : 5, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static TickContext Context(WorldState world) => new(world, world.Rng, world.Scheduler);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
