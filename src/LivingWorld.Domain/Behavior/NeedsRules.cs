using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Behavior;

/// <summary>Todo parâmetro numérico do utility AI e das necessidades,
/// cenário-driven — nenhum literal em C# (R3), mesmo padrão de
/// <see cref="PopulationRules"/>.</summary>
public sealed record NeedsRules(
    double HungerDecayPerHour,
    double ThirstDecayPerHour,
    double SleepDecayPerHour,
    double SocialDecayPerHour,
    int UrgencyThreshold,
    int MaxActionSelectionSteps,
    bool HysteresisEnabled,
    double ContinuityBonus,
    double HomelessSleepEfficiency,
    double StarvationSurvivalHours = 0)
{
    /// <summary>Horas com a fome em zero até a morte. 0 = regra antiga (o tempo que a fome leva de 100 a 0, uns dois dias): o
    /// corpo aguenta semanas sem comer, e o cenário declara quantas (AD-128). A sede continua matando em dias.</summary>
    public long StarvationTicks =>
        StarvationSurvivalHours > 0 ? (long)Math.Ceiling(StarvationSurvivalHours)
        : HungerDecayPerHour > 0 ? (long)Math.Ceiling(100.0 / HungerDecayPerHour) : long.MaxValue;

    public const double DefaultStarvationSurvivalHours = 21 * 24;

    public static Result<NeedsRules> Create(
        double hungerDecayPerHour, double thirstDecayPerHour, double sleepDecayPerHour, double socialDecayPerHour,
        int urgencyThreshold, int maxActionSelectionSteps, bool hysteresisEnabled, double continuityBonus,
        double homelessSleepEfficiency, double starvationSurvivalHours = 0)
    {
        if (hungerDecayPerHour < 0) return Result<NeedsRules>.Fail("HungerDecayPerHour: deve ser >= 0");
        if (thirstDecayPerHour < 0) return Result<NeedsRules>.Fail("ThirstDecayPerHour: deve ser >= 0");
        if (sleepDecayPerHour < 0) return Result<NeedsRules>.Fail("SleepDecayPerHour: deve ser >= 0");
        if (socialDecayPerHour < 0) return Result<NeedsRules>.Fail("SocialDecayPerHour: deve ser >= 0");
        if (urgencyThreshold is < 0 or > 100) return Result<NeedsRules>.Fail("UrgencyThreshold: fora de [0,100]");
        if (maxActionSelectionSteps <= 0) return Result<NeedsRules>.Fail("MaxActionSelectionSteps: deve ser positivo");
        if (continuityBonus < 0) return Result<NeedsRules>.Fail("ContinuityBonus: deve ser >= 0");
        if (homelessSleepEfficiency is < 0 or > 1)
            return Result<NeedsRules>.Fail("HomelessSleepEfficiency: fora de [0,1]");

        if (starvationSurvivalHours < 0) return Result<NeedsRules>.Fail("StarvationSurvivalHours: deve ser >= 0");

        return Result<NeedsRules>.Ok(new NeedsRules(
            hungerDecayPerHour, thirstDecayPerHour, sleepDecayPerHour, socialDecayPerHour,
            urgencyThreshold, maxActionSelectionSteps, hysteresisEnabled, continuityBonus, homelessSleepEfficiency,
            starvationSurvivalHours));
    }
}
