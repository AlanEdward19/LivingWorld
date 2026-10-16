using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>Fase 16.3 T30 (COH-44/45): full reconsideration vs event-driven metrics.</summary>
public class DecisionMetricsTests
{
    [Fact]
    public void Event_driven_mode_produces_same_canonical_fingerprint_as_full()
    {
        var comparison = DecisionMetrics.CompareFullVsEventDriven(seed: 99, hours: 24);

        Assert.Equal(
            comparison.Full.CanonicalFingerprint,
            comparison.EventDriven.CanonicalFingerprint);
    }

    [Fact]
    public void Event_driven_mode_records_fewer_decisions_and_wakeups()
    {
        var comparison = DecisionMetrics.CompareFullVsEventDriven(seed: 99, hours: 24);

        Assert.True(
            comparison.EventDriven.Metrics.Decisions < comparison.Full.Metrics.Decisions,
            $"event-driven decisions ({comparison.EventDriven.Metrics.Decisions}) should be < full ({comparison.Full.Metrics.Decisions})");
        Assert.True(
            comparison.EventDriven.Metrics.Wakeups < comparison.Full.Metrics.Wakeups,
            $"event-driven wakeups ({comparison.EventDriven.Metrics.Wakeups}) should be < full ({comparison.Full.Metrics.Wakeups})");
    }

    [Fact]
    public void Decisions_per_agent_day_is_lower_in_event_driven_mode()
    {
        var comparison = DecisionMetrics.CompareFullVsEventDriven(seed: 42, hours: 48);

        Assert.True(comparison.Full.Metrics.DecisionsPerAgentDay > 0);
        Assert.True(
            comparison.EventDriven.Metrics.DecisionsPerAgentDay
            < comparison.Full.Metrics.DecisionsPerAgentDay);
    }

    [Fact]
    public void Comparison_is_deterministic_across_runs()
    {
        var a = DecisionMetrics.CompareFullVsEventDriven(seed: 7, hours: 12);
        var b = DecisionMetrics.CompareFullVsEventDriven(seed: 7, hours: 12);

        Assert.Equal(a.Full.CanonicalFingerprint, b.Full.CanonicalFingerprint);
        Assert.Equal(a.EventDriven.CanonicalFingerprint, b.EventDriven.CanonicalFingerprint);
        Assert.Equal(a.Full.Metrics.Decisions, b.Full.Metrics.Decisions);
        Assert.Equal(a.EventDriven.Metrics.Decisions, b.EventDriven.Metrics.Decisions);
    }

    [Fact]
    public void Fingerprint_changes_when_intent_diverges()
    {
        var calendar = new WorldCalendar(24, 30, 12);
        var world = new WorldState(
            calendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var npc = new Npc(
            new NpcId(1), "n", Sex.Male, WorldDate.Epoch(calendar).AddYears(-20),
            new CultureId(1), new CellCoord(0, 0), null, null, null, 100, personality,
            ProfessionType.None, new CellCoord(0, 0));
        world.AddNpc(npc);

        var before = DecisionMetrics.Fingerprint(world);
        npc.SetIntent(ActionType.Buy, 1);
        var after = DecisionMetrics.Fingerprint(world);

        Assert.NotEqual(before, after);
    }
}
