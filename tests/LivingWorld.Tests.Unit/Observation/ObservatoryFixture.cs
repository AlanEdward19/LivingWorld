using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Uma corrida observada do cenário padrão (10 anos), compartilhada pelos testes da fatia A para o gate
/// não pagar a simulação duas vezes. Guarda, por ano, contagens tiradas direto do estado (fonte independente do sink).</summary>
internal static class ObservatoryFixture
{
    public const int GateYears = 10;
    public const ulong Seed = 1;

    public sealed record YearCheck(long AliveMaterialized, long PoolTotal, long Born, long Died);

    public static readonly Lazy<(SeedRun Run, IReadOnlyList<YearCheck> Checks, string Hash)> Observed = new(() =>
    {
        var checks = new List<YearCheck>();
        long previousTick = 0;
        string hash = "";
        var run = CivilizationRunner.Run(DefaultScenarioJson(), Seed, GateYears, afterYear: (world, sample) =>
        {
            checks.Add(new YearCheck(
                world.Npcs.LongCount(n => n.IsAlive),
                world.ActiveCities().Sum(c => c.AggregatePool.Count),
                world.Npcs.LongCount(n => n.BirthDate.TotalHours > previousTick && n.BirthDate.TotalHours <= sample.Tick),
                world.Npcs.LongCount(n => n.DeathDate is { } d && d.TotalHours > previousTick && d.TotalHours <= sample.Tick)));
            previousTick = sample.Tick;
            hash = WorldSnapshot.CanonicalHash(world);
        });
        return (run, checks, hash);
    });

    public static string DefaultScenarioJson() => File.ReadAllText(Path.Combine(RepoRoot(), "scenarios", "default.json"));

    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
