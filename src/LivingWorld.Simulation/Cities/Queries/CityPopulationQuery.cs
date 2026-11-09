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

/// <summary>Único ponto que computa população/riqueza/saúde/desigualdade de uma cidade — sempre
/// on-demand a partir de <c>WorldState.Npcs</c> (vivos,
/// filtrados por <see cref="CityId"/>) + <see cref="City.AggregatePool"/>. Sem estado próprio:
/// nenhum campo é cacheado, então não existe divergência incremental-vs-recompute possível.</summary>
public static class CityPopulationQuery
{
    public static long Population(WorldState world, CityId city) =>
        MaterializedAlive(world, city).LongCount() + PoolOf(world, city).Count;

    public static long Wealth(WorldState world, CityId city) =>
        MaterializedAlive(world, city).Sum(n => n.Wallet.Amount) + PoolOf(world, city).WealthSum;

    /// <summary>Soma da saúde de todos os vivos da cidade (materializados + pool). É só o numerador
    /// de <see cref="Health"/>; não é um indicador de cidade (somar saúde cresce com a população).</summary>
    public static long HealthSum(WorldState world, CityId city) =>
        MaterializedAlive(world, city).Sum(n => (long)n.Health) + PoolOf(world, city).HealthSum;

    /// <summary>Saúde MÉDIA por morador vivo: (saúde dos materializados + <c>HealthSum</c> do pool)
    /// dividida por <see cref="Population"/> (materializados + pool, a mesma massa de
    /// <see cref="Population"/>), em divisão real (sem truncar). Cidade sem nenhum vivo: 0 (não há
    /// quem medir; mesma convenção de <see cref="Inequality"/>). Contrato: AD-121.</summary>
    public static double Health(WorldState world, CityId city)
    {
        long population = Population(world, city);
        return population == 0 ? 0.0 : (double)HealthSum(world, city) / population;
    }

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

    /// <summary>Economia da cidade. SPEC_DEVIATION:
    /// nenhum sinal distinto de "economia" existe além da riqueza agregada — reusa
    /// <see cref="Wealth"/> em vez de inventar uma segunda métrica sem dado próprio.</summary>
    public static long Economy(WorldState world, CityId city) => Wealth(world, city);

    /// <summary>Habitação da cidade: soma de
    /// <see cref="BuildingRecipe.HousingCapacityProvided"/> dos <see cref="Building"/> concluídos
    /// da cidade (mesmo cálculo já usado internamente por <see cref="CityGrowthSystem"/>).</summary>
    public static long Housing(WorldState world, CityId city) =>
        world.Buildings.Where(b => b.City == city)
            .Sum(b => world.CityCatalog.BuildingRecipes.TryGetValue(b.BuildingTypeId, out var recipe) ? recipe.HousingCapacityProvided : 0);

    // Segurança, Educação e Infraestrutura NÃO têm fonte: a contagem de prédios que ocupava o lugar
    // fingia medir o que não mede (AD-121). Voltam quando #118, #122 e #124 derem fonte real; até lá o
    // cliente recebe nulo ("não medido"), declarado em CityIndicatorSources (Api).

    public static bool HasFullCosmeticDetail(WorldState world, Npc npc) =>
        MaterializationSystem.HasFullCosmeticDetail(world, npc);

    /// <summary>Verdadeiro se alguma fonte enquadra o <paramref name="npc"/> (união de escopos)
    /// — pode ser camada aproximada (cidade sem prédio enquadrado).</summary>
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

    private static IEnumerable<Npc> MaterializedAlive(WorldState world, CityId city) =>
        world.Npcs.Where(n => n.IsAlive && n.City == city);

    private static AggregatePopulationPool PoolOf(WorldState world, CityId city) =>
        world.FindCity(city)?.AggregatePool ?? AggregatePopulationPool.Empty;
}
