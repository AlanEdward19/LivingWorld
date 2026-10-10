using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Narrative;

/// <summary>Rascunho estruturado pré-renderização — saída do
/// agregador/`ClaimBuilder` antes da validação de ancoragem e da renderização textual.</summary>
public sealed record NarrativeDraft(
    CityId? Location, long PeriodStartTick, long PeriodEndTick, IReadOnlyList<NarrativeClaim> Claims);
