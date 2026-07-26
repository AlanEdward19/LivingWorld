using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class MortalityPlannerTests
{
    private static readonly LifeTable Table = LifeTable.Create(90,
    [
        new LifeTableBracket(0, 1, 0.08),
        new LifeTableBracket(2, 89, 0.02),
    ]).Value!;

    [Fact]
    public void Same_seed_rolls_the_same_death_age()
    {
        int a = MortalityPlanner.RollDeathAge(new WorldRng(7), Table, health: 100);
        int b = MortalityPlanner.RollDeathAge(new WorldRng(7), Table, health: 100);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Death_age_never_exceeds_max_longevity()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            int age = MortalityPlanner.RollDeathAge(new WorldRng(seed), Table, health: 100);
            Assert.InRange(age, 0, Table.MaxLongevityYears);
        }
    }

    [Fact]
    public void Worse_health_does_not_increase_average_death_age_across_seeds()
    {
        long healthySum = 0, sicklySum = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            healthySum += MortalityPlanner.RollDeathAge(new WorldRng(seed), Table, health: 100);
            sicklySum += MortalityPlanner.RollDeathAge(new WorldRng(seed), Table, health: 10);
        }
        Assert.True(sicklySum <= healthySum);
    }
}
