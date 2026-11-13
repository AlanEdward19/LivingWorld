namespace LivingWorld.Domain.Population.Family;

/// <summary>Sexo fora da união e a reação da família e da comunidade (AD-139), norma do cenário. Os casais casados concebem
/// pelo ciclo (sexo regular); quem não é casado só concebe no mês em que há sexo, que depende de oportunidade (namoro, caso,
/// alguém de quem gosta e que gosta dele) e de temperamento (impulsividade e aversão a risco já com o impulso da
/// adolescência). As chances por mês já embutem a norma da vila pré-industrial (castidade pregada, namoro vigiado).
/// Números de calibração; a referência de época (concepção pré-nupcial e ilegitimidade) está no registro AD-139.</summary>
public sealed record IntimacyRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Chance por mês de sexo de um casal de namorados de temperamento médio.</summary>
    public double CourtingPerMonth { get; init; } = 0.5;

    /// <summary>Chance por mês de sexo de uma mulher solteira com caso em curso (com um casado).</summary>
    public double LoverPerMonth { get; init; } = 0.6;

    /// <summary>Chance por mês de um encontro com alguém de quem se gosta, fora de namoro, de temperamento médio.</summary>
    public double CasualPerMonth { get; init; } = 0.012;

    /// <summary>Afeto mútuo mínimo para haver encontro fora de namoro.</summary>
    public double CasualAffectionMin { get; init; } = 45;

    /// <summary>Dias desde a concepção até a gravidez aparecer e a família e a vila reagirem.</summary>
    public int ShowsAfterDays { get; init; } = 110;

    /// <summary>Chance de o pai solteiro ser levado a casar com a mãe (temperamento médio); senão ela é deixada.</summary>
    public double ForcedMarriageShare { get; init; } = 0.7;

    /// <summary>Peso da vergonha nos pais da mãe solteira (relativo ao dela).</summary>
    public double ParentShameScale { get; init; } = 0.6;

    public static IntimacyRules Medieval { get; } = new();

    /// <summary>Cenário moderno: pouca vigilância e pouca pressão para casar.</summary>
    public static IntimacyRules Modern { get; } = new()
    {
        CourtingPerMonth = 0.8,
        CasualPerMonth = 0.05,
        ForcedMarriageShare = 0.1,
        ParentShameScale = 0.1,
    };

    /// <summary>Só casal concebe (o modelo de antes).</summary>
    public static IntimacyRules Disabled { get; } = new() { Enabled = false };

    public string? Validate()
    {
        if (CourtingPerMonth is < 0 or > 1 || LoverPerMonth is < 0 or > 1 || CasualPerMonth is < 0 or > 1)
            return "Intimacy: chances por mês fora de [0,1]";
        if (ForcedMarriageShare is < 0 or > 1) return "Intimacy.ForcedMarriageShare: fora de [0,1]";
        if (ShowsAfterDays <= 0) return "Intimacy.ShowsAfterDays: deve ser > 0";
        return null;
    }
}
