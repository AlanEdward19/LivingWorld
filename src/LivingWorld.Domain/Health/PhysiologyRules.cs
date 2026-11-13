using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Health;

/// <summary>Saúde como reserva (AD-134), dado de cenário. Uma vez por dia o estado alimentar alimenta a desnutrição acumulada
/// (média móvel de <see cref="UndernutritionTimeConstantDays"/> dias, 0 = bem nutrido); a saúde cai com fome e sede em zero,
/// doença e velhice (o teto da idade) e se recupera com sono e comida, mais devagar nos velhos. A desnutrição multiplica o risco
/// mensal de morte (mais nas crianças) e quem está fraco trabalha menos e anda mais devagar. Números aproximados, precisam de
/// conferência.</summary>
public sealed record PhysiologyRules(
    bool Enabled,
    double UndernutritionTimeConstantDays,
    int MildHungerThreshold,
    double MildHungerDeficit,
    AgeCurve HealthCeilingByAge,
    AgeCurve RecoveryPerDayByAge,
    int DeclinePerDay,
    int StarvationHealthLossPerDay,
    int DehydrationHealthLossPerDay,
    double UndernutritionHealthPenalty,
    AgeCurve MalnutritionHazardByAge,
    AgeCurve StarvationSurvivalFactorByAge,
    int HealthFloor,
    int WeakHealthThreshold,
    double WorkFactorAtZeroHealth,
    double MovementPenaltyAtZeroHealth,
    double DeliveryHealthLoss,
    LactationRules Lactation)
{
    /// <summary>Nome das mortes que sobram da tábua (acidente, velhice), AD-137; só rotula, não muda o risco.</summary>
    public DeathCauseRules DeathCauses { get; init; } = DeathCauseRules.PreIndustrial;

    public static PhysiologyRules Default { get; } = new(
        Enabled: true,
        UndernutritionTimeConstantDays: 30,
        MildHungerThreshold: 10, MildHungerDeficit: 0.4,
        // o teto da saúde cai com a idade: quem tem 80 anos não volta a ter a reserva de um jovem
        HealthCeilingByAge: AgeCurve.Of((0, 100), (50, 100), (60, 92), (70, 82), (80, 70), (90, 58), (100, 50)),
        // recuperação: semanas no jovem, o dobro do tempo no velho
        RecoveryPerDayByAge: AgeCurve.Of((0, 2), (45, 2), (60, 1), (100, 1)),
        DeclinePerDay: 1,
        // jejum total leva a saúde de 100 a ~10 em ~45 dias; sem água, a reserva some em dias
        StarvationHealthLossPerDay: 2, DehydrationHealthLossPerDay: 15,
        UndernutritionHealthPenalty: 0.4,
        // desnutrição grave multiplica a mortalidade infantil por 2 a 5 (Pelletier 1994, aprox.); menos no adulto
        MalnutritionHazardByAge: AgeCurve.Of((0, 3.0), (5, 3.0), (10, 1.5), (60, 1.5), (80, 2.0)),
        // criança e idoso aguentam menos tempo de jejum que o adulto
        StarvationSurvivalFactorByAge: AgeCurve.Of((0, 0.15), (1, 0.3), (5, 0.6), (15, 1.0), (60, 1.0), (80, 0.7)),
        HealthFloor: 5, WeakHealthThreshold: 60, WorkFactorAtZeroHealth: 0.3, MovementPenaltyAtZeroHealth: 1.0,
        // parto: perda de reserva proporcional ao risco obstétrico (hemorragia, anemia), recuperada em semanas
        DeliveryHealthLoss: 12,
        Lactation: LactationRules.Default);

    /// <summary>Saúde estática (o modelo de antes): só exaustão, combate e poderes mexem nela.</summary>
    public static PhysiologyRules Static { get; } = Default with { Enabled = false, Lactation = LactationRules.Default with { Enabled = false } };

    /// <summary>Saúde relativa ao teto da idade, para a tábua de vida (que já conta a senescência): 100 no teto.</summary>
    public int RelativeHealth(double ageYears, int health) =>
        !Enabled ? health : Math.Clamp(100 - Math.Max(0, (int)Math.Round(HealthCeilingByAge.At(ageYears)) - health), 0, 100);

    /// <summary>Multiplicador do risco mensal pela desnutrição acumulada.</summary>
    public double MalnutritionMultiplier(double ageYears, double undernutrition) =>
        !Enabled || undernutrition <= 0 ? 1.0 : 1.0 + MalnutritionHazardByAge.At(ageYears) * Math.Clamp(undernutrition, 0, 1);

    /// <summary>Fator de trabalho pela saúde: 1 até <see cref="WeakHealthThreshold"/>, linear até
    /// <see cref="WorkFactorAtZeroHealth"/> em 0.</summary>
    public double WorkFactor(int health)
    {
        if (!Enabled || health >= WeakHealthThreshold || WeakHealthThreshold <= 0) return 1.0;
        double weakness = (WeakHealthThreshold - Math.Max(0, health)) / (double)WeakHealthThreshold;
        return 1.0 - (1.0 - WorkFactorAtZeroHealth) * weakness;
    }

    /// <summary>Multiplicador do custo de andar pela saúde: 1 até o limiar, até 1 + <see cref="MovementPenaltyAtZeroHealth"/>.</summary>
    public double MovementFactor(int health)
    {
        if (!Enabled || health >= WeakHealthThreshold || WeakHealthThreshold <= 0) return 1.0;
        return 1.0 + MovementPenaltyAtZeroHealth * (WeakHealthThreshold - Math.Max(0, health)) / WeakHealthThreshold;
    }

    public Result<PhysiologyRules> Validate()
    {
        if (UndernutritionTimeConstantDays <= 0) return Result<PhysiologyRules>.Fail("UndernutritionTimeConstantDays: deve ser > 0");
        foreach (var curve in new[] { HealthCeilingByAge, RecoveryPerDayByAge, MalnutritionHazardByAge, StarvationSurvivalFactorByAge })
            if (!curve.IsAscendingInAge() || curve.Points.Any(p => p.Value < 0))
                return Result<PhysiologyRules>.Fail("PhysiologyRules: curva fora de ordem ou com valor negativo");
        if (HealthFloor is < 0 or > 100 || WorkFactorAtZeroHealth is < 0 or > 1)
            return Result<PhysiologyRules>.Fail("HealthFloor/WorkFactorAtZeroHealth: fora de faixa");
        return Lactation.Validate() is { IsSuccess: false } bad
            ? Result<PhysiologyRules>.Fail(bad.Error!)
            : Result<PhysiologyRules>.Ok(this);
    }
}

/// <summary>Amamentação (AD-134). O lactente mama na mãe (ou em outra lactante da casa); exclusivo até
/// <see cref="ExclusiveDays"/>, desmame gradual até <see cref="WeaningEndDays"/>. Quem amamenta gasta mais comida (a fome dela
/// decai mais rápido na proporção do leite dado). Sem leite, o bebê come papa da despensa com risco multiplicado pela idade
/// (<see cref="UnnursedHazardByAgeMonths"/>). A morte do lactente encerra a amenorreia da lactação.</summary>
public sealed record LactationRules(
    bool Enabled,
    int ExclusiveDays,
    int WeaningEndDays,
    double HungerDecayExtraPerNursling,
    int LapseDays,
    int NursedRecentlyDays,
    AgeCurve UnnursedHazardByAgeMonths,
    int OvulationReturnDaysWithoutNursing,
    int MinPostpartumInfecundDaysWithoutNursing,
    double StillbirthRiskWhenMotherDies)
{
    public static LactationRules Default { get; } = new(
        Enabled: true,
        // amamentação exclusiva até ~6 meses, desmame entre 18 e 24 meses
        ExclusiveDays: 180, WeaningEndDays: 720,
        // lactação gasta ~500 kcal/dia, ~25% de uma ração de adulto
        HungerDecayExtraPerNursling: 0.25,
        LapseDays: 14, NursedRecentlyDays: 3,
        // lactente sem leite materno na época: mortalidade no 1º ano de 50% ou mais (Knodel, aprox.). O fator multiplica o
        // risco anual da tábua (~0,2 no 1º ano): acima de ~5 ele bate em 1 e mata no mês, mais que o registrado
        UnnursedHazardByAgeMonths: AgeCurve.Of((0, 4.5), (2, 4), (6, 3), (12, 2), (18, 1.3), (24, 1)),
        // sem sucção a ovulação volta em semanas (~6 semanas depois do parto, ~2–4 depois do desmame)
        OvulationReturnDaysWithoutNursing: 30, MinPostpartumInfecundDaysWithoutNursing: 45,
        // a mãe que morre no parto ainda dá à luz; o filho nasce morto em parte desses partos (sem fonte)
        StillbirthRiskWhenMotherDies: 0.3);

    /// <summary>Parte do alimento do bebê que vem do leite: 1 até o fim do exclusivo, cai linear até o desmame.</summary>
    public double MilkShare(double ageDays)
    {
        if (ageDays < ExclusiveDays) return 1.0;
        if (ageDays >= WeaningEndDays || WeaningEndDays <= ExclusiveDays) return 0.0;
        return 1.0 - (ageDays - ExclusiveDays) / (WeaningEndDays - ExclusiveDays);
    }

    public Result<LactationRules> Validate()
    {
        if (ExclusiveDays < 0 || WeaningEndDays < ExclusiveDays) return Result<LactationRules>.Fail("WeaningEndDays: antes do exclusivo");
        if (!UnnursedHazardByAgeMonths.IsAscendingInAge() || UnnursedHazardByAgeMonths.Points.Any(p => p.Value < 0))
            return Result<LactationRules>.Fail("UnnursedHazardByAgeMonths: fora de ordem ou negativa");
        if (StillbirthRiskWhenMotherDies is < 0 or > 1) return Result<LactationRules>.Fail("StillbirthRiskWhenMotherDies: fora de [0,1]");
        return Result<LactationRules>.Ok(this);
    }
}
