using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T22 — ECON-05: desligar a economia (<see cref="EconomyRules.Enabled"/> =
/// falso — os 4 sistemas de economia viram no-op, mesmo mecanismo de <see
/// cref="UtilityAiHashScenarioTests"/>/NEEDS-04) muda o <c>Hash(world)</c> comparado ao mundo
/// com ela ligada, mesma seed. Gate usa 1 mês; confiança de 1 ano fica em
/// <c>Category=Scenario</c>.</summary>
public class EconomyHashScenarioTests
{
    private const long OneMonth = 30 * 24;
    private const long OneYear = 12 * 30 * 24;

    [Fact]
    public void One_month_hash_differs_between_economy_on_and_off_with_the_same_seed()
    {
        string hashWithEconomy = RunAndHash(seed: 42, OneMonth, ScenarioRunner.DefaultEconomyRules);

        string hashWithoutEconomy = RunAndHash(seed: 42, OneMonth, ScenarioRunner.DefaultEconomyRules with { Enabled = false });

        Assert.NotEqual(hashWithEconomy, hashWithoutEconomy);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void One_year_hash_differs_between_economy_on_and_off_with_the_same_seed()
    {
        string hashWithEconomy = RunAndHash(seed: 42, OneYear, ScenarioRunner.DefaultEconomyRules);

        string hashWithoutEconomy = RunAndHash(seed: 42, OneYear, ScenarioRunner.DefaultEconomyRules with { Enabled = false });

        Assert.NotEqual(hashWithEconomy, hashWithoutEconomy);
    }

    private static string RunAndHash(ulong seed, long ticks, EconomyRules economyRules)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.InitialMap(seed, 20), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: economyRules, economyCatalog: ScenarioRunner.DefaultEconomyCatalog);
        PopulationSeeder.SeedInitial(world, 20, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var clock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, ticks);
        return WorldSnapshot.CanonicalHash(world);
    }
}
