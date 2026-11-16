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
    int BaseMinGain = -500,
    int CriticalNeed = 0,
    bool FirstExperienceSetsExpectation = false,
    int BaseMaxGain = 1000,
    int GainStep = 1,
    double PantryDayValue = 0,
    double MoneyDayValue = 0,
    double ValueHorizonDays = 0,
    double WageHoursPerMonth = 0,
    bool InheritExpectation = false,
    bool WeighsLeisure = false)
{
    /// <summary>Ligado, com os números do desenho (`.specs/features/neural-brain/design.md`).</summary>
    public static BrainRules Default { get; } = new(
        Enabled: true, MinGain: -500, MaxGain: 1000,
        LearningPerPoint: 0.1, AdultLearningFloor: 0.25, LearningFullUntilYears: 20, LearningFloorFromYears: 60,
        MeanWindow: 8, InheritedNoiseMilli: 100, RearingPullPerSample: 0.01, CriticalNeed: 30,
        FirstExperienceSetsExpectation: true,
        BaseMinGain: 0, BaseMaxGain: 0, GainStep: 10,
        PantryDayValue: 20, MoneyDayValue: 10, ValueHorizonDays: 4, WageHoursPerMonth: 270,
        InheritExpectation: true, WeighsLeisure: true);

    /// <summary>O experimento como juntado no AD-142 (para comparar): sem nada do AD-160 nem do AD-160b.</summary>
    public static BrainRules Ad142 { get; } = Default with
    {
        CriticalNeed = 0, FirstExperienceSetsExpectation = false, BaseMinGain = -500, BaseMaxGain = 1000, GainStep = 1,
        PantryDayValue = 0, MoneyDayValue = 0, ValueHorizonDays = 0, WageHoursPerMonth = 0, InheritExpectation = false,
        WeighsLeisure = false,
    };

    /// <summary>Variantes nomeadas para os relatórios e as sondas (`LW_BRAIN_VARIANT`, `LW_MEASURE_BRAIN`); nulo = desconhecida.
    /// <c>frozen</c>: o mecanismo inteiro com ganhos presos em zero (escolhe como a utilidade: separa o custo do efeito no mundo).</summary>
    public static BrainRules? Variant(string? name) => name switch
    {
        null or "" or "1" or "default" => Default,
        "frozen" => Default with { LearningPerPoint = 0, InheritedNoiseMilli = 0, RearingPullPerSample = 0 },
        "ad142" => Ad142,
        "ad160" => Ad142 with { CriticalNeed = 20, FirstExperienceSetsExpectation = true },
        "body20" => Default with { CriticalNeed = 20 },
        "noleisure" => Default with { WeighsLeisure = false },
        "nolearn" => Default with { LearningPerPoint = 0 },
        "slow" => Default with { LearningPerPoint = 0.02 },
        _ => null,
    };

    /// <summary>AD-160b: o impulso do corpo (grupo Base: fome, sede, sono, despensa) tem o ganho preso em
    /// [<see cref="BaseMinGain"/>, <see cref="BaseMaxGain"/>] = [0, 0] no padrão: o corpo é o corpo; o que se aprende é quanto pesam o
    /// temperamento, o contexto, o humor, a pressão, a antevisão e o hábito. Antes o ganho de Comprar e Comer na base derivava com
    /// a experiência e a vila comia mais (AD-142) ou comprava menos (AD-160).</summary>
    public bool LearnsBodyDrive => BaseMinGain != 0 || BaseMaxGain != 0;

    /// <summary>AD-160b, valor que o NPC dá ao próprio estado (a recompensa de uma ação é a variação dele do começo ao fim dela):
    /// corpo (as quatro necessidades) + <see cref="PantryDayValue"/> por dia de comida e água na despensa por morador + <see
    /// cref="MoneyDayValue"/> por dia de comida que a moeda compra ao preço de referência, os dois até <see cref="ValueHorizonDays"/>,
    /// + 100 × humor. O trabalho soma o salário que a jornada rende (salário do mês ÷ <see cref="WageHoursPerMonth"/> × horas).
    /// <see cref="PantryDayValue"/> 0 = recompensa do AD-142 (alívio imediato e despensa só pelo déficit sentido).</summary>
    public bool ValuesSustenance => PantryDayValue > 0;

    /// <summary>AD-160: com fome ou sede em <see cref="CriticalNeed"/> ou abaixo (déficit crítico de 80, o mesmo que já põe o corpo
    /// à frente do lazer), o corpo decide pela utilidade pura e o ganho aprendido não pesa; o cérebro pesa o resto. 0 desliga
    /// (o experimento do AD-142).
    /// AD-160, <see cref="FirstExperienceSetsExpectation"/>: a primeira vez que o NPC conclui uma ação só forma a
    /// expectativa dele (o alívio que ela deu); não há surpresa sem expectativa. Desligado (AD-142), a expectativa começava em zero
    /// e toda ação que alivia (comer, dormir) parecia melhor do que o esperado nas primeiras vezes: o ganho de Comer subia em
    /// todo NPC e a vila passava a comer mais (sonda `brain-trace`: Idle→Eat, Work→Eat, Socialize→Eat).</summary>
    public bool BodyDecides(int hunger, int thirst) => hunger <= CriticalNeed || thirst <= CriticalNeed;

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
        (short)Math.Clamp((int)Math.Round(milli, MidpointRounding.AwayFromZero),
            group == 0 ? Math.Max(MinGain, BaseMinGain) : MinGain, group == 0 ? Math.Min(MaxGain, BaseMaxGain) : MaxGain);

    public static Result<BrainRules> Validate(BrainRules rules)
    {
        if (rules.MinGain < -1000 || rules.MinGain > 0) return Result<BrainRules>.Fail("Brain.MinGain: fora de [-1000,0]");
        if (rules.MaxGain < 0 || rules.MaxGain > short.MaxValue) return Result<BrainRules>.Fail("Brain.MaxGain: fora de [0,32767]");
        if (rules.CriticalNeed is < 0 or > 100) return Result<BrainRules>.Fail("Brain.CriticalNeed: fora de [0,100]");
        if (rules.BaseMaxGain < rules.BaseMinGain) return Result<BrainRules>.Fail("Brain.BaseMaxGain: abaixo de BaseMinGain");
        if (rules.GainStep < 1) return Result<BrainRules>.Fail("Brain.GainStep: deve ser >= 1");
        if (rules.PantryDayValue < 0 || rules.MoneyDayValue < 0 || rules.ValueHorizonDays < 0 || rules.WageHoursPerMonth < 0)
            return Result<BrainRules>.Fail("Brain: valores do estado não podem ser negativos");
        if (rules.MeanWindow <= 0) return Result<BrainRules>.Fail("Brain.MeanWindow: deve ser > 0");
        if (rules.LearningFloorFromYears <= rules.LearningFullUntilYears)
            return Result<BrainRules>.Fail("Brain.LearningFloorFromYears: deve passar de LearningFullUntilYears");
        return Result<BrainRules>.Ok(rules);
    }
}
