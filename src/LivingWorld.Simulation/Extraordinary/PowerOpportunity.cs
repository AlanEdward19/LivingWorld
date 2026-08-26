using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Candidato dinâmico de poder no utility loop (Fase 16.3 P1d, COH-31).
/// Volátil — nunca canônico / persistido. Custo e risco vêm de heurística documentada
/// sobre <see cref="PowerDescriptor"/> (design Risks: string→número simples).</summary>
/// <remarks>
/// Heurística P1d (calibração fina é FUTURE_DEPENDENCY):
/// <list type="bullet">
/// <item><see cref="EstimatedCost"/> = <c>Costs.Count * CostPerDeclaration</c> (mais custos → mais caro).</item>
/// <item><see cref="EstimatedRisk"/>: <c>Reliability == "Guaranteed"</c> → risco baixo fixo
/// (<see cref="GuaranteedRisk"/>); <c>"ResolutionCheck"</c> → base
/// <see cref="ResolutionCheckBaseRisk"/> + <c>FailureModes.Count * FailureModeRiskStep</c>;
/// qualquer outro token → <see cref="UnknownReliabilityRisk"/>.</item>
/// </list>
/// </remarks>
public sealed record PowerOpportunity(
    string MechanicToken,
    NpcId? SuggestedTarget,
    decimal EstimatedCost,
    double EstimatedRisk,
    string Reliability)
{
    /// <summary>Custo unitário por declaração em <see cref="PowerDescriptor.Costs"/>.</summary>
    public const decimal CostPerDeclaration = 1.0m;

    /// <summary>Risco baixo fixo quando Reliability é Guaranteed.</summary>
    public const double GuaranteedRisk = 0.1;

    /// <summary>Risco base quando Reliability é ResolutionCheck.</summary>
    public const double ResolutionCheckBaseRisk = 0.5;

    /// <summary>Incremento de risco por FailureMode declarado sob ResolutionCheck.</summary>
    public const double FailureModeRiskStep = 0.1;

    /// <summary>Risco conservador para Reliability desconhecido/não suportado.</summary>
    public const double UnknownReliabilityRisk = 0.75;

    /// <summary>Deriva custo/risco do descritor; <paramref name="mechanicToken"/> identifica
    /// o mechanic no registry (em geral o Id do descritor ou um effect token).</summary>
    public static PowerOpportunity FromDescriptor(
        PowerDescriptor descriptor,
        string mechanicToken,
        NpcId? suggestedTarget = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanicToken);

        return new PowerOpportunity(
            MechanicToken: mechanicToken,
            SuggestedTarget: suggestedTarget,
            EstimatedCost: EstimateCost(descriptor),
            EstimatedRisk: EstimateRisk(descriptor),
            Reliability: descriptor.Reliability);
    }

    /// <summary>Custo proporcional ao número de declarações de custo no descritor.</summary>
    public static decimal EstimateCost(PowerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.Costs.Count * CostPerDeclaration;
    }

    /// <summary>Risco a partir de Reliability + FailureModes (heurística documentada).</summary>
    public static double EstimateRisk(PowerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.Equals(descriptor.Reliability, "Guaranteed", StringComparison.Ordinal))
            return GuaranteedRisk;
        if (string.Equals(descriptor.Reliability, "ResolutionCheck", StringComparison.Ordinal))
            return ResolutionCheckBaseRisk + descriptor.FailureModes.Count * FailureModeRiskStep;
        return UnknownReliabilityRisk;
    }
}
