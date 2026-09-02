using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>Fase 16.3, T19 (COH-31): heurística de custo/risco de PowerOpportunity.</summary>
public class PowerOpportunityTests
{
    private static PowerDescriptor Descriptor(
        string reliability = "Guaranteed",
        IReadOnlyList<string>? costs = null,
        IReadOnlyList<string>? failureModes = null) =>
        new(
            Id: "test-power",
            Source: "test",
            Effects: ["effect:noop"],
            Mode: "Active",
            Costs: costs ?? [],
            Reliability: reliability,
            FailureModes: failureModes ?? [],
            IntrinsicVulnerabilities: [],
            Manifestations: [],
            AcquisitionRules: []);

    [Fact]
    public void Guaranteed_reliability_yields_low_fixed_risk()
    {
        var d = Descriptor(reliability: "Guaranteed", costs: ["cost:hunger:1"]);

        Assert.Equal(PowerOpportunity.GuaranteedRisk, PowerOpportunity.EstimateRisk(d));
    }

    [Fact]
    public void ResolutionCheck_reliability_yields_higher_risk_than_Guaranteed()
    {
        var guaranteed = Descriptor(reliability: "Guaranteed");
        var checkedRel = Descriptor(reliability: "ResolutionCheck");

        Assert.True(
            PowerOpportunity.EstimateRisk(checkedRel) > PowerOpportunity.EstimateRisk(guaranteed));
        Assert.Equal(
            PowerOpportunity.ResolutionCheckBaseRisk,
            PowerOpportunity.EstimateRisk(checkedRel));
    }

    [Fact]
    public void ResolutionCheck_risk_grows_with_failure_modes()
    {
        var none = Descriptor(reliability: "ResolutionCheck", failureModes: []);
        var two = Descriptor(
            reliability: "ResolutionCheck",
            failureModes: ["fail:backlash", "fail:fizzle"]);

        Assert.True(PowerOpportunity.EstimateRisk(two) > PowerOpportunity.EstimateRisk(none));
        Assert.Equal(
            PowerOpportunity.ResolutionCheckBaseRisk + 2 * PowerOpportunity.FailureModeRiskStep,
            PowerOpportunity.EstimateRisk(two));
    }

    [Fact]
    public void EstimatedCost_grows_with_Costs_Count()
    {
        var few = Descriptor(costs: ["cost:hunger:1"]);
        var many = Descriptor(costs: ["cost:hunger:1", "cost:thirst:1", "cost:sleep:1"]);

        Assert.True(PowerOpportunity.EstimateCost(many) > PowerOpportunity.EstimateCost(few));
        Assert.Equal(1.0m, PowerOpportunity.EstimateCost(few));
        Assert.Equal(3.0m, PowerOpportunity.EstimateCost(many));
    }

    [Fact]
    public void FromDescriptor_copies_reliability_and_token()
    {
        var d = Descriptor(reliability: "Guaranteed", costs: ["cost:hunger:2"]);
        var opp = PowerOpportunity.FromDescriptor(d, mechanicToken: "effect:teleport");

        Assert.Equal("test-power", opp.PowerId);
        Assert.Equal("effect:teleport", opp.MechanicToken);
        Assert.Equal("Guaranteed", opp.Reliability);
        Assert.Equal(1.0m, opp.EstimatedCost);
        Assert.Equal(PowerOpportunity.GuaranteedRisk, opp.EstimatedRisk);
        Assert.Null(opp.SuggestedTarget);
    }

    [Fact]
    public void EstimateCost_and_EstimateRisk_are_deterministic()
    {
        var d = Descriptor(
            reliability: "ResolutionCheck",
            costs: ["a", "b"],
            failureModes: ["f1"]);

        Assert.Equal(PowerOpportunity.EstimateCost(d), PowerOpportunity.EstimateCost(d));
        Assert.Equal(PowerOpportunity.EstimateRisk(d), PowerOpportunity.EstimateRisk(d));
    }
}
