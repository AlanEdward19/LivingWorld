using LivingWorld.Domain.Behavior;

namespace LivingWorld.Domain.Cognition;

/// <summary>Motivo do wake que disparou a decisão (Fase 16.3 P2b, COH-54) — volátil,
/// só entra no <see cref="DecisionTrace"/>.</summary>
public enum WakeReason
{
    Unknown = 0,
    UrgentNeed = 1,
    ActionCompleted = 2,
    EventRouted = 3,
    Scheduled = 4,
}

/// <summary>Rastro volátil de uma decisão de utility (doc#55/#84) — top pressões,
/// oportunidades conhecidas, fatores +/- e alternativas. Nunca <c>[Canonical]</c>,
/// nunca persistido, nunca afeta golden hash.</summary>
public sealed record DecisionTrace(
    WakeReason WakeReason,
    ActionType? PreviousIntent,
    IReadOnlyList<Pressure> TopPressures,
    IReadOnlyList<Opportunity> KnownOpportunities,
    ActionType Winner,
    double WinningUtility,
    IReadOnlyList<string> TopPositiveFactors,
    IReadOnlyList<string> TopNegativeFactors,
    IReadOnlyList<string> BlockingFactors,
    IReadOnlyList<ActionType> KnownAlternatives);
