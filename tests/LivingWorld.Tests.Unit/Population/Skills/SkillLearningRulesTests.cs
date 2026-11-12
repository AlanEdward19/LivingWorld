using LivingWorld.Domain.Population.Skills;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>AD-132: a curva de aptidão por idade e a habilidade esperada de quem praticou desde criança.</summary>
public class SkillLearningRulesTests
{
    private static readonly SkillLearningRules Rules = SkillLearningRules.Default;

    [Fact]
    public void Aptitude_is_zero_for_small_children_and_grows_through_adolescence_to_full_in_adulthood()
    {
        double[] at = [Rules.Aptitude(1), Rules.Aptitude(4), Rules.Aptitude(7), Rules.Aptitude(13), Rules.Aptitude(20)];

        Assert.True(at[0] == 0 && at[1] == 0 && at[2] > 0 && at[3] > at[2] && at[4] == 1.0 && at[3] < 1.0,
            string.Join(", ", at));
    }

    [Fact]
    public void Expected_skill_of_a_lifelong_practitioner_has_diminishing_returns_and_a_fifteen_year_old_is_well_below_a_thirty_year_old()
    {
        double E(double age) => Rules.ExpectedPracticedSkill(age, 0.02, 100, 30, 12);

        double at15 = E(15), at20 = E(20), at30 = E(30), at40 = E(40);

        Assert.True(at15 < at30 / 2 && at20 - at15 > at40 - at30 && at40 > at30,
            $"15: {at15}, 20: {at20}, 30: {at30}, 40: {at40}");
    }

    [Fact]
    public void Ageless_rules_keep_the_old_model_with_full_aptitude_and_no_loss()
    {
        var ageless = SkillLearningRules.Ageless;

        Assert.True(ageless.Aptitude(0) == 1.0 && ageless.LossFraction(80, practiced: false, physical: true, 30, 360) == 0);
    }

    [Fact]
    public void Create_rejects_ages_out_of_order()
    {
        var result = SkillLearningRules.Create([new(5, 0), new(5, 1)], 0.5, 12, 0.04, 55, 0.015, 30);

        Assert.False(result.IsSuccess);
    }
}
