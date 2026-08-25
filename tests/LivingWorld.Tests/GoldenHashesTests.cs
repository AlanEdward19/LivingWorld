using System.Text.Json;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Task 9: tests/golden/world-hashes.json versiona {cenário, seed, ticks, hash}.
/// Mudança legítima de regra quebra o arquivo; atualizar o baseline vira commit explícito,
/// nunca efeito colateral do gate. Gate padrão só cobre o entry barato (100 ticks);
/// horizontes longos ficam em <c>Category=Scenario</c>.</summary>
public class GoldenHashesTests
{
    public sealed record GoldenEntry(string Scenario, ulong Seed, long Ticks, string CanonicalHash);

    private static readonly string GoldenPath = Path.Combine(FindRepoRoot(), "tests", "golden", "world-hashes.json");

    private static List<GoldenEntry> AllEntries() =>
        JsonSerializer.Deserialize<List<GoldenEntry>>(File.ReadAllText(GoldenPath))!;

    public static IEnumerable<object[]> GateEntries() =>
        AllEntries().Where(e => e.Ticks <= 100).Select(e => new object[] { e });

    public static IEnumerable<object[]> ScenarioEntries() =>
        AllEntries().Where(e => e.Ticks > 100).Select(e => new object[] { e });

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
    [MemberData(nameof(GateEntries))]
    public void Gate_scenario_hash_matches_committed_golden_file(GoldenEntry entry)
    {
        AssertHashMatches(entry);
    }

    [Theory]
    [MemberData(nameof(ScenarioEntries))]
    [Trait("Category", "Scenario")]
    public void Long_horizon_scenario_hash_matches_committed_golden_file(GoldenEntry entry)
    {
        AssertHashMatches(entry);
    }

    private static void AssertHashMatches(GoldenEntry entry)
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
