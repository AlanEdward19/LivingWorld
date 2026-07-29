using System.Text.Json;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Task 9: tests/golden/world-hashes.json versiona {cenário, seed, ticks, hash}.
/// Mudança legítima de regra quebra o arquivo; atualizar o baseline vira commit explícito,
/// nunca efeito colateral do gate.</summary>
public class GoldenHashesTests
{
    public sealed record GoldenEntry(string Scenario, ulong Seed, long Ticks, string CanonicalHash);

    private static readonly string GoldenPath = Path.Combine(FindRepoRoot(), "tests", "golden", "world-hashes.json");

    public static IEnumerable<object[]> Entries() =>
        JsonSerializer.Deserialize<List<GoldenEntry>>(File.ReadAllText(GoldenPath))!
            .Select(e => new object[] { e });

    [Fact(Skip = "Regravar: dotnet test --filter ZZZ_record_golden_hashes")]
    public void ZZZ_record_golden_hashes()
    {
        var entries = new List<GoldenEntry>
        {
            new("default", 42, 3650, ScenarioRunner.RunAndHash(42, 3650).Item1),
            new("default", 43, 3650, ScenarioRunner.RunAndHash(43, 3650).Item1),
            new("default", 42, 100, ScenarioRunner.RunAndHash(42, 100).Item1),
        };
        File.WriteAllText(GoldenPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Scenario_hash_matches_committed_golden_file(GoldenEntry entry)
    {
        Assert.Equal("default", entry.Scenario); // única cena implementada nesta fase

        var (canonical, _) = ScenarioRunner.RunAndHash(entry.Seed, entry.Ticks);

        Assert.Equal(entry.CanonicalHash, canonical);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
