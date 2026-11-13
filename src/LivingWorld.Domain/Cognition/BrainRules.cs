using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cognition;

/// <summary>Cérebro com ganhos sinápticos herdados e aprendidos (ADR-0046, AD-142). Experimento atrás de flag: o mundo só tem
/// estas regras quando o cenário liga (<c>{"Brain":{"Enabled":true}}</c>); sem elas o hash é o de antes. Ganhos em milésimos,
/// presos a [<see cref="MinGain"/>, <see cref="MaxGain"/>]: −500 deixa no mínimo metade de cada grupo, então a fome nunca some.
/// Números de partida sem fonte empírica (aproximados, a conferir): ruído de segregação, taxas e puxão da criação.</summary>
public sealed record BrainRules(
    bool Enabled,
    int MinGain,
    int MaxGain,
    double LearningPerPoint,
    double AdultLearningFloor,
    int LearningFullUntilYears,
    int LearningFloorFromYears,
    int MeanWindow,
    double InheritedNoiseMilli,
    double RearingPullPerSample,
    int BaseMinGain = -500)
{
    /// <summary>Ligado, com os números do desenho (`.specs/features/neural-brain/design.md`).</summary>
    public static BrainRules Default { get; } = new(
        Enabled: true, MinGain: -500, MaxGain: 1000,
        LearningPerPoint: 0.1, AdultLearningFloor: 0.25, LearningFullUntilYears: 20, LearningFloorFromYears: 60,
        MeanWindow: 8, InheritedNoiseMilli: 100, RearingPullPerSample: 0.01);

    /// <summary>Taxa de aprendizado pela idade: plena até <see cref="LearningFullUntilYears"/>, cai em linha reta até
    /// <see cref="AdultLearningFloor"/> em <see cref="LearningFloorFromYears"/> e fica ali (o adulto ainda aprende, devagar).</summary>
    public double LearningRate(double ageYears)
    {
        if (ageYears <= LearningFullUntilYears) return 1.0;
        if (ageYears >= LearningFloorFromYears) return AdultLearningFloor;
        double t = (ageYears - LearningFullUntilYears) / (LearningFloorFromYears - LearningFullUntilYears);
        return 1.0 - t * (1.0 - AdultLearningFloor);
    }

    public short Clamp(double milli) =>
        (short)Math.Clamp((int)Math.Round(milli, MidpointRounding.AwayFromZero), MinGain, MaxGain);

    /// <summary>Limite de um grupo: o grupo 0 (a base, o impulso que move a ação) tem piso próprio <see cref="BaseMinGain"/>.</summary>
    public short Clamp(double milli, int group) =>
        (short)Math.Clamp((int)Math.Round(milli, MidpointRounding.AwayFromZero), group == 0 ? Math.Max(MinGain, BaseMinGain) : MinGain, MaxGain);

    public static Result<BrainRules> Validate(BrainRules rules)
    {
        if (rules.MinGain < -1000 || rules.MinGain > 0) return Result<BrainRules>.Fail("Brain.MinGain: fora de [-1000,0]");
        if (rules.MaxGain < 0 || rules.MaxGain > short.MaxValue) return Result<BrainRules>.Fail("Brain.MaxGain: fora de [0,32767]");
        if (rules.MeanWindow <= 0) return Result<BrainRules>.Fail("Brain.MeanWindow: deve ser > 0");
        if (rules.LearningFloorFromYears <= rules.LearningFullUntilYears)
            return Result<BrainRules>.Fail("Brain.LearningFloorFromYears: deve passar de LearningFullUntilYears");
        return Result<BrainRules>.Ok(rules);
    }
}
