using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>COH-62: CausalDepth e SystemsTouchedByCausalChain sobre CauseEventId.</summary>
public class CausalDiagnosticsTests
{
    [Fact]
    public void CausalDepth_is_zero_for_root_event()
    {
        var events = new List<WorldEvent>
        {
            new(1, WorldEventKind.ResourceLost, "harvest", EventId: 1, CauseEventId: null, SourceSystem: "CropSystem"),
        };

        Assert.Equal(0, CausalDiagnostics.CausalDepth(events, 1, CausalRules.Default));
    }

    [Fact]
    public void CausalDepth_counts_steps_to_root_on_known_chain()
    {
        var events = ChainOfFive();

        Assert.Equal(4, CausalDiagnostics.CausalDepth(events, 5, maxDepth: 64));
        Assert.Equal(2, CausalDiagnostics.CausalDepth(events, 3, maxDepth: 64));
        Assert.Equal(0, CausalDiagnostics.CausalDepth(events, 1, maxDepth: 64));
    }

    [Fact]
    public void SystemsTouchedByCausalChain_returns_distinct_SourceSystems()
    {
        var events = ChainOfFive();

        var systems = CausalDiagnostics.SystemsTouchedByCausalChain(events, 5, CausalRules.Default);

        Assert.Equal(5, systems.Count);
        Assert.Contains("CropSystem", systems);
        Assert.Contains("MarketPricingSystem", systems);
        Assert.Contains("EmploymentSystem", systems);
        Assert.Contains("NeedsDecaySystem", systems);
        Assert.Contains("BehaviorDecisionSystem", systems);
    }

    [Fact]
    public void Same_chain_is_deterministic_across_two_queries()
    {
        var events = ChainOfFive();

        var a = CausalDiagnostics.SystemsTouchedByCausalChain(events, 5, maxDepth: 64);
        var b = CausalDiagnostics.SystemsTouchedByCausalChain(events, 5, maxDepth: 64);
        Assert.Equal(a.OrderBy(s => s, StringComparer.Ordinal), b.OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(
            CausalDiagnostics.CausalDepth(events, 5, maxDepth: 64),
            CausalDiagnostics.CausalDepth(events, 5, maxDepth: 64));
    }

    [Fact]
    public void Exceeding_maxDepth_throws_naming_culprit()
    {
        var events = ChainOfFive();

        var ex = Assert.Throws<CausalChainTooDeepException>(
            () => CausalDiagnostics.CausalDepth(events, 5, maxDepth: 2));
        Assert.Equal(2, ex.MaxDepth);
        Assert.True(ex.CulpritEventId is > 0);
    }

    private static List<WorldEvent> ChainOfFive() =>
    [
        new(0, WorldEventKind.ResourceLost, "HarvestReduced", EventId: 1, CauseEventId: null, SourceSystem: "CropSystem"),
        new(1, WorldEventKind.ResourceLost, "FoodStockReduced", EventId: 2, CauseEventId: 1, SourceSystem: "MarketPricingSystem"),
        new(2, WorldEventKind.ResourceLost, "PriceIncreased", EventId: 3, CauseEventId: 2, SourceSystem: "NeedsDecaySystem"),
        new(3, WorldEventKind.WageUnpaid, "PurchaseFailed", EventId: 4, CauseEventId: 3, SourceSystem: "BehaviorDecisionSystem"),
        new(4, WorldEventKind.Fired, "EmploymentAffected", EventId: 5, CauseEventId: 4, SourceSystem: "EmploymentSystem"),
    ];
}
