namespace LivingWorld.Domain.Crime;

/// <summary>Furto do cenário (crime-and-theft): quem está desesperado (necessidade abaixo de <see cref="DesperateBelow"/>, sem
/// dinheiro e sem mantimento) e tem o temperamento para isso (peso de personalidade da ação Furtar acima de
/// <see cref="PersonalityThreshold"/>) pode furtar a moeda de alguém na mesma célula. Cada guarda da cidade tira
/// <see cref="GuardEffect"/> da chance de sucesso. <see cref="Disabled"/> é o padrão: ninguém furta.</summary>
public sealed record TheftRules(
    bool Enabled,
    int DesperateBelow,
    double PersonalityThreshold,
    double BaseSuccess,
    double DiscoveryChance,
    double MaxLootFraction,
    int GuardProfessionId,
    double GuardEffect)
{
    public static TheftRules Disabled { get; } = new(false, 25, 1.1, 0.6, 0.5, 0.5, -1, 0.25);
}
