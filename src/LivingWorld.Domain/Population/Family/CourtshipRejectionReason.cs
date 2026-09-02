namespace LivingWorld.Domain;

/// <summary>Motivo auditável de rejeição de cortejo (Fase 7, T2, AD-054) — enum fechado, nunca
/// string livre (evita typo silencioso). Checado nesta ordem por <c>CourtshipSystem.Reject</c>:
/// <see cref="Incesto"/> antes de <see cref="ForaDaFaixaEtaria"/>, antes de qualquer score de
/// atração (<see cref="SemAfinidade"/>) — AC3 exige que incesto reprove mesmo com score
/// compatível.</summary>
public enum CourtshipRejectionReason
{
    Incesto,
    ForaDaFaixaEtaria,
    SemAfinidade,
}
