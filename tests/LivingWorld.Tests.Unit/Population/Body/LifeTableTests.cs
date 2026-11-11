using LivingWorld.Domain.Population.Body;

namespace LivingWorld.Tests.Unit.Population.Body;

public class LifeTableTests
{
    private static readonly LifeTableBracket[] ValidBrackets =
    [
        new(0, 9, 0.05),
        new(10, 19, 0.01),
    ];

    [Fact]
    public void Contiguous_brackets_covering_MaxLongevityYears_build_successfully()
    {
        var result = LifeTable.Create(20, ValidBrackets);
        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public void Gap_between_brackets_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 9, 0.05), new LifeTableBracket(11, 19, 0.01)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Overlap_between_brackets_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 10, 0.05), new LifeTableBracket(9, 19, 0.01)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Brackets_not_reaching_MaxLongevityYears_are_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 9, 0.05)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Mortality_outside_zero_one_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 19, 1.5)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Age_at_or_past_max_longevity_has_certain_mortality()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        Assert.Equal(1.0, table.AnnualMortality(20, health: 100));
        Assert.Equal(1.0, table.AnnualMortality(25, health: 100));
    }

    [Fact]
    public void Worse_health_never_lowers_mortality()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        double healthy = table.AnnualMortality(5, health: 100);
        double sickly = table.AnnualMortality(5, health: 20);
        Assert.True(sickly >= healthy);
    }

    // Fase 7, T9 (FAM-21): AnnualMortality ganha vitalityMultiplier opcional — default 1.0
    // preserva o comportamento anterior (testes acima, sem o parâmetro, continuam intactos).

    [Fact]
    public void Default_vitality_multiplier_matches_pre_T9_result()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        Assert.Equal(table.AnnualMortality(5, health: 100), table.AnnualMortality(5, health: 100, vitalityMultiplier: 1.0));
    }

    [Fact]
    public void Vitality_multiplier_below_one_reduces_mortality_in_same_bracket()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        double baseline = table.AnnualMortality(5, health: 100);
        double reduced = table.AnnualMortality(5, health: 100, vitalityMultiplier: 0.5);
        Assert.True(reduced < baseline);
    }

    [Fact]
    public void Vitality_multiplier_above_one_increases_mortality_in_same_bracket()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        double baseline = table.AnnualMortality(5, health: 100);
        double increased = table.AnnualMortality(5, health: 100, vitalityMultiplier: 2.0);
        Assert.True(increased > baseline);
    }

    [Fact]
    public void Large_vitality_multiplier_never_pushes_mortality_above_one()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        double p = table.AnnualMortality(5, health: 20, vitalityMultiplier: 1000.0);
        Assert.InRange(p, 0.0, 1.0);
    }
    // AD-130: a tábua do cenário padrão é de época (pré-industrial), não a otimista de antes (e0 ≈ 51, 80‰ no 1º ano).
    private static (double E0, double L15) Summary(LifeTable table)
    {
        double l = 1, e0 = 0, l15 = 0;
        for (int age = 0; age < table.MaxLongevityYears; age++)
        {
            if (age == 15) l15 = l;
            double d = l * table.AnnualMortality(age, health: 100);
            e0 += l - (age == 0 ? 0.7 : 0.5) * d;
            l -= d;
        }

        return (e0, l15);
    }

    [Fact]
    public void Default_scenario_life_expectancy_at_birth_is_pre_industrial() =>
        Assert.InRange(Summary(LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultPopulationRules.LifeTable).E0, 25, 35);

    [Fact]
    public void Default_scenario_first_year_mortality_is_150_to_300_per_thousand() =>
        Assert.InRange(LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultPopulationRules.LifeTable.AnnualMortality(0, 100), 0.15, 0.30);

    [Fact]
    public void Default_scenario_has_about_half_of_newborns_reaching_15() =>
        Assert.InRange(Summary(LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultPopulationRules.LifeTable).L15, 0.5, 0.65);

    [Fact]
    public void Default_scenario_adult_risk_is_about_one_percent_a_year_and_rises_in_old_age()
    {
        var table = LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultPopulationRules.LifeTable;
        Assert.InRange(table.AnnualMortality(30, 100), 0.007, 0.02);
        Assert.True(table.AnnualMortality(70, 100) > 4 * table.AnnualMortality(30, 100));
    }

    [Fact]
    public void Period_mortality_of_a_full_year_equals_the_annual_value() =>
        Assert.Equal(LifeTable.PreIndustrial.AnnualMortality(20, 80), LifeTable.PreIndustrial.PeriodMortality(20, 80, 1.0, 1.0), 12);
}
