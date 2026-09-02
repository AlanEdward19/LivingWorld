namespace LivingWorld.Domain.History.Distortion;

/// <summary>Fidelidade, alcance e condição de morte de um <see cref="TransmissionMediumType"/>
/// (Fase 10, HIST-08) — dado cenário-driven via <see cref="HistoryRules"/>.</summary>
public sealed record MediumFidelity(
    double DistortionRatePerHop,
    int ReachHops,
    DeathConditionType DeathCondition);
