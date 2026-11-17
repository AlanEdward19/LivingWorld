using LivingWorld.Domain.Climate;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Climate;

/// <summary>AD-135: o ano de safra padrão é AR(1) com choque em parte comum às regiões: anos ruins se seguem e atingem a
/// vizinhança junto. O braço de controle é o sorteio uniforme independente antigo.</summary>
public sealed class ClimatePersistenceTests
{
    private const long YearHours = 12L * 30 * 24;

    private static List<(int A, int B)> Years(ClimateRules rules, ulong seed, int years)
    {
        var world = ScenarioRunner.Create(seed, initialPopulation: 5, climateRules: rules).World;
        var a = world.Map.Regions[0].Id;
        var b = world.Map.Regions[world.Map.Regions.Count - 1].Id;
        var list = new List<(int, int)>();
        for (long y = 1; y <= years; y++)
        {
            world.CurrentDate = world.CurrentDate.AddHours(y * YearHours - world.CurrentDate.TotalHours);
            new ClimateYearSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
            list.Add((world.ClimateFactorMilli(a, y), world.ClimateFactorMilli(b, y)));
        }

        return list;
    }

    private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++)
        {
            sxy += (x[i] - mx) * (y[i] - my);
            sxx += (x[i] - mx) * (x[i] - mx);
            syy += (y[i] - my) * (y[i] - my);
        }

        return sxy / Math.Sqrt(sxx * syy);
    }

    private static (double Lag1, double Spatial, double Cv) Stats(ClimateRules rules)
    {
        var lagX = new List<double>(); var lagY = new List<double>();
        var regA = new List<double>(); var regB = new List<double>();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var years = Years(rules, seed, 40);
            for (int i = 0; i < years.Count; i++)
            {
                regA.Add(years[i].A); regB.Add(years[i].B);
                if (i > 0) { lagX.Add(years[i - 1].A); lagY.Add(years[i].A); }
            }
        }

        double mean = regA.Average();
        double sd = Math.Sqrt(regA.Sum(v => (v - mean) * (v - mean)) / regA.Count);
        return (Correlation(lagX, lagY), Correlation(regA, regB), sd / mean);
    }

    [Fact]
    public void Default_climate_years_are_persistent_shared_between_regions_and_vary_like_real_harvests()
    {
        // AD-180: o sorteio anual AR(1) é o braço sem tempo diário (com o tempo ligado o fator sai da estação, teste abaixo)
        var (lag1, spatial, cv) = Stats(ClimateRules.Default with { Weather = null });

        Assert.InRange(lag1, 0.15, 0.5);
        Assert.True(spatial > 0.4, $"correlação entre regiões {spatial:F2}");
        Assert.InRange(cv, 0.15, 0.3);
    }

    /// <summary>AD-180: o fator que sai do tempo de duas regiões (sorteios comuns e locais do gerador) por muitos anos.</summary>
    private static (double Lag1, double Spatial, double Cv) WeatherStats(int years)
    {
        var climate = ClimateRules.Default;
        var rules = climate.Weather!;
        var rng = new LivingWorld.Domain.Shared.WorldRng(180);
        var rows = new[] { new WeatherRegionRow(), new WeatherRegionRow() };
        var a = new List<double>(); var b = new List<double>();
        for (int y = 0; y < years; y++)
        {
            ulong ys = rng.DeriveState(y);
            double shared = WeatherGenerator.Read(ref ys).TempNormal;
            for (int i = 0; i < 2; i++) WeatherGenerator.NewYear(climate, rows[i], shared, WeatherGenerator.Read(ref ys).TempNormal, y == 0);
            for (int d = 0; d < 360; d++)
            {
                if (d == rules.CropFactorMonth * 30)
                {
                    a.Add(WeatherGenerator.CropFactor(climate, rules, WeatherGenerator.CropIndex(rules, rows[0])));
                    b.Add(WeatherGenerator.CropFactor(climate, rules, WeatherGenerator.CropIndex(rules, rows[1])));
                }
                ulong sd = rng.DeriveState(1_000_000L + y * 1000L + d);
                var sharedDay = WeatherGenerator.Read(ref sd);
                for (int i = 0; i < 2; i++)
                {
                    ulong ld = rng.DeriveState((i + 2) * 10_000_000L + y * 1000L + d);
                    WeatherGenerator.Step(rules, rows[i], sharedDay, WeatherGenerator.Read(ref ld), d + 0.5, 30, 12, y == 0 && d == 0);
                }
            }
        }

        double mean = a.Average(), sdv = Math.Sqrt(a.Sum(v => (v - mean) * (v - mean)) / a.Count);
        return (Correlation(a.Take(a.Count - 1).ToList(), a.Skip(1).ToList()), Correlation(a, b), sdv / mean);
    }

    [Fact]
    public void With_daily_weather_the_crop_years_keep_the_persistence_the_sharing_and_the_spread_of_real_harvests()
    {
        var (lag1, spatial, cv) = WeatherStats(1500);

        Assert.InRange(lag1, 0.15, 0.5);
        Assert.True(spatial > 0.4, $"correlação entre regiões {spatial:F2}");
        Assert.InRange(cv, 0.15, 0.3);
    }

    [Fact]
    public void Uniform_control_draws_independent_years()
    {
        var (lag1, spatial, _) = Stats(new ClimateRules(true, 300, 1500));

        Assert.InRange(lag1, -0.15, 0.15);
        Assert.InRange(spatial, -0.15, 0.15);
    }
}
