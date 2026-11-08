namespace LivingWorld.Simulation.Observation;

/// <summary>Uma série do mundo usada na diversidade: nome estável e como extraí-la de uma amostra anual.</summary>
public sealed record DiversityMetric(string Name, Func<WorldYearSample, double> Read);

/// <summary>Dispersão entre as trajetórias das seeds. <see cref="Index"/> é a média de <see cref="ByMetric"/>.</summary>
public sealed record DiversityResult(double Index, int Seeds, int Years, IReadOnlyDictionary<string, double> ByMetric);

/// <summary>Índice de diversidade de trajetórias (issue #112 fatia C, seção 6 da visão). Por métrica: (1) a série de
/// cada seed é dividida pela média do valor absoluto de todas as seeds e anos (escala relativa: um desvio pequeno
/// em torno de um valor grande pesa pouco, e a unidade da métrica não importa); (2) a distância entre duas seeds é a
/// média, sobre os anos, de |a − b|; (3) o índice da métrica é a média sobre todos os pares de seeds. O índice geral
/// é a média das métricas. Mundo congelado (mesma série em toda seed) dá 0; métrica toda zero dá 0. Só
/// <c>+ − × ÷</c> e <c>Math.Abs</c>. Cultura e política entram como mais <see cref="DiversityMetric"/> quando existirem.</summary>
public static class TrajectoryDiversity
{
    public static readonly IReadOnlyList<DiversityMetric> DefaultMetrics =
    [
        new("population", s => s.World["population"]),
        new("cities_active", s => s.World["cities_active"]),
        new("gini", s => s.World["gini"]),
        new("treasury_per_capita", s => s.World["population"] > 0 ? s.World["treasury"] / s.World["population"] : 0),
        new("technologies", s => s.World["technologies"]),
    ];

    public static DiversityResult Compute(IReadOnlyList<SeedRun> runs, IReadOnlyList<DiversityMetric>? metrics = null)
    {
        metrics ??= DefaultMetrics;
        int years = runs.Count == 0 ? 0 : runs.Min(r => r.Years.Count);
        var byMetric = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var metric in metrics)
            byMetric[metric.Name] = ForMetric(runs, years, metric);

        double index = byMetric.Count == 0 ? 0 : byMetric.Values.Sum() / byMetric.Count;
        return new DiversityResult(index, runs.Count, years, byMetric);
    }

    private static double ForMetric(IReadOnlyList<SeedRun> runs, int years, DiversityMetric metric)
    {
        if (runs.Count < 2 || years == 0) return 0;

        var series = runs.OrderBy(r => r.Seed).Select(r => r.Years.Take(years).Select(metric.Read).ToArray()).ToArray();
        double scale = series.SelectMany(s => s).Sum(Math.Abs) / (series.Length * years);
        if (scale <= 0) return 0;

        double total = 0;
        int pairs = 0;
        for (int i = 0; i < series.Length; i++)
        {
            for (int j = i + 1; j < series.Length; j++)
            {
                double distance = 0;
                for (int t = 0; t < years; t++) distance += Math.Abs(series[i][t] - series[j][t]);
                total += distance / years / scale;
                pairs++;
            }
        }

        return total / pairs;
    }
}
