using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Climate;

/// <summary>Clima do cenário (weather-drought): o ano de safra de cada região é um fator em milésimos, sorteado uma vez por
/// (região, ano) entre <see cref="MinMilli"/> e <see cref="MaxMilli"/> (1000 = ano médio). Com <see cref="StdDevMilli"/> &gt; 0
/// (AD-135) o fator é AR(1) em torno de <see cref="MeanMilli"/>: o ano herda <see cref="Persistence"/> do desvio do ano
/// anterior, e o choque do ano tem uma parte comum a todas as regiões (<see cref="SharedShare"/> da variância) — secas
/// duram e atingem regiões vizinhas juntas. Com <see cref="StdDevMilli"/> = 0 vale o sorteio uniforme antigo.
/// <see cref="Disabled"/> é o padrão do mundo montado à mão: todo ano vale 1000. AD-180: com <see cref="Weather"/> ligado o sorteio
/// anual vira o regime do ano (chuvoso e frio × seco e quente), o tempo de cada dia sai dele e o fator de safra sai do tempo
/// (uma fonte só); nulo = sem tempo diário (snapshot antigo e mundo montado à mão).</summary>
public sealed record ClimateRules(
    bool Enabled, int MinMilli, int MaxMilli,
    int MeanMilli = 1000, int StdDevMilli = 0, double Persistence = 0, double SharedShare = 0, WeatherRules? Weather = null)
{
    public static ClimateRules Disabled { get; } = new(false, 1000, 1000);

    /// <summary>Padrão dos cenários (AD-135): CV de ~20% do rendimento, autocorrelação 0,3, choque 70% comum; AD-180: tempo diário.</summary>
    public static ClimateRules Default { get; } = new(true, 250, 1500, MeanMilli: 1000, StdDevMilli: 200, Persistence: 0.3, SharedShare: 0.7,
        Weather: WeatherRules.Default);

    /// <summary>O tempo diário está ligado (clima ligado e bloco de tempo ligado).</summary>
    public bool WeatherOn() => Enabled && Weather is { Enabled: true };

    /// <summary>Abaixo disto o ano é de seca.</summary>
    public const int DroughtBelowMilli = 600;
}

/// <summary>Fator de safra de uma região num ano; nunca editado depois de sorteado.</summary>
public sealed record ClimateYear(RegionId Region, long Year, int FactorMilli);
