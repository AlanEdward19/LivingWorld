using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 15 — NEEDS-04: desligar o utility AI (remover <see
/// cref="NeedsDecaySystem"/>/<see cref="BehaviorDecisionSystem"/> da lista de sistemas) muda o
/// <c>Hash(world)</c> comparado ao mundo com ele ligado, mesma seed — prova que a decisão de
/// ação entra na conta do hash canônico, não só demografia agregada
/// (rules/eval-criteria.md: "desligar o sistema novo muda o hash"). Gate usa 1 mês; confiança
/// de 1 ano fica em <c>Category=Scenario</c>.</summary>
public class UtilityAiHashScenarioTests
{
    private const long OneMonth = 30 * 24;
    private const long OneYear = 12 * 30 * 24;

    [Fact]
    public void One_month_hash_differs_between_utility_ai_on_and_off_with_the_same_seed()
    {
        string hashWithUtilityAi = RunAndHash(seed: 42, OneMonth, ScenarioRunner.DefaultSystems());

        var systemsWithoutUtilityAi = ScenarioRunner.DefaultSystems()
            .Where(s => s.Name is not (NeedsDecaySystem.SystemName or BehaviorDecisionSystem.SystemName))
            .ToList();
        string hashWithoutUtilityAi = RunAndHash(seed: 42, OneMonth, systemsWithoutUtilityAi);

        Assert.NotEqual(hashWithUtilityAi, hashWithoutUtilityAi);
    }

    [Fact]
    public void One_month_hash_with_utility_ai_on_is_still_deterministic_across_runs_of_the_same_seed()
    {
        string hashA = RunAndHash(seed: 42, OneMonth, ScenarioRunner.DefaultSystems());
        string hashB = RunAndHash(seed: 42, OneMonth, ScenarioRunner.DefaultSystems());

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void One_year_hash_differs_between_utility_ai_on_and_off_with_the_same_seed()
    {
        string hashWithUtilityAi = RunAndHash(seed: 42, OneYear, ScenarioRunner.DefaultSystems());

        var systemsWithoutUtilityAi = ScenarioRunner.DefaultSystems()
            .Where(s => s.Name is not (NeedsDecaySystem.SystemName or BehaviorDecisionSystem.SystemName))
            .ToList();
        string hashWithoutUtilityAi = RunAndHash(seed: 42, OneYear, systemsWithoutUtilityAi);

        Assert.NotEqual(hashWithUtilityAi, hashWithoutUtilityAi);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void One_year_hash_with_utility_ai_on_is_still_deterministic_across_runs_of_the_same_seed()
    {
        string hashA = RunAndHash(seed: 42, OneYear, ScenarioRunner.DefaultSystems());
        string hashB = RunAndHash(seed: 42, OneYear, ScenarioRunner.DefaultSystems());

        Assert.Equal(hashA, hashB);
    }

    private static string RunAndHash(ulong seed, long ticks, IReadOnlyList<ISimulationSystem> systems)
    {
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 20);
        var clock = new WorldClock(systems);
        clock.Run(world, ticks);
        return WorldSnapshot.CanonicalHash(world);
    }
}
