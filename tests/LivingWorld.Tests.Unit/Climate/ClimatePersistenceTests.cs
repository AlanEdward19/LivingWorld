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
        var (lag1, spatial, cv) = Stats(ClimateRules.Default);

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
