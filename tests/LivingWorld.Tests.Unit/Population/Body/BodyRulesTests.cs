using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>Fase 16.3, T6 (COH-21): catálogo cenário-driven de parâmetros corporais.</summary>
public class BodyRulesTests
{
    private static Result<BodyRules> CreateValid(
        double heightMean = 1.70,
        double heightStdDev = 0.08,
        double weightMean = 68.0,
        double weightStdDev = 10.0,
        double muscleMassMean = 28.0,
        double muscleMassStdDev = 6.0,
        double muscleMassMin = 8.0,
        double muscleMassMax = 55.0,
        bool enabled = true) =>
        BodyRules.Create(
            heightMean, heightStdDev, weightMean, weightStdDev,
            muscleMassMean, muscleMassStdDev, muscleMassMin, muscleMassMax, enabled);

    [Fact]
    public void Create_accepts_valid_parameters()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Enabled);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_height_stddev(double stdDev)
    {
        var result = CreateValid(heightStdDev: stdDev);

        Assert.False(result.IsSuccess);
        Assert.Contains("HeightStdDev", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_weight_stddev(double stdDev)
    {
        var result = CreateValid(weightStdDev: stdDev);

        Assert.False(result.IsSuccess);
        Assert.Contains("WeightStdDev", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    public void Create_rejects_negative_muscle_mass_stddev(double stdDev)
    {
        var result = CreateValid(muscleMassStdDev: stdDev);

        Assert.False(result.IsSuccess);
        Assert.Contains("MuscleMassStdDev", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_muscle_mass_min_greater_than_max()
    {
        var result = CreateValid(muscleMassMin: 60.0, muscleMassMax: 10.0);

        Assert.False(result.IsSuccess);
        Assert.Contains("MuscleMassMin", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_has_plausible_adult_medieval_values()
    {
        var rules = BodyRules.Default;

        Assert.True(rules.Enabled);
        Assert.Equal(1.70, rules.HeightMean);
        Assert.Equal(0.08, rules.HeightStdDev);
        Assert.Equal(68.0, rules.WeightMean);
        Assert.Equal(10.0, rules.WeightStdDev);
        Assert.Equal(28.0, rules.MuscleMassMean);
        Assert.Equal(6.0, rules.MuscleMassStdDev);
        Assert.Equal(8.0, rules.MuscleMassMin);
        Assert.Equal(55.0, rules.MuscleMassMax);
        Assert.True(rules.HeightMin < rules.HeightMax);
        Assert.True(rules.WeightMin < rules.WeightMax);
        Assert.True(rules.MuscleMassMin <= rules.MuscleMassMean);
        Assert.True(rules.MuscleMassMean <= rules.MuscleMassMax);
    }

    [Fact]
    public void Disabled_is_Default_with_Enabled_false()
    {
        Assert.False(BodyRules.Disabled.Enabled);
        Assert.Equal(BodyRules.Default.HeightMean, BodyRules.Disabled.HeightMean);
    }

    [Fact]
    public void Resolve_returns_Default_when_declared_is_null()
    {
        Assert.Same(BodyRules.Default, BodyRules.Resolve(null));
    }
}
