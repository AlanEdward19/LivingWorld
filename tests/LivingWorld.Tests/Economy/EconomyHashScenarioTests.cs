using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T22 — ECON-05: desligar a economia (<see cref="EconomyRules.Enabled"/> =
/// falso — os 4 sistemas de economia viram no-op, mesmo mecanismo de <see
/// cref="UtilityAiHashScenarioTests"/>/NEEDS-04) muda o <c>Hash(world)</c> em 10 anos comparado
/// ao mundo com ela ligada, mesma seed.</summary>
public class EconomyHashScenarioTests
{
    private const long TenYears = 10 * 12 * 30 * 24;

    [Fact]
    public void Ten_year_hash_differs_between_economy_on_and_off_with_the_same_seed()
    {
        string hashWithEconomy = RunAndHash(seed: 42, ScenarioRunner.DefaultEconomyRules);

        string hashWithoutEconomy = RunAndHash(seed: 42, ScenarioRunner.DefaultEconomyRules with { Enabled = false });

        Assert.NotEqual(hashWithEconomy, hashWithoutEconomy);
    }

    private static string RunAndHash(ulong seed, EconomyRules economyRules)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: economyRules, economyCatalog: ScenarioRunner.DefaultEconomyCatalog);
        PopulationSeeder.SeedInitial(world, 20, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var clock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, TenYears);
        return WorldSnapshot.CanonicalHash(world);
    }
}
