using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Climate;

/// <summary>Clima do cenário (weather-drought): o ano de safra de cada região é um fator em milésimos, sorteado uma vez por
/// (região, ano) entre <see cref="MinMilli"/> e <see cref="MaxMilli"/> (1000 = ano médio). Com <see cref="StdDevMilli"/> &gt; 0
/// (AD-135) o fator é AR(1) em torno de <see cref="MeanMilli"/>: o ano herda <see cref="Persistence"/> do desvio do ano
/// anterior, e o choque do ano tem uma parte comum a todas as regiões (<see cref="SharedShare"/> da variância) — secas
/// duram e atingem regiões vizinhas juntas. Com <see cref="StdDevMilli"/> = 0 vale o sorteio uniforme antigo.
/// <see cref="Disabled"/> é o padrão do mundo montado à mão: todo ano vale 1000.</summary>
public sealed record ClimateRules(
    bool Enabled, int MinMilli, int MaxMilli,
    int MeanMilli = 1000, int StdDevMilli = 0, double Persistence = 0, double SharedShare = 0)
{
    public static ClimateRules Disabled { get; } = new(false, 1000, 1000);

    /// <summary>Padrão dos cenários (AD-135): CV de ~20% do rendimento, autocorrelação 0,3, choque 70% comum.</summary>
    public static ClimateRules Default { get; } = new(true, 250, 1500, MeanMilli: 1000, StdDevMilli: 200, Persistence: 0.3, SharedShare: 0.7);

    /// <summary>Abaixo disto o ano é de seca.</summary>
    public const int DroughtBelowMilli = 600;
}

/// <summary>Fator de safra de uma região num ano; nunca editado depois de sorteado.</summary>
public sealed record ClimateYear(RegionId Region, long Year, int FactorMilli);
