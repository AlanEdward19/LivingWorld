namespace LivingWorld.Domain;

/// <summary>
/// Estágio de evolução de um poder: limiar(es) opcional(is) de idade e/ou uso e o conjunto de
/// efeitos ativos quando o estágio é alcançado.
/// </summary>
public sealed record PowerEvolutionStage(
    int? AgeThreshold,
    int? UseCountThreshold,
    IReadOnlyList<string> EffectTokens);
