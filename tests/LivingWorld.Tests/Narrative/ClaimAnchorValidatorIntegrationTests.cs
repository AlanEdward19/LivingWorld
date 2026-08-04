using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T3 (integration): <see cref="ClaimAnchorValidator"/> encadeado com
/// <see cref="WindowedHistoryAggregator"/> — prova que só claims com eventIds que resolvem a
/// <see cref="Fact"/>s reais do motor sobrevivem à validação e alimentam a prosa final (NARR-01,
/// NARR-03).</summary>
public class ClaimAnchorValidatorIntegrationTests
{
    [Fact]
    public void Approved_claims_built_from_aggregated_facts_all_resolve_to_real_events()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "d1"));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.Marriage, [], city, 0.7, "m1"));

        var topFacts = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 10);
        var claims = topFacts.Select(f => new NarrativeClaim($"evento {f.Id.Value}", [f.Id.Value])).ToList();

        var outcome = ClaimAnchorValidator.ValidateClaims(claims);

        Assert.Equal(topFacts.Count, outcome.Approved.Count);
        Assert.Empty(outcome.Rejected);
        foreach (var claim in outcome.Approved)
        {
            foreach (var eventId in claim.EventIds)
            {
                var lookup = HistoryTruthQuery.GetFact(world, new FactId(eventId));
                Assert.True(lookup.IsSuccess, $"claim referencia evento inexistente: {eventId}");
            }
        }
    }

    [Fact]
    public void Claim_without_event_ids_never_reaches_prose_and_generic_orphan_claim_is_dropped()
    {
        var (world, _) = ScenarioRunner.Create(1, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        var fact = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "d1");
        world.AddFact(fact);

        var topFacts = WindowedHistoryAggregator.TopFacts(world, city, periodStartTick: 0, periodEndTick: 100, topK: 10);
        var anchored = new NarrativeClaim($"o evento {fact.Id.Value} marcou a vila", [fact.Id.Value]);
        var orphan = new NarrativeClaim("nada digno de nota", []);

        var outcome = ClaimAnchorValidator.ValidateClaims([anchored, orphan]);
        string prose = string.Join(" ", outcome.Approved.Select(c => c.Text));
        var proseCheck = ClaimAnchorValidator.ValidateProse(prose, outcome.Approved);

        Assert.Single(outcome.Approved);
        Assert.Same(anchored, outcome.Approved[0]);
        Assert.Single(outcome.Rejected);
        Assert.Same(orphan, outcome.Rejected[0].Claim);
        Assert.True(proseCheck.IsSuccess, proseCheck.Error);
        Assert.DoesNotContain("nada digno de nota", prose);
        Assert.NotEmpty(topFacts);
    }
}
