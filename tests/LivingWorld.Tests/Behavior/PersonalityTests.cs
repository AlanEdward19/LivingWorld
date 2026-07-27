using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 1: os 10 traços de <see cref="Personality"/> validados em
/// <c>[0,100]</c> — base para NEEDS-06/NEEDS-08 (peso de personalidade no utility AI).</summary>
public class PersonalityTests
{
    private static Result<Personality> CreateWithOverride(string trait, int value)
    {
        int e = trait == "Extroversion" ? value : 50;
        int a = trait == "Agreeableness" ? value : 50;
        int c = trait == "Conscientiousness" ? value : 50;
        int es = trait == "EmotionalStability" ? value : 50;
        int o = trait == "Openness" ? value : 50;
        int am = trait == "Ambition" ? value : 50;
        int l = trait == "Loyalty" ? value : 50;
        int al = trait == "Altruism" ? value : 50;
        int im = trait == "Impulsivity" ? value : 50;
        int ra = trait == "RiskAversion" ? value : 50;
        return Personality.Create(e, a, c, es, o, am, l, al, im, ra);
    }

    [Fact]
    public void Create_with_all_traits_at_midpoint_succeeds_golden_path()
    {
        var result = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.Extroversion);
        Assert.Equal(50, result.Value!.Agreeableness);
        Assert.Equal(50, result.Value!.Conscientiousness);
        Assert.Equal(50, result.Value!.EmotionalStability);
        Assert.Equal(50, result.Value!.Openness);
        Assert.Equal(50, result.Value!.Ambition);
        Assert.Equal(50, result.Value!.Loyalty);
        Assert.Equal(50, result.Value!.Altruism);
        Assert.Equal(50, result.Value!.Impulsivity);
        Assert.Equal(50, result.Value!.RiskAversion);
    }

    [Fact]
    public void Create_with_boundary_values_0_and_100_succeeds()
    {
        var result = Personality.Create(0, 100, 0, 100, 0, 100, 0, 100, 0, 100);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("Extroversion")]
    [InlineData("Agreeableness")]
    [InlineData("Conscientiousness")]
    [InlineData("EmotionalStability")]
    [InlineData("Openness")]
    [InlineData("Ambition")]
    [InlineData("Loyalty")]
    [InlineData("Altruism")]
    [InlineData("Impulsivity")]
    [InlineData("RiskAversion")]
    public void Create_fails_naming_the_trait_below_zero(string trait)
    {
        var result = CreateWithOverride(trait, -1);

        Assert.False(result.IsSuccess);
        Assert.Contains(trait, result.Error);
    }

    [Theory]
    [InlineData("Extroversion")]
    [InlineData("Agreeableness")]
    [InlineData("Conscientiousness")]
    [InlineData("EmotionalStability")]
    [InlineData("Openness")]
    [InlineData("Ambition")]
    [InlineData("Loyalty")]
    [InlineData("Altruism")]
    [InlineData("Impulsivity")]
    [InlineData("RiskAversion")]
    public void Create_fails_naming_the_trait_above_100(string trait)
    {
        var result = CreateWithOverride(trait, 101);

        Assert.False(result.IsSuccess);
        Assert.Contains(trait, result.Error);
    }
}
