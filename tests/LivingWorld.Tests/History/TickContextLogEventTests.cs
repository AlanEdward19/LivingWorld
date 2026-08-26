using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.History;

/// <summary>COH-01/COH-03: LogEvent aditivo minta EventId e carrega SourceSystem/CauseEventId.</summary>
public class TickContextLogEventTests
{
    private sealed class CapturingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, TickContext Ctx, CapturingSink Sink) Build()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 42, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var sink = new CapturingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        return (world, ctx, sink);
    }

    [Fact]
    public void Legacy_LogEvent_wrapper_uses_Unknown_source_and_null_cause()
    {
        var (world, ctx, sink) = Build();

        var id = ctx.LogEvent(WorldEventKind.Death, "1");

        Assert.Equal(0, id);
        Assert.Equal(1, world.NextHistoryEventId);
        var evt = Assert.Single(sink.Events);
        Assert.Equal(0, evt.EventId);
        Assert.Null(evt.CauseEventId);
        Assert.Equal("Unknown", evt.SourceSystem);
        Assert.Equal(WorldEventKind.Death, evt.Kind);
        Assert.Equal("1", evt.Payload);
    }

    [Fact]
    public void Additive_LogEvent_mints_EventId_and_carries_cause_chain()
    {
        var (world, ctx, sink) = Build();

        var rootId = ctx.LogEvent(WorldEventKind.ExtraordinaryUseAttempted, "attempt", "ExtraordinaryInvocationEngine");
        var childId = ctx.LogEvent(
            WorldEventKind.ExtraordinaryCostPaid, "cost", "ExtraordinaryInvocationEngine", causeEventId: rootId);

        Assert.Equal(0, rootId);
        Assert.Equal(1, childId);
        Assert.Equal(2, world.NextHistoryEventId);
        Assert.Equal(2, sink.Events.Count);
        Assert.Null(sink.Events[0].CauseEventId);
        Assert.Equal(rootId, sink.Events[1].CauseEventId);
        Assert.Equal("ExtraordinaryInvocationEngine", sink.Events[0].SourceSystem);
    }

    [Fact]
    public void Same_seed_produces_identical_EventId_sequence()
    {
        static List<long> Run()
        {
            var world = new WorldState(
                ScenarioRunner.DefaultCalendar, seed: 7, ScenarioRunner.DefaultMap(1),
                ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
                ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
                ScenarioRunner.DefaultLifeStageRules);
            var sink = new CapturingSink();
            var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
            return
            [
                ctx.LogEvent(WorldEventKind.Birth, "a", "natality"),
                ctx.LogEvent(WorldEventKind.Death, "b", "mortality", causeEventId: 0),
            ];
        }

        Assert.Equal(Run(), Run());
    }
}
