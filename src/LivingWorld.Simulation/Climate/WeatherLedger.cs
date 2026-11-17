namespace LivingWorld.Simulation.Climate;

/// <summary>Tipo de precipitação do dia.</summary>
public enum Precipitation : byte { None = 0, Rain = 1, Snow = 2 }

/// <summary>O tempo de uma região (AD-180), uma linha por região: o dia de hoje, o que o tempo deixou no chão (neve, terra molhada) e
/// as somas que outros sistemas leem (seca, estação da safra, frio e calor do mês). Tudo inteiro: centésimos de °C, décimos de mm,
/// milésimos. Canônico (entra no hash): a temperatura, a lavoura, o passo, a doença e a morte leem daqui.</summary>
public sealed class WeatherRegionRow
{
    public int Region { get; set; }

    /// <summary>Regime do ano (desvio-padrão × 1000): positivo = ano chuvoso e frio.</summary>
    public int RegimeMilli { get; set; }

    /// <summary>Anomalia do dia (AR(1), sem o regime), °C × 100.</summary>
    public int AnomalyCenti { get; set; }
    public int MeanCenti { get; set; }
    public int MinCenti { get; set; }
    public int MaxCenti { get; set; }

    /// <summary>Precipitação do dia em décimos de mm (equivalente em água quando é neve).</summary>
    public int PrecipTenths { get; set; }
    public bool Wet { get; set; }
    public Precipitation Kind { get; set; }
    public bool Storm { get; set; }
    public bool Hail { get; set; }
    public bool Frost { get; set; }
    public int WindDecims { get; set; }

    /// <summary>Neve no chão em mm de água (≈ cm de neve fofa).</summary>
    public int SnowMm { get; set; }

    /// <summary>Terra molhada (trilha, terra batida, campo), 0–1000.</summary>
    public int SoilPm { get; set; }

    /// <summary>Média móvel exponencial da chuva e da chuva normal (décimos de mm/dia): a seca é a razão.</summary>
    public int RainAvgTenths { get; set; }
    public int NormalAvgTenths { get; set; }
    public int HotDays { get; set; }
    public int ColdDays { get; set; }

    // mês corrente e o último mês fechado (corpo)
    public int MonthMeanSumCenti { get; set; }
    public int MonthMaxSumCenti { get; set; }
    public int MonthDays { get; set; }
    public int LastColdCenti { get; set; }
    public int LastHeatCenti { get; set; }

    // estação da safra (janeiro até o mês do fator)
    public int SpringRainTenths { get; set; }
    public int SpringNormalTenths { get; set; }
    public int SpringAnomSumCenti { get; set; }
    public int SpringDays { get; set; }
    public int LateFrostNights { get; set; }
    public int WinterKillDays { get; set; }

    /// <summary>Dias do ano em que o trabalho ao ar livre parou (diagnóstico e projeção).</summary>
    public int OutdoorLostDays { get; set; }
}

/// <summary>O tempo do mundo (AD-180): o dia gerado, a luz do dia e uma linha por região em ordem de id.</summary>
public sealed class WeatherLedger
{
    /// <summary>Dia (desde o início do mundo) do tempo guardado; −1 antes do primeiro.</summary>
    public long Day { get; set; } = -1;

    /// <summary>Temperatura-base (°C × 100) da célula onde a climatologia vale: a da primeira cidade do mundo (o lugar da fonte é o
    /// lugar onde se vive), fixada no primeiro dia; <c>int.MinValue</c> = a da regra.</summary>
    public int ReferenceCenti { get; set; } = int.MinValue;

    /// <summary>Minutos de sol do dia (nascer ao pôr).</summary>
    public int DaylightMinutes { get; set; }

    public List<WeatherRegionRow> Regions { get; set; } = [];

    /// <summary>Muda a cada dia gerado (cache volátil de temperatura e de chão).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long Version { get; set; }

    private Dictionary<int, WeatherRegionRow>? _byRegion;

    internal WeatherRegionRow? Find(int region)
    {
        if (_byRegion is null || _byRegion.Count != Regions.Count)
        {
            _byRegion = new Dictionary<int, WeatherRegionRow>(Regions.Count);
            foreach (var row in Regions) _byRegion[row.Region] = row;
        }
        return _byRegion.GetValueOrDefault(region);
    }

    internal void Reindex() => _byRegion = null;
}
