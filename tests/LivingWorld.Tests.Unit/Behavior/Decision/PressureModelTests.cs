using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Behavior.Decision;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>AD-138 (achado 11): as pressões do rastro são os termos da utilidade da ação vencedora, agrupados — antes eram
/// derivadas ao lado da escolha (<c>ProtectHousehold</c>, <c>EarnIncome</c> por ambição) sem pesar em nada. Os testes antigos
/// verificavam essas pressões inventadas; foram reescritos para a mesma intenção (pressão nomeada com fatores) sobre os termos
/// que decidem.</summary>
public class PressureModelTests
{
    [Fact]
    public void Hunger_term_becomes_AcquireFood_with_its_weight()
    {
        var pressures = PressureModel.FromTerms(
        [
            new UtilityContribution(ActionType.Eat, UtilityTerm.Hunger, 80),
            new UtilityContribution(ActionType.Eat, UtilityTerm.Comfort, 8),
        ]);

        var food = Assert.Single(pressures, p => p.Kind == PressureModel.AcquireFood);
        Assert.Equal(80, food.Intensity);
        Assert.Contains("Hunger", food.Factors);
        Assert.Contains(pressures, p => p.Kind == "Comfort" && p.Intensity == 8);
    }

    [Fact]
    public void Negative_terms_never_become_pressures()
    {
        var pressures = PressureModel.FromTerms(
        [
            new UtilityContribution(ActionType.Work, UtilityTerm.Baseline, 50),
            new UtilityContribution(ActionType.Work, UtilityTerm.Grief, -30),
        ]);

        Assert.DoesNotContain(pressures, p => p.Kind == "Grief");
        Assert.Contains(pressures, p => p.Kind == "Baseline" && p.Intensity == 50);
    }

    [Fact]
    public void Income_need_becomes_EarnIncome()
    {
        var pressures = PressureModel.FromTerms(
        [
            new UtilityContribution(ActionType.Work, UtilityTerm.Baseline, 50),
            new UtilityContribution(ActionType.Work, UtilityTerm.IncomeNeed, 25),
        ]);

        var earn = Assert.Single(pressures, p => p.Kind == PressureModel.EarnIncome);
        Assert.Equal(25, earn.Intensity);
        Assert.Equal(["IncomeNeed"], earn.Factors);
    }

    [Fact]
    public void Terms_of_the_same_pressure_are_summed_largest_first()
    {
        var pressures = PressureModel.FromTerms(
        [
            new UtilityContribution(ActionType.Buy, UtilityTerm.Provision, 90),
            new UtilityContribution(ActionType.Buy, UtilityTerm.FamineMemory, 12),
        ]);

        var food = Assert.Single(pressures);
        Assert.Equal(102, food.Intensity);
        Assert.Equal(["Provision", "FamineMemory"], food.Factors);
    }

    [Fact]
    public void FromTerms_is_deterministic()
    {
        UtilityContribution[] terms =
        [
            new(ActionType.Sleep, UtilityTerm.Sleep, 70),
            new(ActionType.Sleep, UtilityTerm.Personality, 4),
            new(ActionType.Sleep, UtilityTerm.Continuity, 10),
        ];

        var a = PressureModel.FromTerms(terms);
        var b = PressureModel.FromTerms(terms);

        Assert.Equal(a.Select(p => (p.Kind, p.Intensity)), b.Select(p => (p.Kind, p.Intensity)));
        Assert.Equal(a.SelectMany(p => p.Factors), b.SelectMany(p => p.Factors));
    }
}
