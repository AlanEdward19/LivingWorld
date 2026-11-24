using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.LongRunning.Economy;

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
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount)
        + world.Cities.Sum(c => c.Treasury.Amount)
        + (world.Ground?.Builds.Sum(b => b.Escrow) ?? 0); // AD-184: o capital que o fundador guardou na obra da oficina

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

    /// <summary>AD-167 (ADR-0044): os quatro horizontes (1 mês, 1, 10 e 100 anos) eram prefixos da mesma vila (seed 42, 20 pessoas),
    /// cada um simulado de novo. Agora uma corrida em duas etapas (até o ano 10, depois até o 100, para quem lê o mês não esperar o
    /// século) amostra igual (todo dia; os horizontes caem em fim de dia) e guarda a primeira falha; cada teste falha se ela cai
    /// dentro do seu horizonte. Ninguém recebe o mundo.</summary>
    private sealed record Stage(WorldState World, WorldClock Clock, long Initial, long Ticks, (long Tick, string Message)? Failure);

    private static readonly Lazy<Stage> Decade = new(() =>
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 20);
        return Advance(new Stage(world, clock, TotalMoney(world), 0, null), TenYearsInHours);
    });

    private static readonly Lazy<Stage> Century = new(() => Advance(Decade.Value, 100 * OneYearInHours));

    private static Stage Advance(Stage stage, long until)
    {
        var (world, clock, initial, done, failure) = stage;
        for (long tick = done; tick < until; tick++)
        {
            clock.Tick(world);
            if (failure is not null || (tick + 1) % SampleEveryHours != 0) continue;
            long expected = initial + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount;
            long actual = TotalMoney(world);
            if (expected != actual)
                failure = (tick + 1, $"tick {world.CurrentDate.TotalHours}: total de dinheiro {actual} != esperado {expected} (inicial {initial} + cunhado {world.MoneyMinted.Amount} - destruído {world.MoneyDestroyed.Amount})");
        }
        return new Stage(world, clock, initial, until, failure);
    }

    private static void AssertMoneyConserved(long ticks)
    {
        var stage = ticks <= TenYearsInHours ? Decade.Value : Century.Value;
        Assert.True(stage.Failure is not { } f || f.Tick > ticks, stage.Failure?.Message);
    }
}
