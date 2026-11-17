using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Climate;

/// <summary>Tempo de verdade (AD-180): climatologia do lugar do cenário e o gerador do tempo diário por região. Padrão: Inglaterra
/// baixa (Oxford, 51,8° N). Médias mensais da Central England Temperature 1961–1990 (Parker et al. 1992), amplitude diária, dias de
/// chuva (≥ 1 mm) e chuva mensal de Oxford (Met Office, normais 1981–2010); o medieval fica dentro de ±0,5 °C disso (Lamb 1982).
/// Meses são índices 0..11 do calendário (0 = janeiro). Persistência do tempo: cadeia de Markov da chuva (Richardson 1981) e AR(1)
/// diário da anomalia de temperatura. Números sem fonte firme estão marcados "escolhido" no doc de domínio (docs/domain/climate.md).</summary>
public sealed record WeatherRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Latitude em graus (negativa = hemisfério sul: as estações invertem e a luz do dia também).</summary>
    public double LatitudeDeg { get; init; } = 51.8;

    /// <summary>Temperatura-base da célula (bioma/altitude) em que a climatologia vale; as outras células ficam acima ou abaixo dela.</summary>
    public double ReferenceBaseC { get; init; } = 12;

    public IReadOnlyList<double> MeanTempC { get; init; } = [3.8, 3.8, 5.7, 7.9, 11.2, 14.2, 16.1, 15.8, 13.6, 10.6, 6.5, 4.6];
    public IReadOnlyList<double> DiurnalRangeC { get; init; } = [6.0, 6.7, 7.9, 9.4, 10.0, 10.0, 10.3, 10.1, 9.2, 7.6, 6.5, 5.8];
    public IReadOnlyList<double> WetDaysPerMonth { get; init; } = [11.4, 9.1, 10.0, 9.1, 9.4, 8.7, 8.1, 8.6, 9.0, 11.0, 10.9, 10.9];
    public IReadOnlyList<double> RainMmPerMonth { get; init; } = [56, 41, 46, 47, 54, 47, 51, 56, 54, 69, 63, 60];

    /// <summary>Desvio-padrão da anomalia diária da média (°C) por mês: maior no inverno (CET diária).</summary>
    public IReadOnlyList<double> AnomalySdC { get; init; } = [3.2, 3.0, 2.4, 2.0, 1.9, 1.9, 1.8, 1.8, 1.9, 2.2, 2.6, 3.0];

    /// <summary>Chance de chover amanhã dado que choveu hoje (frentes: dias de chuva seguidos).</summary>
    public double WetAfterWet { get; init; } = 0.65;

    /// <summary>Autocorrelação diária da anomalia de temperatura (ondas de frio e calor de vários dias).</summary>
    public double AnomalyPersistence { get; init; } = 0.8;

    /// <summary>Parte do tempo comum a todas as regiões (a frente passa por todas); o resto é local.</summary>
    public double SharedShare { get; init; } = 0.85;

    /// <summary>Regime do ano (AR(1) anual com <see cref="RegimePersistence"/> e a parte comum do <see cref="ClimateRules"/>; escolhido
    /// para a safra repetir a autocorrelação ~0,3 da AD-135 — anos chuvosos em série, como 1315–1317): por desvio-padrão do
    /// regime, a chance de chuva muda nesta fração e a temperatura neste tanto de °C (ano chuvoso e frio × seco e quente).</summary>
    public double RegimePersistence { get; init; } = 0.7;
    public double RegimeRainShare { get; init; } = 0.15;
    public double RegimeTempC { get; init; } = -0.4;

    public double SnowAtOrBelowC { get; init; } = 1.0;
    public double SnowMeltMmPerDegDay { get; init; } = 4.0;
    public double StormAtOrAboveMm { get; init; } = 15;
    public double HailAtOrAboveMm { get; init; } = 20;
    public double HailMinMeanC { get; init; } = 12;
    public double FrostBelowC { get; init; } = 0;
    public double HeatWaveMaxC { get; init; } = 27;
    public double ColdWaveMeanC { get; init; } = -2;
    public int WaveDays { get; init; } = 3;

    /// <summary>Vento médio (m/s) por mês e o de tempestade.</summary>
    public IReadOnlyList<double> WindMs { get; init; } = [5.5, 5.3, 5.2, 4.6, 4.2, 3.9, 3.8, 3.8, 4.1, 4.6, 5.0, 5.3];
    public double StormWindMs { get; init; } = 14;

    // chão de terra (trilha, terra batida, campo): quanto molha por mm, quanto seca por dia (mais no calor), e as faixas
    public int SoilWetPmPerMm { get; init; } = 60;
    public int SoilDryBasePm { get; init; } = 40;
    public int SoilDryPerDegPm { get; init; } = 8;
    public int WetAbovePm { get; init; } = 150;
    public int MudAbovePm { get; init; } = 400;
    public int HeavyMudAbovePm { get; init; } = 750;

    /// <summary>Passo no chão de terra: molhado, lama, lamaçal (multiplica o tempo da parte sem calçamento da rota).</summary>
    public double WetTravelFactor { get; init; } = 1.1;
    public double MudTravelFactor { get; init; } = 1.35;
    public double HeavyMudTravelFactor { get; init; } = 1.6;

    /// <summary>Neve no chão (cm) atrasa a rota inteira, calçada ou não, até <see cref="MaxSnowTravelFactor"/>.</summary>
    public double SnowTravelPerCm { get; init; } = 0.03;
    public double MaxSnowTravelFactor { get; init; } = 2.5;
    public int CartStopSnowCm { get; init; } = 15;

    /// <summary>Seca: a chuva da janela (média móvel) abaixo desta fração da normal.</summary>
    public int DroughtWindowDays { get; init; } = 60;
    public double DroughtBelowRatio { get; init; } = 0.5;

    // trabalho ao ar livre: para com chuva forte, neve no chão ou chão congelado; a ceifa para com qualquer chuva
    public double HeavyRainMm { get; init; } = 8;
    public double ReapStopsAtOrAboveMm { get; init; } = 1;
    public int FieldSnowCm { get; init; } = 2;
    public double FrozenGroundMeanC { get; init; } = -1;

    /// <summary>Horas de luz que a jornada do campo usa inteira; abaixo, rende na proporção (do nascer ao pôr do sol).</summary>
    public double FieldDayHours { get; init; } = 10;

    /// <summary>Granizo derruba esta parte do grão em pé.</summary>
    public double HailCropLoss { get; init; } = 0.15;

    // safra do ano: índice de estresse de jan–jun (chuva da primavera fora da faixa, frio/calor da primavera, geada tardia,
    // inverno sem neve que mata a semente), padronizado para a média e o DP do fator de safra (AD-135)
    public double CropDryBelowRatio { get; init; } = 0.55;
    public double CropWetAboveRatio { get; init; } = 1.0;
    public double CropDryWeight { get; init; } = 1.0;
    public double CropWetWeight { get; init; } = 1.5;
    public double CropWarmthWeight { get; init; } = 0.3;
    public double CropLateFrostWeight { get; init; } = 0.04;
    public double CropWinterKillWeight { get; init; } = 0.04;
    public double CropIndexMean { get; init; } = -0.2495;
    public double CropIndexSd { get; init; } = 0.4151;
    public int CropFactorMonth { get; init; } = 6;

    // corpo: excesso de risco de morte pela anomalia fria/quente do mês e o relento
    public double ColdHazardPerC { get; init; } = 0.03;
    public double ColdHazardPerCOthers { get; init; } = 0.01;
    public double ColdSeasonBelowC { get; init; } = 10;
    public double HeatThresholdMaxC { get; init; } = 24;
    public double HeatHazardPerC { get; init; } = 0.03;
    public double RoughSleepDeathPerDegNight { get; init; } = 0.001;
    public double RoughSleepVulnerableFactor { get; init; } = 3;

    /// <summary>Seca: a fonte natural dá só esta fração da água por dia de seca severa (a fração de chuva sobre o limiar).</summary>
    public double DroughtSpringFloor { get; init; } = 0.2;

    /// <summary>Neve que tira metade e três quartos do pasto (cm).</summary>
    public int GrazeHalfSnowCm { get; init; } = 5;
    public int GrazeQuarterSnowCm { get; init; } = 15;

    /// <summary>Peso do mau tempo contra sair para conversar e contra a ida não urgente ao mercado.</summary>
    public double OutingWeatherPenalty { get; init; } = 0.5;

    public static WeatherRules Default { get; } = new();

    public static WeatherRules Off { get; } = new() { Enabled = false };

    /// <summary>Valor do mês pelo dia do ano (0..diasNoAno-1), interpolado entre os meios dos meses: a estação muda aos poucos,
    /// não em degrau. O hemisfério sul desloca seis meses.</summary>
    public double Interpolate(IReadOnlyList<double> monthly, double dayOfYear, int daysPerMonth, int monthsPerYear)
    {
        int n = monthly.Count;
        if (n == 0) return 0;
        double month = dayOfYear / daysPerMonth - 0.5 + (LatitudeDeg < 0 ? monthsPerYear / 2.0 : 0);
        double scaled = month * n / monthsPerYear;
        int lo = (int)Math.Floor(scaled);
        double t = scaled - lo;
        double a = monthly[((lo % n) + n) % n], b = monthly[(((lo + 1) % n) + n) % n];
        return a + (b - a) * t;
    }

    /// <summary>Horas de sol no dia do ano (fórmula da declinação; arco-cosseno por bisseção, só + − × ÷ e cosseno determinístico).</summary>
    public double DaylightHours(double dayOfYear, int daysInYear)
    {
        double realDay = dayOfYear * 365.0 / daysInYear;
        double declTurns = -23.44 / 360.0 * DeterministicMath.CosTurns((realDay + 10) / 365.0);
        double latTurns = Math.Clamp(LatitudeDeg, -89.0, 89.0) / 360.0;
        double tanLat = DeterministicMath.SinTurns(latTurns) / DeterministicMath.CosTurns(latTurns);
        double tanDecl = DeterministicMath.SinTurns(declTurns) / DeterministicMath.CosTurns(declTurns);
        double c = Math.Clamp(-tanLat * tanDecl, -1.0, 1.0);
        double lo = 0, hi = 0.5;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2;
            if (DeterministicMath.CosTurns(mid) > c) lo = mid; else hi = mid;
        }
        return 48.0 * (lo + hi) / 2;
    }
}
