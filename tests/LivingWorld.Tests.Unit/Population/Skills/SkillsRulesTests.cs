using LivingWorld.Domain.Population.Skills;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>Fase 6, task 6: catálogo cenário-driven de parâmetros de habilidade — teto,
/// taxa-base por fonte, mapeamento profissão→habilidade.</summary>
public class SkillsRulesTests
{
    private static IReadOnlyDictionary<SkillGainSource, double> ValidRates => new Dictionary<SkillGainSource, double>
    {
        [SkillGainSource.Practice] = 0.5,
    };

    private static IReadOnlyDictionary<int, SkillType> ValidProfessionMap => new Dictionary<int, SkillType>
    {
        [1] = new SkillType(0),
    };

    [Fact]
    public void Create_rejects_cap_less_than_or_equal_zero()
    {
        var result = SkillsRules.Create(0, ValidRates, ValidProfessionMap, new SkillType(6));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_negative_rate_for_a_source()
    {
        var rates = new Dictionary<SkillGainSource, double> { [SkillGainSource.Practice] = -0.1 };

        var result = SkillsRules.Create(100, rates, ValidProfessionMap, new SkillType(6));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_empty_skill_by_profession()
    {
        var result = SkillsRules.Create(100, ValidRates, new Dictionary<int, SkillType>(), new SkillType(6));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_accepts_valid_parameters()
    {
        var result = SkillsRules.Create(100, ValidRates, ValidProfessionMap, new SkillType(6));

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.Cap);
    }

    [Fact]
    public void Gain_multiplies_curve_result_by_rate_gene()
    {
        var rules = SkillsRules.Create(100, ValidRates, ValidProfessionMap, new SkillType(6)).Value!;

        double gainWithGeneOne = rules.Gain(currentSkill: 0, SkillGainSource.Practice, rateGene: 1.0);
        double gainWithGeneTwo = rules.Gain(currentSkill: 0, SkillGainSource.Practice, rateGene: 2.0);

        Assert.Equal(gainWithGeneOne * 2, gainWithGeneTwo, precision: 10);
    }

    [Fact]
    public void Gain_for_source_without_declared_rate_is_zero()
    {
        var rules = SkillsRules.Create(100, ValidRates, ValidProfessionMap, new SkillType(6)).Value!;

        double gain = rules.Gain(currentSkill: 0, SkillGainSource.School, rateGene: 1.0);

        Assert.Equal(0, gain);
    }
}
