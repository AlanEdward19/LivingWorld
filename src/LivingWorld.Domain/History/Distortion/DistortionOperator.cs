namespace LivingWorld.Domain.History.Distortion;

/// <summary>Operadores de distorção determinística — lista fechada de 8
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
