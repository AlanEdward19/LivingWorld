namespace LivingWorld.Domain;

/// <summary>Pesos cenário-driven para converter <c>PowerOpportunity</c> em utility
/// comparável com ActionType fixos (Fase 16.3 P1d, COH-31) — mesmo template de
/// <see cref="PowerInheritanceRules"/>/<see cref="FamilyRules"/>: validação em
/// <see cref="Create"/>, <see cref="Default"/> documentado.</summary>
/// <remarks>
/// Defaults documentados (design 16.3 AD-012):
/// <list type="bullet">
/// <item><see cref="CostWeight"/> = 1.0 — custo estimado penaliza utility 1:1.</item>
/// <item><see cref="RiskWeight"/> = 1.0 — risco estimado penaliza utility 1:1.</item>
/// <item><see cref="ReliabilityWeight"/> = 1.0 — confiabilidade (Guaranteed) bonifica.</item>
/// <item><see cref="UrgencyWeight"/> = 1.0 — urgência da necessidade amplifica o score.</item>
/// </list>
/// </remarks>
public sealed record PowerUtilityRules(
    double CostWeight,
    double RiskWeight,
    double ReliabilityWeight,
    double UrgencyWeight)
{
    public const double DefaultCostWeight = 1.0;
    public const double DefaultRiskWeight = 1.0;
    public const double DefaultReliabilityWeight = 1.0;
    public const double DefaultUrgencyWeight = 1.0;

    public static Result<PowerUtilityRules> Create(
        double costWeight,
        double riskWeight,
        double reliabilityWeight,
        double urgencyWeight)
    {
        if (costWeight < 0)
            return Result<PowerUtilityRules>.Fail("CostWeight: deve ser >= 0");
        if (riskWeight < 0)
            return Result<PowerUtilityRules>.Fail("RiskWeight: deve ser >= 0");
        if (reliabilityWeight < 0)
            return Result<PowerUtilityRules>.Fail("ReliabilityWeight: deve ser >= 0");
        if (urgencyWeight < 0)
            return Result<PowerUtilityRules>.Fail("UrgencyWeight: deve ser >= 0");

        return Result<PowerUtilityRules>.Ok(new PowerUtilityRules(
            costWeight, riskWeight, reliabilityWeight, urgencyWeight));
    }

    /// <summary>Default documentado — pesos unitários; cenário que omite a regra ainda
    /// pontua poderes de forma neutra. Use <see cref="Resolve"/>.</summary>
    public static readonly PowerUtilityRules Default = Create(
        costWeight: DefaultCostWeight,
        riskWeight: DefaultRiskWeight,
        reliabilityWeight: DefaultReliabilityWeight,
        urgencyWeight: DefaultUrgencyWeight).Value
        ?? throw new InvalidOperationException(
            "PowerUtilityRules.Default inválida — bug no cenário");

    /// <summary>Usa a regra declarada; se ausente, devolve <see cref="Default"/>.</summary>
    public static PowerUtilityRules Resolve(PowerUtilityRules? declared) =>
        declared ?? Default;
}
