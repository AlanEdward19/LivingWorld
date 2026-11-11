using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>Corrida compartilhada (ADR-0044): a vila padrão (<c>ScenarioRunner.Create(seed)</c> e o relógio dele) nas seeds
/// 1..20 por 10 anos, simulada uma vez por processo. Lida pelo baseline de população (ano 10, 20 seeds) e pelo braço de
/// controle de <see cref="Health.DiseaseTests"/> (ano 5, seeds 1..8). Guarda só os vivos de cada fim de ano e o hash do
/// ano 10; ninguém recebe o mundo. Equivalência com a corrida isolada: <see cref="DefaultVillageDecadeTests"/>.</summary>
public static class DefaultVillageDecade
{
    public const int Seeds = 20;
    public const int Years = 10;

    /// <summary><paramref name="Alive"/>: índice = ano (0 = criação).</summary>
    public sealed record Run(ulong Seed, int[] Alive, string Hash);

    private static readonly Lazy<Run[]> All = new(() => ParallelSeeds.Map(1, Seeds, Simulate));

    public static IReadOnlyList<Run> Runs => All.Value;

    private static Run Simulate(ulong seed)
    {
        var (world, clock) = ScenarioRunner.Create(seed);
        var alive = new int[Years + 1];
        alive[0] = world.Npcs.Count(n => n.IsAlive);
        for (int year = 1; year <= Years; year++)
        {
            clock.Run(world, world.Calendar.HoursPerYear);
            alive[year] = world.Npcs.Count(n => n.IsAlive);
        }
        return new Run(seed, alive, WorldSnapshot.CanonicalHash(world));
    }
}

public class DefaultVillageDecadeTests
{
    [Fact]
    [Trait("Category", "Scenario")]
    public void Shared_run_matches_an_isolated_run_of_the_same_seed()
    {
        var (world, clock) = ScenarioRunner.Create(1);
        clock.Run(world, DefaultVillageDecade.Years * world.Calendar.HoursPerYear);

        var shared = DefaultVillageDecade.Runs[0];
        Assert.Equal(1UL, shared.Seed);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), shared.Hash);
        Assert.Equal(world.Npcs.Count(n => n.IsAlive), shared.Alive[DefaultVillageDecade.Years]);
    }
}
