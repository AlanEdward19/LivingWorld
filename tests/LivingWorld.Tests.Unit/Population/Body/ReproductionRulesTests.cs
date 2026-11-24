using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>AD-130: reprodução de época por idade e sexo, gestação com prematuros e risco obstétrico por idade, paridade e
/// saúde. Os limites conferidos aqui são as faixas pedidas pelo dono, não os valores exatos da regra.</summary>
public class ReproductionRulesTests
{
    private static readonly ReproductionRules Rules = ScenarioRunner.DefaultPopulationRules.Fertility;

    [Fact]
    public void Default_rules_validate() => Assert.True(Rules.Validate().IsSuccess);

    [Fact]
    public void Female_fecundability_rises_in_adolescence_and_peaks_in_the_twenties()
    {
        var f = Rules.FemaleMonthlyFecundability;
        Assert.True(f.At(15) < f.At(18) && f.At(18) < f.At(25));
    }

    [Fact]
    public void Female_fecundability_falls_after_35_and_ends_by_50()
    {
        var f = Rules.FemaleMonthlyFecundability;
        Assert.True(f.At(38) < f.At(28) && f.At(44) < f.At(38) && f.At(50) == 0);
    }

    [Fact]
    public void Every_woman_is_sterile_by_50_and_few_are_at_20()
    {
        Assert.Equal(0.0, Rules.FemaleCycleFecundability(50, sterilityDraw: 0.999));
        Assert.True(Rules.FemaleSterileFraction.At(20) < 0.1);
    }

    [Fact]
    public void Men_stay_fertile_after_45_with_a_mild_decline()
    {
        var m = Rules.MaleFertility;
        Assert.True(m.At(60) > 0.5 && m.At(60) < m.At(30));
    }

    [Fact]
    public void Gestation_centers_near_38_weeks_from_conception_with_a_preterm_tail()
    {
        var rng = new WorldRng(7);
        const int draws = 20_000;
        long sum = 0;
        int preterm = 0, veryPreterm = 0, postTerm = 0;
        for (int i = 0; i < draws; i++)
        {
            var (days, band) = Rules.RollGestation(rng);
            sum += days;
            if (band is not null) preterm++;
            if (days < 210) veryPreterm++;
            if (days > 280) postTerm++;
        }

        Assert.InRange(sum / (double)draws, 259, 273); // 37 a 39 semanas desde a concepção
        Assert.InRange(preterm / (double)draws, 0.05, 0.10);
        Assert.InRange(veryPreterm / (double)draws, 0.005, 0.03);
        Assert.True(postTerm > 0);
    }

    [Fact]
    public void Very_preterm_newborns_rarely_survive_the_first_month()
    {
        var band = Rules.PretermBandOf(170)!;
        Assert.True(band.NeonatalDeathRisk >= 0.8);
        Assert.True(Rules.PretermBandOf(230)!.NeonatalDeathRisk < band.NeonatalDeathRisk);
        Assert.Null(Rules.PretermBandOf(268));
    }

    [Theory]
    [InlineData(14, 0, 100)] // adolescência precoce
    [InlineData(42, 1, 100)] // depois dos 35
    [InlineData(25, 9, 100)] // muitos partos
    [InlineData(25, 1, 30)]  // saúde ruim
    public void Obstetric_risk_is_higher_than_for_a_healthy_25_year_old_second_birth(int age, int previousBirths, int health) =>
        Assert.True(Rules.ObstetricRiskMultiplier(age, previousBirths, health) > Rules.ObstetricRiskMultiplier(25, 1, 100));

    [Fact]
    public void Early_pregnancy_loss_is_10_to_20_percent_in_the_twenties_and_higher_after_40()
    {
        var loss = Rules.MiscarriageRiskByAge;
        Assert.InRange(loss.At(25), 0.08, 0.20);
        Assert.True(loss.At(42) > 2 * loss.At(25));
    }

    [Fact]
    public void Twins_are_about_one_percent_of_births() => Assert.InRange(Rules.TwinRate, 0.01, 0.015);

    [Fact]
    public void Preterm_probabilities_above_one_are_rejected() =>
        Assert.False((Rules with { PretermBands = [new PretermBand(0.7, 200, 210, 0, 1), new PretermBand(0.7, 211, 240, 0, 1)] })
            .Validate().IsSuccess);

    [Fact]
    public void Postpartum_infecundity_makes_birth_intervals_of_two_to_three_years()
    {
        // intervalo esperado ≈ gestação + infecundidade pós-parto + espera de concepção no pico (1/fecundabilidade)
        double months = Rules.TermMeanDays / 30.0 + Rules.PostpartumInfecundMeanDays / 30.0
            + 1.0 / Rules.FemaleMonthlyFecundability.At(25);
        Assert.InRange(months, 24, 36);
    }

    [Fact]
    public void Marriage_age_window_binds_the_woman_but_not_the_man()
    {
        var population = ScenarioRunner.DefaultPopulationRules;
        Assert.True(population.IsMarriageableAge(Sex.Male, 60) && !population.IsMarriageableAge(Sex.Female, 60));
    }
}
