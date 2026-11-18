using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Climate;

/// <summary>O passo de um dia do tempo de uma região (AD-180), função pura das regras, da linha de ontem e dos sorteios do dia: o
/// sistema chama com os sorteios do RNG do mundo, a calibração do índice da safra chama com um RNG próprio.</summary>
public static class WeatherGenerator
{
    /// <summary>Sorteios do dia: os comuns a todas as regiões e os locais. Normais de média 0 e variância 1.</summary>
    public readonly record struct Draws(double Pick, double RainU, double AmountU, double WindU, double TempNormal);

    /// <summary>AD-189: sorteios do vento do dia (continua ou muda, setor da rosa, ponto no setor, giro).</summary>
    public readonly record struct WindDraws(double Keep, double Sector, double Within, double Veer);

    public static WindDraws ReadWind(ref ulong state) =>
        new(WorldRng.NextDouble(ref state), WorldRng.NextDouble(ref state), WorldRng.NextDouble(ref state), WorldRng.NextDouble(ref state));

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

    private readonly record struct Climatology(double Sd, double Clim, double Freq, double NormalMm, double Range, double Wind,
        double Advection, double WarmFromDeg, double MeanCos, double VarCos);

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
            r.Interpolate(r.WindMs, dayOfYear, daysPerMonth, monthsPerYear),
            0, 0, 0, 0);
        if (r.WindAdvectionC.Count > 0 && r.WarmWindFromDeg.Count > 0)
        {
            double warm = r.Interpolate(r.WarmWindFromDeg, dayOfYear, daysPerMonth, monthsPerYear);
            var (meanCos, varCos) = RoseMoments(r, warm);
            value = value with
            {
                Advection = r.Interpolate(r.WindAdvectionC, dayOfYear, daysPerMonth, monthsPerYear),
                WarmFromDeg = warm, MeanCos = meanCos, VarCos = varCos,
            };
        }
        _day = (r, dayOfYear, daysPerMonth, monthsPerYear, value);
        return value;
    }

    /// <summary>Média e variância de cos(direção − quente) sob a rosa (setor uniforme de 45°): a advecção sai centrada (a média do
    /// mês não muda) e o ruído do dia perde a variância que ela explica.</summary>
    private static (double Mean, double Var) RoseMoments(WeatherRules r, double warmFromDeg)
    {
        var rose = r.WindRoseFrom;
        double total = 0, m1 = 0, m2 = 0;
        for (int k = 0; k < rose.Count; k++) total += Math.Max(0, rose[k]);
        if (total <= 0) return (0, 0);
        double width = 360.0 / rose.Count;
        for (int k = 0; k < rose.Count; k++)
        {
            double p = Math.Max(0, rose[k]) / total, turns = (k * width - warmFromDeg) / 360.0;
            // média de cos e de cos² num setor uniforme: o centro × sinc da meia largura (π/8 → 0,9745; π/4 → 0,9003 para 8 setores)
            double half = width / 360.0 / 2.0;
            m1 += p * DeterministicMath.CosTurns(turns) * Sinc(half);
            m2 += p * (0.5 + 0.5 * DeterministicMath.CosTurns(2 * turns) * Sinc(2 * half));
        }
        return (m1, Math.Max(0, m2 - m1 * m1));
    }

    /// <summary>sen(2π·t)/(2π·t), com t em voltas.</summary>
    private static double Sinc(double turns) => turns == 0 ? 1 : DeterministicMath.SinTurns(turns) / (2 * Math.PI * turns);

    /// <summary>AD-189: de onde o vento vem hoje (graus a partir do norte): o de ontem com um giro, se o sistema de pressão continua;
    /// senão um novo, sorteado da rosa do lugar.</summary>
    public static int WindFrom(WeatherRules r, int yesterdayDeg, WindDraws d, bool first)
    {
        double deg;
        if (!first && d.Keep < Math.Clamp(r.WindDirectionPersistence, 0, 1))
            deg = yesterdayDeg + (d.Veer * 2 - 1) * r.WindVeerDeg;
        else
        {
            var rose = r.WindRoseFrom;
            double total = 0;
            for (int k = 0; k < rose.Count; k++) total += Math.Max(0, rose[k]);
            if (rose.Count == 0 || total <= 0) return yesterdayDeg;
            double u = d.Sector * total, acc = 0;
            int sector = rose.Count - 1;
            for (int k = 0; k < rose.Count; k++)
            {
                acc += Math.Max(0, rose[k]);
                if (u < acc) { sector = k; break; }
            }
            double width = 360.0 / rose.Count;
            deg = sector * width + (d.Within - 0.5) * width;
        }
        int rounded = (int)Math.Round(deg) % 360;
        return rounded < 0 ? rounded + 360 : rounded;
    }

    /// <summary>Avança a linha um dia. <paramref name="first"/> = o primeiro dia do mundo (chuva média e chão meio molhado).</summary>
    public static void Step(
        WeatherRules r, WeatherRegionRow row, Draws shared, Draws local, double dayOfYear, int daysPerMonth, int monthsPerYear,
        bool first, WindDraws? sharedWind = null, WindDraws? localWind = null)
    {
        var c = DayClimate(r, dayOfYear, daysPerMonth, monthsPerYear);
        double w = Math.Clamp(r.SharedShare, 0, 1);
        double z = row.RegimeMilli / 1000.0;
        double rho = Math.Clamp(r.AnomalyPersistence, 0, 0.99);
        double sd = c.Sd;
        bool shareDraw = local.Pick < w;

        // AD-189: de onde o vento vem (a frente é comum às regiões como a chuva) e o ar que ele traz: a advecção centrada na rosa, e o
        // ruído do dia sem a parte que ela explica (a variância do dia continua a da fonte)
        double advection = 0;
        if (sharedWind is { } sw && localWind is { } lw)
        {
            row.WindFromDeg = WindFrom(r, row.WindFromDeg, shareDraw ? sw : lw, first);
            if (c.Advection != 0)
            {
                advection = c.Advection * (DeterministicMath.CosTurns((row.WindFromDeg - c.WarmFromDeg) / 360.0) - c.MeanCos);
                double explained = c.Advection * c.Advection * c.VarCos;
                sd = Math.Sqrt(Math.Max(0.25 * sd * sd, sd * sd - explained));
            }
        }
        row.AdvectionCenti = (int)Math.Round(advection * 100);

        double shock = Math.Sqrt(w) * shared.TempNormal + Math.Sqrt(1 - w) * local.TempNormal;
        double anomaly = (first ? 0 : rho * row.AnomalyCenti / 100.0) + sd * Math.Sqrt(1 - rho * rho) * shock;
        double clim = c.Clim;
        double mean = clim + anomaly + advection + z * r.RegimeTempC;

        // chuva: cadeia de Markov de dois estados; a frequência do mês, mudada pelo regime do ano
        double normalFreq = c.Freq;
        double freq = Math.Clamp(normalFreq * (1 + z * r.RegimeRainShare), 0.02, 0.95);
        double p11 = Math.Max(Math.Clamp(r.WetAfterWet, 0, 0.99), freq + 0.01);
        double p01 = freq * (1 - p11) / (1 - freq);
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

    /// <summary>AD-189: a folhagem do dia na região. <paramref name="seasonalDay"/> é o dia do ano contado do meio do inverno (janeiro no
    /// norte, julho no sul); <paramref name="daylightHours"/> a luz do dia. Na primeira metade do ano o calor acumula (grau-dia acima da
    /// base) e a folha brota e enche; na segunda, com a luz já curta, o frio acumula (grau-dia abaixo da base da senescência), a copa
    /// muda de cor e, passado o limiar, a folha cai em <see cref="WeatherRules.LeafFallDays"/> dias. No primeiro dia do mundo as somas
    /// partem da climatologia (o valor esperado do dia, pela média e pelo desvio), não de zero.</summary>
    public static void Foliage(WeatherRules r, WeatherRegionRow row, double seasonalDay, int daysPerMonth, int monthsPerYear,
        double daylightHours, bool first)
    {
        if (!r.FoliageEnabled) return;
        int daysInYear = daysPerMonth * monthsPerYear;
        if (first) SeedFoliage(r, row, seasonalDay, daysPerMonth, monthsPerYear);
        else if ((int)seasonalDay == 0)
        {
            row.LeafGddTenths = row.LeafColdTenths = 0;
            if (row.LeafPm > 0) row.LeafPhase = LeafPhase.Falling; // outono quente: o que sobrou cai no inverno
        }
        double mean = row.MeanCenti / 100.0;
        bool autumn = seasonalDay >= daysInYear / 2.0;
        if (!autumn) row.LeafGddTenths += (int)Math.Round(Math.Max(0, mean - r.LeafBaseC) * 10);
        else if (daylightHours < r.SenescenceDaylightHours) row.LeafColdTenths += (int)Math.Round(Math.Max(0, r.SenescenceBaseC - mean) * 10);
        AdvanceLeaves(r, row, autumn);
    }

    private static void AdvanceLeaves(WeatherRules r, WeatherRegionRow row, bool autumn)
    {
        int step = (int)Math.Ceiling(1000.0 / Math.Max(1, r.LeafFallDays));
        if (row.LeafPhase == LeafPhase.Falling || (autumn && row.LeafPm > 0 && row.LeafColdTenths >= r.LeafFallColdDegreeDays * 10))
        {
            row.LeafPm = Math.Max(0, row.LeafPm - step);
            row.LeafPhase = row.LeafPm > 0 ? LeafPhase.Falling : LeafPhase.Bare;
            return;
        }
        if (autumn && row.LeafPhase == LeafPhase.Bare) return; // caiu: não brota de novo até a primavera
        double grown = (row.LeafGddTenths / 10.0 - r.BudburstDegreeDays) / Math.Max(1, r.LeafOutDegreeDays);
        int target = (int)Math.Round(Math.Clamp(grown, 0, 1) * 1000);
        if (!autumn) row.LeafPm = Math.Max(row.LeafPm, target);
        row.LeafPhase = row.LeafPm <= 0 ? LeafPhase.Bare : row.LeafPm < 1000 && !autumn ? LeafPhase.Budding : LeafPhase.Full;
    }

    /// <summary>As somas da folhagem no primeiro dia do mundo, pelo valor esperado de cada dia desde o meio do inverno (média do
    /// clima e desvio do dia, normal), e a folha que isso dá.</summary>
    private static void SeedFoliage(WeatherRules r, WeatherRegionRow row, double seasonalDay, int daysPerMonth, int monthsPerYear)
    {
        int daysInYear = daysPerMonth * monthsPerYear;
        double shift = r.LatitudeDeg < 0 ? daysInYear / 2.0 : 0; // o dia do ano do calendário que corresponde ao dia sazonal
        double gdd = 0, cold = 0;
        row.LeafPm = 0;
        row.LeafPhase = LeafPhase.Bare;
        row.LeafGddTenths = row.LeafColdTenths = 0;
        for (int d = 0; d < (int)seasonalDay; d++)
        {
            double doy = (d + shift + 0.5) % daysInYear;
            double mu = r.Interpolate(r.MeanTempC, doy, daysPerMonth, monthsPerYear);
            double sd = r.Interpolate(r.AnomalySdC, doy, daysPerMonth, monthsPerYear);
            bool autumn = d >= daysInYear / 2.0;
            if (!autumn) gdd += ExpectedExcess(mu - r.LeafBaseC, sd);
            else if (r.DaylightHours(doy, daysInYear) < r.SenescenceDaylightHours) cold += ExpectedExcess(r.SenescenceBaseC - mu, sd);
            row.LeafGddTenths = (int)Math.Round(gdd * 10);
            row.LeafColdTenths = (int)Math.Round(cold * 10);
            AdvanceLeaves(r, row, autumn);
        }
    }

    /// <summary>E[max(0, X)] de uma normal de média <paramref name="mu"/> e desvio <paramref name="sd"/> (Φ pela logística 1,702).</summary>
    private static double ExpectedExcess(double mu, double sd)
    {
        if (sd <= 0) return Math.Max(0, mu);
        double z = mu / sd;
        double pdf = DeterministicMath.Exp(-0.5 * z * z) / 2.5066282746310002;
        double cdf = 1 / (1 + DeterministicMath.Exp(-1.702 * z));
        return Math.Max(0, mu * cdf + sd * pdf);
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
