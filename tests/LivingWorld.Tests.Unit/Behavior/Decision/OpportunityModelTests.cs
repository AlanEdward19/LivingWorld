using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Extraordinary.Opportunity;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>Fase 16.3 T32 (COH-53): <see cref="OpportunityModel.DeriveOpportunities"/>.</summary>
public class OpportunityModelTests
{
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static DecisionContext Ctx(
        int hunger = 100,
        IReadOnlyList<NpcMemory>? memories = null,
        IReadOnlyList<string>? beliefs = null,
        IReadOnlyList<RelationshipFact>? relationships = null,
        IReadOnlyList<PowerOpportunity>? powers = null,
        Personality? personality = null) =>
        new(
            new NpcId(1),
            Tick: 3,
            new NeedsSnapshot(hunger, 100, 100, 100),
            new BodySnapshot(1.75, 70, 30, 1.0, 1.0),
            Household: null,
            memories ?? [],
            beliefs ?? [],
            relationships ?? [],
            powers ?? [],
            personality ?? Neutral,
            CurrentAction: null);

    [Fact]
    public void Unknown_market_never_appears_as_FoodAtMarket()
    {
        // Hunger alto sozinho NÃO cria FoodAtMarket — NPC não conhece o mercado.
        var opportunities = OpportunityModel.DeriveOpportunities(Ctx(hunger: 10));

        Assert.DoesNotContain(opportunities, o => o.Kind == OpportunityModel.FoodAtMarket);
        Assert.Empty(opportunities);
    }

    [Fact]
    public void Known_market_food_belief_yields_FoodAtMarket()
    {
        var opportunities = OpportunityModel.DeriveOpportunities(Ctx(
            hunger: 20,
            beliefs: ["the market has food stock today"]));

        var food = Assert.Single(opportunities, o => o.Kind == OpportunityModel.FoodAtMarket);
        Assert.True(food.Attractiveness > 40);
    }

    [Fact]
    public void ExtraordinaryCapability_only_when_PowerOpportunities_present()
    {
        var empty = OpportunityModel.DeriveOpportunities(Ctx());
        Assert.DoesNotContain(empty, o => o.Kind == OpportunityModel.ExtraordinaryCapability);

        var power = new PowerOpportunity(
            "p1", "npc.teleport", SuggestedTarget: null,
            EstimatedCost: 1m, EstimatedRisk: 0.1, Reliability: "Guaranteed");
        var withPower = OpportunityModel.DeriveOpportunities(Ctx(powers: [power]));

        var capability = Assert.Single(withPower, o => o.Kind == OpportunityModel.ExtraordinaryCapability);
        Assert.Equal("npc.teleport", capability.Detail);
    }

    [Fact]
    public void High_affection_relationship_yields_PotentialPartner()
    {
        var opportunities = OpportunityModel.DeriveOpportunities(Ctx(
            relationships: [new RelationshipFact(new NpcId(9), Trust: 40, Affection: 80, Respect: 30, Familiarity: 20)]));

        var partner = Assert.Single(opportunities, o => o.Kind == OpportunityModel.PotentialPartner);
        Assert.Equal("9", partner.Detail);
        Assert.Equal(80, partner.Attractiveness);
    }

    [Fact]
    public void Low_affection_relationship_is_not_PotentialPartner()
    {
        var opportunities = OpportunityModel.DeriveOpportunities(Ctx(
            relationships: [new RelationshipFact(new NpcId(9), Trust: 10, Affection: 20, Respect: 10, Familiarity: 5)]));

        Assert.DoesNotContain(opportunities, o => o.Kind == OpportunityModel.PotentialPartner);
    }

    [Fact]
    public void Job_belief_yields_NearbyJob_and_is_deterministic()
    {
        var ctx = Ctx(
            beliefs: ["there is a vacancy for mill work"],
            personality: Personality.Create(50, 50, 50, 50, 50, ambition: 70, 50, 50, 50, 50).Value!);

        var a = OpportunityModel.DeriveOpportunities(ctx);
        var b = OpportunityModel.DeriveOpportunities(ctx);

        Assert.Contains(a, o => o.Kind == OpportunityModel.NearbyJob);
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a[0].Kind, b[0].Kind);
        Assert.Equal(a[0].Attractiveness, b[0].Attractiveness);
    }
}
