using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>AD-128, critério de ponta a ponta: a vila padrão passa da segunda geração. Antes a geração nascida na vila
/// crescia sem vaga (os mortos seguravam os empregos), sem salário (a oficina falida não soltava ninguém), sem despensa e
/// sem par, e os nascimentos paravam por volta do ano 50.
/// Veio do Unit (2026-11-13): é cenário de 60 anos, não regra isolada. A seed 42 lê o ano 60 da corrida compartilhada
/// <see cref="DefaultVillageSeed42"/> (ADR-0044); a seed 7 roda o seu mundo. Uma classe por seed: rodam em paralelo.</summary>
public static class VillageSurvivesGenerationsTests
{
    private static void Check(ulong seed, int alive, int children, int employed)
    {
        Assert.True(alive >= 60, $"seed {seed}: só {alive} vivos no ano 60");
        Assert.True(children >= 10, $"seed {seed}: só {children} crianças no ano 60");
        Assert.True(employed >= 40, $"seed {seed}: só {employed} empregados no ano 60");
    }

    [Trait("Category", "Scenario")]
    public class Seed7
    {
        [Theory]
        [InlineData(7UL)]
        public void After_sixty_years_the_village_still_has_working_adults_and_children(ulong seed)
        {
            var (world, clock) = ScenarioRunner.Create(seed);
            clock.Run(world, DefaultVillageSeed42.GenerationsYears * 360L * 24);

            var alive = world.Npcs.Where(n => n.IsAlive).ToList();
            Check(seed, alive.Count, alive.Count(n => n.AgeYears(world.CurrentDate) < 15), alive.Count(n => n.Employer is not null));
        }
    }

    [Trait("Category", "Scenario")]
    public class Seed42
    {
        [Theory]
        [InlineData(42UL)]
        public void After_sixty_years_the_village_still_has_working_adults_and_children(ulong seed)
        {
            Assert.Equal(DefaultVillageSeed42.Seed, seed);
            var run = DefaultVillageSeed42.Long;
            Check(seed, run.AliveYear60, run.ChildrenYear60, run.EmployedYear60);
        }
    }
}
