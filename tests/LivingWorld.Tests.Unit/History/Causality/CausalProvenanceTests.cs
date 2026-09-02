using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Causality;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.History.Causality;

/// <summary>COH-02: ResolveRootCauseEventId percorre CauseEventId até a raiz com guard de ciclo.</summary>
public class CausalProvenanceTests
{
    [Fact]
    public void Single_event_without_cause_is_its_own_root()
    {
        var events = new List<WorldEvent>
        {
            new(1, WorldEventKind.Death, "1", EventId: 7, CauseEventId: null, SourceSystem: "mortality"),
        };

        Assert.Equal(7, CausalProvenance.ResolveRootCauseEventId(events, 7, CausalRules.Default));
    }

    [Fact]
    public void Chain_of_three_resolves_to_root()
    {
        var events = new List<WorldEvent>
        {
            new(1, WorldEventKind.ExtraordinaryUseAttempted, "a", EventId: 1, CauseEventId: null, SourceSystem: "eng"),
            new(1, WorldEventKind.ExtraordinaryCostPaid, "b", EventId: 2, CauseEventId: 1, SourceSystem: "eng"),
            new(1, WorldEventKind.ExtraordinaryEffectApplied, "c", EventId: 3, CauseEventId: 2, SourceSystem: "eng"),
        };

        Assert.Equal(1, CausalProvenance.ResolveRootCauseEventId(events, 3, maxDepth: 10));
        Assert.Equal(1, CausalProvenance.ResolveRootCauseEventId(events, 2, maxDepth: 10));
        Assert.Equal(1, CausalProvenance.ResolveRootCauseEventId(events, 1, maxDepth: 10));
    }

    [Fact]
    public void Long_chain_within_limit_returns_root()
    {
        var events = new List<WorldEvent>
        {
            new(0, WorldEventKind.Birth, "root", EventId: 0, CauseEventId: null, SourceSystem: "s"),
        };
        for (long i = 1; i <= 5; i++)
            events.Add(new(i, WorldEventKind.Birth, $"e{i}", EventId: i, CauseEventId: i - 1, SourceSystem: "s"));

        Assert.Equal(0, CausalProvenance.ResolveRootCauseEventId(events, 5, maxDepth: 5));
    }

    [Fact]
    public void Exact_max_depth_boundary_still_resolves()
    {
        // depth steps: 3→2, 2→1, 1→0 (3 steps) — maxDepth=3 must succeed
        var events = new List<WorldEvent>
        {
            new(0, WorldEventKind.Birth, "0", EventId: 0, CauseEventId: null, SourceSystem: "s"),
            new(1, WorldEventKind.Birth, "1", EventId: 1, CauseEventId: 0, SourceSystem: "s"),
            new(2, WorldEventKind.Birth, "2", EventId: 2, CauseEventId: 1, SourceSystem: "s"),
            new(3, WorldEventKind.Birth, "3", EventId: 3, CauseEventId: 2, SourceSystem: "s"),
        };

        Assert.Equal(0, CausalProvenance.ResolveRootCauseEventId(events, 3, maxDepth: 3));
    }

    [Fact]
    public void Exceeding_max_depth_throws_naming_culprit()
    {
        var events = new List<WorldEvent>
        {
            new(0, WorldEventKind.Birth, "0", EventId: 0, CauseEventId: null, SourceSystem: "s"),
            new(1, WorldEventKind.Birth, "1", EventId: 1, CauseEventId: 0, SourceSystem: "s"),
            new(2, WorldEventKind.Birth, "2", EventId: 2, CauseEventId: 1, SourceSystem: "s"),
            new(3, WorldEventKind.Birth, "3", EventId: 3, CauseEventId: 2, SourceSystem: "s"),
        };

        var ex = Assert.Throws<CausalChainTooDeepException>(
            () => CausalProvenance.ResolveRootCauseEventId(events, 3, maxDepth: 2));
        Assert.Equal(1, ex.CulpritEventId);
        Assert.Equal(2, ex.MaxDepth);
        Assert.Contains("1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cycle_throws_naming_culprit()
    {
        var events = new List<WorldEvent>
        {
            new(0, WorldEventKind.Birth, "a", EventId: 1, CauseEventId: 2, SourceSystem: "s"),
            new(0, WorldEventKind.Birth, "b", EventId: 2, CauseEventId: 1, SourceSystem: "s"),
        };

        var ex = Assert.Throws<CausalChainTooDeepException>(
            () => CausalProvenance.ResolveRootCauseEventId(events, 1, maxDepth: 64));
        Assert.Equal(2, ex.CulpritEventId);
    }

    [Fact]
    public void CausalRules_rejects_non_positive_max_depth()
    {
        var fail = CausalRules.Create(0);
        Assert.False(fail.IsSuccess);
        Assert.Contains("MaxCauseChainDepth", fail.Error, StringComparison.Ordinal);
        Assert.Equal(64, CausalRules.Default.MaxCauseChainDepth);
    }
}
