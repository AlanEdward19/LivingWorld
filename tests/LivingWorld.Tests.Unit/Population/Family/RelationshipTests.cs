using LivingWorld.Domain.Population.Family;

namespace LivingWorld.Tests.Unit.Population.Family;

/// <summary>Fase 7, T5 (FAM-01, FAM-02, FAM-03, FAM-04, FAM-05): os 4 eixos assimétricos de uma
/// relação — evolução por evento nomeado, decaimento sem contato, nunca o mesmo objeto para os
/// dois sentidos do par.</summary>
public class RelationshipTests
{
    private static FamilyRules RulesWith(
        double cohabitationTrustDelta = 0,
        double betrayalTrustDelta = 0,
        double decayPerDay = 1,
        double neutralAxisValue = 50)
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0.0;
        deltas[(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = cohabitationTrustDelta;
        deltas[(RelationshipEventType.Betrayal, RelationshipAxis.Trust)] = betrayalTrustDelta;

        return FamilyRules.Create(
            relationshipDeltas: deltas,
            decayPerDay: decayPerDay,
            contactLossThresholdDays: 30,
            neutralAxisValue: neutralAxisValue,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: 0.6,
            courtshipDurationDays: 90,
            marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: 0.02,
            infantDeathRisk: 0.05,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 5,
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.3,
            environmentalWealthChannelEnabled: true,
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true).Value!;
    }

    [Fact]
    public void Initial_creates_all_four_axes_at_the_minimum_floor()
    {
        var relationship = Relationship.Initial(firstContactTick: 100);

        Assert.Equal(0, relationship.Get(RelationshipAxis.Trust));
        Assert.Equal(0, relationship.Get(RelationshipAxis.Affection));
        Assert.Equal(0, relationship.Get(RelationshipAxis.Respect));
        Assert.Equal(0, relationship.Get(RelationshipAxis.Debt));
    }

    [Fact]
    public void ApplyEvent_applies_the_declared_delta_to_the_events_axis()
    {
        var rules = RulesWith(cohabitationTrustDelta: 5);
        var relationship = Relationship.Initial(firstContactTick: 0);

        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);

        Assert.Equal(5, relationship.Get(RelationshipAxis.Trust));
    }

    [Fact]
    public void ApplyEvent_only_touches_axes_with_a_declared_delta_for_that_event()
    {
        var rules = RulesWith(cohabitationTrustDelta: 5);
        var relationship = Relationship.Initial(firstContactTick: 0);

        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);

        Assert.Equal(0, relationship.Get(RelationshipAxis.Affection));
        Assert.Equal(0, relationship.Get(RelationshipAxis.Respect));
        Assert.Equal(0, relationship.Get(RelationshipAxis.Debt));
    }

    [Fact]
    public void ApplyEvent_clamps_axis_to_the_upper_bound()
    {
        var rules = RulesWith(cohabitationTrustDelta: 1000);
        var relationship = Relationship.Initial(firstContactTick: 0);

        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);

        Assert.Equal(100, relationship.Get(RelationshipAxis.Trust));
    }

    [Fact]
    public void ApplyEvent_clamps_axis_to_the_lower_bound()
    {
        var rules = RulesWith(betrayalTrustDelta: -1000);
        var relationship = Relationship.Initial(firstContactTick: 0);

        relationship.ApplyEvent(RelationshipEventType.Betrayal, rules);

        Assert.Equal(0, relationship.Get(RelationshipAxis.Trust));
    }

    [Fact]
    public void DecayTowardNeutral_never_overshoots_neutral_when_approaching_from_above()
    {
        var rules = RulesWith(cohabitationTrustDelta: 60, decayPerDay: 1000, neutralAxisValue: 50);
        var relationship = Relationship.Initial(firstContactTick: 0);
        relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules); // Trust = 60

        relationship.DecayTowardNeutral(rules);

        Assert.Equal(50, relationship.Get(RelationshipAxis.Trust));
    }

    [Fact]
    public void DecayTowardNeutral_never_overshoots_neutral_when_approaching_from_below()
    {
        var rules = RulesWith(decayPerDay: 1000, neutralAxisValue: 50);
        var relationship = Relationship.Initial(firstContactTick: 0); // Trust = 0, below neutral

        relationship.DecayTowardNeutral(rules);

        Assert.Equal(50, relationship.Get(RelationshipAxis.Trust));
    }

    [Fact]
    public void MarkContact_updates_last_contact_tick()
    {
        var relationship = Relationship.Initial(firstContactTick: 0);

        relationship.MarkContact(42);

        Assert.Equal(42, relationship.LastContactTick);
    }

    [Fact]
    public void AtoB_and_BtoA_diverge_after_different_events_proving_asymmetry()
    {
        var rules = RulesWith(cohabitationTrustDelta: 5, betrayalTrustDelta: -20);
        var aToB = Relationship.Initial(firstContactTick: 0);
        var bToA = Relationship.Initial(firstContactTick: 0);

        aToB.ApplyEvent(RelationshipEventType.Cohabitation, rules);
        bToA.ApplyEvent(RelationshipEventType.Betrayal, rules);

        Assert.NotEqual(aToB.Get(RelationshipAxis.Trust), bToA.Get(RelationshipAxis.Trust));
    }
}
