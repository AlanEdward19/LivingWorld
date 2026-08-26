using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3, T25 (COH-41/43): limiares cenário-driven do Attention Router.</summary>
public class AttentionRulesTests
{
    private static Result<AttentionRules> CreateValid(
        double minPriceChangeMagnitude = 0.05,
        int maxLocationDistanceCells = 8,
        double minRelationshipStrength = 10.0,
        int threatRadiusCells = 4,
        bool enabled = true) =>
        AttentionRules.Create(
            minPriceChangeMagnitude, maxLocationDistanceCells, minRelationshipStrength,
            threatRadiusCells, enabled);

    [Fact]
    public void Create_accepts_valid_thresholds()
    {
        var result = CreateValid(0.01, 4, 5.0, 2, enabled: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.01, result.Value!.MinPriceChangeMagnitude);
        Assert.Equal(4, result.Value.MaxLocationDistanceCells);
        Assert.Equal(5.0, result.Value.MinRelationshipStrength);
        Assert.Equal(2, result.Value.ThreatRadiusCells);
        Assert.True(result.Value.Enabled);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_min_price_change_magnitude(double value)
    {
        var result = CreateValid(minPriceChangeMagnitude: value);

        Assert.False(result.IsSuccess);
        Assert.Contains("MinPriceChangeMagnitude", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-8)]
    public void Create_rejects_negative_max_location_distance(int value)
    {
        var result = CreateValid(maxLocationDistanceCells: value);

        Assert.False(result.IsSuccess);
        Assert.Contains("MaxLocationDistanceCells", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-10.0)]
    public void Create_rejects_negative_min_relationship_strength(double value)
    {
        var result = CreateValid(minRelationshipStrength: value);

        Assert.False(result.IsSuccess);
        Assert.Contains("MinRelationshipStrength", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-4)]
    public void Create_rejects_negative_threat_radius(int value)
    {
        var result = CreateValid(threatRadiusCells: value);

        Assert.False(result.IsSuccess);
        Assert.Contains("ThreatRadiusCells", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_has_documented_thresholds()
    {
        var rules = AttentionRules.Default;

        Assert.Equal(AttentionRules.DefaultMinPriceChangeMagnitude, rules.MinPriceChangeMagnitude);
        Assert.Equal(AttentionRules.DefaultMaxLocationDistanceCells, rules.MaxLocationDistanceCells);
        Assert.Equal(AttentionRules.DefaultMinRelationshipStrength, rules.MinRelationshipStrength);
        Assert.Equal(AttentionRules.DefaultThreatRadiusCells, rules.ThreatRadiusCells);
        Assert.True(rules.Enabled);
    }

    [Fact]
    public void Resolve_returns_Default_when_declared_is_null()
    {
        Assert.Same(AttentionRules.Default, AttentionRules.Resolve(null));
    }

    [Fact]
    public void Resolve_returns_declared_when_present()
    {
        var declared = CreateValid(0.1, 2, 1.0, 1).Value!;

        Assert.Same(declared, AttentionRules.Resolve(declared));
    }
}
