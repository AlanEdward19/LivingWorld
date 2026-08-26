using LivingWorld.Domain;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>Fase 16.3, T18 (COH-31): catálogo cenário-driven de pesos de utility de poder.</summary>
public class PowerUtilityRulesTests
{
    private static Result<PowerUtilityRules> CreateValid(
        double costWeight = 1.0,
        double riskWeight = 1.0,
        double reliabilityWeight = 1.0,
        double urgencyWeight = 1.0) =>
        PowerUtilityRules.Create(costWeight, riskWeight, reliabilityWeight, urgencyWeight);

    [Fact]
    public void Create_accepts_valid_non_negative_weights()
    {
        var result = CreateValid(0.5, 1.0, 2.0, 0.0);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0.5, result.Value!.CostWeight);
        Assert.Equal(1.0, result.Value.RiskWeight);
        Assert.Equal(2.0, result.Value.ReliabilityWeight);
        Assert.Equal(0.0, result.Value.UrgencyWeight);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_cost_weight(double weight)
    {
        var result = CreateValid(costWeight: weight);

        Assert.False(result.IsSuccess);
        Assert.Contains("CostWeight", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_risk_weight(double weight)
    {
        var result = CreateValid(riskWeight: weight);

        Assert.False(result.IsSuccess);
        Assert.Contains("RiskWeight", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_reliability_weight(double weight)
    {
        var result = CreateValid(reliabilityWeight: weight);

        Assert.False(result.IsSuccess);
        Assert.Contains("ReliabilityWeight", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_urgency_weight(double weight)
    {
        var result = CreateValid(urgencyWeight: weight);

        Assert.False(result.IsSuccess);
        Assert.Contains("UrgencyWeight", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_has_documented_unit_weights()
    {
        var rules = PowerUtilityRules.Default;

        Assert.Equal(PowerUtilityRules.DefaultCostWeight, rules.CostWeight);
        Assert.Equal(PowerUtilityRules.DefaultRiskWeight, rules.RiskWeight);
        Assert.Equal(PowerUtilityRules.DefaultReliabilityWeight, rules.ReliabilityWeight);
        Assert.Equal(PowerUtilityRules.DefaultUrgencyWeight, rules.UrgencyWeight);
    }

    [Fact]
    public void Resolve_returns_Default_when_declared_is_null()
    {
        Assert.Same(PowerUtilityRules.Default, PowerUtilityRules.Resolve(null));
    }

    [Fact]
    public void Resolve_returns_declared_when_present()
    {
        var declared = CreateValid(2.0, 3.0, 4.0, 5.0).Value!;

        Assert.Same(declared, PowerUtilityRules.Resolve(declared));
    }
}
