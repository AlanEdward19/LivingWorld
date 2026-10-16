using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Climate;

/// <summary>Clima do cenário (weather-drought): o ano de safra de cada região é um fator em milésimos, sorteado uma vez por
/// (região, ano) entre <see cref="MinMilli"/> e <see cref="MaxMilli"/> (1000 = ano médio). <see cref="Disabled"/> é o padrão: todo
/// ano vale 1000.</summary>
public sealed record ClimateRules(bool Enabled, int MinMilli, int MaxMilli)
{
    public static ClimateRules Disabled { get; } = new(false, 1000, 1000);

    /// <summary>Abaixo disto o ano é de seca.</summary>
    public const int DroughtBelowMilli = 600;
}

/// <summary>Fator de safra de uma região num ano; nunca editado depois de sorteado.</summary>
public sealed record ClimateYear(RegionId Region, long Year, int FactorMilli);
