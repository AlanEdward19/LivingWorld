namespace LivingWorld.Domain.Narrative;

/// <summary>Superfície de leitura de um <see cref="NarrativeDocument"/> (Fase 12, NARR-01,
/// NARR-19..21) — mesmo pipeline de claims ancorados, três formatos de publicação.</summary>
public enum NarrativeType
{
    Chronicle,
    Biography,
    Report,
}
