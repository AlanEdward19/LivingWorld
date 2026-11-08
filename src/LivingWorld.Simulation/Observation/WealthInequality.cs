namespace LivingWorld.Simulation.Observation;

/// <summary>Gini ponderado (issue #112 fatia A). Cada item é um bloco de <c>Weight</c> pessoas com a mesma
/// riqueza <c>Value</c>: as carteiras materializadas entram com peso 1 e o pool agregado entra como UM bloco
/// uniforme (<c>WealthSum / Count</c> por cabeça, aproximação: a distribuição interna do pool não existe).
/// Curva de Lorenz trapezoidal sobre os blocos ordenados por valor; sem riqueza ou sem gente, 0.</summary>
public static class WealthInequality
{
    public static double Gini(IEnumerable<(double Value, double Weight)> blocks)
    {
        var sorted = blocks.Where(b => b.Weight > 0).OrderBy(b => b.Value).ToList();
        double people = 0, wealth = 0;
        foreach (var (value, weight) in sorted) { people += weight; wealth += value * weight; }
        if (people <= 0 || wealth <= 0) return 0;

        double populationShare = 0, wealthShare = 0, area = 0;
        foreach (var (value, weight) in sorted)
        {
            double nextPopulation = populationShare + weight / people;
            double nextWealth = wealthShare + value * weight / wealth;
            area += (nextPopulation - populationShare) * (wealthShare + nextWealth);
            populationShare = nextPopulation;
            wealthShare = nextWealth;
        }

        return 1.0 - area;
    }
}
