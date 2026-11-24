using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>ADR-0044: a corrida compartilhada tem de ser a mesma simulação que cada teste fazia sozinho. O sink que conta trocas,
/// o braço de controle ao lado e a leitura anual não podem mudar o mundo: o hash canônico do ano 10 é igual ao de uma
/// corrida isolada, sem sink, com o relógio do próprio <c>ScenarioRunner.Create</c>.</summary>
[Trait("Category", "Century")] // 20 seeds × 100 anos: fora do gate de lote, roda no --full diário
public class DefaultVillageCenturyTests
{
    [Fact]
    [Trait("Category", "Scenario")]
    public void Shared_run_matches_an_isolated_run_of_the_same_seed_at_year_ten()
    {
        var (world, clock) = ScenarioRunner.Create(1);
        clock.Run(world, DefaultVillageCentury.GateYears * world.Calendar.HoursPerYear);

        var shared = DefaultVillageCentury.Runs[0];
        Assert.Equal(1UL, shared.Seed);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), shared.GateHash);
        Assert.Equal(world.Npcs.Count(n => n.IsAlive), shared.Alive[DefaultVillageCentury.GateYears]);
    }

    [Fact]
    public void Shared_run_covers_twenty_seeds_and_every_year_of_the_century()
    {
        Assert.Equal(Enumerable.Range(1, 20).Select(s => (ulong)s), DefaultVillageCentury.Runs.Select(r => r.Seed));
        Assert.All(DefaultVillageCentury.Runs, r => Assert.Equal(DefaultVillageCentury.CenturyYears + 1, r.Alive.Length));
    }
}
