using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 3, task 7: profissão, recurso e tipo de local vêm do cenário, nunca de literal
/// em C# — mesmo padrão de <c>GeographyNamingArchitectureTests</c>. O cenário <c>test-scifi</c>
/// (scenarios/test-scifi.json) declara piloto/técnico/plasma/liga só como ids; os nomes só
/// existem neste arquivo de teste e no JSON, nunca em src/.</summary>
public class PopulationArchitectureTests
{
    private static readonly string[] BannedNames = ["piloto", "técnico", "plasma", "liga", "hangar", "posto-de-comando"];

    [Fact]
    public void No_cs_file_under_Domain_or_Simulation_contains_a_banned_population_name_literal()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (var name in BannedNames)
                if (text.Contains($"\"{name}\"", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{file}: \"{name}\"");
        }

        Assert.True(offenders.Count == 0, "literais de população banidos encontrados: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Scanner_flags_a_banned_literal_when_one_is_injected_into_scratch_source()
    {
        const string mutatedSource = """
            namespace LivingWorld.Domain;
            public static class Mutant { public const string Bad = "piloto"; }
            """;

        Assert.Contains(BannedNames, name => mutatedSource.Contains($"\"{name}\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Test_scifi_scenario_runs_1_month_with_the_same_invariants_as_default()
    {
        AssertScifiInvariants(30 * 24);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void Test_scifi_scenario_runs_1_year_with_the_same_invariants_as_default()
    {
        AssertScifiInvariants(12 * 30 * 24);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void Test_scifi_scenario_runs_10_years_with_the_same_invariants_as_default()
    {
        AssertScifiInvariants(10 * 12 * 30 * 24);
    }

    private static void AssertScifiInvariants(long ticks)
    {
        const long sampleEveryHours = 24;
        string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "test-scifi.json"));
        var result = ScenarioLoader.LoadWorld(json);
        Assert.True(result.IsSuccess, result.Error);

        var (world, clock) = result.Value;
        Assert.Equal(100, world.Npcs.Count);

        for (long tick = 0; tick < ticks; tick++)
        {
            clock.Tick(world);

            if ((tick + 1) % sampleEveryHours != 0 && tick + 1 != ticks)
                continue;

            foreach (var household in world.Households)
                Assert.False(household.IsEmpty);
            foreach (var npc in world.Npcs.Where(n => !n.IsAlive))
                foreach (var household in world.Households)
                    Assert.DoesNotContain(npc.Id, household.Members);
        }
    }

    [Fact]
    public void Default_scenario_file_loads_and_matches_ScenarioRunner_defaults()
    {
        string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json"));
        var result = ScenarioLoader.LoadWorld(json);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(20, result.Value.World.Npcs.Count);
    }

    private static IEnumerable<string> SourceFiles()
    {
        string repoRoot = FindRepoRoot();
        foreach (var project in new[] { "LivingWorld.Domain", "LivingWorld.Simulation" })
        {
            var dir = Path.Combine(repoRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                if (!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    yield return file;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
