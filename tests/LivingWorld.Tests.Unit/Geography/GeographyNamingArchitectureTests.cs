namespace LivingWorld.Tests.Unit.Geography;

/// <summary>Fase 2, critério final: nome de terreno, bioma ou recurso não pode aparecer como
/// literal em <c>src/LivingWorld.Domain</c> ou <c>src/LivingWorld.Simulation</c> — o motor só
/// conhece ids vindos do cenário (task 2). R5: sensor de mutação prova que a checagem detecta
/// de verdade, não só que "não achou nada".</summary>
public class GeographyNamingArchitectureTests
{
    // Vocabulário representativo de nomes que só podem existir como dado de cenário (JSON),
    // nunca como literal C#.
    private static readonly string[] BannedNames =
        ["plains", "forest", "mountain", "swamp", "desert", "tundra", "grassland", "wheat", "iron ore", "firewood"];

    [Fact]
    public void No_cs_file_under_Domain_or_Simulation_contains_a_banned_geography_name_literal()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (var name in BannedNames)
                if (ContainsQuotedLiteral(text, name))
                    offenders.Add($"{file}: \"{name}\"");
        }

        Assert.True(offenders.Count == 0, "literais de geografia banidos encontrados: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Scanner_flags_a_banned_literal_when_one_is_injected_into_scratch_source()
    {
        const string mutatedSource = """
            namespace LivingWorld.Domain;
            public static class Mutant { public const string Bad = "plains"; }
            """;

        Assert.True(BannedNames.Any(name => ContainsQuotedLiteral(mutatedSource, name)),
            "sensor de mutação não detectou literal injetado — o teste real não mediria nada");
    }

    private static bool ContainsQuotedLiteral(string sourceText, string name) =>
        sourceText.Contains($"\"{name}\"", StringComparison.OrdinalIgnoreCase);

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
