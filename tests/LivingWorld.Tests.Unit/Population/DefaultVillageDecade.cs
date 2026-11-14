using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using LivingWorld.Tests.Unit.Ontogeny;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>Corrida compartilhada (ADR-0044): a vila padrão (<c>ScenarioRunner.Create(seed)</c> e o relógio dele) nas seeds
/// 1..20 por 10 anos, simulada uma vez por processo, pelo laço de <see cref="DemographyReport.Run"/> (dia a dia, só observa).
/// Leitores: o baseline de população (ano 10, 20 seeds), o braço de controle de <see cref="Health.DiseaseTests"/> (ano 5,
/// seeds 1..8), o braço com acolhimento de <see cref="OntogenyDebtsTests"/> (6 anos, 20 seeds) e o calendário demográfico de
/// <see cref="Lifecycle.DemographyCalendarScenarioTests"/> (10 anos, seeds 1..3). Guarda só valores observados; ninguém recebe
/// o mundo. Equivalência com as corridas isoladas: <see cref="DefaultVillageDecadeTests"/>.</summary>
public static class DefaultVillageDecade
{
    public const int Seeds = 20;
    public const int Years = 10;
    public const int DemographySeeds = 3;

    /// <summary><paramref name="Alive"/>: índice = ano (0 = criação). <paramref name="Orphans"/>: observação dos 6 primeiros anos
    /// (<see cref="OrphanWatch"/>) e o hash do mês 72. <paramref name="Demography"/>: só nas seeds 1..<see cref="DemographySeeds"/>.</summary>
    public sealed record Run(ulong Seed, int[] Alive, string Hash, (int Deaths, int OrphanMonths, string Hash) Orphans,
        DemographyReport.SeedResult? Demography);

    private static readonly Lazy<Run[]> All = new(() => ParallelSeeds.Map(1, Seeds, Simulate));

    public static IReadOnlyList<Run> Runs => All.Value;

    private static Run Simulate(ulong seed)
    {
        var alive = new int[Years + 1];
        var watch = new OrphanWatch();
        string hash = "", orphanHash = "";
        var result = DemographyReport.Run(seed, Years, warmupYears: 0,
            observe: world =>
            {
                alive[0] = world.Npcs.Count(n => n.IsAlive);
                return watch.Sink(world);
            },
            afterMonth: (world, month) =>
            {
                if (month < OrphanWatch.Months) watch.AfterMonth(world);
                if (month == OrphanWatch.Months - 1)
                {
                    watch.Active = false;
                    orphanHash = WorldSnapshot.CanonicalHash(world);
                }
                if ((month + 1) % world.Calendar.MonthsPerYear != 0) return;
                int year = (month + 1) / world.Calendar.MonthsPerYear;
                alive[year] = world.Npcs.Count(n => n.IsAlive);
                if (year == Years) hash = WorldSnapshot.CanonicalHash(world);
            });
        return new Run(seed, alive, hash, (watch.Deaths, watch.OrphanMonths, orphanHash),
            seed <= DemographySeeds ? result : null);
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

    [Fact]
    [Trait("Category", "Scenario")]
    public void Shared_orphan_observation_matches_the_isolated_run_with_orphan_care()
    {
        var isolated = OntogenyDebtsTests.RunVillage(1, withOrphanCare: true);

        Assert.Equal(isolated, DefaultVillageDecade.Runs[0].Orphans);
    }
}
