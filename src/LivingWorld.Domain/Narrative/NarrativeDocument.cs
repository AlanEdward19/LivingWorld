using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Narrative;

/// <summary>Documento narrativo publicado (Fase 12, NARR-01) — prosa final mais os claims
/// aprovados que a originaram, para que toda saída legível permaneça auditável até o evento.</summary>
public sealed record NarrativeDocument(
    NarrativeId Id, NarrativeType Type, string Prose, IReadOnlyList<NarrativeClaim> Claims);
