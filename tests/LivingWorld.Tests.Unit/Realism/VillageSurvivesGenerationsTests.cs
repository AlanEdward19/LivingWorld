using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Realism;

/// <summary>AD-128, critério de ponta a ponta: a vila padrão passa da segunda geração. Antes a geração nascida na vila
/// crescia sem vaga (os mortos seguravam os empregos), sem salário (a oficina falida não soltava ninguém), sem despensa e
/// sem par, e os nascimentos paravam por volta do ano 50.
/// Uma classe por seed: linhas de um mesmo <c>[Theory]</c> rodam em série, classes diferentes rodam em paralelo.</summary>
public static class VillageSurvivesGenerationsTests
{
    private static void After_sixty_years(ulong seed)
    {
        var (world, clock) = ScenarioRunner.Create(seed);
        clock.Run(world, 60 * 360L * 24);

        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        int children = alive.Count(n => n.AgeYears(world.CurrentDate) < 15);
        int employed = alive.Count(n => n.Employer is not null);

        Assert.True(alive.Count >= 60, $"seed {seed}: só {alive.Count} vivos no ano 60");
        Assert.True(children >= 10, $"seed {seed}: só {children} crianças no ano 60");
        Assert.True(employed >= 40, $"seed {seed}: só {employed} empregados no ano 60");
    }

    [Trait("Category", "Scenario")]
    public class Seed7
    {
        [Theory]
        [InlineData(7UL)]
        public void After_sixty_years_the_village_still_has_working_adults_and_children(ulong seed) => After_sixty_years(seed);
    }

    [Trait("Category", "Scenario")]
    public class Seed42
    {
        [Theory]
        [InlineData(42UL)]
        public void After_sixty_years_the_village_still_has_working_adults_and_children(ulong seed) => After_sixty_years(seed);
    }
}
