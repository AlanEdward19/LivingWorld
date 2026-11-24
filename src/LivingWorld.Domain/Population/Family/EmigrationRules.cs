namespace LivingWorld.Domain.Population.Family;

/// <summary>Saída de quem não tem lugar na terra (AD-145): o adulto jovem, solteiro e sem filhos, sem trabalho e sem vaga na
/// cidade dele, vai embora buscar a vida. Vai para outra cidade do mundo que tenha vaga para o ofício dele; se não há nenhuma,
/// sai do mundo simulado (emigrante: não é morte). A decisão é dele, mês a mês: a chance anual de quem tem o temperamento médio
/// (<see cref="DecisionPerYear"/>) cresce com a abertura e a ambição, cai com a aversão a risco e com os laços (pais vivos e
/// afeto na cidade). Vive em <see cref="PopulationPressureRules.Emigration"/>; <see cref="Disabled"/> desliga.</summary>
public sealed record EmigrationRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Idade mínima e máxima de quem parte (o migrante da época era o jovem que ainda não tinha casa).</summary>
    public int MinAgeYears { get; init; } = 16;
    public int MaxAgeYears { get; init; } = 35;

    /// <summary>Chance por ano de ir embora, temperamento médio, sem trabalho e sem vaga o ano inteiro.</summary>
    public double DecisionPerYear { get; init; } = 0.35;

    /// <summary>Quanto os laços (pais vivos na cidade, o afeto mais forte por alguém dela) seguram: 0 = nada, 1 = prendem de vez.</summary>
    public double TiesHold { get; init; } = 0.5;

    public static EmigrationRules Default { get; } = new();

    public static EmigrationRules Disabled { get; } = new() { Enabled = false };

    public string? Validate()
    {
        if (MinAgeYears < 0 || MaxAgeYears < MinAgeYears) return "Emigration: faixa de idade inválida";
        if (DecisionPerYear is < 0 or > 1) return "Emigration.DecisionPerYear: fora de [0,1]";
        if (TiesHold is < 0 or > 1) return "Emigration.TiesHold: fora de [0,1]";
        return null;
    }
}
