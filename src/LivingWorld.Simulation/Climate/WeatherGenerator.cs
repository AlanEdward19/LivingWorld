using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Climate;

/// <summary>O passo de um dia do tempo de uma região (AD-180), função pura das regras, da linha de ontem e dos sorteios do dia: o
/// sistema chama com os sorteios do RNG do mundo, a calibração do índice da safra chama com um RNG próprio.</summary>
public static class WeatherGenerator
{
    /// <summary>Sorteios do dia: os comuns a todas as regiões e os locais. Normais de média 0 e variância 1.</summary>
    public readonly record struct Draws(double Pick, double RainU, double AmountU, double WindU, double TempNormal);

    public static Draws Read(ref ulong state)
    {
        double pick = WorldRng.NextDouble(ref state), rain = WorldRng.NextDouble(ref state);
        double amount = WorldRng.NextDouble(ref state), wind = WorldRng.NextDouble(ref state);
        double normal = (WorldRng.NextDouble(ref state) + WorldRng.NextDouble(ref state) + WorldRng.NextDouble(ref state)
            + WorldRng.NextDouble(ref state) - 2.0) * Math.Sqrt(3.0);
        return new Draws(pick, rain, amount, wind, normal);
    }

    /// <summary>Chuva normal do dia do ano (mm) pela climatologia.</summary>
    public static double NormalRainMm(WeatherRules r, double dayOfYear, int daysPerMonth, int monthsPerYear) =>
        r.Interpolate(r.RainMmPerMonth, dayOfYear, daysPerMonth, monthsPerYear) * monthsPerYear / 365.0;

    private readonly record struct Climatology(double Sd, double Clim, double Freq, double NormalMm, double Range, double Wind);

    [ThreadStatic] private static (WeatherRules? Rules, double Day, int Dpm, int Mpy, Climatology Value) _day;

    /// <summary>A climatologia do dia, igual para todas as regiões: calculada uma vez por dia (antes era por região).</summary>
    private static Climatology DayClimate(WeatherRules r, double dayOfYear, int daysPerMonth, int monthsPerYear)
    {
        var known = _day;
        if (ReferenceEquals(known.Rules, r) && known.Day == dayOfYear && known.Dpm == daysPerMonth && known.Mpy == monthsPerYear) return known.Value;
        var value = new Climatology(
            r.Interpolate(r.AnomalySdC, dayOfYear, daysPerMonth, monthsPerYear),
            r.Interpolate(r.MeanTempC, dayOfYear, daysPerMonth, monthsPerYear),
            Math.Clamp(r.Interpolate(r.WetDaysPerMonth, dayOfYear, daysPerMonth, monthsPerYear) / (365.0 / monthsPerYear), 0.02, 0.95),
            NormalRainMm(r, dayOfYear, daysPerMonth, monthsPerYear),
            r.Interpolate(r.DiurnalRangeC, dayOfYear, daysPerMonth, monthsPerYear),
            r.Interpolate(r.WindMs, dayOfYear, daysPerMonth, monthsPerYear));
        _day = (r, dayOfYear, daysPerMonth, monthsPerYear, value);
        return value;
    }

    /// <summary>Avança a linha um dia. <paramref name="first"/> = o primeiro dia do mundo (chuva média e chão meio molhado).</summary>
    public static void Step(
        WeatherRules r, WeatherRegionRow row, Draws shared, Draws local, double dayOfYear, int daysPerMonth, int monthsPerYear,
        bool first)
    {
        var c = DayClimate(r, dayOfYear, daysPerMonth, monthsPerYear);
        double w = Math.Clamp(r.SharedShare, 0, 1);
        double z = row.RegimeMilli / 1000.0;
        double rho = Math.Clamp(r.AnomalyPersistence, 0, 0.99);
        double sd = c.Sd;
        double shock = Math.Sqrt(w) * shared.TempNormal + Math.Sqrt(1 - w) * local.TempNormal;
        double anomaly = (first ? 0 : rho * row.AnomalyCenti / 100.0) + sd * Math.Sqrt(1 - rho * rho) * shock;
        double clim = c.Clim;
        double mean = clim + anomaly + z * r.RegimeTempC;

        // chuva: cadeia de Markov de dois estados; a frequência do mês, mudada pelo regime do ano
        double normalFreq = c.Freq;
        double freq = Math.Clamp(normalFreq * (1 + z * r.RegimeRainShare), 0.02, 0.95);
        double p11 = Math.Max(Math.Clamp(r.WetAfterWet, 0, 0.99), freq + 0.01);
        double p01 = freq * (1 - p11) / (1 - freq);
        bool shareDraw = local.Pick < w;
        double u = shareDraw ? shared.RainU : local.RainU;
        bool wet = u < (row.Wet && !first ? p11 : p01);
        double normalMm = c.NormalMm;
        double intensity = Math.Max(1.0, normalMm / normalFreq); // o regime muda quantos dias chovem, não quanto chove por dia
        double amount = 0;
        if (wet)
        {
            double ua = shareDraw ? shared.AmountU : local.AmountU;
            amount = 1.0 + (intensity - 1.0) * -DeterministicMath.Log(Math.Max(1e-12, 1 - ua));
        }

        // nublado e chuvoso: amplitude menor; seco e limpo: maior (a média do mês continua a da fonte)
        double range = c.Range;
        range *= wet ? 0.75 : (1 - 0.75 * freq) / (1 - freq);
        double min = mean - range / 2, max = mean + range / 2;

        var kind = !wet ? Precipitation.None : mean <= r.SnowAtOrBelowC ? Precipitation.Snow : Precipitation.Rain;
        bool storm = wet && amount >= r.StormAtOrAboveMm;
        double windU = shareDraw ? shared.WindU : local.WindU;
        double wind = c.Wind * (0.5 + windU);
        if (storm) wind = Math.Max(wind, r.StormWindMs);

        // neve no chão e o que ela solta derretendo
        double snow = row.SnowMm + (kind == Precipitation.Snow ? amount : 0);
        double melt = Math.Min(snow, Math.Max(0, mean) * r.SnowMeltMmPerDegDay);
        snow -= melt;

        // terra molhada: a chuva e o degelo molham; o dia seca (mais no calor); congelada ou sob neve, não seca
        double soil = first ? r.MudAbovePm * 0.75 : row.SoilPm;
        soil += ((kind == Precipitation.Rain ? amount : 0) + melt) * r.SoilWetPmPerMm;
        if (mean > r.FrozenGroundMeanC && snow < 1)
            soil -= r.SoilDryBasePm + r.SoilDryPerDegPm * Math.Max(0, mean);

        // seca: médias móveis da chuva e da normal
        int precipTenths = (int)Math.Round(amount * 10);
        int normalTenths = (int)Math.Round(normalMm * 10);
        double alpha = 1.0 / Math.Max(1, r.DroughtWindowDays);
        row.RainAvgTenths = first ? normalTenths : (int)Math.Round(row.RainAvgTenths + alpha * (precipTenths - row.RainAvgTenths));
        row.NormalAvgTenths = first ? normalTenths : (int)Math.Round(row.NormalAvgTenths + alpha * (normalTenths - row.NormalAvgTenths));

        row.AnomalyCenti = (int)Math.Round(anomaly * 100);
        row.MeanCenti = (int)Math.Round(mean * 100);
        row.MinCenti = (int)Math.Round(min * 100);
        row.MaxCenti = (int)Math.Round(max * 100);
        row.PrecipTenths = precipTenths;
        row.Wet = wet;
        row.Kind = kind;
        row.Storm = storm;
        row.Hail = storm && kind == Precipitation.Rain && amount >= r.HailAtOrAboveMm && mean >= r.HailMinMeanC;
        row.Frost = min < r.FrostBelowC;
        row.WindDecims = (int)Math.Round(wind * 10);
        row.SnowMm = (int)Math.Round(snow);
        row.SoilPm = (int)Math.Clamp(Math.Round(soil), 0, 1000);
        row.HotDays = max >= r.HeatWaveMaxC ? row.HotDays + 1 : 0;
        row.ColdDays = mean <= r.ColdWaveMeanC ? row.ColdDays + 1 : 0;
        row.MonthMeanSumCenti += row.MeanCenti;
        row.MonthMaxSumCenti += row.MaxCenti;
        row.MonthDays++;

        int month = (int)(dayOfYear / daysPerMonth);
        if (month < r.CropFactorMonth)
        {
            if (month >= 2)
            {
                row.SpringRainTenths += precipTenths;
                row.SpringNormalTenths += normalTenths;
                row.SpringAnomSumCenti += (int)Math.Round((mean - clim) * 100);
                row.SpringDays++;
            }
            if (month is 3 or 4 && min < -1) row.LateFrostNights++;
            if (month is 0 or 1 && mean <= -4 && snow < 5) row.WinterKillDays++;
        }
    }

    /// <summary>Índice cru de estresse da safra da estação (janeiro até o mês do fator): chuva da primavera fora da faixa boa,
    /// primavera quente ou fria, geada tardia e inverno que congela a semente sem neve por cima.</summary>
    public static double CropIndex(WeatherRules r, WeatherRegionRow row)
    {
        double p = row.SpringNormalTenths > 0 ? row.SpringRainTenths / (double)row.SpringNormalTenths : 1.0;
        double t = row.SpringDays > 0 ? row.SpringAnomSumCenti / (100.0 * row.SpringDays) : 0;
        return -r.CropDryWeight * Math.Max(0, r.CropDryBelowRatio - p) - r.CropWetWeight * Math.Max(0, p - r.CropWetAboveRatio)
            + r.CropWarmthWeight * t - r.CropLateFrostWeight * row.LateFrostNights - r.CropWinterKillWeight * row.WinterKillDays;
    }

    /// <summary>Fator de safra (milésimos) do índice: padronizado e posto na média e no desvio do clima do cenário (AD-135); sem
    /// desvio declarado, a faixa uniforme antiga vira média e desvio dela.</summary>
    public static int CropFactor(ClimateRules climate, WeatherRules r, double index)
    {
        int min = Math.Max(0, climate.MinMilli), max = Math.Max(min, climate.MaxMilli);
        double mean = climate.StdDevMilli > 0 ? climate.MeanMilli : (min + max) / 2.0;
        double sd = climate.StdDevMilli > 0 ? climate.StdDevMilli : (max - min) / Math.Sqrt(12.0);
        double z = (index - r.CropIndexMean) / Math.Max(1e-6, r.CropIndexSd);
        // a cauda de baixo encolhe: o ano chuvoso de verdade tira 40–50% da safra (1315, 1556), não 75%
        if (z < -1) z = -1 + (z + 1) * r.CropLowTailShrink;
        return (int)Math.Round(Math.Clamp(r.CropFactorMeanShift + mean + sd * z, min, max));
    }

    /// <summary>Começo do ano: o regime novo (AR(1) com a persistência e a parte comum do clima) e a estação da safra zerada.</summary>
    public static void NewYear(ClimateRules climate, WeatherRegionRow row, double sharedNormal, double localNormal, bool first)
    {
        double w = Math.Clamp(climate.SharedShare, 0, 1), rho = Math.Clamp(climate.Weather?.RegimePersistence ?? climate.Persistence, 0, 0.99);
        double shock = Math.Sqrt(w) * sharedNormal + Math.Sqrt(1 - w) * localNormal;
        double z = (first ? 0 : rho * row.RegimeMilli / 1000.0) + Math.Sqrt(first ? 1 : 1 - rho * rho) * shock;
        row.RegimeMilli = (int)Math.Round(Math.Clamp(z, -4, 4) * 1000);
        row.SpringRainTenths = row.SpringNormalTenths = row.SpringAnomSumCenti = row.SpringDays = 0;
        row.LateFrostNights = row.WinterKillDays = 0;
        row.OutdoorLostDays = 0;
    }

    /// <summary>Fecha o mês para o corpo: frio do mês abaixo do normal (só na estação fria) e calor das máximas acima do limiar.</summary>
    public static void CloseMonth(WeatherRules r, WeatherRegionRow row, double normalMeanC)
    {
        if (row.MonthDays > 0)
        {
            double mean = row.MonthMeanSumCenti / (100.0 * row.MonthDays), maxMean = row.MonthMaxSumCenti / (100.0 * row.MonthDays);
            row.LastColdCenti = normalMeanC <= r.ColdSeasonBelowC ? (int)Math.Round(Math.Max(0, normalMeanC - mean) * 100) : 0;
            row.LastHeatCenti = (int)Math.Round(Math.Max(0, maxMean - r.HeatThresholdMaxC) * 100);
        }
        row.MonthMeanSumCenti = row.MonthMaxSumCenti = row.MonthDays = 0;
    }
}
