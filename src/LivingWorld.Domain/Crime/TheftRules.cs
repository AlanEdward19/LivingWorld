namespace LivingWorld.Domain.Crime;

/// <summary>Furto do cenário (crime-and-theft; realista no AD-150). Três motivos: <b>fome</b> (necessidade abaixo de
/// <see cref="DesperateBelow"/>, sem comida em casa e sem como comprar: decide na hora, pela ação Furtar), <b>miséria</b> (pouca
/// moeda e despensa curta) e <b>oportunidade</b> (o temperamento basta; os dois últimos são planejados uma vez por dia e
/// executados numa hora do dia). Leva-se qualquer bem que exista: moeda de quem está ao lado ou guardada em casa, comida e água
/// da despensa de outra casa, estoque e caixa de oficina e banca. A barreira moral é o peso de personalidade da ação Furtar
/// (<c>PersonalityWeighting</c>): abaixo de <see cref="MoralFloor"/> ninguém furta por oportunidade; a fome a rebaixa em até
/// <see cref="NeedMoralRelief"/>. Cada guarda da cidade tira <see cref="GuardEffect"/> do sucesso. Quem pensa em furtar pesa
/// o medo da punição (<c>JusticeRules</c>): dissuasão da cidade e a própria lembrança de ter sido punido.
/// Quem tem gente com fome em casa e nada para dar sai a buscar comida (<see cref="FamilyHungerPerDay"/>): o pai que furta para o filho.
/// Números sem fonte firme ("precisa de conferência", AD-150).</summary>
public sealed record TheftRules(
    bool Enabled,
    int DesperateBelow,
    double PersonalityThreshold,
    double BaseSuccess,
    double DiscoveryChance,
    double MaxLootFraction,
    int GuardProfessionId,
    double GuardEffect,
    double OpportunityPerYear = 0.03,
    double PovertyFactor = 4.0,
    long PoorBelowCoin = 10,
    double PoorPantryDays = 3,
    double MoralFloor = 0.6,
    double NeedMoralRelief = 0.6,
    double SexAgeExponent = 2.0,
    int MinAgeYears = 12,
    int FoodDaysTaken = 4,
    long GoodsUnitsTaken = 3,
    double FenceShare = 0.5,
    long MaxCoinTaken = 60,
    double CaughtFightChance = 0.15,
    double FamilyHungerPerDay = 0.02)
{
    /// <summary>Furto desligado (cenários e testes antigos que pedem explicitamente).</summary>
    public static TheftRules Disabled { get; } = new(false, 25, 1.1, 0.6, 0.5, 0.5, -1, 0.25);

    /// <summary>Padrão do cenário medieval (AD-150): furto ligado, sem guarda profissional (a vila não tem).</summary>
    public static TheftRules Default { get; } = new(true, 25, 1.1, 0.75, 0.5, 0.5, -1, 0.25);

    /// <summary>Barreira do furto por fome (peso de Furtar que o temperamento precisa passar): parte de
    /// <see cref="PersonalityThreshold"/> no limite do desespero (<paramref name="need"/> 0) e cai com a fome até
    /// <see cref="NeedMoralRelief"/> abaixo (<paramref name="need"/> 1, fome ou sede em zero); a dissuasão (0..1) a sobe.</summary>
    public double NeedBarrier(double need, double deterrence) =>
        PersonalityThreshold - NeedMoralRelief * Math.Clamp(need, 0, 1) + deterrence;

    /// <summary>Barreira do furto planejado (miséria e oportunidade): <see cref="MoralFloor"/> mais a dissuasão.</summary>
    public double PlannedBarrier(double deterrence) => MoralFloor + deterrence;

    /// <summary>Quanto o temperamento passa da barreira, em unidades da distância entre a barreira e a média (peso 1): 0 =
    /// nunca, 1 = pessoa comum, mais = inclinado ao furto.</summary>
    public double Inclination(double stealWeight, double barrier) =>
        barrier >= 1.0 ? Math.Max(0, stealWeight - barrier) : Math.Max(0, (stealWeight - barrier) / (1.0 - barrier));
}
