namespace LivingWorld.Domain;

/// <summary>Parâmetros cenário-driven da história degradável (Fase 10, HIST-08) — limiar de
/// significância, cânone por comunidade, fidelidade por meio, operadores e pesos de despejo.
/// Mesmo padrão de <see cref="EconomyRules"/>/<see cref="PerfRules"/>.</summary>
public sealed record HistoryRules(
    bool Enabled,
    double SkeletonSignificanceThreshold,
    int CanonSizePerCommunity,
    IReadOnlyDictionary<TransmissionMediumType, MediumFidelity> MediumFidelityByType,
    IReadOnlyDictionary<DistortionOperator, double> OperatorProbability,
    double ImportanceWeight,
    double TransmissibilityWeight,
    double RecencyWeight)
{
    public static Result<HistoryRules> Create(
        bool enabled,
        double skeletonSignificanceThreshold,
        int canonSizePerCommunity,
        IReadOnlyDictionary<TransmissionMediumType, MediumFidelity> mediumFidelityByType,
        IReadOnlyDictionary<DistortionOperator, double> operatorProbability,
        double importanceWeight,
        double transmissibilityWeight,
        double recencyWeight)
    {
        if (canonSizePerCommunity <= 0)
            return Result<HistoryRules>.Fail("CanonSizePerCommunity: deve ser > 0");
        if (skeletonSignificanceThreshold is < 0 or > 1)
            return Result<HistoryRules>.Fail("SkeletonSignificanceThreshold: deve estar em [0,1]");
        if (importanceWeight < 0)
            return Result<HistoryRules>.Fail("ImportanceWeight: deve ser >= 0");
        if (transmissibilityWeight < 0)
            return Result<HistoryRules>.Fail("TransmissibilityWeight: deve ser >= 0");
        if (recencyWeight < 0)
            return Result<HistoryRules>.Fail("RecencyWeight: deve ser >= 0");

        foreach (var (medium, fidelity) in mediumFidelityByType)
        {
            if (fidelity.DistortionRatePerHop is < 0 or > 1)
                return Result<HistoryRules>.Fail($"MediumFidelityByType[{medium}].DistortionRatePerHop: deve estar em [0,1]");
            if (fidelity.ReachHops < 0)
                return Result<HistoryRules>.Fail($"MediumFidelityByType[{medium}].ReachHops: deve ser >= 0");
        }

        foreach (var (op, probability) in operatorProbability)
        {
            if (probability is < 0 or > 1)
                return Result<HistoryRules>.Fail($"OperatorProbability[{op}]: deve estar em [0,1]");
        }

        return Result<HistoryRules>.Ok(new HistoryRules(
            enabled,
            skeletonSignificanceThreshold,
            canonSizePerCommunity,
            mediumFidelityByType,
            operatorProbability,
            importanceWeight,
            transmissibilityWeight,
            recencyWeight));
    }

    /// <summary>Cenário que ainda não declara história — <see cref="Enabled"/> falso, nenhum
    /// fato/relato. Default do <c>WorldState</c> e do <c>ScenarioRunner.Create</c>.</summary>
    public static readonly HistoryRules Disabled = new(
        Enabled: false,
        SkeletonSignificanceThreshold: 1.0,
        CanonSizePerCommunity: 1,
        MediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>(),
        OperatorProbability: new Dictionary<DistortionOperator, double>(),
        ImportanceWeight: 0,
        TransmissibilityWeight: 0,
        RecencyWeight: 0);

    private static readonly IReadOnlyDictionary<TransmissionMediumType, MediumFidelity> DefaultMediumFidelity =
        new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.LivingMemory] = new(0.0, 0, DeathConditionType.WitnessExtinct),
            [TransmissionMediumType.OralTradition] = new(0.25, 4, DeathConditionType.LineageExtinct),
            [TransmissionMediumType.Song] = new(0.20, 6, DeathConditionType.Decay),
            [TransmissionMediumType.Book] = new(0.08, 20, DeathConditionType.Decay),
            [TransmissionMediumType.Monument] = new(0.05, 50, DeathConditionType.StateCollapse),
        };

    private static readonly IReadOnlyDictionary<DistortionOperator, double> DefaultOperatorProbability =
        Enum.GetValues<DistortionOperator>().ToDictionary(op => op, _ => 0.125);

    /// <summary>Default do cenário medieval de teste — limiar moderado, cânone generoso.</summary>
    public static readonly HistoryRules Default = Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 200,
        mediumFidelityByType: DefaultMediumFidelity,
        operatorProbability: DefaultOperatorProbability,
        importanceWeight: 1.0,
        transmissibilityWeight: 0.8,
        recencyWeight: 0.6).Value
        ?? throw new InvalidOperationException("HistoryRules.Default inválida — bug no cenário");
}
