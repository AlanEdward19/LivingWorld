using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Corpo que cresce (AD-134), dado de cenário. Estatura adulta por sexo (média e desvio de época), fração dela por
/// idade (curva de crescimento com estirão: mais cedo nas meninas), encolhimento na velhice, comprimento e peso ao nascer pela
/// duração da gestação (prematuro nasce menor e alcança a curva em <see cref="PretermCatchUpYears"/>), peso pelo IMC da idade,
/// músculo como fração do peso por idade e sexo (sarcopenia depois dos 30) que sobe com trabalho físico e cai sem ele, e
/// nanismo nutricional: desnutrição enquanto cresce tira estatura adulta para sempre. Os números do <see cref="Default"/> são
/// aproximados (antropometria de época e curvas de referência citadas de memória) e precisam de conferência.</summary>
public sealed record GrowthRules(
    bool Enabled,
    double MaleAdultHeightMean,
    double MaleAdultHeightStdDev,
    double FemaleAdultHeightMean,
    double FemaleAdultHeightStdDev,
    double StatureHeritability,
    AgeCurve MaleHeightFraction,
    AgeCurve FemaleHeightFraction,
    AgeCurve ElderHeightFactor,
    AgeCurve BirthLengthByGestationDays,
    AgeCurve BirthWeightByGestationDays,
    double PretermCatchUpYears,
    AgeCurve MaleBmiByAge,
    AgeCurve FemaleBmiByAge,
    double FrameStdDev,
    AgeCurve MaleMuscleFraction,
    AgeCurve FemaleMuscleFraction,
    double TrainingMuscleGain,
    double MuscleAdaptationPerMonth,
    double WastingAtFullUndernutrition,
    double StuntingSensitivity,
    double StuntingUndernutritionThreshold,
    double GrowthEndAgeYears,
    double FemalePeakHeightVelocityAge,
    double MalePeakHeightVelocityAge,
    double ReferenceAdultMuscleMass,
    double ReferenceAdultBmi,
    double ShortStrideHeight)
{
    public static GrowthRules Default { get; } = new(
        Enabled: true,
        // esqueletos medievais ingleses (Roberts & Cox, aprox.): homens ~1,71 m, mulheres ~1,59 m; dp ~6–7 cm
        MaleAdultHeightMean: 1.71, MaleAdultHeightStdDev: 0.065,
        FemaleAdultHeightMean: 1.59, FemaleAdultHeightStdDev: 0.06,
        // herdabilidade da estatura ~0,8 (gêmeos, populações bem nutridas)
        StatureHeritability: 0.8,
        // fração da estatura adulta: ~50 cm ao nascer, ~75 cm com 1 ano, ~1,10 m aos 5; estirão ~13–15 anos nos meninos e
        // ~11–12 nas meninas, crescimento de época um pouco mais lento e mais longo (até ~20 anos)
        MaleHeightFraction: AgeCurve.Of((0, 0.292), (1, 0.44), (2, 0.51), (5, 0.635), (10, 0.785), (12, 0.83), (13, 0.86),
            (14, 0.905), (15, 0.945), (16, 0.97), (18, 0.99), (20, 1.0)),
        FemaleHeightFraction: AgeCurve.Of((0, 0.308), (1, 0.465), (2, 0.535), (5, 0.67), (10, 0.83), (11, 0.865), (12, 0.915),
            (13, 0.95), (14, 0.975), (16, 0.993), (18, 1.0)),
        // perde ~1 cm por década depois dos 40, mais depressa depois dos 70
        ElderHeightFactor: AgeCurve.Of((40, 1.0), (50, 0.995), (60, 0.988), (70, 0.977), (80, 0.962), (90, 0.948)),
        // comprimento e peso ao nascer relativos ao a termo (dias desde a concepção): ~28 semanas ≈ 36 cm e 1,0 kg
        BirthLengthByGestationDays: AgeCurve.Of((154, 0.62), (182, 0.72), (210, 0.83), (245, 0.94), (268, 1.0), (295, 1.02)),
        BirthWeightByGestationDays: AgeCurve.Of((154, 0.18), (182, 0.30), (210, 0.50), (245, 0.80), (268, 1.0), (295, 1.06)),
        PretermCatchUpYears: 2,
        // IMC por idade: ~13 ao nascer, ~17,5 com 1 ano, ~15,5 aos 5, adulto de época magro (~21–22)
        MaleBmiByAge: AgeCurve.Of((0, 13.2), (1, 17.5), (2, 16.5), (5, 15.4), (10, 16.2), (15, 19.2), (20, 21.3), (40, 22.3),
            (70, 21.8), (85, 21.0)),
        FemaleBmiByAge: AgeCurve.Of((0, 13.0), (1, 17.2), (2, 16.3), (5, 15.3), (10, 16.4), (15, 19.8), (20, 21.0), (40, 22.3),
            (70, 22.0), (85, 21.2)),
        FrameStdDev: 0.08,
        // músculo esquelético / peso (Janssen 2000, aprox.): adulto ~40% nos homens e ~31% nas mulheres; perde 3–8% por década
        MaleMuscleFraction: AgeCurve.Of((0, 0.22), (5, 0.27), (10, 0.30), (13, 0.32), (15, 0.36), (18, 0.40), (30, 0.40),
            (40, 0.385), (50, 0.365), (60, 0.345), (70, 0.32), (80, 0.29), (90, 0.27)),
        FemaleMuscleFraction: AgeCurve.Of((0, 0.22), (5, 0.27), (10, 0.29), (13, 0.30), (15, 0.31), (30, 0.31), (40, 0.30),
            (50, 0.285), (60, 0.27), (70, 0.25), (80, 0.23), (90, 0.215)),
        // trabalho braçal todo dia: ~15% mais músculo que o sedentário, alcançado em ~1–2 anos (1–2 kg/ano no começo)
        TrainingMuscleGain: 0.15, MuscleAdaptationPerMonth: 0.06,
        // jejum longo tira até ~30% do peso antes da morte
        WastingAtFullUndernutrition: 0.30,
        // desnutrição a vida toda da infância custa ~5–6% da estatura adulta (~9 cm)
        StuntingSensitivity: 0.08, StuntingUndernutritionThreshold: 0.15,
        GrowthEndAgeYears: 20,
        // ganchos para a fatia de puberdade: idade do pico de velocidade de crescimento (menarca ~1 ano depois nas meninas)
        FemalePeakHeightVelocityAge: 11.5, MalePeakHeightVelocityAge: 13.5,
        // referência da capacidade de trabalho e do custo de andar: adulto médio da vila (homens e mulheres que trabalham);
        // abaixo de 1,45 m a passada curta deixa a criança mais lenta
        ReferenceAdultMuscleMass: 23.0, ReferenceAdultBmi: 21.8, ShortStrideHeight: 1.45);

    /// <summary>Corpo sem ontogenia (o modelo de antes): o sorteio de adulto vale do nascimento à morte.</summary>
    public static GrowthRules Static { get; } = Default with { Enabled = false };

    public double AdultHeightMean(Sex sex) => sex == Sex.Male ? MaleAdultHeightMean : FemaleAdultHeightMean;
    public double AdultHeightStdDev(Sex sex) => sex == Sex.Male ? MaleAdultHeightStdDev : FemaleAdultHeightStdDev;

    /// <summary>Estatura adulta potencial: média do sexo + desvio × (parte herdada do gene + parte ambiental própria).</summary>
    public double AdultHeightFrom(Sex sex, double statureGene, double environmentZ)
    {
        double h2 = Math.Clamp(StatureHeritability, 0, 1);
        return AdultHeightMean(sex) + AdultHeightStdDev(sex) * (statureGene + Math.Sqrt(1 - h2) * environmentZ);
    }

    /// <summary>Altura na idade: estatura adulta potencial × fração da idade × encolhimento × atraso do prematuro.</summary>
    public double HeightAt(Sex sex, double ageYears, double adultHeight, int? gestationDays)
    {
        var fraction = sex == Sex.Male ? MaleHeightFraction : FemaleHeightFraction;
        return adultHeight * fraction.At(ageYears) * ElderHeightFactor.At(ageYears)
            * Catchup(BirthLengthByGestationDays, ageYears, gestationDays);
    }

    /// <summary>Peso na idade: IMC da idade × altura² × compleição individual, menos a perda da desnutrição; prematuro leve.</summary>
    public double WeightAt(Sex sex, double ageYears, double height, double frame, double undernutrition, int? gestationDays)
    {
        var bmi = sex == Sex.Male ? MaleBmiByAge : FemaleBmiByAge;
        double weight = bmi.At(ageYears) * height * height * frame * (1 - WastingAtFullUndernutrition * Math.Clamp(undernutrition, 0, 1));
        if (gestationDays is { } days && ageYears < PretermCatchUpYears)
        {
            // o peso do prematuro cai mais que o quadrado do comprimento: corrige a diferença e some até alcançar a curva
            double length = BirthLengthByGestationDays.At(days);
            double excess = BirthWeightByGestationDays.At(days) / Math.Max(1e-6, length * length);
            weight *= Catchup(excess, ageYears);
        }

        return weight;
    }

    /// <summary>Músculo-alvo: fração do peso da idade e sexo, mais o ganho do treino (parte dos dias com trabalho braçal).</summary>
    public double MuscleTarget(Sex sex, double ageYears, double weight, double workShare)
    {
        var fraction = sex == Sex.Male ? MaleMuscleFraction : FemaleMuscleFraction;
        return weight * fraction.At(ageYears) * (1 + TrainingMuscleGain * Math.Clamp(workShare, 0, 1));
    }

    /// <summary>Estatura perdida num mês de crescimento com desnutrição <paramref name="undernutrition"/>: proporcional ao que
    /// a curva cresceria nesse mês e ao quanto a desnutrição passa do limiar.</summary>
    public double StuntingLoss(Sex sex, double ageYears, double monthYears, double adultHeight, double undernutrition)
    {
        if (ageYears >= GrowthEndAgeYears || undernutrition <= StuntingUndernutritionThreshold) return 0;
        var fraction = sex == Sex.Male ? MaleHeightFraction : FemaleHeightFraction;
        double growth = Math.Max(0, fraction.At(ageYears + monthYears) - fraction.At(ageYears));
        double severity = (undernutrition - StuntingUndernutritionThreshold) / (1 - StuntingUndernutritionThreshold);
        return adultHeight * growth * StuntingSensitivity / Math.Max(1e-6, 1 - fraction.At(0)) * severity;
    }

    private double Catchup(AgeCurve atBirth, double ageYears, int? gestationDays) =>
        gestationDays is { } days ? Catchup(atBirth.At(days), ageYears) : 1.0;

    private double Catchup(double atBirth, double ageYears)
    {
        if (PretermCatchUpYears <= 0 || ageYears >= PretermCatchUpYears) return 1.0;
        return atBirth + (1 - atBirth) * Math.Max(0, ageYears) / PretermCatchUpYears;
    }

    public Result<GrowthRules> Validate()
    {
        if (MaleAdultHeightMean <= 0 || FemaleAdultHeightMean <= 0 || MaleAdultHeightStdDev < 0 || FemaleAdultHeightStdDev < 0)
            return Result<GrowthRules>.Fail("AdultHeight: média > 0 e desvio >= 0");
        if (StatureHeritability is < 0 or > 1) return Result<GrowthRules>.Fail("StatureHeritability: fora de [0,1]");
        foreach (var curve in new[] { MaleHeightFraction, FemaleHeightFraction, ElderHeightFactor, BirthLengthByGestationDays,
                     BirthWeightByGestationDays, MaleBmiByAge, FemaleBmiByAge, MaleMuscleFraction, FemaleMuscleFraction })
            if (!curve.IsAscendingInAge() || curve.Points.Any(p => p.Value < 0))
                return Result<GrowthRules>.Fail("GrowthRules: curva fora de ordem ou com valor negativo");
        if (MuscleAdaptationPerMonth is < 0 or > 1 || WastingAtFullUndernutrition is < 0 or >= 1)
            return Result<GrowthRules>.Fail("MuscleAdaptationPerMonth/WastingAtFullUndernutrition: fora de faixa");
        return Result<GrowthRules>.Ok(this);
    }
}
