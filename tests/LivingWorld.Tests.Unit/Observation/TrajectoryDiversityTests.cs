using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia C: par de controle da métrica. Mundo congelado dá 0 e séries embaralhadas dão &gt; 0;
/// sem o par, "diversidade 0,3" não provaria que a métrica mede alguma coisa.</summary>
public class TrajectoryDiversityTests
{
    private static SeedRun Run(ulong seed, double[] population, double[]? gini = null) => new(seed,
        population.Select((p, i) => new WorldYearSample(i + 1, (i + 1) * 100L, new Dictionary<string, double>
        {
            ["population"] = p,
            ["cities_active"] = 2,
            ["gini"] = gini?[i] ?? 0.4,
            ["treasury"] = p * 10,
            ["technologies"] = 1,
        }, [])).ToList());

    private static readonly double[] Curve = [10, 12, 15, 11, 9, 14];

    [Fact]
    public void A_frozen_world_where_every_seed_has_the_same_series_has_zero_diversity()
    {
        var frozen = Enumerable.Range(1, 6).Select(s => Run((ulong)s, Curve)).ToList();

        var result = TrajectoryDiversity.Compute(frozen);

        Assert.Equal(0.0, result.Index);
        Assert.All(result.ByMetric.Values, v => Assert.Equal(0.0, v));
    }

    [Fact]
    public void The_same_values_shuffled_differently_per_seed_have_diversity_above_zero()
    {
        var shuffled = Enumerable.Range(1, 6).Select(s => Run((ulong)s, Rotate(Curve, s))).ToList();

        var result = TrajectoryDiversity.Compute(shuffled);

        Assert.True(result.Index > 0, $"índice {result.Index}: embaralhar não mudou a métrica");
        Assert.True(result.ByMetric["population"] > 0);
        Assert.True(result.ByMetric["treasury_per_capita"] == 0, "tesouro per capita é constante: não pode divergir");
    }

    [Fact]
    public void Two_seeds_with_opposite_curves_have_the_hand_computed_index()
    {
        // População [1,3] x [3,1]: média 2, distância média 2 / escala 2 = 1. As outras quatro métricas são constantes (0).
        var result = TrajectoryDiversity.Compute([Run(1, [1, 3]), Run(2, [3, 1])]);

        Assert.Equal(1.0, result.ByMetric["population"], 12);
        Assert.Equal(0.2, result.Index, 12);
    }

    [Fact]
    public void The_index_does_not_depend_on_the_unit_of_the_series()
    {
        var a = TrajectoryDiversity.Compute([Run(1, [1, 3]), Run(2, [3, 1])]);
        var b = TrajectoryDiversity.Compute([Run(1, [1000, 3000]), Run(2, [3000, 1000])]);

        Assert.Equal(a.ByMetric["population"], b.ByMetric["population"], 12);
    }

    [Fact]
    public void Fewer_than_two_seeds_have_zero_diversity()
    {
        Assert.Equal(0.0, TrajectoryDiversity.Compute([Run(1, Curve)]).Index);
        Assert.Equal(0.0, TrajectoryDiversity.Compute([]).Index);
    }

    [Fact]
    public void The_result_does_not_depend_on_the_order_of_the_seeds()
    {
        var runs = Enumerable.Range(1, 5).Select(s => Run((ulong)s, Rotate(Curve, s * 2))).ToList();

        Assert.Equal(TrajectoryDiversity.Compute(runs).Index, TrajectoryDiversity.Compute(runs.AsEnumerable().Reverse().ToList()).Index, 12);
    }

    private static double[] Rotate(double[] values, int by) => values.Skip(by % values.Length).Concat(values.Take(by % values.Length)).ToArray();
}
