namespace LivingWorld.Domain.Population.Body;

/// <summary>Causa das mortes que sobram da tábua depois da doença (AD-137), dado de cenário. Não muda quantos morrem: só nomeia
/// a morte sorteada. Acidente (queda, afogamento, fogo, coice, ferramenta) tem taxa anual pela idade, mais alta no homem; velhice
/// é a parte da sobra que nomeia a morte do velho. O resto (AD-164) leva o nome que os registros de óbito davam na idade
/// (<see cref="ResidualByAge"/>), no payload do <c>Death</c>: <c>npcId|nome</c>. Aproximado, precisa de conferência.</summary>
public sealed record DeathCauseRules(
    AgeCurve AccidentAnnualRateByAge,
    double MaleAccidentFactor,
    double FemaleAccidentFactor,
    AgeCurve OldAgeShareByAge)
{
    /// <summary>Nomes da sobra por idade, como nas listas de óbito de Londres (Bills of Mortality, séc. XVII) e nos registros
    /// paroquiais. AD-165, conferido em Graunt 1662 (ed. Hull, <i>Economic Writings of Sir William Petty</i>, 1899, v. 2, p. 41–42):
    /// "Thrush, Convulsion, Rickets, Teeth and Worms" e "Abortives, Chrysomes, Infants, Liver-grown, Overlaid" caíam todos em
    /// crianças "under four or five years old"; "Aged" era quem morria acima de uns sessenta anos. O crisomo é o recém-nascido do
    /// primeiro mês (aqui "debilidade"); a dentição mata entre 6 e 24 meses (a dentição de leite; grau [M], precisa de
    /// conferência), por isso começa em meio ano e a lombriga a partir dos 2. O adulto de hidropisia (coração e rim) e o velho de
    /// apoplexia ficam. As infecções já têm nome pela doença; aqui só o que o modelo não simula como causa.</summary>
    public static IReadOnlyList<ResidualCause> PreIndustrialResidual { get; } =
    [
        new(0, "debilidade"), new(28.0 / 365, "convulsoes"), new(0.5, "denticao"), new(2, "lombrigas"),
        new(15, "hidropisia"), new(55, "apoplexia"),
    ];

    /// <summary>Nome da sobra pela idade (o último degrau com início ≤ idade). Vazio: a morte fica sem causa (o modelo de antes).</summary>
    public IReadOnlyList<ResidualCause> ResidualByAge { get; init; } = PreIndustrialResidual;

    public static DeathCauseRules PreIndustrial { get; } = new(
        // ~0,5 por mil ao ano (registros de coroner e listas de óbito: acidentes ~1–2% das mortes)
        AccidentAnnualRateByAge: AgeCurve.Of((0, 0.0003), (1, 0.0006), (5, 0.0004), (15, 0.0006), (60, 0.0006), (80, 0.001)),
        MaleAccidentFactor: 1.5, FemaleAccidentFactor: 0.5,
        OldAgeShareByAge: AgeCurve.Of((55, 0), (60, 0.4), (70, 0.7), (80, 0.9)));

    /// <summary>Nenhuma causa nomeada: toda morte da sobra fica <c>Death</c> sem nome (o modelo de antes do AD-137).</summary>
    public static DeathCauseRules None { get; } = new(AgeCurve.Of(), 0, 0, AgeCurve.Of()) { ResidualByAge = [] };

    /// <summary>Parte da morte sorteada que é acidente, dado o risco anual que sobrou na idade.</summary>
    public double AccidentShare(double ageYears, bool male, double residualAnnualRisk) =>
        residualAnnualRisk <= 0 ? 0 : Math.Clamp(
            AccidentAnnualRateByAge.At(ageYears) * (male ? MaleAccidentFactor : FemaleAccidentFactor) / residualAnnualRisk, 0, 1);

    /// <summary>Nome da sobra na idade, ou vazio quando não há degrau (sem causa).</summary>
    public string ResidualLabel(double ageYears)
    {
        string label = "";
        for (int i = 0; i < ResidualByAge.Count; i++)
            if (ageYears >= ResidualByAge[i].FromAgeYears) label = ResidualByAge[i].Label;
        return label;
    }
}

/// <summary>Degrau de idade da sobra da tábua e o nome que a morte leva a partir dele (AD-164).</summary>
public sealed record ResidualCause(double FromAgeYears, string Label);
