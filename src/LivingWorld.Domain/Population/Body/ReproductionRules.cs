using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Faixa de parto prematuro (dias desde a concepção): chance, risco de morte no 1º mês e multiplicador do risco no
/// resto do 1º ano.</summary>
public sealed record PretermBand(double Probability, int MinDays, int MaxDays, double NeonatalDeathRisk, double InfantHazardMultiplier);

/// <summary>Reprodução de época sem contracepção (AD-130), dado de cenário. Mulher: fecundabilidade mensal por idade (gestação
/// reconhecida), fração estéril acumulada por idade (inclui a menopausa), sem concepção na gravidez nem no intervalo pós-parto
/// (amenorreia da lactação). Homem: fator por idade, sem teto. Gestação, prematuridade, perda precoce, natimorto e morte
/// materna por idade, saúde e paridade. Os números do <see cref="Default"/> são aproximados (literatura de demografia
/// histórica e obstetrícia citada de memória) e precisam de conferência.</summary>
public sealed record ReproductionRules(
    AgeCurve FemaleMonthlyFecundability,
    AgeCurve FemaleSterileFraction,
    AgeCurve MaleFertility,
    double MaleBirthShare,
    double TwinRate,
    double MonozygoticTwinShare,
    int TermMeanDays,
    int TermStdDevDays,
    int TermMinDays,
    int TermMaxDays,
    IReadOnlyList<PretermBand> PretermBands,
    int PostpartumInfecundMeanDays,
    int PostpartumInfecundStdDevDays,
    int PostpartumInfecundMinDays,
    int PregnancyLossInfecundDays,
    AgeCurve MiscarriageRiskByAge,
    int MiscarriageMinDays,
    int MiscarriageMaxDays,
    AgeCurve MaternalAgeRiskFactor,
    AgeCurve ParityRiskFactor)
{
    public static ReproductionRules Default { get; } = new(
        // pico ~0,2 por ciclo aos 20–29 (Bongaarts: fecundabilidade efetiva ~0,2), subfecundidade na adolescência
        FemaleMonthlyFecundability: AgeCurve.Of((12, 0), (14, 0.03), (16, 0.09), (18, 0.15), (20, 0.19), (25, 0.20), (30, 0.18),
            (35, 0.14), (40, 0.09), (45, 0.045), (50, 0)),
        // fração estéril (Menken/Trussell, aprox.): 3% aos 20, 16% aos 35, 31% aos 40, 69% aos 45, todas aos 50
        FemaleSterileFraction: AgeCurve.Of((15, 0.03), (20, 0.03), (25, 0.06), (30, 0.10), (35, 0.16), (40, 0.31), (45, 0.69), (50, 1.0)),
        MaleFertility: AgeCurve.Of((13, 0), (16, 0.6), (18, 1.0), (40, 1.0), (50, 0.9), (60, 0.75), (70, 0.5), (80, 0.3)),
        MaleBirthShare: 105.0 / 205.0,
        TwinRate: 0.012,
        MonozygoticTwinShare: 0.3,
        // ~38 semanas desde a concepção (266 d), dp ~9 d; pós-termo até ~43 semanas de amenorreia
        TermMeanDays: 268, TermStdDevDays: 9, TermMinDays: 245, TermMaxDays: 295,
        PretermBands:
        [
            new PretermBand(0.004, 154, 181, NeonatalDeathRisk: 0.90, InfantHazardMultiplier: 3.0), // < 28 semanas
            new PretermBand(0.0075, 182, 209, NeonatalDeathRisk: 0.50, InfantHazardMultiplier: 2.5), // 28–31
            new PretermBand(0.0635, 210, 244, NeonatalDeathRisk: 0.12, InfantHazardMultiplier: 1.6), // 32–36 (12%: sem fonte)
        ],
        // amenorreia da lactação ~12 meses: com ~18,5 meses de espera, gestação e perdas (Bongaarts), o intervalo entre
        // partos fica perto dos ~30 meses da Inglaterra pré-industrial. 480 dias davam média de 41 (conferência AD-133).
        PostpartumInfecundMeanDays: 365, PostpartumInfecundStdDevDays: 120, PostpartumInfecundMinDays: 60,
        PregnancyLossInfecundDays: 45,
        // perda de gestação reconhecida (Nybo Andersen 2000): ~10% aos 20–29, 25% aos 35–39, 54,5% aos 42, 74,7% aos 45+
        MiscarriageRiskByAge: AgeCurve.Of((13, 0.20), (18, 0.11), (20, 0.10), (29, 0.11), (33, 0.14), (37, 0.22), (40, 0.40),
            (42, 0.55), (45, 0.75), (50, 0.85)),
        MiscarriageMinDays: 40, MiscarriageMaxDays: 140,
        MaternalAgeRiskFactor: AgeCurve.Of((12, 3.0), (15, 2.0), (18, 1.2), (20, 1.0), (30, 1.0), (35, 1.4), (40, 2.0), (45, 3.0)),
        ParityRiskFactor: AgeCurve.Of((0, 1.3), (1, 1.0), (3, 1.0), (5, 1.2), (8, 1.6), (12, 2.0)));

    /// <summary>Fecundabilidade de um ciclo: zero se a mulher já é estéril na idade (sorteio fixo por mulher,
    /// <paramref name="sterilityDraw"/> em [0,1)).</summary>
    public double FemaleCycleFecundability(double ageYears, double sterilityDraw) =>
        sterilityDraw < FemaleSterileFraction.At(ageYears) ? 0.0 : FemaleMonthlyFecundability.At(ageYears);

    /// <summary>Fator relativo (0..1) para a atração por idade no cortejo: fertilidade da idade, por sexo.</summary>
    public double RelativeFertility(Sex sex, double ageYears)
    {
        var curve = sex == Sex.Female ? FemaleMonthlyFecundability : MaleFertility;
        double max = curve.Max();
        return max <= 0 ? 0.0 : curve.At(ageYears) / max;
    }

    /// <summary>Multiplicador do risco obstétrico (natimorto, morte materna) por idade da mãe, partos anteriores e saúde.</summary>
    public double ObstetricRiskMultiplier(double motherAgeYears, int previousBirths, int health) =>
        MaternalAgeRiskFactor.At(motherAgeYears) * ParityRiskFactor.At(previousBirths) * HealthFactor(health);

    public static double HealthFactor(int health) => 1.0 + (100 - Math.Clamp(health, 0, 100)) / 100.0;

    /// <summary>Duração da gestação em dias e a faixa prematura sorteada (nula = a termo).</summary>
    public (int Days, PretermBand? Band) RollGestation(WorldRng rng)
    {
        double u = rng.NextDouble(), cumulative = 0;
        foreach (var band in PretermBands)
        {
            cumulative += band.Probability;
            if (u < cumulative)
                return (band.MinDays + (int)(rng.NextDouble() * (band.MaxDays - band.MinDays + 1)), band);
        }

        double days = BodyGeneration.SampleTruncatedNormal(rng, TermMeanDays, TermStdDevDays, TermMinDays, TermMaxDays);
        return ((int)Math.Round(days), null);
    }

    /// <summary>Faixa prematura de quem nasceu com <paramref name="gestationDays"/> (nula = a termo).</summary>
    public PretermBand? PretermBandOf(int gestationDays)
    {
        foreach (var band in PretermBands)
            if (gestationDays >= band.MinDays && gestationDays <= band.MaxDays) return band;
        return null;
    }

    public int RollPostpartumInfecundDays(WorldRng rng) => (int)Math.Round(BodyGeneration.SampleTruncatedNormal(
        rng, PostpartumInfecundMeanDays, PostpartumInfecundStdDevDays, PostpartumInfecundMinDays,
        PostpartumInfecundMeanDays + 4.0 * PostpartumInfecundStdDevDays));

    public Result<ReproductionRules> Validate()
    {
        foreach (var (name, curve) in new[]
                 {
                     ("FemaleMonthlyFecundability", FemaleMonthlyFecundability), ("FemaleSterileFraction", FemaleSterileFraction),
                     ("MaleFertility", MaleFertility), ("MiscarriageRiskByAge", MiscarriageRiskByAge),
                     ("MaternalAgeRiskFactor", MaternalAgeRiskFactor), ("ParityRiskFactor", ParityRiskFactor),
                 })
            if (!curve.IsAscendingInAge() || curve.Points.Any(p => p.Value < 0))
                return Result<ReproductionRules>.Fail($"{name}: idades fora de ordem ou valor negativo");
        if (MaleBirthShare is < 0 or > 1 || TwinRate is < 0 or > 1 || MonozygoticTwinShare is < 0 or > 1)
            return Result<ReproductionRules>.Fail("MaleBirthShare/TwinRate/MonozygoticTwinShare: fora de [0,1]");
        if (TermMinDays <= 0 || TermMinDays > TermMeanDays || TermMaxDays < TermMeanDays || TermStdDevDays < 0)
            return Result<ReproductionRules>.Fail("TermMeanDays: precisa caber em [TermMinDays, TermMaxDays]");
        if (PretermBands.Sum(b => b.Probability) > 1 || PretermBands.Any(b => b.MinDays <= 0 || b.MaxDays < b.MinDays))
            return Result<ReproductionRules>.Fail("PretermBands: chances somam mais que 1 ou faixa inválida");
        if (MiscarriageMinDays <= 0 || MiscarriageMaxDays < MiscarriageMinDays || PostpartumInfecundMinDays < 0)
            return Result<ReproductionRules>.Fail("MiscarriageMinDays/MaxDays/PostpartumInfecundMinDays: faixa inválida");
        return Result<ReproductionRules>.Ok(this);
    }
}
