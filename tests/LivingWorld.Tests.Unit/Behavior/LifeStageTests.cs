using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 3: <see cref="LifeStageRules"/> resolve <see cref="LifeStage"/> a
/// partir de limiares do cenário (nunca constante em C#, R3) — base para a rotina diária
/// (NEEDS-10).</summary>
public class LifeStageTests
{
    private static readonly LifeStageRules Rules = LifeStageRules.Create(childMaxAge: 12, adultMaxAge: 59).Value!;

    [Fact]
    public void LifeStageOf_returns_child_at_or_below_child_max_age()
    {
        Assert.Equal(LifeStage.Child, Rules.LifeStageOf(0));
        Assert.Equal(LifeStage.Child, Rules.LifeStageOf(12));
    }

    [Fact]
    public void LifeStageOf_returns_adult_between_child_and_adult_max_age()
    {
        Assert.Equal(LifeStage.Adult, Rules.LifeStageOf(13));
        Assert.Equal(LifeStage.Adult, Rules.LifeStageOf(59));
    }

    [Fact]
    public void LifeStageOf_returns_elder_above_adult_max_age()
    {
        Assert.Equal(LifeStage.Elder, Rules.LifeStageOf(60));
        Assert.Equal(LifeStage.Elder, Rules.LifeStageOf(200));
    }

    [Fact]
    public void Create_rejects_adult_max_age_less_than_or_equal_to_child_max_age()
    {
        var equal = LifeStageRules.Create(childMaxAge: 12, adultMaxAge: 12);
        var less = LifeStageRules.Create(childMaxAge: 12, adultMaxAge: 5);

        Assert.False(equal.IsSuccess);
        Assert.False(less.IsSuccess);
    }

    [Fact]
    public void Create_rejects_negative_child_max_age()
    {
        var result = LifeStageRules.Create(childMaxAge: -1, adultMaxAge: 10);

        Assert.False(result.IsSuccess);
    }
}
