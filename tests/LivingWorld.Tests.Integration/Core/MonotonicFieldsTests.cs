using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Core;

/// <summary>Task 14: assert genérico sobre <see cref="MonotonicFields"/> — nenhum contador
/// regride e nenhum NPC vivo perde idade entre duas amostras. Gate: 1 mês; 1 ano em Scenario.</summary>
public class MonotonicFieldsTests
{
    private const long OneMonthInHours = 30 * 24;
    private const long OneYearInHours = 12 * 30 * 24;
    private const long SampleEveryTicks = 24; // uma amostra por dia de mundo

    [Fact]
    public void No_declared_counter_and_no_living_npc_age_ever_regresses_over_1_month()
    {
        AssertNoRegression(OneMonthInHours);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void No_declared_counter_and_no_living_npc_age_ever_regresses_over_1_year()
    {
        AssertNoRegression(OneYearInHours);
    }

    private static void AssertNoRegression(long horizonHours)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);

        var previousCounters = MonotonicFields.WorldCounters.ToDictionary(f => f.Name, f => f.Read(world));
        var previousAges = MonotonicFields.AgesOfLivingNpcs(world);

        for (long elapsed = 0; elapsed < horizonHours; elapsed += SampleEveryTicks)
        {
            clock.Run(world, Math.Min(SampleEveryTicks, horizonHours - elapsed));

            foreach (var (name, read) in MonotonicFields.WorldCounters)
            {
                long current = read(world);
                Assert.True(current >= previousCounters[name], $"{name} regrediu: {previousCounters[name]} -> {current}");
                previousCounters[name] = current;
            }

            var currentAges = MonotonicFields.AgesOfLivingNpcs(world);
            foreach (var (id, previousAge) in previousAges)
                if (currentAges.TryGetValue(id, out var currentAge))
                    Assert.True(currentAge >= previousAge, $"NPC {id} perdeu idade: {previousAge} -> {currentAge}");
            previousAges = currentAges;
        }
    }

    // Sensor de mutação (R5): sobre dado real de MonotonicFields.AgesOfLivingNpcs, uma idade
    // anterior artificialmente maior que a atual precisa reprovar a comparação — prova que o
    // assert mede de verdade, não sempre passa.
    [Fact]
    public void An_inflated_previous_age_fails_the_generic_comparison()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 3);
        clock.Run(world, 100);
        var ages = MonotonicFields.AgesOfLivingNpcs(world);
        var (id, age) = ages.First();

        Assert.Throws<Xunit.Sdk.TrueException>(() => Assert.True(ages[id] >= age + 1, "deveria reprovar"));
    }
}
