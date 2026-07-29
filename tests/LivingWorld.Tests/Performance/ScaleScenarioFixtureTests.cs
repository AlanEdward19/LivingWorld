namespace LivingWorld.Tests.Performance;

public class ScaleScenarioFixtureTests
{
    private const long OneYearTicks = 12 * 30 * 24;

    [Theory]
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
