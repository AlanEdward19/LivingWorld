using System.Globalization;
using System.Text;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>AD-180: diagnóstico do tempo contra a fonte. <c>weather-diag calib &lt;anos&gt;</c> roda só o gerador (uma região, RNG
/// próprio) e imprime média e DP do índice cru da safra e a distribuição do fator; <c>weather-diag world &lt;seed&gt;</c> roda o
/// mundo de código um ano e imprime, por mês, temperatura média, dias de chuva e de neve, horas de luz, fração dos dias com lama na
/// trilha e dias de trabalho ao ar livre perdidos.</summary>
public static class WeatherDiagCommand
{
    public static int Run(string mode, string arg)
    {
        var inv = CultureInfo.InvariantCulture;
        var cal = ScenarioRunner.DefaultCalendar;
        var rules = WeatherRules.Default;
        if (mode == "calib")
        {
            int years = int.Parse(arg, inv);
            var climate = ClimateRules.Default;
            var rng = new WorldRng(180);
            var row = new WeatherRegionRow();
            var raw = new List<double>();
            var factors = new List<int>();
            var regimes = new List<double>();
            var rainDays = new double[12];
            var snowDays = new double[12];
            for (int y = 0; y < years; y++)
            {
                ulong ys = rng.DeriveState(y);
                WeatherGenerator.NewYear(climate, row, WeatherGenerator.Read(ref ys).TempNormal, WeatherGenerator.Read(ref ys).TempNormal, y == 0);
                for (int d = 0; d < cal.DaysPerMonth * cal.MonthsPerYear; d++)
                {
                    int m = d / cal.DaysPerMonth;
                    if (d % cal.DaysPerMonth == 0 && (y > 0 || d > 0))
                        WeatherGenerator.CloseMonth(rules, row, rules.MeanTempC[(m + 11) % 12]);
                    if (m == rules.CropFactorMonth && d % cal.DaysPerMonth == 0)
                    {
                        double index = WeatherGenerator.CropIndex(rules, row);
                        raw.Add(index);
                        regimes.Add(row.RegimeMilli / 1000.0);
                        factors.Add(WeatherGenerator.CropFactor(climate, rules, index));
                    }
                    ulong s1 = rng.DeriveState(1_000_000L + y * 1000L + d), s2 = rng.DeriveState(9_000_000L + y * 1000L + d);
                    WeatherGenerator.Step(rules, row, WeatherGenerator.Read(ref s1), WeatherGenerator.Read(ref s2), d + 0.5,
                        cal.DaysPerMonth, cal.MonthsPerYear, y == 0 && d == 0);
                    if (row.Kind == Precipitation.Rain) rainDays[m]++;
                    if (row.Kind == Precipitation.Snow) snowDays[m]++;
                }
            }
            double mean = raw.Average(), sd = Math.Sqrt(raw.Sum(x => (x - mean) * (x - mean)) / raw.Count);
            double fm = factors.Average(), fsd = Math.Sqrt(factors.Sum(x => (x - fm) * (x - fm)) / factors.Count);
            double cov = 0;
            for (int i = 1; i < factors.Count; i++) cov += (factors[i] - fm) * (factors[i - 1] - fm);
            double ac = cov / (factors.Count - 1) / (fsd * fsd);
            double rm = regimes.Average(), rsd = Math.Sqrt(regimes.Sum(x => (x - rm) * (x - rm)) / regimes.Count);
            double rc = 0, ra = 0;
            for (int i = 0; i < raw.Count; i++) rc += (raw[i] - mean) * (regimes[i] - rm);
            for (int i = 1; i < regimes.Count; i++) ra += (regimes[i] - rm) * (regimes[i - 1] - rm);
            Console.WriteLine(string.Create(inv, $"indice_cru media={mean:F4} dp={sd:F4} corr_regime={rc / raw.Count / (sd * rsd):F2} regime_dp={rsd:F2} regime_autocorr={ra / (regimes.Count - 1) / (rsd * rsd):F2}"));
            Console.WriteLine(string.Create(inv,
                $"fator media={fm:F0} dp={fsd:F0} autocorr1={ac:F2} seca(<600)={factors.Count(f => f < ClimateRules.DroughtBelowMilli) / (double)factors.Count:P1} min={factors.Min()} max={factors.Max()} p<400={factors.Count(f => f < 400) / (double)factors.Count:P1} p<500={factors.Count(f => f < 500) / (double)factors.Count:P1} p1={factors.OrderBy(f => f).ElementAt(factors.Count / 100)}"));
            Console.WriteLine("chuva_dias/mes=" + string.Join(" ", rainDays.Select(x => (x / years).ToString("F1", inv))));
            Console.WriteLine("neve_dias/mes=" + string.Join(" ", snowDays.Select(x => (x / years).ToString("F1", inv))));
            return 0;
        }

        ulong seed = ulong.Parse(arg, inv);
        var (world, clock) = ScenarioRunner.Create(seed, cityLayout: CityLayoutSwitch.Rules());
        var sum = new double[12];
        var rain = new int[12];
        var snow = new int[12];
        var snowCover = new int[12];
        var mud = new int[12];
        var lost = new int[12];
        var light = new double[12];
        var frost = new int[12];
        var village = world.ActiveCities().First().Location;
        int days = cal.DaysPerMonth * cal.MonthsPerYear;
        for (int d = 0; d < days; d++)
        {
            clock.Run(world, cal.HoursPerDay);
            var row = WeatherNow.RowAt(world, village)!;
            int m = (int)(world.Weather!.Day / cal.DaysPerMonth % cal.MonthsPerYear);
            sum[m] += row.MeanCenti / 100.0;
            if (row.Kind == Precipitation.Rain) rain[m]++;
            if (row.Kind == Precipitation.Snow) snow[m]++;
            if (row.SnowMm >= 1) snowCover[m]++;
            if (row.Frost) frost[m]++;
            if (WeatherNow.Ground(rules, row) is GroundWeather.Mud or GroundWeather.HeavyMud) mud[m]++;
            if (!WeatherNow.OutdoorWorkable(rules, row)) lost[m]++;
            light[m] += world.Weather.DaylightMinutes / 60.0;
        }
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"seed={seed} mes temp_media_C chuva_dias neve_dias neve_no_chao_dias geada_noites lama_trilha_% luz_h trabalho_fora_perdido_dias");
        for (int m = 0; m < 12; m++)
            sb.AppendLine(inv, $"m{m} {sum[m] / cal.DaysPerMonth:F1} {rain[m]} {snow[m]} {snowCover[m]} {frost[m]} {100.0 * mud[m] / cal.DaysPerMonth:F0} {light[m] / cal.DaysPerMonth:F1} {lost[m]}");
        sb.AppendLine(inv, $"ano temp={sum.Sum() / days:F1} chuva={rain.Sum()} neve={snow.Sum()} lama_%={100.0 * mud.Sum() / days:F0} perdidos={lost.Sum()} fator_safra={world.ClimateFactorAt(village, world.CurrentDate.TotalHours - 1)}");
        Console.Write(sb.ToString());
        return 0;
    }
}
