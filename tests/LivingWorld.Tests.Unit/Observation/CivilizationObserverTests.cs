using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia A: o observatório só lê o mundo (hash idêntico), a contabilidade fecha todo ano e
/// a tábua de vida/Gini dão o valor conhecido numa entrada sintética.</summary>
public class CivilizationObserverTests
{
    private static string HashWithoutObserver()
    {
        var root = JsonNode.Parse(ObservatoryFixture.DefaultScenarioJson())!.AsObject();
        root["Seed"] = ObservatoryFixture.Seed;
        var (world, baseClock) = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
        baseClock.Run(world, world.Calendar.HoursPerYear * ObservatoryFixture.GateYears);
        return WorldSnapshot.CanonicalHash(world);
    }

    [Fact]
    public void Observing_ten_years_of_the_default_scenario_leaves_the_canonical_hash_unchanged()
    {
        Assert.Equal(HashWithoutObserver(), ObservatoryFixture.Observed.Value.Hash);
    }

    [Fact]
    public void City_populations_sum_to_the_world_total_and_to_the_state_every_year()
    {
        var (run, checks, _) = ObservatoryFixture.Observed.Value;
        for (int i = 0; i < run.Years.Count; i++)
        {
            var year = run.Years[i];
            Assert.Equal(year.World["population"], year.Cities.Sum(c => c.Metrics["population"]));
            Assert.Equal(checks[i].AliveMaterialized + checks[i].PoolTotal, year.World["population"]);
            Assert.Equal(0, year.World["population_unassigned"]);
        }
    }

    [Fact]
    public void Births_and_deaths_from_the_sink_match_the_state_every_year()
    {
        var (run, checks, _) = ObservatoryFixture.Observed.Value;
        for (int i = 0; i < run.Years.Count; i++)
        {
            Assert.Equal(checks[i].Born, run.Years[i].World["births"]);
            Assert.Equal(checks[i].Died, run.Years[i].World["deaths"]);
        }
    }

    [Fact]
    public void Population_change_equals_births_minus_deaths_plus_migration_and_migration_only_removes_people()
    {
        var run = ObservatoryFixture.Observed.Value.Run;
        foreach (var year in run.Years)
        {
            var w = year.World;
            Assert.Equal(w["population_delta"], w["births"] - w["deaths"] + w["net_migration"]);
            Assert.True(w["net_migration"] <= 0, $"ano {year.Year}: migração líquida {w["net_migration"]} cria gente do nada");
        }
    }

    [Fact]
    public void Deaths_by_cause_sum_to_deaths_in_every_city_and_year()
    {
        var run = ObservatoryFixture.Observed.Value.Run;
        foreach (var year in run.Years.SelectMany(y => y.Cities.Select(c => c.Metrics).Append(y.World)))
            Assert.Equal(year["deaths"], year["deaths_hunger_thirst"] + year["deaths_disease"] + year["deaths_age"] + year["deaths_other"]);
    }

    [Fact]
    public void Life_table_with_one_open_band_returns_the_inverse_of_the_death_rate()
    {
        double e0 = LifeTable.LifeExpectancyAtBirth([new AgeBand(10, Deaths: 2, Exposure: 100)]);
        Assert.Equal(50.0, e0, 9);
    }

    [Fact]
    public void Life_table_with_a_closed_and_an_open_band_matches_the_hand_computed_value()
    {
        // q0 = 10·0,02 / (1 + 0,1) = 2/11; l1 = 9/11; L0 = 10·(1 + 9/11)/2 = 100/11; L1 = l1/0,05 = 180/11 → e0 = 280/11.
        double e0 = LifeTable.LifeExpectancyAtBirth([new AgeBand(10, 2, 100), new AgeBand(10, 5, 100)]);
        Assert.Equal(280.0 / 11.0, e0, 9);
    }

    [Fact]
    public void Life_table_without_deaths_assumes_the_last_band_is_lived_halfway()
    {
        double e0 = LifeTable.LifeExpectancyAtBirth([new AgeBand(10, 0, 50), new AgeBand(10, 0, 50)]);
        Assert.Equal(15.0, e0, 9);
    }

    [Fact]
    public void Gini_of_equal_wealth_is_zero_and_of_one_holder_among_four_is_three_quarters()
    {
        Assert.Equal(0.0, WealthInequality.Gini([(10, 1), (10, 1), (10, 1)]), 9);
        Assert.Equal(0.75, WealthInequality.Gini([(0, 1), (0, 1), (0, 1), (100, 1)]), 9);
    }

    [Fact]
    public void Gini_treats_a_pool_block_as_the_same_number_of_uniform_people()
    {
        Assert.Equal(WealthInequality.Gini([(0, 1), (0, 1), (0, 1), (100, 1)]), WealthInequality.Gini([(0, 3), (100, 1)]), 9);
    }

    [Fact]
    public void The_observer_reports_the_documented_metric_keys_for_the_world_and_for_every_city()
    {
        var year = ObservatoryFixture.Observed.Value.Run.Years[^1];
        string[] common = ["population", "births", "deaths", "age_mean", "age_median", "life_expectancy_at_birth", "gini", "treasury",
            "unemployment_rate", "workshops", "buildings", "technologies"];
        foreach (var key in common)
        {
            Assert.Contains(key, year.World.Keys);
            Assert.All(year.Cities, c => Assert.Contains(key, c.Metrics.Keys));
        }
    }
}
