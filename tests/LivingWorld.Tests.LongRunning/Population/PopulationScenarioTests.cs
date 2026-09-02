using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Critérios de verificação da Fase 3A sobre o cenário default: 1 mês no gate
/// (invariantes amostrados); 1 ano, 10 anos e 10k×10 anos ficam em <c>Category=Scenario</c>.
/// 100 anos: <see cref="LifeTable100YearScenarioTests"/>.</summary>
public class PopulationScenarioTests
{
    private const long OneMonthInHours = 30 * 24;
    private const long OneYearInHours = 12 * 30 * 24;
    private const long TenYearsInHours = 10 * OneYearInHours;
    private const long SampleEveryHours = 24;

    [Fact]
    public void One_month_with_100_initial_npcs_never_breaks_invariants_at_any_sample()
    {
        AssertInvariantsOverHorizon(OneMonthInHours);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void One_year_with_100_initial_npcs_never_breaks_invariants_at_any_sample()
    {
        AssertInvariantsOverHorizon(OneYearInHours);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void Ten_years_with_100_initial_npcs_never_breaks_invariants_at_any_tick()
    {
        AssertInvariantsOverHorizon(TenYearsInHours);
    }

    private static void AssertInvariantsOverHorizon(long ticks)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        Assert.Equal(100, world.Npcs.Count);

        for (long tick = 0; tick < ticks; tick++)
        {
            clock.Tick(world);
            if ((tick + 1) % SampleEveryHours != 0 && tick + 1 != ticks)
                continue;
            AssertInvariants(world);
        }
    }

    private static void AssertInvariants(WorldState world)
    {
        foreach (var household in world.Households)
            Assert.False(household.IsEmpty, $"household {household.Id} vazio deveria ter sido dissolvido");

        foreach (var npc in world.Npcs.Where(n => !n.IsAlive))
            foreach (var household in world.Households)
                Assert.DoesNotContain(npc.Id, household.Members);

        foreach (var npc in world.Npcs)
            Assert.True(npc.AgeYears(world.CurrentDate) >= 0);
    }

    [Fact]
    public void Age_advances_with_the_world_clock_without_any_system_running()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1);
        var before = world.Npcs.Where(n => n.IsAlive).Select(n => (n.Id, Age: n.AgeYears(world.CurrentDate))).ToList();

        world.CurrentDate = world.CurrentDate.AddYears(5); // avança o relógio direto — nenhum sistema roda

        foreach (var (id, age) in before)
        {
            var npc = world.Npcs.Single(n => n.Id == id);
            Assert.Equal(age + 5, npc.AgeYears(world.CurrentDate));
        }
    }

    [Fact]
    public void Zero_npc_world_runs_1000_ticks_without_exception_and_keeps_a_stable_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 1, initialPopulation: 0);
        Assert.Empty(world.Npcs);

        clock.Run(world, 1000);

        Assert.Empty(world.Npcs);
        Assert.Empty(world.Households);
    }

    [Fact]
    public void Single_npc_world_never_produces_a_child()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 1, initialPopulation: 1);
        clock.Run(world, OneMonthInHours);

        Assert.Single(world.Npcs); // ninguém nasce sem parceiro
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void Population_100x_the_initial_size_does_not_exceed_the_tick_iteration_budget()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 1, maxIterationsPerTick: 1000, initialPopulation: 10_000);
        clock.Run(world, TenYearsInHours); // não deve lançar TickBudgetExceededException
        Assert.True(world.Npcs.Count >= 10_000);
    }

    /// <summary>Sensor causal (Category=Scenario): 1 ano — horizontes ≤90d no seed 42 ainda
    /// não divergem o hash canônico com vs sem natalidade.</summary>
    [Trait("Category", "Scenario")]
    [Fact]
    public void Disabling_natality_changes_the_1_year_canonical_hash()
    {
        var (withNatality, clockWith) = ScenarioRunner.Create(seed: 42);
        clockWith.Run(withNatality, OneYearInHours);

        var systemsWithoutNatality = ScenarioRunner.DefaultSystems().Where(s => s.Name != NatalitySystem.SystemName).ToList();
        var (withoutNatality, _) = ScenarioRunner.Create(seed: 42);
        new WorldClock(systemsWithoutNatality).Run(withoutNatality, OneYearInHours);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(withNatality), WorldSnapshot.CanonicalHash(withoutNatality));
    }
}
