namespace LivingWorld.Domain.Narrative;

/// <summary>Superfície de leitura de um <see cref="NarrativeDocument"/> — mesmo pipeline de
/// claims ancorados, três formatos de publicação.</summary>
public enum NarrativeType
{
    Chronicle,
    Biography,
    Report,
}
