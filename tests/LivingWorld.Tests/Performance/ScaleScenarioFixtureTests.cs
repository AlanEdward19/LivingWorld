namespace LivingWorld.Tests.Performance;

[Collection(ScalePerformanceCollection.Name)]
public class ScaleScenarioFixtureTests
{
    private const long OneMonthTicks = 30 * 24;
    private const long OneYearTicks = 12 * 30 * 24;

    /// <summary>Gate curto: 1 mês-sim, pop pequena — demografia não colapsa.</summary>
    [Fact]
    public void One_month_small_population_stays_above_twenty_percent_of_initial()
    {
        const int initial = ScaleScenarioFixture.PopulationSmall;
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 42, initial);
        clock.Run(world, OneMonthTicks);

        int alive = world.Npcs.Count(n => n.IsAlive);
        int floor = ScaleScenarioFixture.MinimumAliveAfterOneYear(initial);
        Assert.True(alive >= floor, $"vivos={alive}, mínimo={floor}, inicial={initial}");
    }

    [Theory]
    [Trait("Category", "Scenario")]
    [InlineData(ScaleScenarioFixture.PopulationSmall)]
    [InlineData(ScaleScenarioFixture.PopulationLarge)]
    public void One_sim_year_keeps_alive_population_above_twenty_percent_of_initial(int initialPopulation)
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld(seed: 42, initialPopulation);
        clock.Run(world, OneYearTicks);

        int alive = world.Npcs.Count(n => n.IsAlive);
        int floor = ScaleScenarioFixture.MinimumAliveAfterOneYear(initialPopulation);
        Assert.True(alive >= floor, $"vivos={alive}, mínimo={floor}, inicial={initialPopulation}");
    }
}
