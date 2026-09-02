using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T23 — o critério mais importante da fase: <c>soma de todas as moedas do
/// mundo == inicial + cunhado - destruído</c>, exato, amostrado ao longo do horizonte (ECON-14/27).
/// Gate: 1 mês; 1/10/100 anos em <c>Category=Scenario</c>.</summary>
public class MoneyConservationTests
{
    private const long OneMonthInHours = 30 * 24;
    private const long OneYearInHours = 12 * 30 * 24;
    private const long TenYearsInHours = 10 * OneYearInHours;
    private const long SampleEveryHours = 24;

    private static long TotalMoney(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount);

    [Fact]
    public void Total_money_is_conserved_every_sample_over_1_month()
    {
        AssertMoneyConserved(OneMonthInHours);
    }

    /// <summary>Variante de 1 ano (nightly, Category=Scenario) — mesma asserção, horizonte
    /// maior. Fora do gate padrão (<c>Category!=Scenario</c>).</summary>
    [Trait("Category", "Scenario")]
    [Fact]
    public void Total_money_is_conserved_every_sample_over_1_year()
    {
        AssertMoneyConserved(OneYearInHours);
    }

    /// <summary>Variante de 10 anos (nightly, Category=Scenario) — mesma asserção, horizonte
    /// maior. Fora do gate padrão (<c>Category!=Scenario</c>).</summary>
    [Trait("Category", "Scenario")]
    [Fact]
    public void Total_money_is_conserved_every_sample_over_10_years()
    {
        AssertMoneyConserved(TenYearsInHours);
    }

    /// <summary>Sensor de mutação (R5): um caminho só-de-teste que esquece de incrementar
    /// MoneyMinted ao cunhar prova que o assert acima mede algo real — sem isso, o assert
    /// passaria mesmo com o contador quebrado.</summary>
    [Fact]
    public void Mutation_sensor_a_mint_that_forgets_to_increment_the_counter_breaks_the_invariant()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 1);
        long initial = TotalMoney(world);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        // Cunha "de verdade" (credita a um NPC) mas sem passar pelo World.Mint — simula o bug que
        // o critério precisa detectar.
        var npc = world.Npcs.First();
        npc.CreditWallet(new Money(100));

        long expected = initial + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount;
        long actual = TotalMoney(world);
        Assert.NotEqual(expected, actual); // prova que o assert do teste principal teria pego isso
    }

    /// <summary>Variante de 100 anos (nightly, Category=Scenario) — mesma asserção, horizonte
    /// maior. Nunca executada neste gate de rotina, só pelo `--filter Category=Scenario` manual.</summary>
    [Trait("Category", "Scenario")]
    [Fact]
    public void Total_money_is_conserved_every_sample_over_100_years()
    {
        AssertMoneyConserved(100 * OneYearInHours);
    }

    private static void AssertMoneyConserved(long ticks)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 20);
        long initial = TotalMoney(world);

        for (long tick = 0; tick < ticks; tick++)
        {
            clock.Tick(world);

            if ((tick + 1) % SampleEveryHours != 0 && tick + 1 != ticks)
                continue;

            long expected = initial + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount;
            long actual = TotalMoney(world);
            Assert.True(expected == actual,
                $"tick {world.CurrentDate.TotalHours}: total de dinheiro {actual} != esperado {expected} (inicial {initial} + cunhado {world.MoneyMinted.Amount} - destruído {world.MoneyDestroyed.Amount})");
        }
    }
}
