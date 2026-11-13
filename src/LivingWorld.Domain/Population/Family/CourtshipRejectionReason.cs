namespace LivingWorld.Domain.Population.Family;

/// <summary>Motivo auditável de rejeição de cortejo — enum fechado, nunca
/// string livre (evita typo silencioso). Checado nesta ordem por <c>CourtshipSystem.Reject</c>:
/// <see cref="Incesto"/> antes de <see cref="ForaDaFaixaEtaria"/>, antes de qualquer score de
/// atração (<see cref="SemAfinidade"/>) — incesto deve reprovar mesmo com score
/// compatível.</summary>
public enum CourtshipRejectionReason
{
    Incesto,
    ForaDaFaixaEtaria,
    SemAfinidade,

    /// <summary>Quer o outro, mas sem trabalho nem economias o casal não tem como montar casa e a prudência adia (AD-145).</summary>
    SemMeios,
}
