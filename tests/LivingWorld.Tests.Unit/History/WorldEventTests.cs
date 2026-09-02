using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.History;

/// <summary>COH-01: WorldEvent carrega EventId/CauseEventId/SourceSystem; WorldState minta
/// EventId via contador irmão de ScheduledEvent (AD-013).</summary>
public class WorldEventTests
{
    [Fact]
    public void WorldEvent_carries_causal_provenance_fields()
    {
        var evt = new WorldEvent(
            Tick: 10,
            Kind: WorldEventKind.Birth,
            Payload: "1|2|3|4",
            EventId: 7,
            CauseEventId: 3,
            SourceSystem: "natality");

        Assert.Equal(7, evt.EventId);
        Assert.Equal(3, evt.CauseEventId);
        Assert.Equal("natality", evt.SourceSystem);
        Assert.Equal(10, evt.Tick);
        Assert.Equal(WorldEventKind.Birth, evt.Kind);
    }

    [Fact]
    public void WorldEvent_without_cause_is_a_chain_root()
    {
        var evt = new WorldEvent(5, WorldEventKind.Death, "9", EventId: 1);

        Assert.Null(evt.CauseEventId);
        Assert.Equal("Unknown", evt.SourceSystem);
    }

    [Fact]
    public void NextHistoryEventIdAndAdvance_is_monotonic_and_independent_of_NextEventId()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);

        Assert.Equal(0, world.NextHistoryEventId);
        Assert.Equal(0, world.NextEventId);

        var historyA = world.NextHistoryEventIdAndAdvance();
        var historyB = world.NextHistoryEventIdAndAdvance();
        var scheduled = world.NextEventIdAndAdvance();

        Assert.Equal(0, historyA);
        Assert.Equal(1, historyB);
        Assert.Equal(0, scheduled);
        Assert.Equal(2, world.NextHistoryEventId);
        Assert.Equal(1, world.NextEventId);
    }
}
