namespace LivingWorld.Domain.Population.Family;

/// <summary>Só casa quem tem do que viver (AD-158): a casa nova precisa de um sustento — uma posse de terra vaga, um ofício
/// fora da lavoura com salário, ou a casa que um dos dois já chefia. Há posse vaga quando a terra demarcada da vila
/// (<c>CropBatch.ArableSeed</c>) menos o que comem os que vivem dela comporta mais uma posse. Com a terra toda tomada, o
/// casamento espera a morte dos velhos, e quem não espera vai embora: o freio preventivo da aldeia do noroeste europeu.</summary>
public sealed record HoldingRules
{
    /// <summary>Hectares semeados por ano numa posse que sustenta uma família de ~5 (0,68 ha por pessoa, o que os fundadores
    /// semeiam, AD-149). Lembrança: meia virgata inglesa ~15 acres (~6 ha), ~2/3 semeados em três folhas. Precisa de conferência.</summary>
    public double HoldingSownHectares { get; init; } = 3.5;

    /// <summary>Hectares semeados por ano que cada morador de família da terra come (os fundadores semeiam ~0,68 por pessoa,
    /// metade da terra demarcada, AD-149). Calibração do modelo.</summary>
    public double PersonSownHectares { get; init; } = 0.7;

    public static HoldingRules Default { get; } = new();

    public string? Validate() => HoldingSownHectares <= 0 || PersonSownHectares <= 0 ? "Pressure.Holdings: hectares devem ser > 0" : null;
}
