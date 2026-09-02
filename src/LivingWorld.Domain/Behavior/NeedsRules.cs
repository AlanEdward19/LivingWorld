using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Behavior;

/// <summary>Todo parâmetro numérico do utility AI e das necessidades (Fase 4, task 4),
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
    double HomelessSleepEfficiency)
{
    public static Result<NeedsRules> Create(
        double hungerDecayPerHour, double thirstDecayPerHour, double sleepDecayPerHour, double socialDecayPerHour,
        int urgencyThreshold, int maxActionSelectionSteps, bool hysteresisEnabled, double continuityBonus,
        double homelessSleepEfficiency)
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

        return Result<NeedsRules>.Ok(new NeedsRules(
            hungerDecayPerHour, thirstDecayPerHour, sleepDecayPerHour, socialDecayPerHour,
            urgencyThreshold, maxActionSelectionSteps, hysteresisEnabled, continuityBonus, homelessSleepEfficiency));
    }
}
