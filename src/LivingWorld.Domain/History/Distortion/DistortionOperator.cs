namespace LivingWorld.Domain;

/// <summary>Operadores de distorção determinística (Fase 10, HIST-05) — lista fechada de 8
/// transformações; probabilidade por operador vem de <see cref="HistoryRules"/>.</summary>
public enum DistortionOperator
{
    AttributionSwap,
    MagnitudeInflation,
    TemporalCompression,
    CausalLoss,
    Moralization,
    Anachronism,
    ConvenientOmission,
    CharacterMerge,
}
