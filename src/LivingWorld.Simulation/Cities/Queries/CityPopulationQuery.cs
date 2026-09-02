using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Queries;

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

    /// <summary>Economia da cidade (Fase 8, fix round 1, gap 1 — CITY-01 AC1). SPEC_DEVIATION:
    /// nenhum sinal distinto de "economia" existe nesta fase além da riqueza agregada — reusa
    /// <see cref="Wealth"/> em vez de inventar uma segunda métrica sem dado próprio.</summary>
    public static long Economy(WorldState world, CityId city) => Wealth(world, city);

    /// <summary>Habitação da cidade (Fase 8, fix round 1, gap 1 — CITY-01 AC1): soma de
    /// <see cref="BuildingRecipe.HousingCapacityProvided"/> dos <see cref="Building"/> concluídos
    /// da cidade (mesmo cálculo já usado internamente por <see cref="CityGrowthSystem"/>).</summary>
    public static long Housing(WorldState world, CityId city) =>
        world.Buildings.Where(b => b.City == city)
            .Sum(b => world.CityCatalog.BuildingRecipes.TryGetValue(b.BuildingTypeId, out var recipe) ? recipe.HousingCapacityProvided : 0);

    // SPEC_DEVIATION (Fase 8, fix round 1, gap 1 — CITY-01 AC1): design.md só declara sinal real
    // pra Habitação (HousingCapacityProvided por receita, AD-023). BuildingRecipe não tem campo
    // distinto pra "provê segurança"/"provê educação"/"provê infraestrutura" e nenhum critério de
    // verificação da Fase 8 exige um — inventar um limiar/campo novo aqui seria requisito não
    // pedido (R3, eval-criteria.md). Os três ficam como a contagem total de Building concluído da
    // cidade: existe e é derivado (o que a task 1 pede), sem simular comportamento real (isso é
    // society.md/Fase 13+).

    /// <summary>Segurança da cidade (Fase 8, fix round 1, gap 1 — CITY-01 AC1). Ver SPEC_DEVIATION acima.</summary>
    public static long Security(WorldState world, CityId city) => BuildingCount(world, city);

    /// <summary>Educação da cidade (Fase 8, fix round 1, gap 1 — CITY-01 AC1). Ver SPEC_DEVIATION acima.</summary>
    public static long Education(WorldState world, CityId city) => BuildingCount(world, city);

    /// <summary>Infraestrutura da cidade (Fase 8, fix round 1, gap 1 — CITY-01 AC1). Ver SPEC_DEVIATION acima.</summary>
    public static long Infrastructure(WorldState world, CityId city) => BuildingCount(world, city);

    /// <summary>LOD observacional (Fase 28, T4): delega a
    /// <see cref="MaterializationSystem.HasFullCosmeticDetail"/>.</summary>
    public static bool HasFullCosmeticDetail(WorldState world, Npc npc) =>
        MaterializationSystem.HasFullCosmeticDetail(world, npc);

    /// <summary>Verdadeiro se alguma fonte enquadra o <paramref name="npc"/> (união de escopos,
    /// LOD-04) — pode ser camada aproximada (cidade sem prédio enquadrado).</summary>
    public static bool IsCosmeticallyObserved(WorldState world, Npc npc) =>
        world.ObservationRegistry.IsObserved(npc, world);

    /// <summary>Materializados vivos com camada cosmética plena (interior enquadrado, rua em
    /// cidade observada, ou escopo mundo).</summary>
    public static long CosmeticallyDetailedPopulation(WorldState world, CityId city) =>
        MaterializedAlive(world, city).LongCount(n => HasFullCosmeticDetail(world, n));

    /// <summary>Materializados vivos enquadrados por escopo mas com camada cosmética
    /// aproximada (ex.: dentro de prédio não enquadrado numa cidade observada).</summary>
    public static long CosmeticallyApproximatePopulation(WorldState world, CityId city) =>
        MaterializedAlive(world, city).LongCount(n => IsCosmeticallyObserved(world, n) && !HasFullCosmeticDetail(world, n));

    private static long BuildingCount(WorldState world, CityId city) =>
        world.Buildings.Count(b => b.City == city);

    private static IEnumerable<Npc> MaterializedAlive(WorldState world, CityId city) =>
        world.Npcs.Where(n => n.IsAlive && n.City == city);

    private static AggregatePopulationPool PoolOf(WorldState world, CityId city) =>
        world.FindCity(city)?.AggregatePool ?? AggregatePopulationPool.Empty;
}
