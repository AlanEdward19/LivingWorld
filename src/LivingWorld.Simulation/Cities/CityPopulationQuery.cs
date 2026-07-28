using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION: design.md pede este tipo em src/LivingWorld.Domain/Cities/ — mas
// LivingWorld.Domain não referencia LivingWorld.Simulation (é o inverso: Simulation -> Domain,
// ver os .csproj), e WorldState só existe em Simulation. Vive aqui, mesmo pacote de
// ReferentialIntegritySweep (outro serviço estático "consulta o WorldState inteiro").

/// <summary>Único ponto que computa população/riqueza/saúde/desigualdade de uma cidade (Fase 8,
/// T8, CITY-01/CITY-09, approach A) — sempre on-demand a partir de <c>WorldState.Npcs</c> (vivos,
/// filtrados por <see cref="CityId"/>) + <see cref="City.AggregatePool"/>. Sem estado próprio:
/// nenhum campo é cacheado, então não existe divergência incremental-vs-recompute possível.</summary>
public static class CityPopulationQuery
{
    public static long Population(WorldState world, CityId city) =>
        MaterializedAlive(world, city).LongCount() + PoolOf(world, city).Count;

    public static long Wealth(WorldState world, CityId city) =>
        MaterializedAlive(world, city).Sum(n => n.Wallet.Amount) + PoolOf(world, city).WealthSum;

    public static long Health(WorldState world, CityId city) =>
        MaterializedAlive(world, city).Sum(n => (long)n.Health) + PoolOf(world, city).HealthSum;

    /// <summary>Coeficiente de Gini sobre <see cref="Npc.Wallet"/> dos materializados (Tech
    /// Decision do design: o pool agregado entra só pela soma, não pela distribuição — Gini de
    /// amostra parcial é a aproximação aceita nesta fase). Sem ninguém materializado, 0
    /// (nenhuma desigualdade medível).</summary>
    public static double Inequality(WorldState world, CityId city)
    {
        var wallets = MaterializedAlive(world, city).Select(n => (double)n.Wallet.Amount).OrderBy(w => w).ToList();
        double total = wallets.Sum();
        if (wallets.Count == 0 || total == 0)
            return 0.0;

        double weightedSum = 0;
        for (int i = 0; i < wallets.Count; i++)
            weightedSum += (2.0 * (i + 1) - wallets.Count - 1) * wallets[i];

        return weightedSum / (wallets.Count * total);
    }

    private static IEnumerable<Npc> MaterializedAlive(WorldState world, CityId city) =>
        world.Npcs.Where(n => n.IsAlive && n.City == city);

    private static AggregatePopulationPool PoolOf(WorldState world, CityId city) =>
        world.FindCity(city)?.AggregatePool ?? AggregatePopulationPool.Empty;
}
