namespace LivingWorld.Simulation.Observation;

/// <summary>Faixa etária de uma tábua de período: largura em anos, mortes no ano e exposição
/// (pessoas-ano vividas na faixa). A última faixa da lista é aberta (sem limite superior).</summary>
public readonly record struct AgeBand(int WidthYears, long Deaths, double Exposure);

/// <summary>Expectativa de vida ao nascer por tábua de período (issue #112 fatia A). Fórmula, com
/// <c>n</c> = largura da faixa e <c>m = mortes / exposição</c>:
/// <c>q = n·m / (1 + n·m/2)</c> (mortes no meio da faixa, truncado em 1); <c>l₀ = 1</c>, <c>lᵢ₊₁ = lᵢ·(1 − qᵢ)</c>;
/// <c>Lᵢ = n·(lᵢ + lᵢ₊₁)/2</c> nas faixas fechadas e <c>L = l/m</c> na aberta (<c>l·n/2</c> sem mortes);
/// <c>e₀ = ΣLᵢ</c>. Só <c>+ − × ÷</c>: nenhuma função da libm.</summary>
public static class LifeTable
{
    public static double LifeExpectancyAtBirth(IReadOnlyList<AgeBand> bands)
    {
        if (bands.Count == 0) return 0;

        double survivors = 1.0, personYears = 0;
        for (int i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            double rate = band.Exposure > 0 ? band.Deaths / band.Exposure : 0;
            if (i == bands.Count - 1)
            {
                personYears += rate > 0 ? survivors / rate : survivors * band.WidthYears / 2.0;
                break;
            }

            double n = band.WidthYears;
            double q = Math.Min(1.0, n * rate / (1.0 + n * rate / 2.0));
            double next = survivors * (1.0 - q);
            personYears += n * (survivors + next) / 2.0;
            survivors = next;
        }

        return personYears;
    }
}
