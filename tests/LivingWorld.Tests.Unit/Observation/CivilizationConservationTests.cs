using System.Text.Json.Nodes;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia A, conservação por fonte independente do resíduo: a população da série anual, a emigração
/// contada pelos ids que saíram do pool e as cidades criadas/fundidas contadas direto em <c>world.Cities</c>. Dois braços
/// com a mesma seed: sem emigração (taxa 0) e com emigração real (pool de 50 e taxa alta).</summary>
public class CivilizationConservationTests
{
    private const int Years = 3;

    private sealed record Arm(SeedRun Run, long InitialPopulation, IReadOnlyList<long> Population, IReadOnlyList<long> Emigrated,
        long InitialCities, long TotalCities, long TombstonedCities);

    private static Arm Observe(ulong seed, int years, double emigrationRate, long pool)
    {
        var root = JsonNode.Parse(ObservatoryFixture.DefaultScenarioJson())!.AsObject();
        root["EmigrationRatePerDeficitUnit"] = emigrationRate;
        root["Cities"]![0]!["AggregatePool"] = new JsonObject { ["Count"] = pool, ["WealthSum"] = pool * 100, ["HealthSum"] = pool * 100 };

        long initial = 0, initialCities = 0;
        var population = new List<long>();
        var emigrated = new List<long>();
        HashSet<long> previousPool = [];
        HashSet<long> PoolIds(WorldState w) => w.Cities.SelectMany(c => c.PoolNpcIds).Select(id => id.Value).ToHashSet();
        long Population(WorldState w) => w.Npcs.LongCount(n => n.IsAlive) + w.ActiveCities().Sum(c => c.AggregatePool.Count);

        long totalCities = 0, tombstoned = 0;
        var run = CivilizationRunner.Run(root.ToJsonString(), seed, years, onStart: w =>
        {
            initial = Population(w);
            initialCities = w.Cities.Count;
            previousPool = PoolIds(w);
        }, afterYear: (w, _) =>
        {
            var current = PoolIds(w);
            // Saiu do pool sem virar NPC materializado nem morto (vivo, morto retido ou arquivado): foi embora.
            emigrated.Add(previousPool.Count(id => !current.Contains(id) && w.FindNpc(new NpcId(id)) is null
                && !w.ColdArchive.AllNpcSummaries.ContainsKey(id)));
            previousPool = current;
            population.Add(Population(w));
            totalCities = w.Cities.Count;
            tombstoned = w.Cities.Count(c => c.MergedIntoCityId is not null);
        });
        return new Arm(run, initial, population, emigrated, initialCities, totalCities, tombstoned);
    }

    [Fact]
    public void Without_emigration_the_population_change_is_births_minus_deaths_and_matches_the_population_series()
    {
        var arm = Observe(seed: 1, Years, emigrationRate: 0, pool: 50);

        long previous = arm.InitialPopulation;
        for (int i = 0; i < Years; i++)
        {
            var w = arm.Run.Years[i].World;
            Assert.Equal(arm.Population[i] - previous, w["population_delta"]);
            Assert.Equal(w["births"] - w["deaths"], w["population_delta"]);
            Assert.Equal(0, w["net_migration"]);
            previous = arm.Population[i];
        }
    }

    [Fact]
    public void With_births_and_no_emigration_net_migration_is_still_zero_and_the_change_is_births_minus_deaths()
    {
        var arm = Observe(seed: 2, Years, emigrationRate: 0, pool: 0);

        Assert.True(arm.Run.Years.Sum(y => y.World["births"]) > 0, "a seed não teve nascimentos: o teste não mediu nada");
        long previous = arm.InitialPopulation;
        for (int i = 0; i < Years; i++)
        {
            var w = arm.Run.Years[i].World;
            Assert.Equal(arm.Population[i] - previous, w["population_delta"]);
            Assert.Equal(w["births"] - w["deaths"], w["population_delta"]);
            Assert.Equal(0, w["net_migration"]);
            previous = arm.Population[i];
        }
    }

    [Fact]
    public void With_real_emigration_net_migration_is_minus_the_people_who_left_the_pool()
    {
        var arm = Observe(seed: 1, Years, emigrationRate: 5.0, pool: 50);

        Assert.True(arm.Emigrated.Sum() > 0, "o braço não teve emigração: o teste não mediu nada");
        long previous = arm.InitialPopulation;
        for (int i = 0; i < Years; i++)
        {
            var w = arm.Run.Years[i].World;
            Assert.Equal(arm.Population[i] - previous, w["population_delta"]);
            Assert.Equal(-arm.Emigrated[i], w["net_migration"]);
            previous = arm.Population[i];
        }
    }

    [Fact]
    public void Cities_founded_and_merged_per_year_add_up_to_what_the_world_holds_at_the_end()
    {
        // Seed 2 funda e funde cidades já no ano 1 (cidade fundada e fundida no mesmo ano continua contando como fundada).
        var arm = Observe(seed: 2, Years, emigrationRate: 0.1, pool: 0);

        double founded = arm.Run.Years.Sum(y => y.World["cities_founded"]);
        double merged = arm.Run.Years.Sum(y => y.World["cities_merged"]);
        Assert.True(founded > 0 && merged > 0, "a seed não fundou nem fundiu: o teste não mediu nada");
        Assert.Equal(arm.TotalCities, arm.InitialCities + founded);
        Assert.Equal(arm.TombstonedCities, merged);
    }
}
