using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 4: <see cref="NeedsRules"/> valida cada parâmetro numérico do
/// utility AI, nomeando o campo fora de faixa — base para NEEDS-01/02/03/09/12/15.</summary>
public class NeedsRulesTests
{
    private static Result<NeedsRules> CreateValid(
        double hungerDecayPerHour = 4, double thirstDecayPerHour = 5, double sleepDecayPerHour = 6,
        double socialDecayPerHour = 2, int urgencyThreshold = 70, int maxActionSelectionSteps = 10,
        bool hysteresisEnabled = true, double continuityBonus = 5, double homelessSleepEfficiency = 0.5) =>
        NeedsRules.Create(
            hungerDecayPerHour, thirstDecayPerHour, sleepDecayPerHour, socialDecayPerHour,
            urgencyThreshold, maxActionSelectionSteps, hysteresisEnabled, continuityBonus, homelessSleepEfficiency);

    [Fact]
    public void Create_with_valid_values_succeeds()
    {
        var result = CreateValid();

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.HungerDecayPerHour);
        Assert.Equal(70, result.Value!.UrgencyThreshold);
        Assert.True(result.Value!.HysteresisEnabled);
    }

    [Theory]
    [InlineData(-1.0)]
    public void Create_fails_naming_hunger_decay_below_zero(double invalid)
    {
        var result = CreateValid(hungerDecayPerHour: invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains("HungerDecayPerHour", result.Error);
    }

    [Fact]
    public void Create_fails_naming_thirst_decay_below_zero()
    {
        var result = CreateValid(thirstDecayPerHour: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("ThirstDecayPerHour", result.Error);
    }

    [Fact]
    public void Create_fails_naming_sleep_decay_below_zero()
    {
        var result = CreateValid(sleepDecayPerHour: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("SleepDecayPerHour", result.Error);
    }

    [Fact]
    public void Create_fails_naming_social_decay_below_zero()
    {
        var result = CreateValid(socialDecayPerHour: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("SocialDecayPerHour", result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_fails_naming_urgency_threshold_out_of_range(int invalid)
    {
        var result = CreateValid(urgencyThreshold: invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains("UrgencyThreshold", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_fails_naming_max_action_selection_steps_not_positive(int invalid)
    {
        var result = CreateValid(maxActionSelectionSteps: invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains("MaxActionSelectionSteps", result.Error);
    }

    [Fact]
    public void Create_fails_naming_continuity_bonus_below_zero()
    {
        var result = CreateValid(continuityBonus: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("ContinuityBonus", result.Error);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_fails_naming_homeless_sleep_efficiency_out_of_range(double invalid)
    {
        var result = CreateValid(homelessSleepEfficiency: invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains("HomelessSleepEfficiency", result.Error);
    }
}
