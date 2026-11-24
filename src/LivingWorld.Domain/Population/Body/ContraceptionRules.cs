namespace LivingWorld.Domain.Population.Body;

/// <summary>Contracepção só se inventada (AD-139): nenhum método existe por padrão. Uma tecnologia da categoria
/// <see cref="Category"/> que a cidade da mulher adotou (o motor de emergência decide se e quando alguém a inventa) protege
/// quem a usa: eficácia = média dos eixos do vetor / teto do eixo × <see cref="MaxEfficacy"/> (a taxa de falha é o resto).
/// Quem usa é sorteio fixo por mulher contra <see cref="UseShare"/> (norma e custo do lugar) e só depois de ter
/// <see cref="DesiredChildren"/> filhos. Até lá valem amamentação e abstinência, que já emergem.</summary>
public sealed record ContraceptionRules
{
    public const string FertilityControlCategory = "fertility-control";

    public string Category { get; init; } = FertilityControlCategory;
    public double MaxEfficacy { get; init; } = 0.95;
    public double UseShare { get; init; } = 0.5;
    public int DesiredChildren { get; init; } = 2;

    public static ContraceptionRules Default { get; } = new();
}
