using LivingWorld.Domain.Cities;

namespace LivingWorld.Simulation.Cities.Founding;

/// <summary>Fórmula de concentração compartilhada por <see cref="SettlementFoundingSystem"/> e
/// <see cref="SpatialSettlementFoundingSystem"/> — a mesma população/limiar decide fundação
/// normal e fundação por cluster de overflow, nunca um limiar mais fraco por um dos dois
/// caminhos.</summary>
internal static class FoundingConcentration
{
    public static double Level(long population) => population / (population + 1.0);

    public static bool ClearsThreshold(long population, CityRules rules) =>
        Level(population) >= rules.FoundingConcentrationThreshold;
}
