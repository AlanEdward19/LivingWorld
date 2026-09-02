using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>REALISM-32 / Design Risk: foresight empty dictionary is shared — no per-call
/// allocation on the common path (SelectByUtility without active foresight).</summary>
[Collection(ScalePerformanceCollection.Name)]
public sealed class ForesightHotPathAllocTests
{
    [Fact]
    public void Empty_foresight_previews_are_shared_singleton_not_allocated_per_call()
    {
        var a = ForesightMechanic.EmptyPreviews;
        var b = ForesightMechanic.EmptyPreviews;
        Assert.Same(a, b);

        // PreviewsFor with no stored data must hand back the shared empty instance.
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 7, ScenarioRunner.DefaultMap(7),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var first = ForesightMechanic.PreviewsFor(world, new NpcId(1), tick: 0);
        var second = ForesightMechanic.PreviewsFor(world, new NpcId(2), tick: 0);
        Assert.Same(ForesightMechanic.EmptyPreviews, first);
        Assert.Same(first, second);

        // SelectByUtility common path: null foresight ≡ EmptyPreviews (no new dict for foresight).
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var ctxNull = new DecisionContext(
            new NpcId(1), 0,
            new NeedsSnapshot(40, 100, 100, 100),
            new BodySnapshot(1.7, 68, 28, 1, 1),
            null,
            Array.Empty<NpcMemory>(),
            Array.Empty<string>(),
            Array.Empty<RelationshipFact>(),
            Array.Empty<PowerOpportunity>(),
            personality,
            null,
            ForesightPreviews: null);
        var ctxEmpty = ctxNull with { ForesightPreviews = ForesightMechanic.EmptyPreviews };
        Assert.Null(ctxNull.ForesightPreviews);
        Assert.Same(ForesightMechanic.EmptyPreviews, ctxEmpty.ForesightPreviews);
    }
}
