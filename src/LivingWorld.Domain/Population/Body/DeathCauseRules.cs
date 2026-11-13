namespace LivingWorld.Domain.Population.Body;

/// <summary>Causa das mortes que sobram da tábua depois da doença (AD-137), dado de cenário. Não muda quantos morrem: só nomeia
/// a morte sorteada. Acidente (queda, afogamento, fogo, coice, ferramenta) tem taxa anual pela idade, mais alta no homem; velhice
/// é a parte da sobra que nomeia a morte do velho. O resto fica sem causa (<c>Death</c>). Aproximado, precisa de conferência.</summary>
public sealed record DeathCauseRules(
    AgeCurve AccidentAnnualRateByAge,
    double MaleAccidentFactor,
    double FemaleAccidentFactor,
    AgeCurve OldAgeShareByAge)
{
    public static DeathCauseRules PreIndustrial { get; } = new(
        // ~0,5 por mil ao ano (registros de coroner e listas de óbito: acidentes ~1–2% das mortes)
        AccidentAnnualRateByAge: AgeCurve.Of((0, 0.0003), (1, 0.0006), (5, 0.0004), (15, 0.0006), (60, 0.0006), (80, 0.001)),
        MaleAccidentFactor: 1.5, FemaleAccidentFactor: 0.5,
        OldAgeShareByAge: AgeCurve.Of((55, 0), (60, 0.4), (70, 0.7), (80, 0.9)));

    /// <summary>Nenhuma causa nomeada: toda morte da sobra fica <c>Death</c> (o modelo de antes).</summary>
    public static DeathCauseRules None { get; } = new(AgeCurve.Of(), 0, 0, AgeCurve.Of());

    /// <summary>Parte da morte sorteada que é acidente, dado o risco anual que sobrou na idade.</summary>
    public double AccidentShare(double ageYears, bool male, double residualAnnualRisk) =>
        residualAnnualRisk <= 0 ? 0 : Math.Clamp(
            AccidentAnnualRateByAge.At(ageYears) * (male ? MaleAccidentFactor : FemaleAccidentFactor) / residualAnnualRisk, 0, 1);
}
