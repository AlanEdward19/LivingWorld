using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 6, task 2 (SKILL-02): curva de retornos decrescentes — função pura, sem
/// <c>ScenarioRunner</c>, sem seed.</summary>
public class SkillCurveTests
{
    private const double Cap = 1000.0;
    private const double BaseRate = 1.0;

    [Fact]
    public void Gain_never_increases_as_level_rises_from_1_to_1000()
    {
        double previous = SkillCurve.Gain(1, Cap, BaseRate);

        for (int n = 2; n <= 1000; n++)
        {
            double current = SkillCurve.Gain(n, Cap, BaseRate);
            Assert.True(current <= previous, $"Gain({n}) = {current} > Gain({n - 1}) = {previous}");
            previous = current;
        }
    }

    [Fact]
    public void Gain_is_pure_same_input_always_produces_same_output()
    {
        double first = SkillCurve.Gain(42, Cap, BaseRate);
        double second = SkillCurve.Gain(42, Cap, BaseRate);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Gain_with_level_zero_or_negative_does_not_throw_and_is_non_negative(double currentSkill)
    {
        double gain = SkillCurve.Gain(currentSkill, Cap, BaseRate);

        Assert.True(gain >= 0);
    }

    [Fact]
    public void Gain_at_or_above_cap_is_non_negative()
    {
        Assert.True(SkillCurve.Gain(Cap, Cap, BaseRate) >= 0);
        Assert.True(SkillCurve.Gain(Cap * 2, Cap, BaseRate) >= 0);
    }

    [Fact]
    public void Gain_decreases_as_baseRate_alone_scales_the_curve_but_never_negative()
    {
        double gainHalfway = SkillCurve.Gain(Cap / 2, Cap, BaseRate);
        double gainNearCap = SkillCurve.Gain(Cap * 0.9, Cap, BaseRate);

        Assert.True(gainNearCap < gainHalfway);
        Assert.True(gainNearCap >= 0);
    }
}
